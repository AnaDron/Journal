using Aura.Journal.Application;
using Aura.Journal.Core;
using FluentAssertions;
using Xunit;

namespace Aura.Journal.Application.Tests;

/// <summary>
/// Поведение <see cref="JournalEnricher"/> поверх минимального набора plugin-points:
/// дефолтный резолвер имени (поле <c>name</c>), декларация ссылки <c>Product.groupId</c> на
/// <c>ProductGroup</c> и конвенция ключей <c>{type}/{id}</c>. Доменно-специфичные форматтеры и
/// композитные резолверы имени к этим сценариям не нужны.
/// </summary>
public sealed class JournalEnricherTests {
	private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private static JournalEnricher MakeEnricher(FakeRepository repo) =>
		new(
			repo,
			nameResolvers: Array.Empty<IJournalEntityNameResolver>(),
			referenceResolvers: new[] { new ProductGroupReferenceResolver() },
			fieldFormatters: Array.Empty<IJournalFieldFormatter>(),
			entityKeyResolver: new ConventionEntityKeyResolver());

	[Fact]
	public async Task Update_FillsOldValueFromCreate() {
		var productKey = "product/" + Guid.NewGuid().ToString("D");
		var createId = Guid.NewGuid();
		var updateId = Guid.NewGuid();
		var createEvent = MakeEvent(BaseTime, new JournalChange(
			Id: createId,
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Create,
			Fields: new[] {
				new JournalFieldChange("name", "\"Молоко\""),
				new JournalFieldChange("quantity", "10"),
			}));
		var updateEvent = MakeEvent(BaseTime.AddHours(1), new JournalChange(
			Id: updateId,
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Update,
			Fields: new[] {
				new JournalFieldChange("quantity", "9.3"),
			}));

		var repo = new FakeRepository(history: new[] {
			createEvent.Changes[0],
			updateEvent.Changes[0],
		});
		var enricher = MakeEnricher(repo);

		var page = new Page<JournalEvent>(new[] { updateEvent }, PageNumber: 1, TotalCount: 1);
		var enriched = await enricher.EnrichAsync(page);
		var change = enriched.Items.Single().Changes.Single();

		change.EntityName.Should().Be("Молоко");
		change.Fields.Should().ContainSingle();
		var field = change.Fields!.Single();
		field.Path.Should().Be("quantity");
		field.ValueJson.Should().Be("9.3");
		field.OldValueJson.Should().Be("10");
	}

	[Fact]
	public async Task Update_OldValueFromPreviousUpdate_NotFromCreate() {
		var productKey = "product/" + Guid.NewGuid().ToString("D");
		var createChange = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Create,
			Fields: new[] {
				new JournalFieldChange("name", "\"Молоко\""),
				new JournalFieldChange("price", "100.00"),
			});
		var firstUpdate = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Update,
			Fields: new[] {
				new JournalFieldChange("price", "110.00"),
			});
		var secondUpdate = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Update,
			Fields: new[] {
				new JournalFieldChange("price", "120.00"),
			});

		var repo = new FakeRepository(history: new[] { createChange, firstUpdate, secondUpdate });
		var enricher = MakeEnricher(repo);

		var pageEvent = MakeEvent(BaseTime.AddHours(2), secondUpdate);
		var page = new Page<JournalEvent>(new[] { pageEvent }, PageNumber: 1, TotalCount: 1);
		var enriched = await enricher.EnrichAsync(page);
		var field = enriched.Items.Single().Changes.Single().Fields!.Single();

		field.OldValueJson.Should().Be("110.00");
	}

	[Fact]
	public async Task Create_LeavesOldValueNullAndExposesEntityName() {
		var productKey = "product/" + Guid.NewGuid().ToString("D");
		var createChange = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Create,
			Fields: new[] {
				new JournalFieldChange("name", "\"Новое молоко\""),
				new JournalFieldChange("price", "120.00"),
			});

		var repo = new FakeRepository(history: new[] { createChange });
		var enricher = MakeEnricher(repo);

		var pageEvent = MakeEvent(BaseTime, createChange);
		var page = new Page<JournalEvent>(new[] { pageEvent }, PageNumber: 1, TotalCount: 1);
		var enriched = await enricher.EnrichAsync(page);
		var change = enriched.Items.Single().Changes.Single();

		change.EntityName.Should().Be("Новое молоко");
		change.Fields.Should().AllSatisfy(f => f.OldValueJson.Should().BeNull());
	}

	[Fact]
	public async Task Update_WithoutAnyHistory_LeavesOldValueNull() {
		var productKey = "product/" + Guid.NewGuid().ToString("D");
		var update = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Update,
			Fields: new[] {
				new JournalFieldChange("quantity", "5"),
			});

		var repo = new FakeRepository(history: new[] { update });
		var enricher = MakeEnricher(repo);

		var pageEvent = MakeEvent(BaseTime, update);
		var page = new Page<JournalEvent>(new[] { pageEvent }, PageNumber: 1, TotalCount: 1);
		var enriched = await enricher.EnrichAsync(page);
		var field = enriched.Items.Single().Changes.Single().Fields!.Single();

		field.OldValueJson.Should().BeNull();
	}

	[Fact]
	public async Task Update_ResolvesGroupIdToProductGroupName() {
		var productKey = "product/" + Guid.NewGuid().ToString("D");
		var oldGroupId = Guid.NewGuid();
		var newGroupId = Guid.NewGuid();
		var oldGroupKey = "group/" + oldGroupId.ToString("D");
		var newGroupKey = "group/" + newGroupId.ToString("D");

		var productCreate = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Create,
			Fields: new[] {
				new JournalFieldChange("name", "\"Молоко\""),
				new JournalFieldChange("groupId", $"\"{oldGroupId:D}\""),
			});
		var productUpdate = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Update,
			Fields: new[] {
				new JournalFieldChange("groupId", $"\"{newGroupId:D}\""),
			});
		var oldGroupCreate = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "ProductGroup",
			EntityKey: oldGroupKey,
			Operation: JournalOperation.Create,
			Fields: new[] { new JournalFieldChange("name", "\"Корзина\"") });
		var newGroupCreate = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "ProductGroup",
			EntityKey: newGroupKey,
			Operation: JournalOperation.Create,
			Fields: new[] { new JournalFieldChange("name", "\"Витрина\"") });

		var repo = new FakeRepository(history: new[] {
			productCreate,
			oldGroupCreate,
			newGroupCreate,
			productUpdate,
		});
		var enricher = MakeEnricher(repo);

		var pageEvent = MakeEvent(BaseTime.AddHours(1), productUpdate);
		var page = new Page<JournalEvent>(new[] { pageEvent }, PageNumber: 1, TotalCount: 1);
		var enriched = await enricher.EnrichAsync(page);
		var field = enriched.Items.Single().Changes.Single().Fields!.Single();

		field.Path.Should().Be("groupId");
		field.RelatedNewName.Should().Be("Витрина");
		field.RelatedOldName.Should().Be("Корзина");
	}

	[Fact]
	public async Task Create_ResolvesGroupIdToProductGroupName() {
		var productKey = "product/" + Guid.NewGuid().ToString("D");
		var groupId = Guid.NewGuid();
		var groupKey = "group/" + groupId.ToString("D");

		var groupCreate = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "ProductGroup",
			EntityKey: groupKey,
			Operation: JournalOperation.Create,
			Fields: new[] { new JournalFieldChange("name", "\"Витрина\"") });
		var productCreate = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Create,
			Fields: new[] {
				new JournalFieldChange("name", "\"Молоко\""),
				new JournalFieldChange("groupId", $"\"{groupId:D}\""),
			});

		var repo = new FakeRepository(history: new[] { groupCreate, productCreate });
		var enricher = MakeEnricher(repo);

		var pageEvent = MakeEvent(BaseTime.AddHours(1), productCreate);
		var page = new Page<JournalEvent>(new[] { pageEvent }, PageNumber: 1, TotalCount: 1);
		var enriched = await enricher.EnrichAsync(page);
		var fields = enriched.Items.Single().Changes.Single().Fields!;

		var groupField = fields.Single(f => f.Path == "groupId");
		groupField.RelatedNewName.Should().Be("Витрина");
		groupField.RelatedOldName.Should().BeNull();
		var nameField = fields.Single(f => f.Path == "name");
		nameField.RelatedNewName.Should().BeNull();
	}

	[Fact]
	public async Task Delete_DoesNotThrowAndKeepsFieldsNull() {
		var productKey = "product/" + Guid.NewGuid().ToString("D");
		var createChange = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Create,
			Fields: new[] { new JournalFieldChange("name", "\"X\"") });
		var deleteChange = new JournalChange(
			Id: Guid.NewGuid(),
			EventId: Guid.NewGuid(),
			EntityType: "Product",
			EntityKey: productKey,
			Operation: JournalOperation.Delete,
			Fields: null);

		var repo = new FakeRepository(history: new[] { createChange, deleteChange });
		var enricher = MakeEnricher(repo);

		var pageEvent = MakeEvent(BaseTime.AddHours(1), deleteChange);
		var page = new Page<JournalEvent>(new[] { pageEvent }, PageNumber: 1, TotalCount: 1);
		var enriched = await enricher.EnrichAsync(page);
		var change = enriched.Items.Single().Changes.Single();

		change.Operation.Should().Be(JournalOperation.Delete);
		change.Fields.Should().BeNull();
		change.EntityName.Should().Be("X");
	}

	private static JournalEvent MakeEvent(DateTimeOffset createdAtUtc, JournalChange change) {
		var eventId = change.EventId == Guid.Empty ? Guid.NewGuid() : change.EventId;
		var stamped = change with { EventId = eventId };
		return new JournalEvent {
			Id = eventId,
			CreatedAtUtc = createdAtUtc,
			InitiatorKind = JournalInitiatorKind.System,
			Changes = new[] { stamped },
		};
	}

	private sealed class FakeRepository(IReadOnlyList<JournalChange> history) : IJournalEventRepository {
		public Task<IReadOnlyList<JournalChange>> GetHistoryAsync(
			IReadOnlyCollection<JournalEntityRef> keys,
			DateTimeOffset upToCreatedAtUtc,
			CancellationToken cancellationToken = default) {
			IReadOnlyList<JournalChange> filtered = history
				.Where(c => keys.Contains(new JournalEntityRef(c.EntityType, c.EntityKey)))
				.ToList();
			return Task.FromResult(filtered);
		}

		public Task<Page<JournalEvent>> ListAsync(
			int page,
			int pageSize,
			string? entityType,
			Guid? eventId,
			JournalOperation? operation,
			JournalInitiatorKind? initiatorKind,
			DateTimeOffset? createdFromUtc,
			DateTimeOffset? createdToUtc,
			DateTimeOffset? occurredFromUtc,
			DateTimeOffset? occurredToUtc,
			CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<JournalEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<IReadOnlyList<JournalEvent>> GetEntityHistoryAsync(
			string entityType,
			string entityKey,
			CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public void Add(JournalEvent journalEvent) => throw new NotSupportedException();

		public void AddChange(JournalChange change) => throw new NotSupportedException();
	}

	/// <summary>Test-fake: декларирует <c>Product.groupId → ProductGroup</c>.</summary>
	private sealed class ProductGroupReferenceResolver : IJournalReferenceResolver {
		public string EntityType => "Product";
		public string FieldPath => "groupId";
		public string ReferencedEntityType => "ProductGroup";
	}

	/// <summary>Test-fake: ключевая конвенция <c>product/{}</c>, <c>group/{}</c>, <c>store/{}</c>, иначе <c>{type}/{id}</c>.</summary>
	private sealed class ConventionEntityKeyResolver : IJournalEntityKeyResolver {
		public string MakeKey(string entityType, Guid id) => entityType switch {
			"Product" => $"product/{id:D}",
			"ProductGroup" => $"group/{id:D}",
			"Store" => $"store/{id:D}",
			_ => $"{entityType}/{id:D}",
		};
	}
}

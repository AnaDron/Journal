using System.Text.Json;
using EventJournal.Application.Views;
using EventJournal.Core;

namespace EventJournal.Application;

/// <summary>
/// Read-side обогащение журнала: преобразует доменные события в view-модели, для каждой записи Update
/// заполняя <c>OldValueJson</c> предыдущим значением поля, а <c>EntityName</c> — именем сущности на момент
/// события. Доменно-специфичная логика (имя Employee из 3 полей, маппинг ссылок Product.groupId, форматирование
/// enum Employee.kind) подключается через plugin-points: <see cref="IJournalEntityNameResolver"/>,
/// <see cref="IJournalReferenceResolver"/>, <see cref="IJournalFieldFormatter"/>,
/// <see cref="IJournalEntityKeyResolver"/>. SDK по умолчанию: имя из поля <c>name</c>,
/// <c>EntityKey = {EntityType}/{id:D}</c>, никаких ссылок и форматтеров.
/// </summary>
public sealed class JournalEnricher {
	private const string DefaultNameField = "name";

	private readonly IJournalEventRepository _repository;
	private readonly Dictionary<string, IJournalEntityNameResolver> _nameResolvers;
	private readonly Dictionary<(string EntityType, string FieldPath), string> _referenceMap;
	private readonly Dictionary<(string EntityType, string FieldPath), IJournalFieldFormatter> _formatters;
	private readonly IJournalEntityKeyResolver _entityKeyResolver;

	public JournalEnricher(
		IJournalEventRepository repository,
		IEnumerable<IJournalEntityNameResolver> nameResolvers,
		IEnumerable<IJournalReferenceResolver> referenceResolvers,
		IEnumerable<IJournalFieldFormatter> fieldFormatters,
		IJournalEntityKeyResolver entityKeyResolver) {
		_repository = repository;
		_entityKeyResolver = entityKeyResolver;
		_nameResolvers = nameResolvers.ToDictionary(r => r.EntityType, StringComparer.Ordinal);
		_referenceMap = referenceResolvers.ToDictionary(
			r => (r.EntityType, r.FieldPath),
			r => r.ReferencedEntityType);
		_formatters = fieldFormatters.ToDictionary(f => (f.EntityType, f.FieldPath));
	}

	public async Task<Page<JournalEventView>> EnrichAsync(Page<JournalEvent> page, CancellationToken cancellationToken = default) {
		var enriched = await EnrichEventsAsync(page.Items, cancellationToken);
		return new Page<JournalEventView>(enriched, page.PageNumber, page.TotalCount);
	}

	public async Task<JournalEventView?> EnrichAsync(JournalEvent? journalEvent, CancellationToken cancellationToken = default) {
		if (journalEvent is null) {
			return null;
		}

		var enriched = await EnrichEventsAsync(new[] { journalEvent }, cancellationToken);
		return enriched.FirstOrDefault();
	}

	public async Task<JournalEntityHistoryView> EnrichEntityHistoryAsync(
		string entityType,
		string entityKey,
		IReadOnlyList<JournalEvent> events,
		CancellationToken cancellationToken = default) {
		var enrichedEvents = await EnrichEventsAsync(events, cancellationToken);
		var items = new List<JournalEntityHistoryItemView>();
		string? lastKnownName = null;
		foreach (var ev in enrichedEvents) {
			foreach (var change in ev.Changes) {
				if (!string.Equals(change.EntityType, entityType, StringComparison.Ordinal) ||
					!string.Equals(change.EntityKey, entityKey, StringComparison.Ordinal)) {
					continue;
				}

				if (change.EntityName is { Length: > 0 } name) {
					lastKnownName = name;
				}

				items.Add(new JournalEntityHistoryItemView(
					Id: change.Id,
					EventId: ev.Id,
					CreatedAtUtc: ev.CreatedAtUtc,
					OccurredAtUtc: ev.OccurredAtUtc,
					Reason: ev.Reason,
					InitiatorKind: ev.InitiatorKind,
					InitiatorId: ev.InitiatorId,
					Operation: change.Operation,
					Fields: change.Fields));
			}
		}

		// Items собирались по ходу обогащения (хронологически ASC), но в UI ожидается
		// обратный порядок — новые сверху.
		items.Reverse();
		return new JournalEntityHistoryView {
			EntityType = entityType,
			EntityKey = entityKey,
			EntityName = lastKnownName,
			Items = items,
		};
	}

	/// <summary>
	/// Имя сущности по её field-state. Делегирует зарегистрированному резолверу; иначе — дефолт SDK:
	/// раскавыченное значение поля <c>name</c>.
	/// </summary>
	private string? ResolveEntityName(string entityType, IReadOnlyDictionary<string, string?> fieldState) {
		if (_nameResolvers.TryGetValue(entityType, out var resolver)) {
			return resolver.Resolve(fieldState);
		}

		return JournalValueJson.Unquote(fieldState.TryGetValue(DefaultNameField, out var v) ? v : null);
	}

	private IEnumerable<string> NameFieldsFor(string entityType) =>
		_nameResolvers.TryGetValue(entityType, out var resolver)
			? resolver.NameRelevantFields
			: new[] { DefaultNameField };

	private string? TransformFieldValueJson(string entityType, string fieldPath, string? valueJson) {
		if (valueJson is null) {
			return null;
		}

		return _formatters.TryGetValue((entityType, fieldPath), out var formatter)
			? formatter.Format(valueJson)
			: valueJson;
	}

	private bool TryGetReferencedType(string entityType, string fieldPath, out string referencedType) {
		if (_referenceMap.TryGetValue((entityType, fieldPath), out var refType)) {
			referencedType = refType;
			return true;
		}

		referencedType = "";
		return false;
	}

	private async Task<List<JournalEventView>> EnrichEventsAsync(
		IReadOnlyList<JournalEvent> events,
		CancellationToken cancellationToken) {
		if (events.Count == 0) {
			return new List<JournalEventView>();
		}

		var pairs = CollectPairs(events);
		var pageChangeIds = new HashSet<Guid>(events.SelectMany(e => e.Changes).Select(c => c.Id));
		var maxCreated = events.Max(e => e.CreatedAtUtc);

		Dictionary<Guid, ChangeEnrichment> enrichmentByChangeId;
		if (pairs.Count == 0) {
			enrichmentByChangeId = new Dictionary<Guid, ChangeEnrichment>();
		} else {
			var history = await _repository.GetHistoryAsync(pairs, maxCreated, cancellationToken);
			enrichmentByChangeId = ReplayHistory(history, pageChangeIds);
		}

		// Второй проход: собираем ссылки в обогащаемых записях и резолвим имя связанной сущности.
		var resolvedReferences = await ResolveReferencesAsync(events, enrichmentByChangeId, maxCreated, cancellationToken);

		var result = new List<JournalEventView>(events.Count);
		foreach (var ev in events) {
			var changes = new List<JournalChangeView>(ev.Changes.Count);
			foreach (var change in ev.Changes) {
				enrichmentByChangeId.TryGetValue(change.Id, out var enrichment);
				changes.Add(ToView(change, enrichment, resolvedReferences));
			}

			result.Add(new JournalEventView {
				Id = ev.Id,
				CreatedAtUtc = ev.CreatedAtUtc,
				OccurredAtUtc = ev.OccurredAtUtc,
				Reason = ev.Reason,
				InitiatorKind = ev.InitiatorKind,
				InitiatorId = ev.InitiatorId,
				Changes = changes,
			});
		}

		return result;
	}

	private async Task<Dictionary<JournalEntityRef, string?>> ResolveReferencesAsync(
		IReadOnlyList<JournalEvent> events,
		IReadOnlyDictionary<Guid, ChangeEnrichment> enrichmentByChangeId,
		DateTimeOffset upToCreatedAtUtc,
		CancellationToken cancellationToken) {
		var refPairs = new HashSet<JournalEntityRef>();
		foreach (var ev in events) {
			foreach (var change in ev.Changes) {
				if (change.Fields is null) {
					continue;
				}

				enrichmentByChangeId.TryGetValue(change.Id, out var enrichment);
				foreach (var field in change.Fields) {
					if (!TryGetReferencedType(change.EntityType, field.Path, out var refType)) {
						continue;
					}

					AddReferencedPair(refPairs, refType, field.ValueJson);
					var oldValueJson = enrichment.OldValuesByPath?.TryGetValue(field.Path, out var prev) == true ? prev : null;
					AddReferencedPair(refPairs, refType, oldValueJson);
				}
			}
		}

		if (refPairs.Count == 0) {
			return new Dictionary<JournalEntityRef, string?>();
		}

		var refHistory = await _repository.GetHistoryAsync(refPairs.ToList(), upToCreatedAtUtc, cancellationToken);
		var nameByRef = new Dictionary<JournalEntityRef, string?>();
		var state = new Dictionary<JournalEntityRef, Dictionary<string, string?>>();
		foreach (var change in refHistory) {
			var key = new JournalEntityRef(change.EntityType, change.EntityKey);
			if (!state.TryGetValue(key, out var fieldState)) {
				fieldState = new Dictionary<string, string?>(StringComparer.Ordinal);
				state[key] = fieldState;
			}

			ApplyToState(fieldState, change);
		}

		foreach (var (key, fieldState) in state) {
			nameByRef[key] = ResolveEntityName(key.EntityType, fieldState);
		}

		return nameByRef;
	}

	private void AddReferencedPair(HashSet<JournalEntityRef> set, string referencedType, string? valueJson) {
		var guid = TryParseGuidFromJson(valueJson);
		if (guid is null) {
			return;
		}

		set.Add(new JournalEntityRef(referencedType, _entityKeyResolver.MakeKey(referencedType, guid.Value)));
	}

	private static Guid? TryParseGuidFromJson(string? valueJson) {
		if (string.IsNullOrWhiteSpace(valueJson)) {
			return null;
		}

		try {
			using var doc = JsonDocument.Parse(valueJson);
			if (doc.RootElement.ValueKind != JsonValueKind.String) {
				return null;
			}

			var s = doc.RootElement.GetString();
			return Guid.TryParse(s, out var g) ? g : null;
		} catch (JsonException) {
			return null;
		}
	}

	private JournalChangeView ToView(
		JournalChange change,
		ChangeEnrichment enrichment,
		IReadOnlyDictionary<JournalEntityRef, string?> resolvedReferences) {
		IReadOnlyList<JournalFieldChangeView>? fieldsView = null;
		if (change.Fields is { } fields) {
			var list = new List<JournalFieldChangeView>(fields.Count);
			foreach (var field in fields) {
				var oldValue = enrichment.OldValuesByPath?.TryGetValue(field.Path, out var v) == true ? v : null;
				string? relatedNew = null;
				string? relatedOld = null;
				if (TryGetReferencedType(change.EntityType, field.Path, out var refType)) {
					relatedNew = LookupRelatedName(resolvedReferences, refType, field.ValueJson);
					relatedOld = LookupRelatedName(resolvedReferences, refType, oldValue);
				}

				// Per-(entityType, fieldPath) преобразование сырого JSON-значения для отображения
				// (например, enum-число → строка-имя).
				var displayNew = TransformFieldValueJson(change.EntityType, field.Path, field.ValueJson);
				var displayOld = TransformFieldValueJson(change.EntityType, field.Path, oldValue);

				list.Add(new JournalFieldChangeView(field.Path, displayNew, displayOld, relatedNew, relatedOld));
			}

			fieldsView = list;
		}

		return new JournalChangeView(
			Id: change.Id,
			EventId: change.EventId,
			EntityType: change.EntityType,
			EntityKey: change.EntityKey,
			EntityName: enrichment.EntityName,
			Operation: change.Operation,
			Fields: fieldsView);
	}

	private string? LookupRelatedName(
		IReadOnlyDictionary<JournalEntityRef, string?> resolvedReferences,
		string referencedType,
		string? valueJson) {
		var guid = TryParseGuidFromJson(valueJson);
		if (guid is null) {
			return null;
		}

		var key = new JournalEntityRef(referencedType, _entityKeyResolver.MakeKey(referencedType, guid.Value));
		return resolvedReferences.TryGetValue(key, out var name) ? name : null;
	}

	private static List<JournalEntityRef> CollectPairs(IEnumerable<JournalEvent> events) {
		var set = new HashSet<JournalEntityRef>();
		foreach (var ev in events) {
			foreach (var change in ev.Changes) {
				set.Add(new JournalEntityRef(change.EntityType, change.EntityKey));
			}
		}

		return set.ToList();
	}

	private Dictionary<Guid, ChangeEnrichment> ReplayHistory(
		IReadOnlyList<JournalChange> history,
		HashSet<Guid> pageChangeIds) {
		var state = new Dictionary<JournalEntityRef, Dictionary<string, string?>>();
		var result = new Dictionary<Guid, ChangeEnrichment>(pageChangeIds.Count);
		foreach (var change in history) {
			var key = new JournalEntityRef(change.EntityType, change.EntityKey);
			if (!state.TryGetValue(key, out var fieldState)) {
				fieldState = new Dictionary<string, string?>(StringComparer.Ordinal);
				state[key] = fieldState;
			}

			var isPageChange = pageChangeIds.Contains(change.Id);
			Dictionary<string, string?>? oldValuesSnapshot = null;
			if (isPageChange && change.Operation == JournalOperation.Update && change.Fields is { Count: > 0 }) {
				oldValuesSnapshot = new Dictionary<string, string?>(StringComparer.Ordinal);
				foreach (var field in change.Fields) {
					oldValuesSnapshot[field.Path] = fieldState.TryGetValue(field.Path, out var prev) ? prev : null;
				}
			}

			// Для Delete state уже очистится после ApplyToState; имя берём из снимка ДО операции.
			var nameFields = NameFieldsFor(change.EntityType);
			Dictionary<string, string?>? nameStateBefore = null;
			if (change.Operation == JournalOperation.Delete) {
				nameStateBefore = new Dictionary<string, string?>(StringComparer.Ordinal);
				foreach (var f in nameFields) {
					nameStateBefore[f] = fieldState.TryGetValue(f, out var v) ? v : null;
				}
			}

			ApplyToState(fieldState, change);

			if (!isPageChange) {
				continue;
			}

			var nameState = change.Operation == JournalOperation.Delete ? nameStateBefore! : fieldState;
			var resolvedName = ResolveEntityName(change.EntityType, nameState);
			result[change.Id] = new ChangeEnrichment(resolvedName, oldValuesSnapshot);
		}

		return result;
	}

	private static void ApplyToState(Dictionary<string, string?> fieldState, JournalChange change) {
		switch (change.Operation) {
			case JournalOperation.Create:
				fieldState.Clear();
				if (change.Fields is { } createFields) {
					foreach (var field in createFields) {
						fieldState[field.Path] = field.ValueJson;
					}
				}

				break;
			case JournalOperation.Update:
				if (change.Fields is { } updateFields) {
					foreach (var field in updateFields) {
						fieldState[field.Path] = field.ValueJson;
					}
				}

				break;
			case JournalOperation.Delete:
				fieldState.Clear();
				break;
		}
	}

	private readonly record struct ChangeEnrichment(string? EntityName, Dictionary<string, string?>? OldValuesByPath);
}

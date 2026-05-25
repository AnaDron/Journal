using Aura.Journal.Core;
using Riok.Mapperly.Abstractions;

namespace Aura.Journal.Infrastructure;

/// <summary>Маппер журнала: domain ↔ EF-сущности (extension methods).</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class JournalPersistenceMapper {
	[MapperIgnoreTarget(nameof(JournalChangeEntity.Event))]
	[MapProperty(nameof(JournalChange.Fields), nameof(JournalChangeEntity.FieldsJson), Use = nameof(SerializeFields))]
	public static partial JournalChangeEntity ToEntity(this JournalChange source);

	[MapperIgnoreTarget(nameof(JournalEventEntity.Changes))]
	public static partial JournalEventEntity ToEntity(this JournalEvent source);

	public static JournalEvent ToDomain(this JournalEventEntity source, bool includeFields) =>
		new() {
			Id = source.Id,
			CreatedAtUtc = source.CreatedAtUtc,
			OccurredAtUtc = source.OccurredAtUtc,
			Reason = source.Reason,
			InitiatorKind = source.InitiatorKind,
			InitiatorId = source.InitiatorId,
			Changes = MapOrderedChanges(source.Changes, includeFields),
		};

	private static List<JournalChange> MapOrderedChanges(
		IEnumerable<JournalChangeEntity> changes,
		bool includeFields) {
		var ordered = changes
			.OrderBy(c => c.EntityType)
			.ThenBy(c => c.EntityKey);
		var list = new List<JournalChange>();
		foreach (var c in ordered) {
			list.Add(new JournalChange(
				c.Id,
				c.EventId,
				c.EntityType,
				c.EntityKey,
				c.Operation,
				MapFields(c.FieldsJson, includeFields)));
		}

		return list;
	}

	private static IReadOnlyList<JournalFieldChange>? MapFields(string? fieldsJson, bool includeFields) {
		if (!includeFields || fieldsJson is null) {
			return null;
		}

		return JournalChangesJson.ParseChanges(fieldsJson);
	}

	private static string? SerializeFields(IReadOnlyList<JournalFieldChange>? fields)
		=> JournalChangesJson.SerializeChanges(fields);
}

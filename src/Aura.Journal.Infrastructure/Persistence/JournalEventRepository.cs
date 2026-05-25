using Aura.Journal.Core;
using Microsoft.EntityFrameworkCore;

namespace Aura.Journal.Infrastructure;

public sealed class JournalEventRepository(IJournalDbContext db) : IJournalEventRepository {
	public async Task<Page<JournalEvent>> ListAsync(
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
		CancellationToken cancellationToken = default) {
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 200);
		var query = db.JournalEvents.AsNoTracking().AsQueryable();
		if (eventId is { } eid) {
			query = query.Where(ev => ev.Id == eid);
		}

		if (initiatorKind is { } ik) {
			query = query.Where(ev => ev.InitiatorKind == ik);
		}

		if (createdFromUtc is { } cf) {
			query = query.Where(ev => ev.CreatedAtUtc >= cf);
		}

		if (createdToUtc is { } ct) {
			query = query.Where(ev => ev.CreatedAtUtc <= ct);
		}

		if (occurredFromUtc is { } of) {
			query = query.Where(ev => ev.OccurredAtUtc != null && ev.OccurredAtUtc >= of);
		}

		if (occurredToUtc is { } ot) {
			query = query.Where(ev => ev.OccurredAtUtc != null && ev.OccurredAtUtc <= ot);
		}

		if (!string.IsNullOrWhiteSpace(entityType) || operation is { }) {
			query = query.Where(ev =>
				db.JournalChanges.AsNoTracking().Any(c =>
					c.EventId == ev.Id
					&& (string.IsNullOrWhiteSpace(entityType) || c.EntityType == entityType)
					&& (!operation.HasValue || c.Operation == operation)));
		}

		query = query.OrderByDescending(ev => ev.CreatedAtUtc);
		var total = await query.CountAsync(cancellationToken);
		var rows = await query
			.Include(ev => ev.Changes)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);
		var list = rows.ConvertAll(e => e.ToDomain(includeFields: true));
		return new Page<JournalEvent>(list, page, total);
	}

	public async Task<JournalEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) {
		var row = await db.JournalEvents.AsNoTracking()
			.Include(e => e.Changes)
			.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
		return row?.ToDomain(includeFields: true);
	}

	public async Task<IReadOnlyList<JournalEvent>> GetEntityHistoryAsync(
		string entityType,
		string entityKey,
		CancellationToken cancellationToken = default) {
		var rows = await (
			from c in db.JournalChanges.AsNoTracking()
			join e in db.JournalEvents.AsNoTracking() on c.EventId equals e.Id
			where c.EntityType == entityType && c.EntityKey == entityKey
			orderby e.CreatedAtUtc, c.Id
			select new { Change = c, Event = e }
		).ToListAsync(cancellationToken);

		var byEvent = new Dictionary<Guid, (JournalEventEntity Event, List<JournalChangeEntity> Changes)>();
		var order = new List<Guid>();
		foreach (var row in rows) {
			if (!byEvent.TryGetValue(row.Event.Id, out var bucket)) {
				bucket = (row.Event, new List<JournalChangeEntity>());
				byEvent[row.Event.Id] = bucket;
				order.Add(row.Event.Id);
			}

			bucket.Changes.Add(row.Change);
		}

		var result = new List<JournalEvent>(order.Count);
		foreach (var id in order) {
			var (eventEntity, changeEntities) = byEvent[id];
			var clone = new JournalEventEntity {
				Id = eventEntity.Id,
				CreatedAtUtc = eventEntity.CreatedAtUtc,
				OccurredAtUtc = eventEntity.OccurredAtUtc,
				Reason = eventEntity.Reason,
				InitiatorKind = eventEntity.InitiatorKind,
				InitiatorId = eventEntity.InitiatorId,
				Changes = changeEntities,
			};
			result.Add(clone.ToDomain(includeFields: true));
		}

		return result;
	}

	public async Task<IReadOnlyList<JournalChange>> GetHistoryAsync(
		IReadOnlyCollection<JournalEntityRef> keys,
		DateTimeOffset upToCreatedAtUtc,
		CancellationToken cancellationToken = default) {
		if (keys.Count == 0) {
			return Array.Empty<JournalChange>();
		}

		// EF Core LINQ не умеет напрямую использовать .Contains по value-tuple. Делаем
		// плоские множества и фильтруем по EntityType IN ... AND EntityKey IN ..., а
		// финальную точную фильтрацию пар выполняем уже в C#.
		var entityTypes = keys.Select(k => k.EntityType).Distinct().ToList();
		var entityKeys = keys.Select(k => k.EntityKey).Distinct().ToList();
		var query =
			from c in db.JournalChanges.AsNoTracking()
			join e in db.JournalEvents.AsNoTracking() on c.EventId equals e.Id
			where entityTypes.Contains(c.EntityType)
				&& entityKeys.Contains(c.EntityKey)
				&& e.CreatedAtUtc <= upToCreatedAtUtc
			orderby e.CreatedAtUtc, c.Id
			select new { Change = c, e.CreatedAtUtc };
		var rows = await query.ToListAsync(cancellationToken);
		var pairs = new HashSet<JournalEntityRef>(keys);
		var list = new List<JournalChange>(rows.Count);
		foreach (var row in rows) {
			var pair = new JournalEntityRef(row.Change.EntityType, row.Change.EntityKey);
			if (!pairs.Contains(pair)) {
				continue;
			}

			list.Add(new JournalChange(
				Id: row.Change.Id,
				EventId: row.Change.EventId,
				EntityType: row.Change.EntityType,
				EntityKey: row.Change.EntityKey,
				Operation: row.Change.Operation,
				Fields: row.Change.FieldsJson is null
					? null
					: JournalChangesJson.ParseChanges(row.Change.FieldsJson)));
		}

		return list;
	}

	public void Add(JournalEvent journalEvent) {
		var entity = journalEvent.ToEntity();
		entity.Changes = new List<JournalChangeEntity>();
		db.JournalEvents.Add(entity);
	}

	public void AddChange(JournalChange change) {
		db.JournalChanges.Add(change.ToEntity());
	}
}

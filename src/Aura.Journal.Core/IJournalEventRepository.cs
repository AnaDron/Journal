namespace Aura.Journal.Core;

public interface IJournalEventRepository {
	Task<Page<JournalEvent>> ListAsync(
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
		CancellationToken cancellationToken = default);

	Task<JournalEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Возвращает все журнальные записи (с полями) для указанных пар <c>(EntityType, EntityKey)</c>,
	/// у которых <c>Event.CreatedAtUtc &lt;= upToCreatedAtUtc</c>, отсортированные по
	/// <c>Event.CreatedAtUtc</c> ASC, затем по <c>Change.Id</c>.
	/// </summary>
	Task<IReadOnlyList<JournalChange>> GetHistoryAsync(
		IReadOnlyCollection<JournalEntityRef> keys,
		DateTimeOffset upToCreatedAtUtc,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Возвращает все события, в которых произошло хотя бы одно изменение указанной сущности
	/// <c>(entityType, entityKey)</c>. У каждого события в <c>Changes</c> остаются только записи
	/// этой сущности (с полями). События отсортированы по <c>CreatedAtUtc</c> ASC.
	/// </summary>
	Task<IReadOnlyList<JournalEvent>> GetEntityHistoryAsync(
		string entityType,
		string entityKey,
		CancellationToken cancellationToken = default);

	void Add(JournalEvent journalEvent);

	void AddChange(JournalChange change);
}

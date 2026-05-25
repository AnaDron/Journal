using EventJournal.Core;

namespace EventJournal.Application;

/// <summary>
/// Управление контекстом записи журнала (событие и AsyncLocal до SaveChanges).
/// </summary>
public interface IJournalControlService {
	/// <summary>
	/// Начинает событие журнала и устанавливает AsyncLocal-контекст для последующих SaveChanges.
	/// При dispose контекст снимается. Заголовок события добавляется в DbContext и сохраняется вместе с изменениями.
	/// </summary>
	IJournalEventScope BeginEvent(JournalEventScopeOptions options);

	void AddChange(JournalChange change);

	void Add<T>(string key, T entity);

	void Update<T>(string key, T current, T updated);

	void Remove<T>(string key);
}

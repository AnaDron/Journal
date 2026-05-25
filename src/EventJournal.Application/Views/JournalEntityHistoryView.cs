namespace EventJournal.Application.Views;

/// <summary>
/// Read-side представление полной истории одной сущности: пара (EntityType, EntityKey),
/// её последнее известное имя из журнала и список изменений в обратном хронологическом
/// порядке (новые сверху) — для прямого отображения в UI.
/// </summary>
public sealed class JournalEntityHistoryView {
	public required string EntityType { get; init; }
	public required string EntityKey { get; init; }
	public string? EntityName { get; init; }
	public IReadOnlyList<JournalEntityHistoryItemView> Items { get; init; } = Array.Empty<JournalEntityHistoryItemView>();
}

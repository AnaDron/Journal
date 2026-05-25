namespace EventJournal.Application.Views;

/// <summary>
/// Read-side представление поля журнала. Содержит само значение из записи (<see cref="ValueJson"/>),
/// реконструированное «было» (<see cref="OldValueJson"/>), а для полей-ссылок — отображаемые имена
/// связанной сущности на момент события (<see cref="RelatedNewName"/> и <see cref="RelatedOldName"/>).
/// </summary>
public sealed record JournalFieldChangeView(
	string Path,
	string? ValueJson,
	string? OldValueJson,
	string? RelatedNewName = null,
	string? RelatedOldName = null);

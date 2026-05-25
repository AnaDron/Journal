using Aura.Journal.Core;

namespace Aura.Journal.Application.Views;

/// <summary>
/// Read-side представление одной строки истории сущности: журнальная запись + снимок
/// связанных метаданных события (когда, по какой причине и кем выполнена).
/// </summary>
public sealed record JournalEntityHistoryItemView(
	Guid Id,
	Guid EventId,
	DateTimeOffset CreatedAtUtc,
	DateTimeOffset? OccurredAtUtc,
	string? Reason,
	JournalInitiatorKind InitiatorKind,
	string? InitiatorId,
	JournalOperation Operation,
	IReadOnlyList<JournalFieldChangeView>? Fields);

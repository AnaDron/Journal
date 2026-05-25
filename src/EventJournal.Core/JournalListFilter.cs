namespace EventJournal.Core;

public sealed record JournalListFilter(
	int Page,
	int PageSize,
	string? EntityType,
	Guid? EventId,
	JournalOperation? Operation,
	JournalInitiatorKind? InitiatorKind,
	DateTimeOffset? CreatedFromUtc,
	DateTimeOffset? CreatedToUtc,
	DateTimeOffset? OccurredFromUtc,
	DateTimeOffset? OccurredToUtc);

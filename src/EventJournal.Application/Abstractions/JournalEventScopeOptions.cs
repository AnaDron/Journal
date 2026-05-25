using EventJournal.Core;

namespace EventJournal.Application;

public sealed record JournalEventScopeOptions(
	string? Reason = null,
	DateTimeOffset? OccurredAtUtc = null,
	JournalInitiatorKind InitiatorKind = JournalInitiatorKind.System,
	string? InitiatorId = null);

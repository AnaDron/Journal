namespace EventJournal.Application;

public sealed record JournalExecutionContext(Guid EventId, JournalEventScopeOptions Options);

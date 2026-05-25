namespace Aura.Journal.Application;

public sealed record JournalExecutionContext(Guid EventId, JournalEventScopeOptions Options);

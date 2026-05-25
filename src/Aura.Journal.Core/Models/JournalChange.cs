namespace Aura.Journal.Core;

public sealed record JournalChange(
	Guid Id,
	Guid EventId,
	string EntityType,
	string EntityKey,
	JournalOperation Operation,
	IReadOnlyList<JournalFieldChange>? Fields);

namespace EventJournal.Core;

public readonly record struct JournalEntityRef(string EntityType, string EntityKey);

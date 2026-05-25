namespace EventJournal.Core;

public sealed record JournalFieldChange(string Path, string? ValueJson);

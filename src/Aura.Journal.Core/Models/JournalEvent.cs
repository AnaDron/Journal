namespace Aura.Journal.Core;

public sealed class JournalEvent {
	public Guid Id { get; init; }

	public DateTimeOffset CreatedAtUtc { get; init; }

	public DateTimeOffset? OccurredAtUtc { get; init; }

	public string? Reason { get; init; }

	public JournalInitiatorKind InitiatorKind { get; init; }

	public string? InitiatorId { get; init; }

	public IReadOnlyList<JournalChange> Changes { get; init; } = Array.Empty<JournalChange>();
}

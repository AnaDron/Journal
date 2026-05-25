using EventJournal.Core;

namespace EventJournal.Infrastructure;

public sealed class JournalEventEntity {
	public Guid Id { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public DateTimeOffset? OccurredAtUtc { get; set; }

	public string? Reason { get; set; }

	public JournalInitiatorKind InitiatorKind { get; set; }

	public string? InitiatorId { get; set; }

	public ICollection<JournalChangeEntity> Changes { get; set; } = new List<JournalChangeEntity>();
}

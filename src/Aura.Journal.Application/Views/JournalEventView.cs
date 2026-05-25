using Aura.Journal.Core;

namespace Aura.Journal.Application.Views;

/// <summary>
/// Read-side представление события журнала. От доменного <see cref="JournalEvent"/>
/// отличается тем, что <see cref="Changes"/> состоят из обогащённых view-записей.
/// </summary>
public sealed class JournalEventView {
	public required Guid Id { get; init; }
	public required DateTimeOffset CreatedAtUtc { get; init; }
	public DateTimeOffset? OccurredAtUtc { get; init; }
	public string? Reason { get; init; }
	public required JournalInitiatorKind InitiatorKind { get; init; }
	public string? InitiatorId { get; init; }
	public IReadOnlyList<JournalChangeView> Changes { get; init; } = Array.Empty<JournalChangeView>();
}

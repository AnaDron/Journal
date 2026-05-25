namespace Aura.Journal.Application;

public interface IJournalEventScope : IDisposable {
	Guid EventId { get; }
}

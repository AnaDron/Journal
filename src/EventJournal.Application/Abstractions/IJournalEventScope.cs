namespace EventJournal.Application;

public interface IJournalEventScope : IDisposable {
	Guid EventId { get; }
}

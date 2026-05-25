using Aura.Journal.Application.Views;
using Aura.Journal.Core;

namespace Aura.Journal.Application;

public interface IJournalService {
	Task<Page<JournalEventView>> ListEventsAsync(JournalListFilter filter, CancellationToken cancellationToken = default);

	Task<JournalEventView?> GetEventByIdAsync(Guid id, CancellationToken cancellationToken = default);

	Task<JournalEntityHistoryView> GetEntityHistoryAsync(string entityType, string entityKey, CancellationToken cancellationToken = default);
}

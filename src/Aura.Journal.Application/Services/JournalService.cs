using Aura.Journal.Application.Views;
using Aura.Journal.Core;

namespace Aura.Journal.Application;

public sealed class JournalService(IJournalEventRepository eventRepository, JournalEnricher enricher) : IJournalService {
	public async Task<Page<JournalEventView>> ListEventsAsync(JournalListFilter filter, CancellationToken cancellationToken = default) {
		var page = Math.Max(1, filter.Page);
		var pageSize = Math.Clamp(filter.PageSize, 1, 200);
		var raw = await eventRepository.ListAsync(
			page,
			pageSize,
			filter.EntityType,
			filter.EventId,
			filter.Operation,
			filter.InitiatorKind,
			filter.CreatedFromUtc,
			filter.CreatedToUtc,
			filter.OccurredFromUtc,
			filter.OccurredToUtc,
			cancellationToken);
		return await enricher.EnrichAsync(raw, cancellationToken);
	}

	public async Task<JournalEventView?> GetEventByIdAsync(Guid id, CancellationToken cancellationToken = default) {
		var raw = await eventRepository.GetByIdAsync(id, cancellationToken);
		return await enricher.EnrichAsync(raw, cancellationToken);
	}

	public async Task<JournalEntityHistoryView> GetEntityHistoryAsync(
		string entityType,
		string entityKey,
		CancellationToken cancellationToken = default) {
		var raw = await eventRepository.GetEntityHistoryAsync(entityType, entityKey, cancellationToken);
		return await enricher.EnrichEntityHistoryAsync(entityType, entityKey, raw, cancellationToken);
	}
}

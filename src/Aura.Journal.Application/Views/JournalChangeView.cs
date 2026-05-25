using Aura.Journal.Core;

namespace Aura.Journal.Application.Views;

/// <summary>
/// Read-side представление журнальной записи об изменении сущности. Дополняет доменную
/// <see cref="JournalChange"/> отображаемым именем сущности (<see cref="EntityName"/>),
/// восстановленным из истории журнала, и view-полями со «было/стало».
/// </summary>
public sealed record JournalChangeView(
	Guid Id,
	Guid EventId,
	string EntityType,
	string EntityKey,
	string? EntityName,
	JournalOperation Operation,
	IReadOnlyList<JournalFieldChangeView>? Fields);

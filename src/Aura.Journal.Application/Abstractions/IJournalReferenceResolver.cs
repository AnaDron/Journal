namespace Aura.Journal.Application;

/// <summary>
/// Plugin-point: декларирует, что поле <see cref="FieldPath"/> сущности <see cref="EntityType"/>
/// — это ссылка на другую сущность <see cref="ReferencedEntityType"/>. Enricher по этой связке
/// резолвит имя связанной сущности из её собственной истории журнала.
/// </summary>
public interface IJournalReferenceResolver {
	string EntityType { get; }

	string FieldPath { get; }

	string ReferencedEntityType { get; }
}

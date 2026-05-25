namespace Aura.Journal.Application;

/// <summary>
/// Plugin-point: единая для приложения конвенция построения <c>EntityKey</c> из <c>(EntityType, Guid)</c>.
/// Используется Enricher'ом для построения ключа при резолве полей-ссылок. Дефолтная реализация
/// SDK возвращает <c>{EntityType}/{id:D}</c>; консьюмер регистрирует свой вариант (например,
/// <c>product/{id}</c>, <c>group/{id}</c>) и переопределяет конвенцию.
/// </summary>
public interface IJournalEntityKeyResolver {
	string MakeKey(string entityType, Guid id);
}

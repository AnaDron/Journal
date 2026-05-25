namespace EventJournal.Application;

/// <summary>
/// Plugin-point: вычисляет отображаемое имя сущности по её field-state журнала.
/// Каждая реализация привязана к одному <see cref="EntityType"/> и знает, какие поля
/// формируют имя (для Employee — три поля ФИО, для остальных — одно поле <c>name</c>).
/// Если для <c>EntityType</c> резолвер не зарегистрирован, Enricher возвращается к дефолту: поле <c>name</c>.
/// </summary>
public interface IJournalEntityNameResolver {
	/// <summary>Тип сущности журнала, для которого работает резолвер.</summary>
	string EntityType { get; }

	/// <summary>
	/// Имена полей, участвующих в построении имени. Нужны для <c>ReplayHistory</c>: перед операцией
	/// Delete состояние очищается, и имя берётся из снимка ДО операции — именно по этому списку.
	/// </summary>
	IEnumerable<string> NameRelevantFields { get; }

	/// <summary>
	/// Строит отображаемое имя из текущего field-state (значения — сырой JSON, строки в кавычках).
	/// Возвращает <c>null</c>, если имени собрать нельзя.
	/// </summary>
	string? Resolve(IReadOnlyDictionary<string, string?> fieldState);
}

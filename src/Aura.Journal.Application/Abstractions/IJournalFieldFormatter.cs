namespace Aura.Journal.Application;

/// <summary>
/// Plugin-point: преобразует сырое JSON-значение поля журнала для отображения
/// (например, enum-число → строка-имя). Привязан к паре (<see cref="EntityType"/>, <see cref="FieldPath"/>).
/// Если форматтер не зарегистрирован, Enricher оставляет исходное значение.
/// </summary>
public interface IJournalFieldFormatter {
	string EntityType { get; }

	string FieldPath { get; }

	/// <summary>
	/// Преобразует входной JSON. Возвращает преобразованное JSON-значение либо исходную строку,
	/// если преобразование к ней неприменимо.
	/// </summary>
	string? Format(string valueJson);
}

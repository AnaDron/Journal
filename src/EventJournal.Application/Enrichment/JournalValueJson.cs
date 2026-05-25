using System.Text.Json;

namespace EventJournal.Application;

/// <summary>
/// Утилиты для работы с сырыми JSON-значениями полей журнала. Значения в field-state хранятся
/// как сырые JSON-строки («"имя"», «10», «true», ...) — резолверы должны их раскавычивать.
/// </summary>
public static class JournalValueJson {
	/// <summary>
	/// Если входная строка — JSON-строка в кавычках, возвращает её содержимое; иначе тримит и
	/// возвращает как есть. Возвращает <c>null</c>, если вход <c>null</c>.
	/// </summary>
	public static string? Unquote(string? json) {
		if (json is null) {
			return null;
		}

		var trimmed = json.Trim();
		if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"') {
			try {
				return JsonSerializer.Deserialize<string>(trimmed);
			} catch (JsonException) {
				return trimmed;
			}
		}

		return trimmed;
	}
}

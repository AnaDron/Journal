using System.Text.Json;
using System.Text.Json.Nodes;
using Aura.Journal.Core;

namespace Aura.Journal.Infrastructure;

internal static class JournalChangesJson {
	internal static IReadOnlyList<JournalFieldChange> ParseChanges(string changesJson) {
		try {
			using var doc = JsonDocument.Parse(changesJson);
			var root = doc.RootElement;
			if (root.ValueKind != JsonValueKind.Array) {
				return Array.Empty<JournalFieldChange>();
			}

			var list = new List<JournalFieldChange>();
			foreach (var el in root.EnumerateArray()) {
				if (el.ValueKind != JsonValueKind.Object) {
					continue;
				}

				if (!el.TryGetProperty("path", out var pathProp)) {
					continue;
				}

				var path = pathProp.GetString() ?? "";
				string? valueJson = null;
				if (el.TryGetProperty("value", out var valueProp)) {
					valueJson = valueProp.ValueKind == JsonValueKind.Null ? null : valueProp.GetRawText();
				}

				list.Add(new JournalFieldChange(path, valueJson));
			}

			return list;
		} catch (JsonException) {
			return Array.Empty<JournalFieldChange>();
		}
	}

	internal static string? SerializeChanges(IReadOnlyList<JournalFieldChange>? changes) {
		if (changes is null || changes.Count == 0) {
			return null;
		}

		var arr = new JsonArray();
		foreach (var c in changes) {
			var o = new JsonObject { ["path"] = c.Path };
			if (c.ValueJson is null) {
				o["value"] = null;
			} else {
				try {
					o["value"] = JsonNode.Parse(c.ValueJson);
				} catch (JsonException) {
					o["value"] = JsonValue.Create(c.ValueJson);
				}
			}

			arr.Add(o);
		}

		return arr.ToJsonString(JournalJson.Options);
	}
}

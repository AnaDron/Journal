using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EventJournal.Core;

namespace EventJournal.Infrastructure;

/// <summary>
/// Разворот JSON-дерева в плоский список путей и значений (листья) для записи Create в журнал.
/// </summary>
public static class JournalCreateFieldFlatten {
	private static readonly JsonSerializerOptions ElementOptions = new() {
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	public static IReadOnlyList<JournalFieldChange> Flatten(JsonNode? root) {
		var list = new List<JournalFieldChange>();
		if (root is null) {
			return list;
		}

		WalkLeaves(root, "", (path, node) => list.Add(new JournalFieldChange(path, node.ToJsonString(ElementOptions))));
		return list;
	}

	private static void WalkLeaves(JsonNode node, string path, Action<string, JsonNode> onLeaf) {
		switch (node) {
			case JsonValue:
				onLeaf(TrimPath(path), node);
				break;
			case JsonObject obj:
				foreach (var p in obj) {
					if (p.Value is null) {
						continue;
					}

					WalkLeaves(p.Value, Join(path, p.Key), onLeaf);
				}

				break;
			case JsonArray arr:
				for (var i = 0; i < arr.Count; i++) {
					var item = arr[i];
					if (item is null) {
						continue;
					}

					WalkLeaves(item, Join(path, i.ToString(CultureInfo.InvariantCulture)), onLeaf);
				}

				break;
			default:
				onLeaf(TrimPath(path), node);
				break;
		}
	}

	private static string Join(string prefix, string segment) => string.IsNullOrEmpty(prefix) ? segment : $"{prefix}.{segment}";

	private static string TrimPath(string path) => path;
}

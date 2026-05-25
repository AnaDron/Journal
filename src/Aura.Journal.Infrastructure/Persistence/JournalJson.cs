using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aura.Journal.Infrastructure;

internal static class JournalJson {
	internal static readonly JsonSerializerOptions Options = new() {
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};
}

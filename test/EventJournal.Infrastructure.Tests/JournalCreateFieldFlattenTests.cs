using System.Text.Json.Nodes;
using EventJournal.Infrastructure;
using FluentAssertions;
using Xunit;

namespace EventJournal.Infrastructure.Tests;

public sealed class JournalCreateFieldFlattenTests {
	[Fact]
	public void Flatten_collects_primitive_and_nested_leaf_paths() {
		var root = JsonNode.Parse("""{"name":"x","qty":3,"nested":{"flag":true}}""")!;
		var flat = JournalCreateFieldFlatten.Flatten(root);

		flat.Should().HaveCount(3);
		flat.Should().Contain(c => c.Path == "name");
		flat.Should().Contain(c => c.Path == "qty");
		flat.Should().Contain(c => c.Path == "nested.flag");
	}
}

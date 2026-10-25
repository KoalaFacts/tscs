using System.Text.Json;
using System.Text.Json.Nodes;
using Tscs.Conformance;
using Xunit;
namespace Tscs.Tests;
public class FixtureConfigSelectionTests
{
 public static IEnumerable<object[]> Vectors(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/config-selection.json")));foreach(var r in d.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 [Theory,MemberData(nameof(Vectors))]
 public void SelectsFirstConfigAndPreservesOtherUnits(string id,string json){using var d=JsonDocument.Parse(json);Assert.True(JsonNode.DeepEquals(JsonNode.Parse(d.RootElement.GetProperty("expected").GetRawText()),JsonSerializer.SerializeToNode(FixtureConfigSelection.Probe(d.RootElement))),id);}
}

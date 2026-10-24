using System.Text.Json;
using System.Text.Json.Nodes;
using Tscs.Conformance;
using Xunit;
namespace Tscs.Tests;
public class FixtureFileMapTests
{
 public static IEnumerable<object[]> Vectors(){using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/fixture-map.json")));foreach(var r in doc.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 [Theory,MemberData(nameof(Vectors))]
 public void RootedFileAndLinkMapsMatchPinnedGo(string id,string json){Assert.NotEmpty(id);using var doc=JsonDocument.Parse(json);var expected=JsonNode.Parse(doc.RootElement.GetProperty("expected").GetRawText());var actual=JsonSerializer.SerializeToNode(FixtureFileMap.Probe(doc.RootElement));Assert.True(JsonNode.DeepEquals(expected,actual),id+"\n"+actual);}
}

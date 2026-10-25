using System.Text.Json;
using System.Text.Json.Nodes;
using Tscs.Conformance;
using Xunit;
namespace Tscs.Tests;
public class FixtureOptionValuesTests
{
 private static readonly JsonDocument Data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/option-values.json")));
 public static IEnumerable<object[]> Vectors(){foreach(var r in Data.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 [Theory,MemberData(nameof(Vectors))]
 public void SplitsMatchActualPinnedGo(string id,string json){using var d=JsonDocument.Parse(json);var r=d.RootElement;Assert.True(JsonNode.DeepEquals(JsonNode.Parse(r.GetProperty("expected").GetRawText()),JsonSerializer.SerializeToNode(FixtureOptionValues.Split(r.GetProperty("option").GetString()!,r.GetProperty("value").GetString()!,Data.RootElement.GetProperty("options")))),id);}
}

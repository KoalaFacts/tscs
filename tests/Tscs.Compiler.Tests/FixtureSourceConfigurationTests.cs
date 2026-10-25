using System.Text.Json;
using System.Text.Json.Nodes;
using Tscs.Conformance;
using Xunit;
namespace Tscs.Tests;
public class FixtureSourceConfigurationTests
{
 public static IEnumerable<object[]> Vectors(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/source-configurations.json")));foreach(var r in d.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 [Theory,MemberData(nameof(Vectors))]
 public void SourceSettingsAndRunnerFallbackMatchPinnedGo(string id,string json)
 {
  using var d=JsonDocument.Parse(json);var row=JsonNode.Parse(json)!;row["profile"]="source-configurations";
  using var output=new StringWriter();FixtureProtocol.Run(new StringReader(row.ToJsonString()+"\n"),output);
  Assert.True(JsonNode.DeepEquals(JsonNode.Parse(d.RootElement.GetProperty("expected").GetRawText()),JsonNode.Parse(output.ToString())!["expected"]),id);
 }
}

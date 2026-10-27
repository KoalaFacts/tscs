using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
namespace Tscs.Tests;
public class ConfigValueTests
{
 public static IEnumerable<object[]> Vectors(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/config-values.json")));foreach(var r in d.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 private static object Snapshot(ConfigValue v)=>new{kind=v.Kind,bits=v.Kind=="Number"?unchecked((ulong)BitConverter.DoubleToInt64Bits(v.Number)).ToString("x16"):"",units=v.Text.Select(c=>(int)c).ToArray(),properties=v.Properties.Select(p=>new{key=p.Key.Select(c=>(int)c).ToArray(),value=Snapshot(p.Value)}).ToArray(),elements=v.Elements.Select(Snapshot).ToArray(),isNil=v.IsNil};
 [Theory,MemberData(nameof(Vectors))]
 public void ConvertsSupportedAstLikeActualPinnedConvertToObject(string id,string json)
 {
  using var d=JsonDocument.Parse(json);var r=d.RootElement;Assert.Equal(id,r.GetProperty("id").GetString());var parsed=JsonSyntax.Parse("/tsconfig.json",r.GetProperty("source").GetString()!);Assert.DoesNotContain(parsed.Diagnostics,x=>x.Code.StartsWith("TSCS"));
  var value=JsonConfigValues.Convert(parsed.Root);Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(Snapshot(value)),JsonNode.Parse(r.GetProperty("value").GetRawText())),id);Assert.Empty(r.GetProperty("diagnostics").EnumerateArray());
 }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
namespace Tscs.Tests;
public class JsonScalarOptionTests
{
 public static IEnumerable<object[]> Rows(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/json-scalar-options.json")));foreach(var r in d.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 private static object Snapshot(ConfigValue v)=>new{kind=v.Kind,bits=v.Kind=="Number"?unchecked((ulong)BitConverter.DoubleToInt64Bits(v.Number)).ToString("x16"):"",units=v.Text.Select(c=>(int)c).ToArray(),properties=v.Properties.Select(p=>new{key=p.Key.Select(c=>(int)c).ToArray(),value=Snapshot(p.Value)}).ToArray(),elements=v.Elements.Select(Snapshot).ToArray(),isNil=v.IsNil};
 [Theory,MemberData(nameof(Rows))]
 public void ScalarConversionAndDiagnosticsMatchActualPrivateGo(string id,string json)
 {
  using var d=JsonDocument.Parse(json);var r=d.RootElement;Assert.Equal(id,r.GetProperty("id").GetString());
  var o=r.GetProperty("option");var descriptor=new JsonScalarOption(o.GetProperty("name").GetString()!,new(o.GetProperty("kind").GetString()!,o.GetProperty("disallowNull").GetBoolean()),o.GetProperty("commandLineOnly").GetBoolean());
  var parsed=JsonSyntax.Parse("/tsconfig.json",r.GetProperty("source").GetString()!);Assert.Empty(parsed.Diagnostics);
  var property=Assert.Single(Assert.Single(parsed.Root!.Properties).Value.Properties);
  bool located=r.GetProperty("located").GetBoolean();
  var result=JsonScalarOptions.Convert(descriptor,JsonConfigValues.Convert(property.Value),located?"/tsconfig.json":"",located?property.Name.Token:null,located?property.Value.Token:null);
  Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(Snapshot(result.Value)),JsonNode.Parse(r.GetProperty("value").GetRawText())),id+" value");
  var diagnostics=result.Diagnostics.Select(x=>new{code=x.Code,message=x.Message,start=x.Start,length=x.Length}).ToArray();
  Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(diagnostics),JsonNode.Parse(r.GetProperty("diagnostics").GetRawText())),id+" diagnostics: "+JsonSerializer.Serialize(diagnostics));
  Assert.All(result.Diagnostics,x=>Assert.Equal(located?"/tsconfig.json":"",x.File));
 }
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tscs.Conformance;
using Xunit;
namespace Tscs.Tests;
public class JsonOptionsMapTests
{
 public static IEnumerable<object[]> Rows(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/json-options-map.json")));foreach(var r in d.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 private static object Snapshot(ConfigValue v)=>new{kind=v.Kind,bits=v.Kind=="Number"?unchecked((ulong)BitConverter.DoubleToInt64Bits(v.Number)).ToString("x16"):"",units=v.Text.Select(c=>(int)c).ToArray(),properties=v.Properties.Select(p=>new{key=p.Key.Select(c=>(int)c).ToArray(),value=Snapshot(p.Value)}).ToArray(),elements=v.Elements.Select(Snapshot).ToArray(),isNil=v.IsNil};
 private static object[] Diagnostics(IEnumerable<Diagnostic> ds)=>ds.Select(x=>(object)new{code=x.Code,message=x.Message,start=x.Start,length=x.Length}).ToArray();
 private static void Equal(object actual,JsonElement expected,string id)=>Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(actual),JsonNode.Parse(expected.GetRawText())),id+": "+JsonSerializer.Serialize(actual));
 [Theory,MemberData(nameof(Rows))]
 public void OrderedMapDispatchAndDiagnosticsMatchActualPrivateGo(string id,string json)
 {
  using var d=JsonDocument.Parse(json);var r=d.RootElement;Assert.Equal(id,r.GetProperty("id").GetString());
  var parsed=JsonSyntax.Parse("/tsconfig.json",r.GetProperty("source").GetString()!);
  Equal(Diagnostics(parsed.Diagnostics),r.GetProperty("parserDiagnostics"),id+" parser");
  Assert.DoesNotContain(parsed.Diagnostics,x=>x.Code.StartsWith("TSCS",StringComparison.Ordinal));
  var root=JsonConfigValues.Convert(parsed.Root);
  var options=root.Properties.FirstOrDefault(p=>p.Key=="compilerOptions")?.Value??new("Null","",0,false,[],[]);
  var result=JsonOptionsFromJson.Convert(options,r.GetProperty("basePath").GetString()!);
  // This old Go golden fully captured the formerly unsupported results. Its
  // historical classification stays intact while we compare every call.
  foreach(var old in r.GetProperty("unsupported").EnumerateArray())
   Assert.Contains(JsonOptionsFromJson.Declarations,x=>x.Name==old.GetString()&&x.Supported);
  Assert.Empty(result.Unsupported);
  Equal(result.Calls.Select(p=>new{key=p.Key.Select(c=>(int)c).ToArray(),value=Snapshot(p.Value)}).ToArray(),r.GetProperty("calls"),id+" calls");
  Equal(Diagnostics(result.Diagnostics),r.GetProperty("diagnostics"),id+" options");
  Assert.All(result.Diagnostics,x=>Assert.Equal("",x.File));
 }
 [Fact]
 public void AllPinnedCompilerDeclarationsHaveConversionPolicies()
 {
  Assert.Equal(126,JsonOptionsFromJson.Declarations.Count);
  Assert.Equal(125,JsonOptionsFromJson.Declarations.Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count());
  Assert.All(JsonOptionsFromJson.Declarations,x=>Assert.True(x.Supported,x.Name));
  Assert.Equal(15,JsonOptionsFromJson.Declarations.Count(x=>x.PathKind!=0||x.ExtraValidation=="locale"||x.Kind=="list"&&x.Element?.PathKind!=0));
 }
 public static IEnumerable<object[]> SourceRows(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/json-options-map.json")));var vectors=d.RootElement.GetProperty("vectors").EnumerateArray().ToDictionary(v=>v.GetProperty("id").GetString()!);foreach(var b in d.RootElement.GetProperty("sourceBindings").EnumerateArray())yield return [b.GetProperty("id").GetString()!,b.GetRawText(),vectors[b.GetProperty("id").GetString()!].GetRawText()];}
 [Theory,MemberData(nameof(SourceRows))]
 public void RealFixturePreparationFeedsTheProductMapWithSeparateOutcomes(string id,string binding,string json)
 {
  var b=JsonNode.Parse(binding)!.AsObject();var req=b["request"]!.DeepClone().AsObject();req["profile"]="config-options";
  using var input=new StringReader(req.ToJsonString(new JsonSerializerOptions{WriteIndented=false})+"\n");using var output=new StringWriter();FixtureProtocol.Run(input,output);
  using var d=JsonDocument.Parse(output.ToString());var actual=d.RootElement.GetProperty("expected");using var r=JsonDocument.Parse(json);var row=r.RootElement;
  Assert.Equal(id,d.RootElement.GetProperty("id").GetString());
  Assert.True(JsonNode.DeepEquals(b["selection"],JsonNode.Parse(actual.GetProperty("preparation").GetProperty("selection").GetRawText())));
  Assert.True(JsonNode.DeepEquals(b["context"],JsonNode.Parse(actual.GetProperty("preparation").GetProperty("context").GetRawText())));
  Assert.Equal(0,actual.GetProperty("unsupportedOptions").GetArrayLength());
  Equal(actual.GetProperty("preparation").GetProperty("ast").GetProperty("diagnostics"),row.GetProperty("parserDiagnostics"),id);
  bool diagnostics=row.GetProperty("parserDiagnostics").GetArrayLength()+row.GetProperty("diagnostics").GetArrayLength()>0;
  Assert.Equal(diagnostics?"OPTIONS_DIAGNOSTICS":"OPTIONS",actual.GetProperty("outcome").GetString());
  Equal(actual.GetProperty("calls"),row.GetProperty("calls"),id);
  Equal(actual.GetProperty("optionDiagnostics"),row.GetProperty("diagnostics"),id);
 }
 [Theory]
 [InlineData("const x=1;","NOT_APPLICABLE")]
 [InlineData("// @filename: tsconfig.json\n{\"compilerOptions\":{\"strict\":a.b}}","UNSUPPORTED")]
 public void MissingOrUnsupportedConfigNeverReceivesOptionCredit(string source,string expected)
 {
  var request=JsonSerializer.Serialize(new{id="guard",input=Convert.ToBase64String(Encoding.UTF8.GetBytes(source)),fileName=Convert.ToBase64String("fixture.ts"u8),@implicit=false,failAt=0,returnNil=false,profile="config-options"});
  using var input=new StringReader(request+"\n");using var output=new StringWriter();FixtureProtocol.Run(input,output);using var d=JsonDocument.Parse(output.ToString());var row=d.RootElement.GetProperty("expected");Assert.Equal(expected,row.GetProperty("outcome").GetString());Assert.Equal(JsonValueKind.Null,row.GetProperty("calls").ValueKind);
 }

 [Theory]
 [InlineData("zz\\ud800\\ud800zz","zz\uFFFDzz")]
 [InlineData("zz\\udc00\\udc00zz","zz\uFFFDzz")]
 [InlineData("zz\\udc00\\ud800zz","zz\uFFFDzz")]
 [InlineData("zz\\ud800X\\ud800zz","zz\uFFFDX\uFFFDzz")]
 [InlineData("zz\\ud83d\\ude00zz","zz\U0001F600zz")]
 [InlineData("zz\\ud800\\ud83d\\ude00\\ud800zz","zz\uFFFD\U0001F600\uFFFDzz")]
 public void InvalidWtf8OptionNamesUsePinnedDiagnosticArgumentFormatting(string escapedKey,string printedKey)
 {
  string json="{\"compilerOptions\":{\""+escapedKey+"\":true}}";
  var parsed=JsonSyntax.Parse("/tsconfig.json",json);
  Assert.Empty(parsed.Diagnostics);
  var root=JsonConfigValues.Convert(parsed.Root);
  var options=Assert.Single(root.Properties).Value;
  var rawKey=Assert.Single(options.Properties).Key;
  Assert.Equal(Assert.Single(Assert.Single(parsed.Root!.Properties).Value.Properties).Name.Token.Value,rawKey);
  var converted=JsonOptionsFromJson.Convert(options);
  Assert.Empty(converted.Calls);
  Assert.Empty(converted.Unsupported);
  var diagnostic=Assert.Single(converted.Diagnostics);
  Assert.Equal("TS5023",diagnostic.Code);
  string expected="Unknown compiler option '"+printedKey+"'.";
  Assert.Equal(expected,diagnostic.Message);

  // Execute the actual fixture JSONL transport as well as the product entry.
  string fixture="// @filename: tsconfig.json\n"+json;
  var request=JsonSerializer.Serialize(new
  {
   id="invalid-wtf8-option",input=Convert.ToBase64String(Encoding.UTF8.GetBytes(fixture)),
   fileName=Convert.ToBase64String("fixture.ts"u8),@implicit=false,failAt=0,returnNil=false,
   profile="config-options"
  });
  using var input=new StringReader(request+"\n");using var output=new StringWriter();
  FixtureProtocol.Run(input,output);
  using var response=JsonDocument.Parse(output.ToString());
  var result=response.RootElement.GetProperty("expected");
  Assert.Equal("OPTIONS_DIAGNOSTICS",result.GetProperty("outcome").GetString());
  Assert.Equal(JsonValueKind.Null,result.GetProperty("unsupported").ValueKind);
  var ds=result.GetProperty("optionDiagnostics");
  Assert.Equal(1,ds.GetArrayLength());
  Assert.Equal("TS5023",ds[0].GetProperty("code").GetString());
  Assert.Equal(expected,ds[0].GetProperty("message").GetString());
 }
}

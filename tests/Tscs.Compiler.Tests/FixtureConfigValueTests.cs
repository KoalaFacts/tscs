using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tscs.Conformance;
using Xunit;
namespace Tscs.Tests;
public class FixtureConfigValueTests
{
 public static IEnumerable<object[]> Sources()
 {
  using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/source-config-values.json")));
  foreach(var row in d.RootElement.GetProperty("vectors").EnumerateArray())yield return [row.GetProperty("id").GetString()!,row.GetRawText()];
 }
 private static JsonElement Run(JsonElement request)
 {
  using var input=new StringReader(JsonSerializer.Serialize(request)+"\n");using var output=new StringWriter();FixtureProtocol.Run(input,output);
  using var actual=JsonDocument.Parse(output.ToString());Assert.Equal(request.GetProperty("id").GetString(),actual.RootElement.GetProperty("id").GetString());return actual.RootElement.GetProperty("expected").Clone();
 }
 private static void Equal(JsonElement expected,JsonElement actual)=>Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected.GetRawText()),JsonNode.Parse(actual.GetRawText())),actual.GetRawText());
 [Theory,MemberData(nameof(Sources))]
 public void SourceSelectedConfigValuesAndDiagnosticsMatchActualPinnedGo(string id,string json)
 {
  using var d=JsonDocument.Parse(json);var row=d.RootElement;Assert.Equal(id,row.GetProperty("request").GetProperty("id").GetString());var actual=Run(row.GetProperty("request"));var preparation=actual.GetProperty("preparation");var reference=row.GetProperty("reference");
  Equal(row.GetProperty("selection"),preparation.GetProperty("selection"));Equal(row.GetProperty("context"),preparation.GetProperty("context"));
  Assert.Equal(reference.GetProperty("fileName").GetString(),preparation.GetProperty("context").GetProperty("configPath").GetString());
  Assert.Equal(reference.GetProperty("sourceBytes").GetString(),preparation.GetProperty("selection").GetProperty("configFile").GetProperty("content").GetString());
  Equal(reference.GetProperty("parserDiagnostics"),preparation.GetProperty("ast").GetProperty("diagnostics"));
  Equal(reference.GetProperty("value"),actual.GetProperty("value"));Equal(reference.GetProperty("conversionDiagnostics"),actual.GetProperty("conversionDiagnostics"));
  Assert.Equal(JsonValueKind.Null,actual.GetProperty("unsupported").ValueKind);
  Assert.Equal(reference.GetProperty("parserDiagnostics").GetArrayLength()==0?"VALUE":"VALUE_DIAGNOSTICS",actual.GetProperty("outcome").GetString());
 }
 [Theory]
 [InlineData("let x;","NOT_APPLICABLE",null)]
 [InlineData("// @filename: tsconfig.json\n{\"n\":1.5}","UNSUPPORTED",null)]
 [InlineData("// @filename: tsconfig.json\n{\"n\":}","UNSUPPORTED",null)]
 [InlineData("// @filename: tsconfig.json\n/* empty */","VALUE","EmptySource")]
 [InlineData("// @filename: tsconfig.json\n{}","VALUE","Object")]
 public void AbsentUnsupportedAndEmptyConfigsCannotMasqueradeAsEachOther(string source,string outcome,string? kind)
 {
  var request=JsonSerializer.SerializeToElement(new{id="guard",input=Convert.ToBase64String(Encoding.UTF8.GetBytes(source)),fileName=Convert.ToBase64String("fixture.ts"u8),@implicit=false,failAt=0,returnNil=false,profile="config-values"});
  var actual=Run(request);Assert.Equal(outcome,actual.GetProperty("outcome").GetString());
  if(kind==null)Assert.Equal(JsonValueKind.Null,actual.GetProperty("value").ValueKind);else Assert.Equal(kind,actual.GetProperty("value").GetProperty("kind").GetString());
  Assert.Equal(outcome=="UNSUPPORTED",actual.GetProperty("unsupported").ValueKind==JsonValueKind.String);
 }
}

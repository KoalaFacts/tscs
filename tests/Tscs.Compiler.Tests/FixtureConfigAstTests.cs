using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tscs.Conformance;
using Xunit;
namespace Tscs.Tests;
public class FixtureConfigAstTests
{
 [Theory]
 [InlineData(0)]
 [InlineData(9)]
 [InlineData(11)]
 [InlineData(13)]
 [InlineData(17)]
 [InlineData(21)]
 public void SelectedFixtureConfigUsesTheProductAstAndLosslessValues(int index)
 {
  using var golden=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/json-ast.json")));var row=golden.RootElement.GetProperty("vectors")[index];
  var source="// @filename: a.ts\nlet a;\n// @filename: TSConfig.JSON\n"+row.GetProperty("source").GetString()+"\n// @filename: jsconfig.json\n{}";
  var request=JsonSerializer.SerializeToElement(new{id="fixture",input=Convert.ToBase64String(Encoding.UTF8.GetBytes(source)),fileName=Convert.ToBase64String("fixture.ts"u8),@implicit=false,failAt=0,returnNil=false,profile="config-ast"});
  using var input=new StringReader(request.GetRawText()+"\n");using var output=new StringWriter();FixtureProtocol.Run(input,output);
  using var result=JsonDocument.Parse(output.ToString());var actual=result.RootElement.GetProperty("expected");Assert.Equal("fixture",result.RootElement.GetProperty("id").GetString());Assert.Equal(1,actual.GetProperty("selection").GetProperty("configIndex").GetInt32());
  Assert.Equal(2,actual.GetProperty("selection").GetProperty("units").GetArrayLength());
  Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("nodes").GetRawText()),JsonNode.Parse(actual.GetProperty("ast").GetProperty("nodes").GetRawText())));
  var expected=row.GetProperty("diagnostics").EnumerateArray().Select(d=>new{code="TS"+d.GetProperty("code").GetInt32(),start=d.GetProperty("start").GetInt32(),length=d.GetProperty("length").GetInt32(),message=d.GetProperty("message").GetString()});
  Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected),JsonNode.Parse(actual.GetProperty("ast").GetProperty("diagnostics").GetRawText())));Assert.Equal(JsonValueKind.Null,actual.GetProperty("unsupported").ValueKind);
 }
 [Theory]
 [InlineData("", "/.src/pkg/tsconfig.json")]
 [InlineData("// @currentDirectory: /project\n", "/project/pkg/tsconfig.json")]
 public void ParserFilenameUsesTheRootedConfigContext(string prefix,string expected)
 {
  var source=prefix+"// @filename: pkg/tsconfig.json\n{}";
  var q=JsonSerializer.SerializeToElement(new{input=Convert.ToBase64String(Encoding.UTF8.GetBytes(source)),fileName=Convert.ToBase64String("fixture.ts"u8),@implicit=false,failAt=0,returnNil=false});
  var p=JsonSerializer.SerializeToElement(FixtureConfigAst.Probe(q));
  Assert.Equal(expected,Encoding.UTF8.GetString(Convert.FromBase64String(p.GetProperty("context").GetProperty("configPath").GetString()!)));
 }
 [Fact]
 public void MissingPropertyCommaRetainsTheActualPinnedSourceConfigTreeAndDiagnostic()
 {
  using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/source-config-ast-missing-comma.json")));
  var actual=FixtureConfigAst.Probe(data.RootElement.GetProperty("request"));
  Assert.True(JsonNode.DeepEquals(JsonNode.Parse(data.RootElement.GetProperty("expected").GetRawText()),JsonSerializer.SerializeToNode(actual)));
 }
}

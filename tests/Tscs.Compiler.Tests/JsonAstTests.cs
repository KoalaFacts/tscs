using System.Text.Json;
using Tscs;
using Xunit;
namespace Tscs.Tests;
public class JsonAstTests
{
 public static IEnumerable<object[]> Vectors(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/json-ast.json")));foreach(var r in d.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 private static IEnumerable<(string Path,JsonSyntaxNode Node)> Walk(JsonSyntaxNode? node,string path="$")
 {
  if(node==null)yield break;yield return(path,node);
  for(int i=0;i<node.Properties.Count;i++){foreach(var p in Walk(node.Properties[i].Name,path+"/p"+i+"/name"))yield return p;foreach(var p in Walk(node.Properties[i].Value,path+"/p"+i+"/value"))yield return p;}
  for(int i=0;i<node.Elements.Count;i++)foreach(var p in Walk(node.Elements[i],path+"/e"+i))yield return p;
 }
 [Theory,MemberData(nameof(Vectors))]
 public void BoundedJsonAstAndQuoteDiagnosticsMatchActualPinnedParser(string id,string json)
 {
  using var d=JsonDocument.Parse(json);var r=d.RootElement;Assert.Equal(id,r.GetProperty("id").GetString());var actual=JsonSyntax.Parse("tsconfig.json",r.GetProperty("source").GetString()!);var nodes=Walk(actual.Root,"$").ToArray();var expected=r.GetProperty("nodes").EnumerateArray().ToArray();Assert.Equal(expected.Length,nodes.Length);
  for(int i=0;i<nodes.Length;i++){var (path,n)=nodes[i];var e=expected[i];Assert.Equal(e.GetProperty("path").GetString(),path);Assert.Equal(e.GetProperty("kind").GetString(),n.Kind);Assert.Equal(e.GetProperty("start").GetInt32(),n.Token.Start);Assert.Equal(e.GetProperty("end").GetInt32(),n.Token.Start+n.Token.Length);Assert.Equal(e.GetProperty("number").GetString(),n.Number);Assert.Equal(e.GetProperty("units").EnumerateArray().Select(x=>x.GetUInt16()),(n.Token.Value??"").Select(x=>(ushort)x));}
  var ds=r.GetProperty("diagnostics").EnumerateArray().ToArray();Assert.Equal(ds.Length,actual.Diagnostics.Count);for(int i=0;i<ds.Length;i++){var e=ds[i];var a=actual.Diagnostics[i];Assert.Equal("TS"+e.GetProperty("code").GetInt32(),a.Code);Assert.Equal(e.GetProperty("start").GetInt32(),a.Start);Assert.Equal(e.GetProperty("length").GetInt32(),a.Length);Assert.Equal(e.GetProperty("message").GetString(),a.Message);}
 }
 [Theory]
 [InlineData("{")]
 [InlineData("[1,,2]")]
 [InlineData("1.5")]
 [InlineData("{} {}")]
 public void UnportedGrammarDoesNotBecomeAcceptedConfig(string source)=>Assert.Contains(JsonSyntax.Parse("tsconfig.json",source).Diagnostics,d=>d.Code=="TSCS1001");
 [Fact]
 public void JsonNullTokenIsSupportedInSourceWithoutEnablingUnsupportedThisKeyword()
 {
  Assert.Empty(Compilation.Check("a.ts","let n = null;"));
  Assert.Equal("TSCS1001",Assert.Single(Compilation.Check("a.ts","let n = this;")).Code);
 }
 [Fact]
 public void ExcessiveJsonNestingRemainsBounded()=>Assert.Contains(JsonSyntax.Parse("tsconfig.json",new string('[',130)+new string(']',130)).Diagnostics,d=>d.Code=="TSCS1001");
 [Theory]
 [InlineData("ⸯ")]
 [InlineData("aⸯ")]
 [InlineData("é")]
 [InlineData("漢")]
 public void UnportedUnicodeUnquotedNamesStayExplicitlyUnsupported(string name)=>Assert.Contains(JsonSyntax.Parse("tsconfig.json","{"+name+":true}").Diagnostics,d=>d.Code=="TSCS1001");
}

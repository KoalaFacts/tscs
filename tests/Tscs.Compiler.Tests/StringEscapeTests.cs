using System.Text.Json;
using Tscs;
using Xunit;
namespace Tscs.Tests;
public class StringEscapeTests
{
 public static IEnumerable<object[]> Vectors(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/string-escapes.json")));foreach(var r in d.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 [Theory,MemberData(nameof(Vectors))]
 public void TokenValueAndSpellingMatchActualPinnedGo(string id,string json)
 {
  using var d=JsonDocument.Parse(json);var r=d.RootElement;Assert.Equal(id,r.GetProperty("id").GetString());
  var literal=r.GetProperty("literal").GetString()!;var tokens=new Scanner("case.ts",literal).Scan();var token=tokens[0];
  Assert.Equal(TokenKind.String,token.Kind);Assert.Equal(literal,token.Text);Assert.Equal(r.GetProperty("tokenEndUtf16").GetInt32(),token.Length);
  Assert.Equal(r.GetProperty("value").GetString(),token.Value);Assert.False(token.LineBreak);
  Assert.False(new Scanner("case.ts",literal+" next").Scan()[1].LineBreak);
  Assert.True(new Scanner("case.ts","\n"+literal).Scan()[0].LineBreak);
  Assert.Empty(Compilation.Check("case.ts","let s: string = "+literal+";"));
 }
 [Fact]
 public void TrailingBackslashRemainsExplicitlyUnsupported()=>Assert.Equal("TSCS1001",Assert.Single(Compilation.Check("case.ts","let s = \"a\\")).Code);
 [Theory]
 [InlineData("\\u0061")]
 [InlineData("\\x61")]
 [InlineData("\\0")]
 [InlineData("\\8")]
 [InlineData("\\q")]
 public void UnportedEscapeBranchesRemainExplicitlyUnsupported(string escape)=>Assert.Equal("TSCS1001",Assert.Single(Compilation.Check("case.ts","let s = \""+escape+"\";")).Code);
}

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
 public void TrailingBackslashReportsBothLexicalErrors()=>Assert.Equal(new[]{"TS1002","TS1126"},Compilation.Check("case.ts","let s = \"a\\").Select(d=>d.Code));
 [Theory]
 [InlineData("\\uZZZZ","TS1125")]
 [InlineData("\\xZZ","TS1125")]
 [InlineData("\\0","")]
 [InlineData("\\8","TS1488")]
 [InlineData("\\q","")]
 public void EscapeBranchesReportSupportedDiagnosticsOrAcceptance(string escape,string code){var diagnostics=Compilation.Check("case.ts","let s = \""+escape+"\";");if(code.Length==0)Assert.Empty(diagnostics);else Assert.Equal(code,Assert.Single(diagnostics).Code);}
}

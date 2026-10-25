using System.Text.Json;
using Tscs;
using Xunit;
namespace Tscs.Tests;
public class UnicodeEscapeTests
{
 public static IEnumerable<object[]> Vectors(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/unicode-escapes.json")));foreach(var r in d.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 [Theory,MemberData(nameof(Vectors))]
 public void Utf16ValuesMatchActualPinnedGoWithoutSurrogateReplacement(string id,string json)
 {
  using var d=JsonDocument.Parse(json);var r=d.RootElement;Assert.Equal(id,r.GetProperty("id").GetString());
  string literal=r.GetProperty("literal").GetString()!;var token=new Scanner("case.ts",literal).Scan()[0];
  Assert.Equal(TokenKind.String,token.Kind);Assert.Equal(literal,token.Text);Assert.Equal(r.GetProperty("tokenEndUtf16").GetInt32(),token.Length);
  Assert.Equal(r.GetProperty("valueUnits").EnumerateArray().Select(v=>v.GetUInt16()),token.Value!.Select(c=>(ushort)c));
  Assert.Empty(Compilation.Check("case.ts","let s: string = "+literal+";"));
 }
 [Theory]
 [InlineData("\\u{}")]
 [InlineData("\\u{110000}")]
 [InlineData("\\u{61")]
 [InlineData("\\u{xyz}")]
 public void UnportedMalformedEscapeDiagnosticsRemainExplicit(string escape)=>Assert.Equal("TSCS1001",Assert.Single(Compilation.Check("case.ts","let s=\""+escape+"\";")).Code);
}

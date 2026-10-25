using System.Text.Json;
using Tscs;
using Xunit;
namespace Tscs.Tests;
public class StringLineTests
{
 public static IEnumerable<object[]> Vectors(){using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/string-lines.json")));foreach(var r in data.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 [Theory,MemberData(nameof(Vectors))]
 public void UnescapedStringLineCharactersMatchPinnedScanner(string id,string json)
 {
  using var data=JsonDocument.Parse(json);var r=data.RootElement;Assert.Equal(id,r.GetProperty("id").GetString());const string prefix="let s: string = ";
  var diagnostics=Compilation.Check("case.ts",prefix+r.GetProperty("literal").GetString()+";");
  if(r.GetProperty("unterminated").GetBoolean()){var d=Assert.Single(diagnostics);Assert.Equal("TS1002",d.Code);Assert.Equal(prefix.Length+r.GetProperty("tokenEndUtf16").GetInt32(),d.Start);}
  else { Assert.Empty(diagnostics);Assert.Equal(r.GetProperty("value").GetString(),new Scanner("case.ts",r.GetProperty("literal").GetString()!).Scan()[0].Value); }
 }
 [Theory]
 [InlineData("\u2028")]
 [InlineData("\u2029")]
 public void UnicodeLineSeparatorOutsideStringStillSeparatesStatements(string separator)=>Assert.Empty(Compilation.Check("case.ts","let s = 1"+separator+"s = 2;"));
}

using System.Text.Json;
using Tscs;
using Xunit;
namespace Tscs.Tests;
public class NumericIdentityTests
{
 public static IEnumerable<object[]> Vectors(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/numeric-identity.json")));foreach(var r in d.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 [Theory,MemberData(nameof(Vectors))]
 public void RemainingStringEscapesMatchActualPinnedGo(string id,string json)
 {
  using var d=JsonDocument.Parse(json);var r=d.RootElement;Assert.Equal(id,r.GetProperty("id").GetString());string source=r.GetProperty("source").GetString()!;
  var scanner=new Scanner("case.ts",source);var tokens=scanner.Scan();var expected=r.GetProperty("tokens").EnumerateArray().ToArray();Assert.Equal(expected.Length,tokens.Count);
  for(int i=0;i<tokens.Count;i++)
  {
   var e=expected[i];var t=tokens[i];Assert.Equal(e.GetProperty("kind").GetString(),t.Kind.ToString());Assert.Equal(e.GetProperty("start").GetInt32(),t.Start);Assert.Equal(e.GetProperty("end").GetInt32(),t.Start+t.Length);Assert.Equal(e.GetProperty("text").GetString(),t.Text);Assert.Equal(e.GetProperty("lineBreak").GetBoolean(),t.LineBreak);Assert.Equal(e.GetProperty("flags").GetInt32(),(int)t.Flags);
   Assert.Equal(e.GetProperty("valueUnits").EnumerateArray().Select(v=>v.GetUInt16()),(t.Value??"").Select(c=>(ushort)c));
  }
  var diagnostics=r.GetProperty("diagnostics").EnumerateArray().ToArray();Assert.Equal(diagnostics.Length,scanner.Diagnostics.Count);
  for(int i=0;i<diagnostics.Length;i++){var e=diagnostics[i];var a=scanner.Diagnostics[i];Assert.Equal("TS"+e.GetProperty("code").GetInt32(),a.Code);Assert.Equal(e.GetProperty("start").GetInt32(),a.Start);Assert.Equal(e.GetProperty("length").GetInt32(),a.Length);Assert.Equal(e.GetProperty("message").GetString(),a.Message);}
  if(diagnostics.Length>0){var analysis=Compilation.Analyze("case.ts",source);Assert.True(analysis.HasSyntaxErrors);Assert.Equal(scanner.Diagnostics.OrderBy(x=>x.Start).ThenBy(x=>x.Length).ThenBy(x=>x.Code,StringComparer.Ordinal),analysis.Diagnostics);}
 }
}

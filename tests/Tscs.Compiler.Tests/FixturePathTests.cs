using System.Text.Json;
using Tscs.Conformance;
using Xunit;
namespace Tscs.Tests;
public class FixturePathTests
{
 public static IEnumerable<object[]> Vectors(){using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/paths.json")));foreach(var r in doc.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 [Theory,MemberData(nameof(Vectors))]
 public void BytePathOperationsMatchActualPinnedGo(string id,string json)
 {
  Assert.NotEmpty(id);using var doc=JsonDocument.Parse(json);var row=doc.RootElement;var expected=row.GetProperty("expected");
  var path=Convert.FromBase64String(row.GetProperty("path").GetString()!);var directory=Convert.FromBase64String(row.GetProperty("directory").GetString()!);
  Assert.Equal(expected.GetProperty("encodedRoot").GetInt32(),FixturePaths.EncodedRootLength(path));
  Assert.Equal(expected.GetProperty("normalize").GetString(),Convert.ToBase64String(FixturePaths.Normalize(path)));
  Assert.Equal(expected.GetProperty("combine").GetString(),Convert.ToBase64String(FixturePaths.Combine(directory,path)));
  byte[]? result=null;string? error=null;try{result=FixturePaths.ToRooted(path,directory);}catch(InvalidOperationException e){error=e.Message;}
  Assert.Equal(expected.GetProperty("error").GetString(),error);
  Assert.Equal(expected.GetProperty("rooted").GetString(),result is null?null:Convert.ToBase64String(result));
 }
}

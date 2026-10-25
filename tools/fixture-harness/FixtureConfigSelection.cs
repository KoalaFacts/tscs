using System.Text.Json;
using System.Text;
namespace Tscs.Conformance;
// Pre-parse unit selection only; not a parsed/effective tsconfig result.
public static class FixtureConfigSelection
{
 private static bool IsConfig(byte[] name)
 {
  byte[] slashes=name.Select(b=>b==(byte)'\\'?(byte)'/':b).ToArray();
  int Root(byte[] p){int n=FixturePaths.EncodedRootLength(p);return n<0?~n:n;}
  if(Root(slashes)==slashes.Length)return false;
  if(slashes.Length>0&&slashes[^1]==(byte)'/')slashes=slashes[..^1];
  int start=Math.Max(Root(slashes),Array.LastIndexOf(slashes,(byte)'/')+1);
  string basename=PinnedGoLowercase.Lower(Encoding.UTF8.GetString(slashes.AsSpan(start)));
  return basename is "tsconfig.json" or "jsconfig.json";
 }
 public static object Probe(JsonElement request)
 {
  // Reuse raw parsing without constructing VFS or executing external mappers.
  var row=System.Text.Json.Nodes.JsonNode.Parse(request.GetRawText())!;
  row["implicit"]=false;row["failAt"]=0;row["returnNil"]=false;
  var raw=JsonSerializer.SerializeToElement(FixtureProtocol.Probe(JsonSerializer.SerializeToElement(row)),new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
  if(raw.GetProperty("panic").ValueKind!=JsonValueKind.Null)return new{panic=raw.GetProperty("panic").GetString()};
  var units=new List<object>();object? configFile=null;int configIndex=-1,i=0;
  foreach(var unit in raw.GetProperty("units").EnumerateArray())
  {
   byte[] name=Convert.FromBase64String(unit.GetProperty("name").GetString()!);
   var snapshot=new{name,content=Convert.FromBase64String(unit.GetProperty("content").GetString()!)};
   if(configIndex<0&&IsConfig(name)){configFile=snapshot;configIndex=i;}else units.Add(snapshot);
   i++;
  }
  bool runExternalCode=raw.GetProperty("options").TryGetProperty("runexternalcode",out var option)&&Convert.FromBase64String(option.GetString()!).AsSpan().SequenceEqual("true"u8);
  return new{units,configFile,configIndex,runExternalCode,panic=(string?)null};
 }
}

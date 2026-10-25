using System.Text;
using System.Text.Json;
namespace Tscs.Conformance;
// Case-sensitive developer profile joining raw fixture mapping to real VFS queries.
public static class FixtureConstruction
{
 public static object Probe(JsonElement request)
 {
  try
  {
   var map=JsonSerializer.SerializeToElement(FixtureFileMap.Probe(request));
   if(map.GetProperty("panic").ValueKind!=JsonValueKind.Null)return new{panic=map.GetProperty("panic").GetString()};
   var entries=new Dictionary<string,object>(StringComparer.Ordinal);
   var queries=new SortedSet<string>(StringComparer.Ordinal);
   byte[] Decode(JsonElement value)=>Convert.FromBase64String(value.GetString()!);
   string Text(JsonElement value)=>Encoding.Latin1.GetString(Decode(value));
   string directory=Text(map.GetProperty("directory"));queries.Add(directory);
   foreach(var file in map.GetProperty("files").EnumerateArray())
   {
    string path=Text(file.GetProperty("path"));queries.Add(path);
    entries[path]=new{path=Decode(file.GetProperty("path")),data=Decode(file.GetProperty("content")),link=false};
   }
   var aliases=new HashSet<string>(StringComparer.Ordinal);
   foreach(var link in map.GetProperty("links").EnumerateArray())
   {
    string alias=Text(link.GetProperty("alias")),target=Text(link.GetProperty("target"));
    if(!aliases.Add(alias))return new{unsupported="normalized link alias collision"};
    entries[alias]=new{path=Decode(link.GetProperty("alias")),data=Decode(link.GetProperty("target")),link=true};
    queries.Add(alias);queries.Add(target);
   }
   var fs=new FixtureVfs.Snapshot(JsonSerializer.SerializeToElement(entries.Values.ToArray()));
   var results=new List<Dictionary<string,JsonElement>>();
   foreach(string q in queries)
   {
    byte[] path=Encoding.Latin1.GetBytes(q);
    var row=JsonSerializer.SerializeToElement(fs.Query(path)).EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value);
    foreach(var property in JsonSerializer.SerializeToElement(fs.Entries(path)).EnumerateObject())row[property.Name]=property.Value;
    results.Add(row);
   }
   return new{queries=results,panic=(string?)null};
  }
  catch(InvalidOperationException e){return new{panic=e.Message};}
  catch(NotSupportedException e){return new{unsupported=e.Message};}
 }
}

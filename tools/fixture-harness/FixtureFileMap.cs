using System.Text.Json;
using System.Text;
namespace Tscs.Conformance;
public static class FixtureFileMap
{
 public static object Probe(JsonElement request)
 {
  byte[] input=Convert.FromBase64String(request.GetProperty("input").GetString()!);
  byte[] fileName=Convert.FromBase64String(request.GetProperty("fileName").GetString()!);
  try
  {
   var parsed=FixtureLoader.Parse(input,fileName,(name,body,options)=>new FixtureCallbackResult<FixtureUnit>(new(name,body,new Dictionary<string,byte[]>(options)),null));
   byte[] directory=Encoding.UTF8.GetBytes("/.src");
   if(parsed.Directory.Length>0)directory=FixturePaths.ToRooted(parsed.Directory,directory);
   var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
   foreach(var unit in parsed.Units??[])files[Encoding.Latin1.GetString(FixturePaths.ToRooted(unit.Name,directory))]=unit.Content;
   var entries=files.OrderBy(p=>Convert.ToHexString(Encoding.Latin1.GetBytes(p.Key)),StringComparer.Ordinal).Select(p=>new{path=Encoding.Latin1.GetBytes(p.Key),content=p.Value}).ToArray();
   // Preserve every raw alias when normalized aliases collide. Choosing the VFS
   // map insertion order here would hide upstream Go-map-order ambiguity.
   var links=parsed.Links.OrderBy(l=>Convert.ToHexString(l.Alias),StringComparer.Ordinal).Select(l=>new{rawAlias=l.Alias,rawTarget=l.Target,alias=FixturePaths.NormalizedAbsolute(l.Alias,directory),target=FixturePaths.NormalizedAbsolute(l.Target,directory)}).ToArray();
   return new{directory,files=entries,links,unitCount=parsed.Units?.Count??0,panic=(string?)null};
  }
  catch(InvalidOperationException e){return new{panic=e.Message};}
 }
}

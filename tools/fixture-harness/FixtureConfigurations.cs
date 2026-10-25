using System.Text.Json;
namespace Tscs.Conformance;
// GetFileBasedTestConfigurations helper only; runner fallback is not applied here.
public static class FixtureConfigurations
{
 private sealed record Named(string Name,Dictionary<string,string> Config);
 private static string ByteKey(string value)=>Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(value));
 private static int CompareConfigs(Dictionary<string,string> a,Dictionary<string,string> b)
 {
  var ak=a.Keys.OrderBy(ByteKey,StringComparer.Ordinal).ToArray();var bk=b.Keys.OrderBy(ByteKey,StringComparer.Ordinal).ToArray();
  for(int i=0;i<Math.Min(ak.Length,bk.Length);i++){int c=StringComparer.Ordinal.Compare(ByteKey(ak[i]),ByteKey(bk[i]));if(c!=0)return c;c=StringComparer.Ordinal.Compare(ByteKey(a[ak[i]]),ByteKey(b[bk[i]]));if(c!=0)return c;}
  return ak.Length.CompareTo(bk.Length);
 }
 public static object Expand(JsonElement settings,JsonElement varyBy,JsonElement declarations)
 {
  object Result(List<Named>? configurations=null,string? panic=null,bool fatal=false)=>new{configurations,panic,fatal};
  var varying=varyBy.EnumerateArray().Select(v=>v.GetString()!).ToHashSet(StringComparer.Ordinal);
  var scalars=new Dictionary<string,string>(StringComparer.Ordinal);
  var axes=new List<(string Key,string[] Values)>();int count=1;
  foreach(var setting in settings.EnumerateObject().OrderBy(p=>ByteKey(p.Name),StringComparer.Ordinal))
  {
   string value=setting.Value.GetString()!;
   if(!varying.Contains(setting.Name)){scalars[setting.Name]=value;continue;}
   var split=JsonSerializer.SerializeToElement(FixtureOptionValues.Split(setting.Name,value,declarations));
   if(split.GetProperty("fatal").GetBoolean())return Result(fatal:true);
   if(split.GetProperty("panic").ValueKind!=JsonValueKind.Null)return Result(panic:split.GetProperty("panic").GetString());
   var values=split.GetProperty("values");if(values.ValueKind==JsonValueKind.Null)continue;
   string[] entries=values.EnumerateArray().Select(v=>v.GetString()!).ToArray();
   if(entries.Length>1){if(count>25/entries.Length)return Result(fatal:true);count*=entries.Length;axes.Add((setting.Name,entries));}
   else if(entries.Length==1)scalars[setting.Name]=entries[0];
  }
  if(axes.Count==0)return scalars.Count==0?Result():Result([new("",scalars)]);
  var products=new List<Dictionary<string,string>>{new(StringComparer.Ordinal)};
  foreach(var (key,values) in axes)products=products.SelectMany(p=>values.Select(v=>{var c=new Dictionary<string,string>(p,StringComparer.Ordinal){[key]=v};return c;})).ToList();
  var named=new List<Named>();
  foreach(var p in products)
  {
   string name=string.Join(',',p.OrderBy(e=>ByteKey(e.Key),StringComparer.Ordinal).Select(e=>e.Key+"="+FixtureOptionValues.Lower(e.Value)));
   foreach(var scalar in scalars)p[scalar.Key]=scalar.Value;
   named.Add(new(name,p.OrderBy(e=>ByteKey(e.Key),StringComparer.Ordinal).ToDictionary(e=>e.Key,e=>e.Value,StringComparer.Ordinal)));
  }
  // Normalize only Go map/product enumeration order; preserve names and values.
  return Result(named.OrderBy(n=>ByteKey(n.Name),StringComparer.Ordinal).ThenBy(n=>n.Config,Comparer<Dictionary<string,string>>.Create(CompareConfigs)).ToList());
 }
}

using System.Text.Json;
namespace Tscs.Conformance;
// splitOptionValues, driven by declarations extracted from the exact Go oracle.
// It splits values only; products, effective options and config parsing are separate.
public static class FixtureOptionValues
{
 private static bool Space(char c)=>c is >= '\t' and <= '\r' or ' ' or '\u0085' or '\u00a0' or '\u1680' or >= '\u2000' and <= '\u200a' or '\u2028' or '\u2029' or '\u202f' or '\u205f' or '\u3000';
 private static string Trim(string s){int a=0,b=s.Length;while(a<b&&Space(s[a]))a++;while(b>a&&Space(s[b-1]))b--;return s[a..b];}
 // Pinned declaration names are ASCII. Go EqualFold additionally recognizes these
 // two non-ASCII members of ASCII simple-fold cycles; no culture-sensitive casing.
 private static string FoldName(string s)=>new(s.Select(c=>c=='\u017f'?'s':c=='\u212a'?'k':c is >= 'A' and <= 'Z'?(char)(c+32):c).ToArray());
 private static string Lower(string s)=>s.Replace('\u0130','i').ToLowerInvariant();
 public static object Split(string option,string value,JsonElement declarations)
 {
  object Result(string[]? values=null,string? panic=null,bool fatal=false)=>new{values,panic,fatal};
  if(value.Length==0)return Result();
  JsonElement? declaration=null;
  foreach(var d in declarations.EnumerateArray())if(FoldName(d.GetProperty("name").GetString()!)==FoldName(option)){declaration=d;break;}
  string? Canonical(string token)
  {
   if(declaration is not { } d)return null;
   string kind=d.GetProperty("kind").GetString()!;
   if(kind=="boolean")return Lower(token) switch{"true"=>"bool:true","false"=>"bool:false",_=>null};
   if(kind=="enum")
   {
    string key=Lower(token);
    foreach(var entry in d.GetProperty("values").EnumerateArray())if(entry.GetProperty("key").GetString()==key)return entry.GetProperty("canonical").GetString();
    return null;
   }
   return "string:"+token;
  }
  bool star=false;var includes=new List<string>();var excludes=new List<string>();
  foreach(string raw in value.Split(','))
  {
   string token=Trim(raw);if(token.Length==0)continue;
   if(token=="*")star=true;else if(token[0] is '-' or '!')excludes.Add(token[1..]);else includes.Add(token);
  }
  if(includes.Count==0&&!star&&excludes.Count==0)return Result();
  var variants=new Dictionary<string,string>(StringComparer.Ordinal);
  foreach(string token in includes){string? key=Canonical(token);if(key is null)return Result(fatal:true);variants.TryAdd(key,token);}
  if(star&&declaration is { } decl)foreach(var e in decl.GetProperty("values").EnumerateArray()){string token=e.GetProperty("key").GetString()!;variants.TryAdd(Canonical(token)!,token);}
  foreach(string token in excludes){string? key=Canonical(token);if(key is not null)variants.Remove(key);}
  if(variants.Count==0)return Result(panic:$"Variations in test option '@{option}' resulted in an empty set.");
  // Go map iteration is intentionally not compared; oracle sorts returned values.
  return Result(variants.Values.OrderBy(s=>Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(s)),StringComparer.Ordinal).ToArray());
 }
}

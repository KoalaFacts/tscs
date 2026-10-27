using System.Text.Json;
namespace Tscs;
internal sealed record JsonEnumEntry(string Key,ConfigValue Value,bool Deprecated=false);
internal sealed record JsonEnumOption(string Name,IReadOnlyList<JsonEnumEntry>? Entries,bool CommandLineOnly=false,bool DisallowNull=false);
internal static class JsonEnumOptions
{
 public static IReadOnlyDictionary<string,JsonEnumOption> Declarations {get;}=Load();
 private static IReadOnlyDictionary<string,JsonEnumOption> Load()
 {
  using var stream=typeof(JsonEnumOptions).Assembly.GetManifestResourceStream("Tscs.EnumDeclarations")??throw new InvalidOperationException("Missing pinned enum declarations");
  using var d=JsonDocument.Parse(stream);
  return d.RootElement.EnumerateArray().Select(ReadDeclaration).ToDictionary(o=>o.Name,StringComparer.Ordinal);
 }
 internal static JsonEnumOption ReadDeclaration(JsonElement o)=>new(o.GetProperty("name").GetString()!,o.GetProperty("entries").EnumerateArray().Select(e=>new JsonEnumEntry(e.GetProperty("key").GetString()!,Read(e.GetProperty("value")),e.GetProperty("deprecated").GetBoolean())).ToArray(),o.GetProperty("commandLineOnly").GetBoolean(),o.GetProperty("disallowNull").GetBoolean());

 private static ConfigValue Read(JsonElement v)
 {
  string kind=v.GetProperty("kind").GetString()!;
  if(kind is not ("String" or "Number"))throw new InvalidOperationException("Unimplemented enum value projection");
  return new(kind,new string(v.GetProperty("units").EnumerateArray().Select(x=>(char)x.GetInt32()).ToArray()),kind=="Number"?BitConverter.Int64BitsToDouble(unchecked((long)System.Convert.ToUInt64(v.GetProperty("bits").GetString(),16))):0,false,[],[]);
 }
 public static JsonScalarOptionResult Convert(JsonEnumOption option,ConfigValue value,string file="",Token? name=null,Token? expression=null)
 {
  if(option.CommandLineOnly)return Error("TS6266",$"Option '{option.Name}' can only be specified on command line.",name);
  if(!JsonCompilerOptionTypes.Accepts(new("enum",option.DisallowNull),value))return Error("TS5024",$"Compiler option '{option.Name}' requires a value of type enum.",expression);
  if(value.Kind=="Null"||value.Text.Length==0||option.Entries==null)return new(new("Null","",0,false,[],[]),[]);
  string key=PinnedEnumLowercase.Lower(value.Text);
  var match=option.Entries.FirstOrDefault(e=>e.Key==key);
  if(match!=null)return new(match.Value,[]);
  string allowed="'"+string.Join("', '",option.Entries.Where(e=>!e.Deprecated).Select(e=>e.Key))+"'";
  return Error("TS6046",$"Argument for '--{option.Name}' option must be: {allowed}.",expression);

  JsonScalarOptionResult Error(string code,string message,Token? token)
   =>new(new("Null","",0,false,[],[]),[file.Length>0&&token is Token location?Diagnostic.Error(file,location,code,message):new(code,"error","",-1,0,message,[],[])]);
 }
}

namespace Tscs;
internal sealed record JsonOptionType(string Kind,bool DisallowNull,JsonOptionType? Element=null);
internal static class JsonCompilerOptionTypes
{
 // Pinned isCompilerOptionsValue: shape admission only, not enum membership,
 // per-element validation, command-line-only restrictions or path conversion.
 public static bool Accepts(JsonOptionType? option,ConfigValue value)
 {
  if(option==null)return false;
  if(value.Kind=="Null")return !option.DisallowNull;
  return option.Kind switch
  {
   "list"=>value.Kind=="Array",
   "listOrElement"=>value.Kind=="Array"||Accepts(option.Element,value),
   "string" or "enum"=>value.Kind=="String",
   "boolean"=>value.Kind is "True" or "False",
   "number"=>value.Kind=="Number",
   "object"=>value.Kind=="Object",
   _=>false
  };
 }
}

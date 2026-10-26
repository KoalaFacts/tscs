namespace Tscs;
// Bounded list conversion: caller supplies a primitive/object element declaration
// with no path/extra-validation policy; optional enum element uses the pinned map. This is not the whole option map.
internal sealed record JsonListOption(string Name,JsonScalarOption Element,bool PreserveFalsy,bool CommandLineOnly=false,JsonEnumOption? EnumElement=null);
internal static class JsonListOptions
{
 public static JsonScalarOptionResult Convert(JsonListOption option,ConfigValue value,string file="",Token? name=null,JsonSyntaxNode? expression=null)
 {
  if(option.CommandLineOnly)return Error("TS6266",$"Option '{option.Name}' can only be specified on command line.",name);
  if(value.Kind is not ("Array" or "Null"))return Error("TS5024",$"Compiler option '{option.Name}' requires a value of type Array.",expression?.Token);
  if(value.Kind=="Null")return new(new("Array","",0,true,[],[]),[]);
  var values=new List<ConfigValue>();var diagnostics=new List<Diagnostic>();
  for(int index=0;index<value.Elements.Count;index++)
  {
   // The pinned function indexes the original syntax list using the converted
   // value index, even when convertToObject has filtered scalar nulls earlier.
   Token? token=expression?.Elements[index].Token;
   var result=option.EnumElement is JsonEnumOption enumOption
    ?JsonEnumOptions.Convert(enumOption,value.Elements[index],file,name,token)
    :JsonScalarOptions.Convert(option.Element,value.Elements[index],file,name,token);
   diagnostics.AddRange(result.Diagnostics);
   if(option.PreserveFalsy||!Falsy(result.Value))values.Add(result.Value);
  }
  return new(new("Array","",0,value.IsNil,[],values),diagnostics);

  JsonScalarOptionResult Error(string code,string message,Token? token)
   =>new(new("Null","",0,false,[],[]),[file.Length>0&&token is Token location?Diagnostic.Error(file,location,code,message):new(code,"error","",-1,0,message,[],[])]);
 }
 private static bool Falsy(ConfigValue value)=>value.Kind is "Null" or "False"||value.Kind=="Number"&&value.Number==0||value.Kind=="String"&&value.Text.Length==0;
}

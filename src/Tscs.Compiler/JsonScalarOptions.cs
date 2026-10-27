namespace Tscs;
// Caller supplies only a declaration without path/extra validation. Enum/list
// conversion has a different policy and must not enter this bounded component.
internal sealed record JsonScalarOption(string Name,JsonOptionType Type,bool CommandLineOnly=false);
internal sealed record JsonScalarOptionResult(ConfigValue Value,IReadOnlyList<Diagnostic> Diagnostics);
internal static class JsonScalarOptions
{
 public static JsonScalarOptionResult Convert(JsonScalarOption option,ConfigValue value,string file="",Token? name=null,Token? expression=null)
 {
  if(option.Type.Kind is not ("boolean" or "number" or "string" or "object"))
   throw new NotSupportedException("This option conversion branch is not implemented.");
  if(option.CommandLineOnly)
   return Error("TS6266",$"Option '{option.Name}' can only be specified on command line.",name);
  if(!JsonCompilerOptionTypes.Accepts(option.Type,value))
   return Error("TS5024",$"Compiler option '{option.Name}' requires a value of type {option.Type.Kind}.",expression);
  return new(value,[]);

  JsonScalarOptionResult Error(string code,string message,Token? token)
  {
   // As upstream, a source without the relevant node still gets a compiler
   // diagnostic. A bare compiler diagnostic has no source range.
   var diagnostic=file.Length>0&&token is Token location
    ?Diagnostic.Error(file,location,code,message)
    :new Diagnostic(code,"error","",-1,0,message,[],[]);
   return new(new("Null","",0,false,[],[]),[diagnostic]);
  }
 }
}

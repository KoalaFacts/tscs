using System.Text;
using System.Text.Json;
namespace Tscs.Conformance;
// Real fixture preparation -> product value -> declaration-driven option map.
// This pre-VFS profile never executes external code or fabricates effective options.
public static class FixtureConfigOptions
{
 public static object Probe(JsonElement request)
 {
  var (preparation,parsed)=FixtureConfigAst.Prepare(request);var p=JsonSerializer.SerializeToElement(preparation);
  string outcome;string? unsupported=null;object? calls=null;object[] optionDiagnostics=[];IReadOnlyList<string> unsupportedOptions=[];
  if(p.GetProperty("selection").GetProperty("panic").ValueKind!=JsonValueKind.Null)outcome="PREPARATION_PANIC";
  else if(p.GetProperty("selection").GetProperty("configFile").ValueKind==JsonValueKind.Null)outcome="NOT_APPLICABLE";
  else if(p.GetProperty("unsupported").ValueKind==JsonValueKind.String){outcome="UNSUPPORTED";unsupported=p.GetProperty("unsupported").GetString();}
  else if(p.GetProperty("ast").GetProperty("unsupported").ValueKind==JsonValueKind.String){outcome="UNSUPPORTED";unsupported=p.GetProperty("ast").GetProperty("unsupported").GetString();}
  else
  {
   if(parsed==null)throw new InvalidOperationException("Selected supported config has no parse result");
   var value=JsonConfigValues.Convert(parsed.Root);var options=value.Properties.FirstOrDefault(x=>x.Key=="compilerOptions")?.Value??new("Null","",0,false,[],[]);
   string directory=new UTF8Encoding(false,true).GetString(Convert.FromBase64String(p.GetProperty("context").GetProperty("directory").GetString()!));
   var result=JsonOptionsFromJson.Convert(options,directory);unsupportedOptions=result.Unsupported;
   calls=result.Calls.Select(x=>new{key=x.Key.Select(c=>(int)c).ToArray(),value=Snapshot(x.Value)}).ToArray();
   optionDiagnostics=result.Diagnostics.Select(x=>(object)new{code=x.Code,message=x.Message,start=x.Start,length=x.Length}).ToArray();
   outcome=result.Unsupported.Count>0?"UNSUPPORTED":parsed.Diagnostics.Count+result.Diagnostics.Count>0?"OPTIONS_DIAGNOSTICS":"OPTIONS";
   if(result.Unsupported.Count>0)unsupported="OPTION_POLICY";
  }
  return new{preparation,calls,optionDiagnostics,unsupportedOptions,unsupported,outcome};
 }
 private static object Snapshot(ConfigValue v)=>new{kind=v.Kind,bits=v.Kind=="Number"?unchecked((ulong)BitConverter.DoubleToInt64Bits(v.Number)).ToString("x16"):"",units=v.Text.Select(c=>(int)c).ToArray(),properties=v.Properties.Select(p=>new{key=p.Key.Select(c=>(int)c).ToArray(),value=Snapshot(p.Value)}).ToArray(),elements=v.Elements.Select(Snapshot).ToArray(),isNil=v.IsNil};
}

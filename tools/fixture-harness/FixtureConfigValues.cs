using System.Text.Json;
namespace Tscs.Conformance;
// Pre-VFS, no option declarations/notifier: syntax errors remain visible and
// unsupported or absent parses never enter the converter as an empty source.
public static class FixtureConfigValues
{
 public static object Probe(JsonElement request)
 {
  var (preparation,parsed)=FixtureConfigAst.Prepare(request);
  var envelope=JsonSerializer.SerializeToElement(preparation);
  string outcome;string? unsupported=null;object? value=null;
  if(envelope.GetProperty("selection").GetProperty("panic").ValueKind!=JsonValueKind.Null)outcome="PREPARATION_PANIC";
  else if(envelope.GetProperty("selection").GetProperty("configFile").ValueKind==JsonValueKind.Null)outcome="NOT_APPLICABLE";
  else if(envelope.GetProperty("unsupported").ValueKind==JsonValueKind.String){outcome="UNSUPPORTED";unsupported=envelope.GetProperty("unsupported").GetString();}
  else if(envelope.GetProperty("ast").GetProperty("unsupported").ValueKind==JsonValueKind.String){outcome="UNSUPPORTED";unsupported=envelope.GetProperty("ast").GetProperty("unsupported").GetString();}
  else
  {
   if(parsed==null)throw new InvalidOperationException("Selected supported config has no parse result");
   value=Snapshot(JsonConfigValues.Convert(parsed.Root));outcome=parsed.Diagnostics.Count==0?"VALUE":"VALUE_DIAGNOSTICS";
  }
  return new{preparation,value,conversionDiagnostics=Array.Empty<object>(),unsupported,outcome};
 }
 private static object Snapshot(ConfigValue value)=>new
 {
  kind=value.Kind,
  bits=value.Kind=="Number"?unchecked((ulong)BitConverter.DoubleToInt64Bits(value.Number)).ToString("x16"):"",
  units=value.Text.Select(c=>(int)c).ToArray(),
  properties=value.Properties.Select(p=>new{key=p.Key.Select(c=>(int)c).ToArray(),value=Snapshot(p.Value)}).ToArray(),
  elements=value.Elements.Select(Snapshot).ToArray(),isNil=value.IsNil
 };
}

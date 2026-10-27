using System.Text;
using System.Text.Json;
namespace Tscs.Conformance;
// Pre-VFS selected config syntax: not makeUnitsFromTest/effective options.
public static class FixtureConfigAst
{
 private static readonly UTF8Encoding StrictUtf8=new(false,true);
 public static object Probe(JsonElement request)=>Prepare(request).Envelope;
 // Share the actual parsed AST with the value profile, without parsing twice.
 internal static (object Envelope,JsonSyntaxResult? Parsed) Prepare(JsonElement request)
 {
  var envelope=ProbeCore(request,out var parsed);return (envelope,parsed);
 }
 private static object ProbeCore(JsonElement request,out JsonSyntaxResult? parsedResult)
 {
  parsedResult=null;
  var selection=JsonSerializer.SerializeToElement(FixtureConfigSelection.Probe(request));
  if(selection.GetProperty("panic").ValueKind!=JsonValueKind.Null)return new{selection,context=(object?)null,ast=(object?)null,unsupported=(string?)null};
  var parsedUnits=FixtureLoader.Parse(Convert.FromBase64String(request.GetProperty("input").GetString()!),Convert.FromBase64String(request.GetProperty("fileName").GetString()!),(unitName,body,options)=>new FixtureCallbackResult<FixtureUnit?>(null,null));
  byte[] directory="/.src"u8.ToArray();if(parsedUnits.Directory.Length>0)directory=FixturePaths.ToRooted(parsedUnits.Directory,directory);
  var config=selection.GetProperty("configFile");byte[]? configPath=config.ValueKind==JsonValueKind.Null?null:FixturePaths.ToRooted(Convert.FromBase64String(config.GetProperty("name").GetString()!),directory);
  var context=new{directory,configPath};
  if(configPath==null)return new{selection,context,ast=(object?)null,unsupported=(string?)null};
  string name,text;
  try{name=StrictUtf8.GetString(configPath);text=StrictUtf8.GetString(Convert.FromBase64String(config.GetProperty("content").GetString()!));}
  catch(DecoderFallbackException){return new{selection,context,ast=(object?)null,unsupported="INVALID_UTF8_CONFIG"};}
  var parsed=JsonSyntax.Parse(name,text);parsedResult=parsed;var nodes=new List<object>();
  void Walk(JsonSyntaxNode? node,string path)
  {
   if(node==null)return;
   nodes.Add(new{path,kind=node.Kind,start=node.Token.Start,end=node.Token.Start+node.Token.Length,number=node.Number,units=(node.Token.Value??"").Select(c=>(int)c).ToArray()});
   for(int i=0;i<node.Properties.Count;i++){Walk(node.Properties[i].Name,path+"/p"+i+"/name");Walk(node.Properties[i].Value,path+"/p"+i+"/value");}
   for(int i=0;i<node.Elements.Count;i++)Walk(node.Elements[i],path+"/e"+i);
  }
  Walk(parsed.Root,"$");
  var ast=new{nodes,diagnostics=parsed.Diagnostics.Select(d=>new{code=d.Code,start=d.Start,length=d.Length,message=d.Message}).ToArray(),unsupported=parsed.Diagnostics.Any(d=>d.Code.StartsWith("TSCS",StringComparison.Ordinal))?"JSON_GRAMMAR":(string?)null,panic=(string?)null};
  return new{selection,context,ast,unsupported=(string?)null};
 }
}

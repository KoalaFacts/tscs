using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tscs.Compiler;
namespace Tscs.Conformance;
public static class FixtureSourceConfigurations
{
 private static readonly JsonDocument Policy=JsonDocument.Parse(typeof(FixtureSourceConfigurations).Assembly.GetManifestResourceStream("Tscs.Conformance.ConfigurationPolicy")!);
 private static readonly Regex Option=new(@"^//[\t\n\f\r ]*@([A-Za-z0-9_]+)[\t\n\f\r ]*:[\t\n\f\r ]*([^\r\n]*)",RegexOptions.Multiline|RegexOptions.CultureInvariant|RegexOptions.NonBacktracking);
 private static readonly UTF8Encoding Utf8=new(false,true);
 public static object Probe(JsonElement request)
 {
  string runner=request.GetProperty("runner").GetString()!;
  if(runner is not ("compiler" or "transpile"))throw new JsonException("Unknown configuration runner");
  byte[] decoded=SourceDecoder.Decode(Convert.FromBase64String(request.GetProperty("input").GetString()!));
  var settings=new Dictionary<string,byte[]>(StringComparer.Ordinal);
  // Whole decoded byte string, unlike line-by-line file/link processing. Go's
  // ASCII whitespace can cross line boundaries; @link remains its raw value.
  foreach(Match m in Option.Matches(Encoding.Latin1.GetString(decoded)))
  {
   byte[] value=FixtureDirectives.TrimSpace(m.Groups[2].Value);
   if(value.Length>0&&value[^1]==(byte)';')value=value[..^1];
   settings[m.Groups[1].Value.ToLowerInvariant()]=value;
  }
  var text=new Dictionary<string,string>(StringComparer.Ordinal);
  try {foreach(var (key,value) in settings)text[key]=Utf8.GetString(value);}
  catch(DecoderFallbackException){return new{settings,helper=(object?)null,configurations=(object?)null,panic=(string?)null,fatal=false,unsupported="INVALID_UTF8_SETTING"};}
  var expanded=JsonSerializer.SerializeToElement(FixtureConfigurations.Expand(JsonSerializer.SerializeToElement(text),Policy.RootElement.GetProperty(runner+"VaryBy"),Policy.RootElement.GetProperty("options")));
  var helper=expanded.GetProperty("configurations");
  bool fatal=expanded.GetProperty("fatal").GetBoolean();string? panic=expanded.GetProperty("panic").GetString();
  object? configurations=fatal||panic is not null?null:helper.ValueKind!=JsonValueKind.Null?helper:
    runner=="compiler"?new object?[]{null}:new object[]{new{Name="",Config=text}};
  // Use JsonElement to preserve the pinned Go NamedTestConfiguration field names
  // under the outer transport's camel-case serializer.
  if(configurations is not null&&helper.ValueKind==JsonValueKind.Null&&runner=="transpile")configurations=JsonSerializer.SerializeToElement(configurations);
  return new{settings,helper,configurations,panic,fatal,unsupported=(string?)null};
 }
}

using System.Text;
using System.Text.Json;
namespace Tscs;
internal sealed record JsonOptionsMapResult(IReadOnlyList<ConfigProperty> Calls,IReadOnlyList<Diagnostic> Diagnostics,IReadOnlyList<string> Unsupported);
internal sealed record JsonOptionDeclaration(string Name,string Kind,bool DisallowNull,bool CommandLineOnly,bool Supported,bool PreserveFalsy,JsonOptionDeclaration? Element,JsonEnumOption? Enum,int PathKind,string ExtraValidation);
// convertOptionsFromJson dispatch/ParseOption boundary only. Calls remain values;
// typed CompilerOptions/defaults/host remain separate work.
internal static class JsonOptionsFromJson
{
 internal static IReadOnlyList<JsonOptionDeclaration> Declarations {get;}=Load();
 private static readonly IReadOnlyDictionary<string,JsonOptionDeclaration> NameMap=Names();
 private static IReadOnlyList<JsonOptionDeclaration> Load()
 {
  using var stream=typeof(JsonOptionsFromJson).Assembly.GetManifestResourceStream("Tscs.OptionDeclarations")??throw new InvalidOperationException("Missing pinned option declarations");
  using var data=JsonDocument.Parse(stream);return data.RootElement.EnumerateArray().Select(Read).ToArray();
 }
 private static JsonOptionDeclaration Read(JsonElement o)
 {
  string kind=o.GetProperty("kind").GetString()!;
  return new(o.GetProperty("name").GetString()!,kind,o.GetProperty("disallowNull").GetBoolean(),o.GetProperty("commandLineOnly").GetBoolean(),o.GetProperty("supported").GetBoolean(),o.TryGetProperty("preserveFalsy",out var falsy)&&falsy.GetBoolean(),o.TryGetProperty("element",out var element)?Read(element):null,kind=="enum"?JsonEnumOptions.ReadDeclaration(o):null,o.GetProperty("pathKind").GetInt32(),o.GetProperty("extraValidation").GetString()??"");
 }
 private static IReadOnlyDictionary<string,JsonOptionDeclaration> Names()
 {
  var result=new Dictionary<string,JsonOptionDeclaration>(StringComparer.Ordinal);
  // As commandLineOptionsToMap, later duplicate declarations replace names.
  foreach(var o in Declarations){result[o.Name]=o;result[PinnedEnumLowercase.Lower(o.Name)]=o;}return result;
 }
 public static JsonOptionsMapResult Convert(ConfigValue value,string basePath="/.src")
 {
  var calls=new List<ConfigProperty>();var diagnostics=new List<Diagnostic>();var unsupported=new List<string>();
  if(value.Kind!="Object")return new(calls,diagnostics,unsupported);
  foreach(var property in value.Properties)
  {
   string key=property.Key;
   if(!NameMap.TryGetValue(key,out var option))NameMap.TryGetValue(PinnedEnumLowercase.Lower(key),out option);
   if(option==null||option.Name!=key)
   {
    string? suggestion=option?.Name??Suggestion(key);
    // Keep the original UTF-16 key for lookup and weighted suggestions. The
    // pinned Go formatter repairs only the eventual diagnostic arguments.
    string printedKey=DiagnosticArgument(key);
    diagnostics.Add(suggestion==null?Error("TS5023",$"Unknown compiler option '{printedKey}'."):Error("TS5025",$"Unknown compiler option '{printedKey}'. Did you mean '{DiagnosticArgument(suggestion)}'?"));continue;
   }
   if(!option.Supported){unsupported.Add(key);continue;}
   var converted=Dispatch(option,property.Value,basePath);diagnostics.AddRange(converted.Diagnostics);calls.Add(new(key,converted.Value));
  }
  return new(calls,diagnostics,unsupported);
 }
 private static JsonScalarOptionResult Dispatch(JsonOptionDeclaration o,ConfigValue value,string basePath)
 {
  if(o.Enum!=null)return JsonEnumOptions.Convert(o.Enum,value);
  if(o.Kind=="list")
  {
   var e=o.Element??throw new InvalidOperationException("Missing list element declaration");
   return JsonListOptions.Convert(new(o.Name,new(e.Name,new(e.Kind,e.DisallowNull),e.CommandLineOnly),o.PreserveFalsy,o.CommandLineOnly,e.Enum,e.PathKind,basePath),value);
  }
  var scalar=new JsonScalarOption(o.Name,new(o.Kind,o.DisallowNull),o.CommandLineOnly);
  if(o.ExtraValidation.Length>0&&!o.CommandLineOnly)throw new NotSupportedException("Unvalidated config policy: "+o.Name);
  return o.PathKind!=0?JsonConfigPaths.Convert(scalar,o.PathKind,value,basePath):JsonScalarOptions.Convert(scalar,value);
 }
 // Pinned diagnostics.Format uses strings.ToValidUTF8: one U+FFFD for each
 // contiguous invalid WTF-8 byte run, not one per malformed UTF-16 code unit.
 // Valid UTF-16 pairs remain intact and terminate an invalid run.
 private static string DiagnosticArgument(string value)
 {
  var result=new StringBuilder(value.Length);bool invalidRun=false;
  for(int i=0;i<value.Length;i++)
  {
   char c=value[i];
   if(char.IsHighSurrogate(c)&&i+1<value.Length&&char.IsLowSurrogate(value[i+1]))
   {result.Append(c).Append(value[++i]);invalidRun=false;}
   else if(char.IsSurrogate(c))
   {if(!invalidRun)result.Append('\uFFFD');invalidRun=true;}
   else{result.Append(c);invalidRun=false;}
  }
  return result.ToString();
 }
 private static Diagnostic Error(string code,string message)=>new(code,"error","",-1,0,message,[],[]);
 // Go []rune sees each invalid WTF8 surrogate byte as RuneError. No host ICU.
 private static int[] Runes(string value)
 {
  var result=new List<int>();for(int i=0;i<value.Length;i++)
  {
   char c=value[i];if(char.IsHighSurrogate(c)&&i+1<value.Length&&char.IsLowSurrogate(value[i+1])){result.Add(char.ConvertToUtf32(c,value[++i]));}
   else if(char.IsSurrogate(c)){result.AddRange([0xfffd,0xfffd,0xfffd]);}
   else result.Add(c);
  }
  return result.ToArray();
 }
 private static string? Suggestion(string name)
 {
  int[] input=Runes(name);int maxLengthDifference=Math.Max(2,(int)(input.Length*0.34));double best=Math.Floor(input.Length*0.4)+0.9;string? candidate=null;
  foreach(var option in Declarations)
  {
   string s=option.Name;if(s==name||Math.Abs(s.Length-input.Length)>maxLengthDifference)continue;
   if(s.Length<3&&PinnedEnumLowercase.Lower(s)!=PinnedEnumLowercase.Lower(name))continue;
   double distance=Distance(input,s,best);if(distance<0)continue;
   if(distance<best||candidate==null||string.CompareOrdinal(s,candidate)<0){best=distance;candidate=s;}
  }
  return candidate;
 }
 private static double Distance(int[] a,string name,double maxValue)
 {
  int[] b=Runes(name);double[] previous=Enumerable.Range(0,b.Length+1).Select(x=>(double)x).ToArray(),current=new double[b.Length+1];double big=maxValue+0.01;
  for(int i=1;i<=a.Length;i++)
  {
   int minJ=Math.Max((int)Math.Ceiling(i-maxValue),1),maxJ=Math.Min((int)Math.Floor(maxValue+i),b.Length);double colMin=i;current[0]=i;
   for(int j=1;j<minJ;j++)current[j]=big;
   for(int j=minJ;j<=maxJ;j++)
   {
    double substitution=previous[j-1]+(Lower(a[i-1])==Lower(b[j-1])?0.1:2);
    double dist=a[i-1]==b[j-1]?previous[j-1]:Math.Min(previous[j]+1,Math.Min(current[j-1]+1,substitution));current[j]=dist;colMin=Math.Min(colMin,dist);
   }
   for(int j=maxJ+1;j<=b.Length;j++)current[j]=big;
   if(colMin>maxValue)return -1;(previous,current)=(current,previous);
  }
  return previous[b.Length]>maxValue?-1:previous[b.Length];
 }
 private static string Lower(int scalar)=>PinnedEnumLowercase.Lower(char.ConvertFromUtf32(scalar));
}

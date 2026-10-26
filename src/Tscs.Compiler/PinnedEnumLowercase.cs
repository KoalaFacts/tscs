using System.Text;
using System.Text.Json;
namespace Tscs;
// Generated from actual pinned Go unicode.ToLower; independent of host ICU/NLS.
internal static class PinnedEnumLowercase
{
 private static readonly IReadOnlyDictionary<int,int> Mappings=Load();
 private static IReadOnlyDictionary<int,int> Load()
 {
  using var stream=typeof(PinnedEnumLowercase).Assembly.GetManifestResourceStream("Tscs.EnumLowercase")??throw new InvalidOperationException("Missing pinned lowercase data");
  using var data=JsonDocument.Parse(stream);
  if(data.RootElement.GetProperty("unicodeVersion").GetString()!="17.0.0")throw new InvalidOperationException("Unreviewed Unicode version");
  return data.RootElement.GetProperty("lowercase").EnumerateObject().ToDictionary(p=>int.Parse(p.Name,System.Globalization.CultureInfo.InvariantCulture),p=>p.Value.GetInt32());
 }
 public static string Lower(string text)
 {
  var result=new StringBuilder();foreach(var rune in text.EnumerateRunes())result.Append(new Rune(Mappings.TryGetValue(rune.Value,out int lower)?lower:rune.Value).ToString());return result.ToString();
 }
}

using System.Text.Json;
using Xunit;
namespace Tscs.Tests;
public class JsonOptionTypeTests
{
 public static IEnumerable<object[]> Rows(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/json-option-types.json")));foreach(var r in d.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 private static JsonOptionType? Type(JsonElement e)=>e.ValueKind==JsonValueKind.Null?null:new(e.GetProperty("kind").GetString()!,e.GetProperty("disallowNull").GetBoolean(),Type(e.GetProperty("element")));
 [Theory,MemberData(nameof(Rows))]
 public void ConfigurationValueTypeAdmissionMatchesActualPinnedHelper(string id,string json){using var d=JsonDocument.Parse(json);var r=d.RootElement;Assert.Equal(id,r.GetProperty("id").GetString());var parsed=JsonSyntax.Parse("/tsconfig.json",r.GetProperty("source").GetString()!);Assert.Empty(parsed.Diagnostics);Assert.Equal(r.GetProperty("accepted").GetBoolean(),JsonCompilerOptionTypes.Accepts(Type(r.GetProperty("option")),JsonConfigValues.Convert(parsed.Root)));}
}

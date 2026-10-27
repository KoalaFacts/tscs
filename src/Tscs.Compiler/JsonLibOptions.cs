using System.Text.Json;
namespace Tscs;
internal static class JsonLibOptions
{
 public static JsonListOption Declaration {get;}=Load();
 private static JsonListOption Load()
 {
  using var stream=typeof(JsonLibOptions).Assembly.GetManifestResourceStream("Tscs.LibDeclaration")??throw new InvalidOperationException("Missing pinned lib declaration");
  using var d=JsonDocument.Parse(stream);var o=d.RootElement;
  var element=JsonEnumOptions.ReadDeclaration(o.GetProperty("element"));
  return new(o.GetProperty("name").GetString()!,new(element.Name,new("enum",element.DisallowNull),element.CommandLineOnly),o.GetProperty("preserveFalsy").GetBoolean(),o.GetProperty("commandLineOnly").GetBoolean(),element);
 }
}

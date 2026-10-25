using System.Globalization;
namespace Tscs;
internal sealed record ConfigProperty(string Key,ConfigValue Value);
// IsNil distinguishes a Go nil array slice from an allocated empty array;
// a nil array remains an array value when nested, unlike scalar JSON null.
internal sealed record ConfigValue(string Kind,string Text,double Number,bool IsNil,IReadOnlyList<ConfigProperty> Properties,IReadOnlyList<ConfigValue> Elements);
// convertToObject/convertToJson: returnValue=true, no option/notifier callbacks.
// Call only on the supported AST; null root means an empty source, not failure.
internal static class JsonConfigValues
{
 private static ConfigValue Make(string kind,string text="",double number=0,bool isNil=false,IReadOnlyList<ConfigProperty>? properties=null,IReadOnlyList<ConfigValue>? elements=null)=>new(kind,text,number,isNil,properties??[],elements??[]);
 public static ConfigValue Convert(JsonSyntaxNode? node)
 {
  if(node==null)return Make("EmptySource");
  switch(node.Kind)
  {
   case "True":case "False":case "Null":return Make(node.Kind);
   case "String":return Make("String",node.Token.Value??"");
   case "Number":return Make("Number",number:double.Parse(node.Number,NumberStyles.Float,CultureInfo.InvariantCulture));
   case "Array":
    var values=node.Elements.Select(Convert).Where(v=>v.Kind!="Null").ToArray();
    return Make("Array",isNil:node.Elements.Count>0&&values.Length==0,elements:values);
   case "Object":
    var properties=new List<ConfigProperty>();var positions=new Dictionary<string,int>(StringComparer.Ordinal);
    foreach(var p in node.Properties)
    {
     var value=Convert(p.Value);string key=p.Name.Kind=="Number"?p.Name.Number:p.Name.Token.Value??p.Name.Token.Text;
     if(key.Length==0)continue;
     var property=new ConfigProperty(key,value);
     if(positions.TryGetValue(key,out int index))properties[index]=property;
     else{positions.Add(key,properties.Count);properties.Add(property);}
    }
    return Make("Object",properties:properties);
   default:throw new InvalidOperationException("Unsupported config AST kind: "+node.Kind);
  }
 }
}

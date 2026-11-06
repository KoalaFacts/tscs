using System.Text;
using System.Text.Json;
namespace Tscs.Conformance;

// Development-only probe: parse one ambient declaration file and report its
// syntax diagnostics. No binding or checking happens here.
public static class FixtureDeclarationSyntax
{
    public static object Probe(JsonElement request)
    {
        string fileName=request.GetProperty("fileName").GetString()??throw new JsonException("Missing fileName");
        string text=Encoding.UTF8.GetString(Convert.FromBase64String(request.GetProperty("input").GetString()!));
        var result=DeclarationParser.Parse(fileName,text);
        return new
        {
            statements=result.Statements.Count,
            references=result.ReferenceDirectives.Select(r=>new{name=r.Name,value=r.Value}).ToArray(),
            diagnostics=result.Diagnostics.Select(d=>new{code=d.Code,start=d.Start,message=d.Message}).ToArray()
        };
    }
}

using System.Text;
using System.Text.Json;
namespace Tscs.Conformance;

// Development-only probe: parse one ambient declaration file and report its
// syntax diagnostics, or bind several files together into one global symbol
// table and report the binder's diagnostics. Nothing is checked here.
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

    public static object ProbeBinding(JsonElement request)
    {
        var files=request.GetProperty("files").EnumerateArray().Select(f=>DeclarationParser.Parse(
            f.GetProperty("fileName").GetString()??throw new JsonException("Missing fileName"),
            Encoding.UTF8.GetString(Convert.FromBase64String(f.GetProperty("input").GetString()!)))).ToArray();
        var syntax=files.SelectMany(f=>f.Diagnostics).FirstOrDefault();
        if(syntax is not null)throw new JsonException($"{syntax.File}: {syntax.Code} {syntax.Message}");
        var globals=DeclarationBinder.Bind(files);
        return new
        {
            symbols=globals.Symbols.Count,
            merged=globals.Symbols.Values.Count(s=>s.Declarations.Count>1),
            diagnostics=globals.Diagnostics.Select(d=>new{file=d.File,code=d.Code,start=d.Start,message=d.Message}).ToArray(),
            unresolved=DeclarationResolver.Resolve(files,globals).Select(d=>new{file=d.File,code=d.Code,start=d.Start,message=d.Message}).ToArray()
        };
    }
}

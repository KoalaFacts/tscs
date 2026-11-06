using System.Text;
using System.Text.Json;
namespace Tscs.Conformance;

// Development-only probes: parse one ambient declaration file and report its
// syntax diagnostics, or parse and bind a set of files into one global symbol
// table and report the binder's diagnostics. Nothing is type-checked here.
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
            Encoding.UTF8.GetString(Convert.FromBase64String(f.GetProperty("input").GetString()!)))).ToList();
        var syntax=files.SelectMany(f=>f.Diagnostics).ToList();
        if(syntax.Count>0)
            return new{globals=0,diagnostics=syntax.Select(d=>new{file=d.File,code=d.Code,start=d.Start,message=d.Message}).ToArray()};
        var table=DeclarationBinder.Bind(files);
        return new
        {
            globals=table.Globals.Count,
            diagnostics=table.Diagnostics.Select(d=>new{file=d.File,code=d.Code,start=d.Start,message=d.Message}).ToArray()
        };
    }
}

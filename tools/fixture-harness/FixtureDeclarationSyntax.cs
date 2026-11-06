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
            diagnostics=result.Diagnostics.Select(d=>new{code=d.Code,start=d.Start,message=d.Message}).ToArray(),
            inventory=result.Statements.Select(Inventory).ToArray()
        };
    }

    // The statement inventory tools/declaration-ast-go prints for the reference
    // parser (roadmap A2.2c): kind, declared names and member count. A dotted
    // namespace is the reference's outer ModuleDeclaration, whose body is the
    // next segment, so it has no direct members.
    private static object Inventory(AmbientStatement statement)=>statement switch
    {
        AmbientInterface i=>Row("KindInterfaceDeclaration",[i.Name.Text],i.Members.Count),
        AmbientClass c=>Row("KindClassDeclaration",[c.Name.Text],c.Members.Count),
        AmbientTypeAlias a=>Row("KindTypeAliasDeclaration",[a.Name.Text],0),
        AmbientFunction f=>Row("KindFunctionDeclaration",[f.Name.Text],0),
        AmbientVariable v=>Row("KindVariableStatement",[v.Name.Text],0),
        AmbientNamespace { GlobalKeyword: { } } g=>Row("KindModuleDeclaration",["global"],g.Body.Count),
        AmbientNamespace n=>Row("KindModuleDeclaration",[n.Name[0].Text],n.Name.Count>1?0:n.Body.Count),
        AmbientExportList=>Row("KindExportDeclaration",[],0),
        _=>Row(statement.GetType().Name,[],0)
    };

    private static object Row(string kind,string[] names,int members)=>new{kind,names,members};

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

namespace Tscs;

internal sealed class Symbol(Variable declaration)
{
    public Variable Declaration { get; } = declaration;
    public string? Type { get; set; }
    public bool Initialized { get; set; }
}
internal sealed class Scope(Scope? parent)
{
    public Dictionary<string,Symbol> Symbols { get; } = new(StringComparer.Ordinal);
    public Symbol? Find(string name)=>Symbols.GetValueOrDefault(name)??parent?.Find(name);
}
// Block-local tables follow bindBlockScopedDeclaration; prebinding prevents accidental outer-scope lookup.
internal static class Binder
{
    public static Scope Bind(string file,Block block,Scope? parent)
    {
        var scope=new Scope(parent);
        foreach(var v in block.Statements.OfType<Variable>())
            if(!scope.Symbols.TryAdd(v.Name.Text,new Symbol(v)))
                throw new CompilationFailure(Diagnostic.Error(file,v.Name,"TSCS1001","Redeclaration diagnostics are not supported yet."));
        return scope;
    }
}

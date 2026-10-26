namespace Tscs;

internal sealed class Symbol(string file,Variable declaration)
{
    public string File { get; } = file;
    public Variable Declaration { get; } = declaration;
    public string? Type { get; set; }
    public bool Initialized { get; set; }
    public bool CrossFileTypeReady { get; set; }
}
internal sealed class Scope(Scope? parent)
{
    public Dictionary<string,Symbol> Symbols { get; } = new(StringComparer.Ordinal);
    public Symbol? Find(string name)=>Symbols.GetValueOrDefault(name)??parent?.Find(name);
}
// A shared global scope is prebound for all .ts scripts before type checking.
// Complex cross-file inference and declaration merging intentionally fail closed.
internal static class Binder
{
    public static Scope Bind(string file,Block block,Scope? parent)
    {
        var scope=new Scope(parent);
        foreach(var variable in block.Statements.OfType<Variable>())Declare(file,scope,variable);
        return scope;
    }

    public static Scope BindProgram(IEnumerable<(string File,Block Block)> sources)
    {
        var global=new Scope(null);
        foreach(var (file,root) in sources)
            foreach(var variable in root.Statements.OfType<Variable>())
                Declare(file,global,variable);
        foreach(var symbol in global.Symbols.Values)
        {
            var declaration=symbol.Declaration;
            string? literalType=declaration.Initializer is ValueAtom atom
                ? atom.Token.Kind switch
                {
                    TokenKind.Number => "number",
                    TokenKind.String => "string",
                    TokenKind.True or TokenKind.False => "boolean",
                    _ => null
                }
                : null;
            symbol.Type=declaration.Annotation??literalType;
            symbol.Initialized=declaration.Initializer!=null;
            symbol.CrossFileTypeReady=symbol.Type!=null;
        }
        return global;
    }
    private static void Declare(string file,Scope scope,Variable declaration)
    {
        if(!scope.Symbols.TryAdd(declaration.Name.Text,new Symbol(file,declaration)))
            throw new CompilationFailure(Diagnostic.Error(file,declaration.Name,"TSCS1001",
                "Redeclaration diagnostics and declaration merging are not supported yet."));
    }
}

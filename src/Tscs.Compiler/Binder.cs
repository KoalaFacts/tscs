namespace Tscs;

internal sealed class Symbol(string file,Variable declaration)
{
    public string File { get; } = file;
    public Variable Declaration { get; } = declaration;
    public TypeSymbol? Type { get; set; }
    public bool Initialized { get; set; }
    public bool CrossFileTypeReady { get; set; }
    public bool Imported { get; set; }
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
        InitializeDeclaredTypes(global);
        return global;
    }

    public static Scope BindModule(string file,Block block,Scope global)
    {
        var local=Bind(file,block,global);
        InitializeDeclaredTypes(local);
        return local;
    }

    public static void Import(string file,Scope scope,ImportSpecifier specifier,Symbol exported)
    {
        if(!exported.CrossFileTypeReady)
            throw new CompilationFailure(Diagnostic.Error(file,specifier.Imported,"TSCS1001",
                "Imported declarations require an explicit primitive annotation or literal type."));
        var imported=new Symbol(exported.File,exported.Declaration)
        {
            Type=exported.Type,
            Initialized=exported.Initialized,
            CrossFileTypeReady=true,
            Imported=true
        };
        if(!scope.Symbols.TryAdd(specifier.Local.Text,imported))
            throw new CompilationFailure(Diagnostic.Error(file,specifier.Local,"TSCS1001",
                "Duplicate import or local declaration is not supported yet."));
    }

    private static void InitializeDeclaredTypes(Scope scope)
    {
        foreach(var symbol in scope.Symbols.Values)
        {
            var declaration=symbol.Declaration;
            TypeSymbol? literalType=declaration.Initializer is ValueAtom atom
                ? CompilerTypes.FromLiteral(atom.Token) : null;
            symbol.Type=declaration.Annotation is { } annotation
                ? CompilerTypes.FromAnnotation(annotation) : literalType;
            symbol.Initialized=declaration.Initializer!=null;
            symbol.CrossFileTypeReady=symbol.Type!=null;
        }
    }
    private static void Declare(string file,Scope scope,Variable declaration)
    {
        if(!scope.Symbols.TryAdd(declaration.Name.Text,new Symbol(file,declaration)))
            throw new CompilationFailure(Diagnostic.Error(file,declaration.Name,"TSCS1001",
                "Redeclaration diagnostics and declaration merging are not supported yet."));
    }
}

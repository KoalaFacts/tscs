namespace Tscs;

internal sealed class Symbol(string file,Variable declaration)
{
    public string File { get; } = file;
    public Variable Declaration { get; } = declaration;
    public string? Type { get; set; }
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

    // A module has an explicit export table independent from its lexical scope.
    // Local names never leak into another module just because a file was loaded
    // through the same Program. The table retains the declaring Symbol identity
    // across renamed exports and multiple consumers.
    public static IReadOnlyDictionary<string,Symbol> BindExports(string file,Block block,Scope scope)
    {
        var exports=new Dictionary<string,Symbol>(StringComparer.Ordinal);
        void Add(Token exported,Symbol symbol)
        {
            if(!exports.TryAdd(exported.Text,symbol))
                throw new CompilationFailure(Diagnostic.Error(file,exported,"TSCS1001",
                    "Duplicate module export names are not supported yet."));
        }

        foreach(var statement in block.Statements)
        {
            if(statement is Variable { Exported:true } variable)
            {
                Add(variable.Name,scope.Symbols[variable.Name.Text]);
            }
            else if(statement is ExportDeclaration list)
            {
                foreach(var specifier in list.Specifiers)
                {
                    if(!scope.Symbols.TryGetValue(specifier.Local.Text,out var symbol))
                    {
                        bool imported=block.Statements.OfType<ImportDeclaration>()
                            .SelectMany(import=>import.Specifiers)
                            .Any(import=>import.Local.Text==specifier.Local.Text);
                        if(imported)
                            throw new CompilationFailure(Diagnostic.Error(file,specifier.Local,"TSCS1001",
                                "Re-exporting an imported binding is not supported yet."));
                        throw new CompilationFailure(Diagnostic.Error(file,specifier.Local,"TS2304",
                            $"Cannot find name '{specifier.Local.Text}'."));
                    }
                    Add(specifier.Exported,symbol);
                }
            }
        }
        return exports;
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
    }
    private static void Declare(string file,Scope scope,Variable declaration)
    {
        if(!scope.Symbols.TryAdd(declaration.Name.Text,new Symbol(file,declaration)))
            throw new CompilationFailure(Diagnostic.Error(file,declaration.Name,"TSCS1001",
                "Redeclaration diagnostics and declaration merging are not supported yet."));
    }
}

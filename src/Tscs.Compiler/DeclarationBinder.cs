namespace Tscs;

// The meanings a parsed ambient declaration contributes to its name, after
// the reference binder's SymbolFlags (only the kinds DeclarationParser builds).
[Flags]
internal enum AmbientMeaning
{
    None=0,
    BlockScopedVariable=1,
    FunctionScopedVariable=2,
    Function=4,
    Class=8,
    Interface=16,
    TypeAlias=32,
    // A namespace that declares values (the reference ValueModule).
    ValueModule=64,
    // A namespace that declares only types (the reference NamespaceModule).
    NamespaceModule=128,
    Value=BlockScopedVariable|FunctionScopedVariable|Function|Class|ValueModule,
    Type=Class|Interface|TypeAlias,
}

internal sealed class AmbientSymbol(string name)
{
    public string Name{get;}=name;
    public AmbientMeaning Meaning{get;set;}
    public List<AmbientStatement> Declarations{get;}=[];
    // Members of a merged namespace, keyed by name.
    public Dictionary<string,AmbientSymbol> Exports{get;}=new(StringComparer.Ordinal);
}

internal sealed record AmbientSymbolTable(IReadOnlyDictionary<string,AmbientSymbol> Globals,IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>
/// Binds parsed ambient declaration files into one global symbol table,
/// merging declarations by name as the reference binder does (interfaces with
/// interfaces and classes, namespaces with functions, classes and other
/// namespaces, values with types) and reporting TS2300/TS2451 for
/// declarations whose meanings exclude each other. Nothing is type-checked.
/// </summary>
internal static class DeclarationBinder
{
    public static AmbientSymbolTable Bind(IEnumerable<AmbientDeclarationFile> files)
    {
        var globals=new Dictionary<string,AmbientSymbol>(StringComparer.Ordinal);
        var diagnostics=new List<Diagnostic>();
        foreach(var file in files)
        {
            if(file.Diagnostics.Count>0)
                throw new ArgumentException($"{file.File} has syntax errors and cannot be bound.",nameof(files));
            BindStatements(file.File,file.Statements,globals,globals,diagnostics);
        }
        return new(globals,diagnostics);
    }

    // Reference SymbolFlags *Excludes: meanings that cannot share a name.
    private static AmbientMeaning Excludes(AmbientMeaning meaning)=>meaning switch
    {
        AmbientMeaning.BlockScopedVariable=>AmbientMeaning.Value,
        AmbientMeaning.FunctionScopedVariable=>AmbientMeaning.Value&~AmbientMeaning.FunctionScopedVariable,
        AmbientMeaning.Function=>AmbientMeaning.Value&~(AmbientMeaning.Function|AmbientMeaning.ValueModule|AmbientMeaning.Class),
        AmbientMeaning.Class=>(AmbientMeaning.Value|AmbientMeaning.Type)&~(AmbientMeaning.ValueModule|AmbientMeaning.Interface|AmbientMeaning.Function),
        AmbientMeaning.Interface=>AmbientMeaning.Type&~(AmbientMeaning.Interface|AmbientMeaning.Class),
        AmbientMeaning.TypeAlias=>AmbientMeaning.Type,
        AmbientMeaning.ValueModule=>AmbientMeaning.Value&~(AmbientMeaning.Function|AmbientMeaning.Class|AmbientMeaning.ValueModule),
        AmbientMeaning.NamespaceModule=>AmbientMeaning.None,
        _=>throw new ArgumentOutOfRangeException(nameof(meaning)),
    };

    private static void BindStatements(string file,IReadOnlyList<AmbientStatement> statements,
        Dictionary<string,AmbientSymbol> scope,Dictionary<string,AmbientSymbol> globals,List<Diagnostic> diagnostics)
    {
        foreach(var statement in statements)
        {
            switch(statement)
            {
                case AmbientInterface face:
                    Declare(file,face.Name,AmbientMeaning.Interface,statement,scope,diagnostics);
                    break;
                case AmbientTypeAlias alias:
                    Declare(file,alias.Name,AmbientMeaning.TypeAlias,statement,scope,diagnostics);
                    break;
                case AmbientVariable variable:
                    Declare(file,variable.Name,variable.Keyword.Text=="var"
                        ?AmbientMeaning.FunctionScopedVariable:AmbientMeaning.BlockScopedVariable,statement,scope,diagnostics);
                    break;
                case AmbientFunction function:
                    Declare(file,function.Name,AmbientMeaning.Function,statement,scope,diagnostics);
                    break;
                case AmbientClass type:
                    Declare(file,type.Name,AmbientMeaning.Class,statement,scope,diagnostics);
                    break;
                case AmbientNamespace {Name.Count:0} global:
                    BindStatements(file,global.Body,globals,globals,diagnostics);
                    break;
                case AmbientNamespace space:
                    BindNamespace(file,space,0,scope,globals,diagnostics);
                    break;
                case AmbientExportList:
                    // Export lists only change module visibility; the bundled
                    // libs are global scripts, so they add no global names.
                    break;
                default:
                    throw new NotSupportedException($"Unbound ambient statement {statement.GetType().Name}.");
            }
        }
    }

    // `namespace A.B { ... }` declares A containing B.
    private static void BindNamespace(string file,AmbientNamespace space,int part,
        Dictionary<string,AmbientSymbol> scope,Dictionary<string,AmbientSymbol> globals,List<Diagnostic> diagnostics)
    {
        var meaning=Instantiated(space.Body)?AmbientMeaning.ValueModule:AmbientMeaning.NamespaceModule;
        var symbol=Declare(file,space.Name[part],meaning,space,scope,diagnostics);
        if(part+1<space.Name.Count)BindNamespace(file,space,part+1,symbol.Exports,globals,diagnostics);
        else BindStatements(file,space.Body,symbol.Exports,globals,diagnostics);
    }

    private static bool Instantiated(IReadOnlyList<AmbientStatement> body)=>body.Any(s=>s switch
    {
        AmbientVariable or AmbientFunction or AmbientClass=>true,
        AmbientNamespace {Name.Count:>0} inner=>Instantiated(inner.Body),
        _=>false,
    });

    private static AmbientSymbol Declare(string file,Token name,AmbientMeaning meaning,AmbientStatement declaration,
        Dictionary<string,AmbientSymbol> scope,List<Diagnostic> diagnostics)
    {
        if(!scope.TryGetValue(name.Text,out var symbol))scope.Add(name.Text,symbol=new AmbientSymbol(name.Text));
        if((symbol.Meaning&Excludes(meaning))!=AmbientMeaning.None)
        {
            bool blockScoped=((symbol.Meaning|meaning)&AmbientMeaning.BlockScopedVariable)!=0;
            diagnostics.Add(blockScoped
                ?Diagnostic.Error(file,name,"TS2451",$"Cannot redeclare block-scoped variable '{name.Text}'.")
                :Diagnostic.Error(file,name,"TS2300",$"Duplicate identifier '{name.Text}'."));
        }
        symbol.Meaning|=meaning;
        symbol.Declarations.Add(declaration);
        return symbol;
    }
}

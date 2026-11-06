namespace Tscs;

// Binding of ambient declaration files (roadmap A2.2a). It builds the global
// symbol table the standard library contributes: declarations with the same
// name merge across files under the pinned binder's exclusion rules
// (binder.go declareSymbol with ast.SymbolFlags*Excludes), and a declaration
// that a symbol's earlier meanings exclude is reported as the reference
// reports it, at the excluded declaration (the reference also reports the
// earlier declarations; that is not modelled yet). Types are not resolved and
// nothing is checked yet.
[Flags]
internal enum AmbientMeaning
{
    None=0,
    FunctionScopedVariable=1,
    BlockScopedVariable=2,
    Function=4,
    Class=8,
    Interface=16,
    TypeAlias=32,
    ValueModule=64,
    NamespaceModule=128,
    Value=FunctionScopedVariable|BlockScopedVariable|Function|Class|ValueModule,
    Type=Class|Interface|TypeAlias,
}

internal sealed class AmbientSymbol(string name)
{
    public string Name{get;}=name;
    public AmbientMeaning Meanings{get;set;}
    public List<AmbientStatement> Declarations{get;}=[];
    // Exports of a merged namespace, keyed by name; empty for other symbols.
    public Dictionary<string,AmbientSymbol> Exports{get;}=new(StringComparer.Ordinal);
}

internal sealed record AmbientGlobals(IReadOnlyDictionary<string,AmbientSymbol> Symbols,IReadOnlyList<Diagnostic> Diagnostics);

internal static class DeclarationBinder
{
    public static AmbientGlobals Bind(IEnumerable<AmbientDeclarationFile> files)
    {
        var globals=new Dictionary<string,AmbientSymbol>(StringComparer.Ordinal);
        var diagnostics=new List<Diagnostic>();
        foreach(var file in files)
            foreach(var statement in file.Statements)
                Declare(file.File,globals,statement,diagnostics);
        return new(globals,diagnostics);
    }

    private static void Declare(string file,Dictionary<string,AmbientSymbol> table,AmbientStatement statement,List<Diagnostic> diagnostics)
    {
        switch(statement)
        {
            case AmbientNamespace { Name.Count: 0 } global:
                // `declare global` augments the global scope itself.
                foreach(var inner in global.Body)Declare(file,table,inner,diagnostics);
                return;
            case AmbientNamespace ns:
                DeclareNamespace(file,table,ns,0,diagnostics);
                return;
            case AmbientExportList:
                return;
        }
        var (name,meaning)=statement switch
        {
            AmbientInterface i=>(i.Name,AmbientMeaning.Interface),
            AmbientTypeAlias a=>(a.Name,AmbientMeaning.TypeAlias),
            AmbientVariable { Keyword.Text: "var" } v=>(v.Name,AmbientMeaning.FunctionScopedVariable),
            AmbientVariable v=>(v.Name,AmbientMeaning.BlockScopedVariable),
            AmbientFunction f=>(f.Name,AmbientMeaning.Function),
            AmbientClass c=>(c.Name,AmbientMeaning.Class),
            _=>throw new InvalidOperationException($"unbound ambient statement {statement.GetType().Name}"),
        };
        Add(file,table,name,meaning,statement,diagnostics);
    }

    // `namespace A.B.C { }` declares A, whose export B holds C.
    private static void DeclareNamespace(string file,Dictionary<string,AmbientSymbol> table,AmbientNamespace ns,int index,List<Diagnostic> diagnostics)
    {
        var last=index==ns.Name.Count-1;
        // Every level of a dotted name is instantiated exactly when the body is.
        var meaning=HasValues(ns.Body)?AmbientMeaning.ValueModule:AmbientMeaning.NamespaceModule;
        var symbol=Add(file,table,ns.Name[index],meaning,ns,diagnostics);
        if(!last){DeclareNamespace(file,symbol.Exports,ns,index+1,diagnostics);return;}
        foreach(var inner in ns.Body)Declare(file,symbol.Exports,inner,diagnostics);
    }

    // A namespace is instantiated (a value) when it declares any value.
    private static bool HasValues(IReadOnlyList<AmbientStatement> body)=>body.Any(s=>s switch
    {
        AmbientVariable or AmbientFunction or AmbientClass=>true,
        AmbientNamespace inner=>HasValues(inner.Body),
        _=>false,
    });

    private static AmbientMeaning Excludes(AmbientMeaning meaning)=>meaning switch
    {
        AmbientMeaning.FunctionScopedVariable=>AmbientMeaning.Value&~AmbientMeaning.FunctionScopedVariable,
        AmbientMeaning.BlockScopedVariable=>AmbientMeaning.Value,
        AmbientMeaning.Function=>AmbientMeaning.Value&~(AmbientMeaning.Function|AmbientMeaning.ValueModule|AmbientMeaning.Class),
        AmbientMeaning.Class=>(AmbientMeaning.Value|AmbientMeaning.Type)&~(AmbientMeaning.ValueModule|AmbientMeaning.Interface|AmbientMeaning.Function),
        AmbientMeaning.Interface=>AmbientMeaning.Type&~(AmbientMeaning.Interface|AmbientMeaning.Class),
        AmbientMeaning.TypeAlias=>AmbientMeaning.Type,
        AmbientMeaning.ValueModule=>AmbientMeaning.Value&~(AmbientMeaning.Function|AmbientMeaning.Class|AmbientMeaning.ValueModule),
        _=>AmbientMeaning.None,
    };

    private static AmbientSymbol Add(string file,Dictionary<string,AmbientSymbol> table,Token name,AmbientMeaning meaning,
        AmbientStatement declaration,List<Diagnostic> diagnostics)
    {
        if(!table.TryGetValue(name.Text,out var symbol))table[name.Text]=symbol=new(name.Text);
        else if((symbol.Meanings&Excludes(meaning))!=0)
        {
            // The reference reports block-scoped redeclarations as TS2451 and
            // every other exclusion as TS2300.
            var blockScoped=((symbol.Meanings|meaning)&AmbientMeaning.BlockScopedVariable)!=0;
            diagnostics.Add(blockScoped
                ?Diagnostic.Error(file,name,"TS2451",$"Cannot redeclare block-scoped variable '{name.Text}'.")
                :Diagnostic.Error(file,name,"TS2300",$"Duplicate identifier '{name.Text}'."));
            return symbol;
        }
        symbol.Meanings|=meaning;
        symbol.Declarations.Add(declaration);
        return symbol;
    }
}

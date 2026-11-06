namespace Tscs;

// Binding of ambient declaration files (roadmap A2.2a). It builds the global
// symbol table the standard library contributes, in the reference's two
// stages: each file is bound on its own (binder.go declareSymbol with
// ast.SymbolFlags*Excludes; a conflict there is TS2451 when the earlier
// symbol is block-scoped, else TS2300, and the excluded declaration gets a
// fresh, unlisted symbol), then script files' symbols merge into the globals
// (checker.go mergeSymbol; a conflict is TS2451 when either side is
// block-scoped, else TS2300, or TS2649 when the earlier symbol includes a
// non-instantiated namespace). A file with a top-level export is a module: only
// its `declare global` blocks reach the globals, and `declare global` in a
// script or nested in a namespace is TS2669. Diagnostics are reported at the
// excluded declaration only (the reference also reports the earlier
// declarations), and members hidden by an `export {}` inside a namespace are
// not modelled. Names are resolved by DeclarationResolver; types are not
// computed and nothing is checked yet.
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

internal sealed record AmbientDeclaration(string File,Token Name,AmbientStatement Statement);

internal sealed class AmbientSymbol(string name)
{
    public string Name{get;}=name;
    public AmbientMeaning Meanings{get;set;}
    public List<AmbientDeclaration> Declarations{get;}=[];
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
        {
            var module=IsModule(file);
            var locals=new Dictionary<string,AmbientSymbol>(StringComparer.Ordinal);
            var augmentations=new Dictionary<string,AmbientSymbol>(StringComparer.Ordinal);
            foreach(var statement in file.Statements)
            {
                if(statement is AmbientNamespace { Name.Count: 0 } global)
                {
                    if(!module)
                    {
                        diagnostics.Add(Diagnostic.Error(file.File,global.GlobalKeyword!.Value,"TS2669",
                            "Augmentations for the global scope can only be directly nested in external modules or ambient module declarations."));
                        continue;
                    }
                    foreach(var inner in global.Body)Declare(file.File,augmentations,inner,diagnostics);
                }
                else Declare(file.File,locals,statement,diagnostics);
            }
            if(!module)MergeTable(globals,locals,diagnostics);
            MergeTable(globals,augmentations,diagnostics);
        }
        return new(globals,diagnostics);
    }

    // A file with a top-level export is a module.
    public static bool IsModule(AmbientDeclarationFile file)=>file.Statements.Any(s=>s.Exported || s is AmbientExportList);

    private static void Declare(string file,Dictionary<string,AmbientSymbol> table,AmbientStatement statement,List<Diagnostic> diagnostics)
    {
        switch(statement)
        {
            case AmbientNamespace { Name.Count: 0 } nested:
                // checker.go reports `declare global` nested in a namespace too.
                diagnostics.Add(Diagnostic.Error(file,nested.GlobalKeyword!.Value,"TS2669",
                    "Augmentations for the global scope can only be directly nested in external modules or ambient module declarations."));
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
        Add(table,new(file,name,statement),meaning,diagnostics);
    }

    // `namespace A.B.C { }` declares A, whose export B holds C.
    private static void DeclareNamespace(string file,Dictionary<string,AmbientSymbol> table,AmbientNamespace ns,int index,List<Diagnostic> diagnostics)
    {
        // Every level of a dotted name is instantiated exactly when the body is.
        var meaning=HasValues(ns.Body)?AmbientMeaning.ValueModule:AmbientMeaning.NamespaceModule;
        var symbol=Add(table,new(file,ns.Name[index],ns),meaning,diagnostics);
        if(index<ns.Name.Count-1){DeclareNamespace(file,symbol.Exports,ns,index+1,diagnostics);return;}
        foreach(var inner in ns.Body)Declare(file,symbol.Exports,inner,diagnostics);
    }

    // A namespace is instantiated (a value) when it declares any value.
    private static bool HasValues(IReadOnlyList<AmbientStatement> body)=>body.Any(s=>s switch
    {
        AmbientVariable or AmbientFunction or AmbientClass=>true,
        AmbientNamespace inner=>HasValues(inner.Body),
        _=>false,
    });

    private static AmbientMeaning Excludes(AmbientMeaning meanings)
    {
        var excludes=AmbientMeaning.None;
        foreach(var meaning in Enum.GetValues<AmbientMeaning>())
            if(meaning!=AmbientMeaning.None && meaning!=AmbientMeaning.Value && meaning!=AmbientMeaning.Type && (meanings&meaning)==meaning)
                excludes|=meaning switch
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
        return excludes;
    }

    private static Diagnostic Conflict(AmbientDeclaration declaration,bool blockScoped)=>blockScoped
        ?Diagnostic.Error(declaration.File,declaration.Name,"TS2451",$"Cannot redeclare block-scoped variable '{declaration.Name.Text}'.")
        :Diagnostic.Error(declaration.File,declaration.Name,"TS2300",$"Duplicate identifier '{declaration.Name.Text}'.");

    // Binder stage: the message follows the earlier symbol's meanings, and the
    // excluded declaration binds to a fresh symbol that stays out of the table.
    private static AmbientSymbol Add(Dictionary<string,AmbientSymbol> table,AmbientDeclaration declaration,AmbientMeaning meaning,List<Diagnostic> diagnostics)
    {
        var name=declaration.Name.Text;
        if(!table.TryGetValue(name,out var symbol))table[name]=symbol=new(name);
        else if((symbol.Meanings&Excludes(meaning))!=0)
        {
            diagnostics.Add(Conflict(declaration,(symbol.Meanings&AmbientMeaning.BlockScopedVariable)!=0));
            symbol=new(name);
        }
        symbol.Meanings|=meaning;
        symbol.Declarations.Add(declaration);
        return symbol;
    }

    // Checker stage: a file's symbols merge into the globals, namespace exports recursively.
    private static void MergeTable(Dictionary<string,AmbientSymbol> target,Dictionary<string,AmbientSymbol> source,List<Diagnostic> diagnostics)
    {
        foreach(var (name,symbol) in source)
        {
            if(!target.TryGetValue(name,out var existing)){target[name]=symbol;continue;}
            if((existing.Meanings&Excludes(symbol.Meanings))!=0 && (existing.Meanings&AmbientMeaning.NamespaceModule)!=0)
            {
                // checker.go mergeSymbol: a non-instantiated namespace target gets
                // TS2649 at the later symbol's first declaration only.
                var first=symbol.Declarations[0];
                diagnostics.Add(Diagnostic.Error(first.File,first.Name,"TS2649",
                    $"Cannot augment module '{name}' with value exports because it resolves to a non-module entity."));
                continue;
            }
            if((existing.Meanings&Excludes(symbol.Meanings))!=0)
            {
                var blockScoped=((existing.Meanings|symbol.Meanings)&AmbientMeaning.BlockScopedVariable)!=0;
                foreach(var declaration in symbol.Declarations)diagnostics.Add(Conflict(declaration,blockScoped));
                continue;
            }
            existing.Meanings|=symbol.Meanings;
            existing.Declarations.AddRange(symbol.Declarations);
            MergeTable(existing.Exports,symbol.Exports,diagnostics);
        }
    }
}

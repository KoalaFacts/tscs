namespace Tscs;

internal sealed class Symbol(string file,Statement declaration)
{
    public string File { get; } = file;
    public Statement Declaration { get; } = declaration;
    public bool IsConstant => Declaration is Variable { Constant:true } or ForOfStatement { Constant:true };
    public int DeclarationStart => Declaration switch
    {
        Variable v => v.Name.Start,
        FunctionDeclaration f => f.Name.Start,
        ParameterDeclaration p => p.Name.Start,
        ForOfStatement loop => loop.Name.Start,
        _ => throw new InvalidOperationException("Unsupported symbol declaration.")
    };
    public TypeSymbol? Type { get; set; }
    public bool Initialized { get; set; }
    public bool CrossFileTypeReady { get; set; }
    public bool Imported { get; set; }
}
internal sealed class Scope(Scope? parent)
{
    public Scope? Parent { get; } = parent;
    public Dictionary<string,Symbol> Symbols { get; } = new(StringComparer.Ordinal);
    public Symbol? Find(string name)=>Symbols.GetValueOrDefault(name)??Parent?.Find(name);
}
// A shared global scope is prebound for all .ts scripts before type checking.
// Complex cross-file inference and declaration merging intentionally fail closed.
internal static class Binder
{
    public static Scope Bind(string file,Block block,Scope? parent)
    {
        var scope=new Scope(parent);
        foreach(var statement in block.Statements)
            if(statement is Variable or FunctionDeclaration)Declare(file,scope,statement);
        InitializeDeclaredTypes(scope);
        return scope;
    }

    public static Scope BindProgram(IEnumerable<(string File,Block Block)> sources)
    {
        var global=new Scope(null);
        foreach(var (file,root) in sources)
            foreach(var statement in root.Statements)
                if(statement is Variable or FunctionDeclaration)Declare(file,global,statement);
        InitializeDeclaredTypes(global);
        return global;
    }

    public static Scope BindModule(string file,Block block,Scope global)
    {
        var local=Bind(file,block,global);
        InitializeDeclaredTypes(local);
        return local;
    }

    public static void DeclareParameter(string file,Scope scope,ParameterDeclaration parameter)
    {
        Declare(file,scope,parameter);
        var symbol=scope.Symbols[parameter.Name.Text];
        symbol.Type=CompilerTypes.FromAnnotation(parameter.Annotation);
        symbol.Initialized=true;
        symbol.CrossFileTypeReady=true;
    }

    public static void DeclareIterationVariable(string file,Scope scope,ForOfStatement loop,TypeSymbol type)
    {
        var symbol=new Symbol(file,loop)
        {
            Type=type, Initialized=true, CrossFileTypeReady=true
        };
        if(!scope.Symbols.TryAdd(loop.Name.Text,symbol))
            throw new CompilationFailure(Diagnostic.Error(file,loop.Name,"TSCS1001",
                "Duplicate for..of iteration binding is not supported."));
    }

    public static void Import(string file,Scope scope,ImportSpecifier specifier,Symbol exported)
    {
        if(!exported.CrossFileTypeReady)
            throw new CompilationFailure(Diagnostic.Error(file,specifier.Imported,"TSCS1001",
                "Imported declarations require a known semantic type."));
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
            switch(symbol.Declaration)
            {
                case Variable declaration:
                    TypeSymbol? literalType=CompilerTypes.FromInitializer(declaration.Initializer);
                    symbol.Type=declaration.Annotation is { } annotation
                        ? CompilerTypes.FromAnnotation(annotation) : literalType;
                    symbol.Initialized=declaration.Initializer!=null;
                    symbol.CrossFileTypeReady=symbol.Type!=null;
                    break;
                case FunctionDeclaration function:
                    symbol.Type=new FunctionTypeSymbol(
                        function.Parameters.Select(p=>CompilerTypes.FromAnnotation(p.Annotation)).ToArray(),
                        CompilerTypes.FromAnnotation(function.ReturnAnnotation));
                    symbol.Initialized=true;
                    symbol.CrossFileTypeReady=true;
                    break;
            }
        }
    }
    private static void Declare(string file,Scope scope,Statement declaration)
    {
        var name=declaration switch
        {
            Variable v => v.Name,
            FunctionDeclaration f => f.Name,
            ParameterDeclaration p => p.Name,
            _ => throw new ArgumentOutOfRangeException(nameof(declaration))
        };
        if(!scope.Symbols.TryAdd(name.Text,new Symbol(file,declaration)))
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",
                "Redeclaration diagnostics and declaration merging are not supported yet."));
    }
}

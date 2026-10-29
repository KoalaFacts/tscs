namespace Tscs;

internal sealed class Symbol(string file,Statement declaration,Scope? declaringScope=null)
{
    public string File { get; } = file;
    public Statement Declaration { get; } = declaration;
    // Imported symbols retain their original type environment across aliases/re-exports.
    public Scope? DeclaringScope { get; } = declaringScope;
    public bool IsConstant => Declaration is Variable { Constant:true } or ForOfStatement { Constant:true } or ForInStatement { Constant:true };
    public int DeclarationStart => Declaration switch
    {
        Variable v => v.Name.Start,
        FunctionDeclaration f => f.Name.Start,
        ParameterDeclaration p => p.Name.Start,
        ForOfStatement loop => loop.Name.Start,
        ForInStatement loop => loop.Name.Start,
        _ => throw new InvalidOperationException("Unsupported symbol declaration.")
    };
    // Declared/widened assignment target is never replaced by a flow fact.
    // Type is the temporary read type used by branch/guard narrowing.
    public TypeSymbol? Type { get; set; }
    public TypeSymbol? DeclaredType { get; set; }
    // Conservative may-write marker. Unlike temporary read types it is never
    // rolled back when inspecting mutually exclusive control-flow paths.
    public int FlowWriteVersion { get; set; }
    public bool Initialized { get; set; }
    public bool CrossFileTypeReady { get; set; }
    public bool Imported { get; set; }
}
internal sealed class Scope(Scope? parent)
{
    public Scope? Parent { get; } = parent;
    public Dictionary<string,Symbol> Symbols { get; } = new(StringComparer.Ordinal);
    // Value and type declarations have separate namespaces; import type is
    // never fabricated as a runtime symbol or emitted JavaScript binding.
    public Dictionary<string,TypeBinding> Types { get; } = new(StringComparer.Ordinal);
    public Symbol? Find(string name)=>Symbols.GetValueOrDefault(name)??Parent?.Find(name);
    public TypeBinding? FindType(string name)=>Types.GetValueOrDefault(name)??Parent?.FindType(name);
}
// A shared global scope is prebound for all .ts scripts before type checking.
// Complex cross-file inference and declaration merging intentionally fail closed.
internal static class Binder
{
    public static Scope Bind(string file,Block block,Scope? parent,bool initialize=true)
    {
        var scope=new Scope(parent);
        foreach(var statement in block.Statements)
        {
            if(statement is Variable or FunctionDeclaration)Declare(file,scope,statement);
            else if(statement is TypeAliasDeclaration or InterfaceDeclaration)DeclareType(file,scope,statement);
        }
        if(initialize)InitializeDeclaredTypes(scope);
        return scope;
    }

    public static Scope BindProgram(IEnumerable<(string File,Block Block)> sources)
    {
        var global=new Scope(null);
        foreach(var (file,root) in sources)
            foreach(var statement in root.Statements)
            {
                if(statement is Variable or FunctionDeclaration)Declare(file,global,statement);
                else if(statement is TypeAliasDeclaration or InterfaceDeclaration)DeclareType(file,global,statement);
            }
        InitializeDeclaredTypes(global);
        return global;
    }

    public static Scope BindModule(string file,Block block,Scope global)=>Bind(file,block,global,initialize:false);

    public static void ImportType(string file,Scope scope,ImportSpecifier specifier,TypeBinding exported)
    {
        if(!scope.Types.TryAdd(specifier.Local.Text,exported))
            throw new CompilationFailure(Diagnostic.Error(file,specifier.Local,"TSCS1001",
                "Duplicate type import or declaration is not supported yet."));
    }

    private static void DeclareType(string file,Scope scope,Statement declaration)
    {
        var (name,syntax)=declaration switch
        {
            TypeAliasDeclaration alias=>(alias.Name,alias.Target),
            InterfaceDeclaration iface=>(iface.Name,(TypeSyntax)iface.Body),
            _=>throw new InvalidOperationException("Not a type declaration.")
        };
        if(!scope.Types.TryAdd(name.Text,new TypeBinding(file,name,syntax,scope)))
            throw new CompilationFailure(Diagnostic.Error(file,name,"TS2300",
                $"Duplicate identifier '{name.Text}'."));
    }

    public static void DeclareParameter(string file,Scope scope,ParameterDeclaration parameter)
    {
        Declare(file,scope,parameter);
        var symbol=scope.Symbols[parameter.Name.Text];
        symbol.Type=TypeResolver.Resolve(parameter.Annotation,scope,file);
        symbol.DeclaredType=symbol.Type;
        symbol.Initialized=true;
        symbol.CrossFileTypeReady=true;
    }

    // Catch bindings are initialized unknown values scoped to their block;
    // this internal semantic type does not enable arbitrary unknown syntax.
    public static void DeclareCatchBinding(string file,Scope scope,Token name)
    {
        var declaration=new ParameterDeclaration(name,
            new PrimitiveTypeSyntax(new Token(TokenKind.Identifier,"unknown",name.Start,name.Length)));
        Declare(file,scope,declaration);
        var symbol=scope.Symbols[name.Text];
        symbol.Type=CompilerTypes.Unknown;
        symbol.DeclaredType=CompilerTypes.Unknown;
        symbol.Initialized=true;
        symbol.CrossFileTypeReady=true;
    }

    public static void DeclareIterationVariable(string file,Scope scope,ForOfStatement loop,TypeSymbol type)
    {
        var symbol=new Symbol(file,loop)
        {
            Type=type, DeclaredType=type, Initialized=true, CrossFileTypeReady=true
        };
        if(!scope.Symbols.TryAdd(loop.Name.Text,symbol))
            throw new CompilationFailure(Diagnostic.Error(file,loop.Name,"TSCS1001",
                "Duplicate for..of iteration binding is not supported."));
    }

    // For..in produces string property keys, even when the source is an array.
    public static void DeclareIterationVariable(string file,Scope scope,ForInStatement loop)
    {
        var symbol=new Symbol(file,loop)
        {
            Type=CompilerTypes.String, DeclaredType=CompilerTypes.String,
            Initialized=true, CrossFileTypeReady=true
        };
        if(!scope.Symbols.TryAdd(loop.Name.Text,symbol))
            throw new CompilationFailure(Diagnostic.Error(file,loop.Name,"TSCS1001",
                "Duplicate for..in iteration binding is not supported."));
    }

    // Stage value aliases before resolving type namespaces so that a type-level
    // typeof query may refer to an imported declaration in its *origin* scope.
    // No uninitialized type is made visible to the runtime checker.
    public static Symbol StageImport(string file,Scope scope,ImportSpecifier specifier,Symbol exported)
    {
        var imported=new Symbol(exported.File,exported.Declaration,exported.DeclaringScope)
        {
            Imported=true
        };
        if(!scope.Symbols.TryAdd(specifier.Local.Text,imported))
            throw new CompilationFailure(Diagnostic.Error(file,specifier.Local,"TSCS1001",
                "Duplicate import or local declaration is not supported yet."));
        return imported;
    }

    public static void CompleteImport(string file,ImportSpecifier specifier,Symbol imported,Symbol exported)
    {
        if(!exported.CrossFileTypeReady)
            throw new CompilationFailure(Diagnostic.Error(file,specifier.Imported,"TSCS1001",
                "Imported declarations require a known semantic type."));
        imported.Type=exported.Type;
        imported.DeclaredType=exported.DeclaredType;
        imported.Initialized=exported.Initialized;
        imported.CrossFileTypeReady=true;
    }

    // Shared signature construction is also used by type-level typeof queries.
    // Thread the resolution depth through recursive annotations to fail closed.
    public static FunctionTypeSymbol ResolveFunctionSignature(FunctionDeclaration function,Scope scope,string file,int depth=0)
    {
        if(depth>64)
            throw new CompilationFailure(Diagnostic.Error(file,function.Name,"TSCS1001",
                "Function signature type query exceeds the supported depth."));
        return new FunctionTypeSymbol(
            function.Parameters.Select(p=>TypeResolver.Resolve(p.Annotation,scope,file,depth+1)).ToArray(),
            TypeResolver.Resolve(function.ReturnAnnotation,scope,file,depth+1),
            function.Parameters.TakeWhile(p=>!p.Optional).Count(),
            function.Parameters.LastOrDefault() is { Rest:true,Annotation:ArrayTypeSyntax restArray }
                ? TypeResolver.Resolve(restArray.Element,scope,file,depth+1)
                : null);
    }

    public static void InitializeDeclaredTypes(Scope scope)
    {
        // Validate all declarations even when nothing references their aliases.
        // The scope is complete before any named type is resolved.
        foreach(var binding in scope.Types.Values.Distinct())_ = TypeResolver.ResolveBinding(binding);
        foreach(var symbol in scope.Symbols.Values)
        {
            // Staged imports are materialized only after every module has
            // resolved its own declarations and aliases.
            if(symbol.Imported)continue;
            switch(symbol.Declaration)
            {
                case Variable declaration:
                    // Prebind cross-module assertion exports from their type,
                    // without changing the runtime initializer or skipping
                    // the later checked expression. Satisfies keeps inference.
                    ValueNode? initial=declaration.Initializer;
                    while(initial is ValueParenthesized group)initial=group.Expression;
                    TypeSymbol? literalType=initial switch
                    {
                        ValueTypeOperator { Keyword.Text:"as" } asserted =>
                            TypeResolver.Resolve(asserted.Target,scope,symbol.File),
                        ValueTypeOperator { Keyword.Text:"satisfies" } satisfied =>
                            CompilerTypes.FromInitializer(satisfied.Expression) is { } source
                                ? CompilerTypes.PreserveSatisfiedLiteralTypes(satisfied.Expression,source,
                                    TypeResolver.Resolve(satisfied.Target,scope,symbol.File))
                                : null,
                        _=>CompilerTypes.FromInitializer(initial)
                    };
                    symbol.Type=declaration.Annotation is { } annotation
                        ? TypeResolver.Resolve(annotation,scope,symbol.File) : literalType;
                    symbol.Initialized=declaration.Initializer!=null;
                    symbol.CrossFileTypeReady=symbol.Type!=null;
                    break;
                case FunctionDeclaration function:
                    symbol.Type=ResolveFunctionSignature(function,scope,symbol.File);
                    symbol.Initialized=true;
                    symbol.CrossFileTypeReady=true;
                    break;
            }
            symbol.DeclaredType=symbol.Type;
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
        if(!scope.Symbols.TryAdd(name.Text,new Symbol(file,declaration,scope)))
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",
                "Redeclaration diagnostics and declaration merging are not supported yet."));
    }
}

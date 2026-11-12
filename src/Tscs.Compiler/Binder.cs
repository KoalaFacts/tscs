namespace Tscs;

internal sealed class Symbol(string file,Statement declaration,Scope? declaringScope=null)
{
    public string File { get; } = file;
    public Statement Declaration { get; } = declaration;
    // Imported symbols retain their original type environment across aliases/re-exports.
    public Scope? DeclaringScope { get; } = declaringScope;
    // A named function expression binds a read-only self name, without an outer declaration.
    public bool FunctionExpressionName { get; init; }
    // A named expression's self type is unresolved until body inference finishes.
    public bool PendingFunctionReturnInference { get; init; }
    public bool IsConstant => FunctionExpressionName || Declaration is Variable { Constant:true } or ForOfStatement { Constant:true } or ForInStatement { Constant:true };
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
    // Only identically typed ambient var declarations can share this binding.
    // Other redeclarations fail closed; these remain source-backed.
    public List<(string File,Variable Declaration)> AmbientVarMerges { get; }=[];
}
internal sealed class Scope(Scope? parent)
{
    public Scope? Parent { get; } = parent;
    public Dictionary<string,Symbol> Symbols { get; } = new(StringComparer.Ordinal);
    // Pending parameter declarations shadow globals and enclosing bindings
    // throughout their own (and later) default initializers. They are TDZ.
    public HashSet<string> PendingParameters { get; } = new(StringComparer.Ordinal);
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
            InterfaceDeclaration iface=>(iface.Name,iface.Bases is { Count:>0 }
                ?(TypeSyntax)new InterfaceExtensionTypeSyntax(iface.Name,iface.Bases,iface.Body)
                :iface.Body),
            _=>throw new InvalidOperationException("Not a type declaration.")
        };
        var typeParameters=declaration switch
        {
            TypeAliasDeclaration alias=>alias.TypeParameters,
            InterfaceDeclaration iface=>iface.TypeParameters,
            _=>null
        }??[];
        var typeDefaults=declaration switch
        {
            TypeAliasDeclaration alias=>alias.TypeParameterDefaults,
            InterfaceDeclaration iface=>iface.TypeParameterDefaults,
            _=>null
        }??[];
        var typeConstraints=declaration switch
        {
            TypeAliasDeclaration alias=>alias.TypeParameterConstraints,
            InterfaceDeclaration iface=>iface.TypeParameterConstraints,
            _=>null
        }??[];
        // Predefined type names are not valid interface names (TS2427).
        if(declaration is InterfaceDeclaration && name.Text is "any" or "unknown" or "never" or "number" or
               "bigint" or "boolean" or "string" or "symbol" or "void" or "object" or "undefined")
            throw new CompilationFailure(Diagnostic.Error(file,name,"TS2427",
                $"Interface name cannot be '{name.Text}'."));
        // The compiler models these library types intrinsically ('Array<T>'
        // always means the built-in array type, bootstrap globals come from
        // the validated library contract), so a generic user declaration
        // would be silently ignored instead of merged (TS2428/TS2317).
        if(declaration is InterfaceDeclaration && typeParameters.Count!=0 &&
           name.Text is "Array" or "ReadonlyArray" or "Iterable" or "Iterator" or "IterableIterator" or "IteratorResult" or
               "Boolean" or "CallableFunction" or "Function" or "IArguments" or "NewableFunction" or
               "Number" or "Object" or "RegExp" or "String")
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",
                $"A generic interface named after the library type '{name.Text}' is not supported yet."));
        // Predefined type names are not valid type parameter names (TS2368).
        foreach(var parameter in typeParameters)
            if(parameter.Text is "number" or "string" or "boolean" or "any" or "unknown" or
               "undefined" or "never" or "object" or "bigint" or "symbol")
                throw new CompilationFailure(Diagnostic.Error(file,parameter,"TS2368",
                    $"Type parameter name cannot be '{parameter.Text}'."));
        if(scope.Types.TryGetValue(name.Text,out var previous))
        {
            // Non-generic interfaces may reopen a single global/module type.
            // Imported types and aliases remain separate, non-mergeable symbols.
            if(declaration is InterfaceDeclaration && previous.IsInterface &&
               ReferenceEquals(previous.Owner,scope))
            {
                // Defaults on reopened generic interfaces require their own
                // cross-declaration equivalence proof. Fail closed for now.
                if(typeDefaults.Count!=0 || previous.TypeParameterDefaults.Count!=0 ||
                   typeConstraints.Count!=0 || previous.TypeParameterConstraints.Count!=0)
                    throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",
                        "Merging generic interface declarations with defaults or constraints is not supported yet."));
                // Reopening a generic interface with an identical plain type
                // parameter list merges per instantiation. Different lists
                // are TS2428 (reported on every declaration): not modelled yet.
                if(!typeParameters.Select(p=>p.Text).SequenceEqual(
                       previous.TypeParameters.Select(p=>p.Text),StringComparer.Ordinal))
                    throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",
                        "Merging interface declarations with different type parameter lists is not supported yet."));
                // Exported and local declarations of one name are TS2395 on
                // every declaration in the reference: not modelled yet.
                if(((InterfaceDeclaration)declaration).Exported!=previous.ExportedDeclaration)
                    throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",
                        "Merging exported and local declarations is not supported yet."));
                previous.MergedInterfaces.Add((file,name,syntax,scope));
                return;
            }
            throw new CompilationFailure(Diagnostic.Error(file,name,"TS2300",
                $"Duplicate identifier '{name.Text}'."));
        }
        scope.Types.Add(name.Text,new TypeBinding(file,name,syntax,scope)
        {
            IsInterface=declaration is InterfaceDeclaration,
            IsTypeAlias=declaration is TypeAliasDeclaration,
            ExportedDeclaration=declaration is InterfaceDeclaration { Exported:true },
            TypeParameters=typeParameters,
            TypeParameterDefaults=typeDefaults,
            TypeParameterConstraints=typeConstraints
        });
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

    // A named expression's name belongs to a private scope outside parameters:
    // recursion sees it, parameters can shadow it, the enclosing scope cannot.
    public static void DeclareFunctionExpressionName(string file,Scope scope,Token name,
        IReadOnlyList<ParameterDeclaration> parameters,TypeSyntax returnType,Block body)
    {
        var declaration=new FunctionDeclaration(name,parameters,returnType,body);
        var signature=ResolveFunctionSignature(declaration,scope,file);
        var symbol=new Symbol(file,declaration,scope)
        {
            Type=signature,DeclaredType=signature,Initialized=true,
            CrossFileTypeReady=true,FunctionExpressionName=true
        };
        if(!scope.Symbols.TryAdd(name.Text,symbol))
            throw new CompilationFailure(Diagnostic.Error(file,name,"TS2300",
                $"Duplicate identifier '{name.Text}'."));
    }

    // Reserve a lexical self name without fabricating 'any' or a callable type.
    // Resolve fails closed when the body references this pending binding.
    public static void DeclareInferredFunctionExpressionName(string file,Scope scope,Token name)
    {
        var pending=new Symbol(file,new Variable(name,true,null,null),scope)
        {
            FunctionExpressionName=true,PendingFunctionReturnInference=true,
            Initialized=true,CrossFileTypeReady=true
        };
        if(!scope.Symbols.TryAdd(name.Text,pending))
            throw new CompilationFailure(Diagnostic.Error(file,name,"TS2300",
                $"Duplicate identifier '{name.Text}'."));
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

    // Named members of a fresh const literal retain their original types.
    private static TypeSymbol? ResolveConstAssertionIdentifier(Token token,Scope scope,string file,int depth)
    {
        if(depth>64)return null;
        var symbol=scope.Find(token.Text);
        if(symbol is null)return token.Text=="undefined"?CompilerTypes.Undefined:null;
        if(symbol.PendingFunctionReturnInference ||
           symbol.File==file && symbol.Declaration is not FunctionDeclaration &&
               token.Start<=symbol.DeclarationStart ||
           symbol.File!=file && !symbol.CrossFileTypeReady)
            return null;
        if(symbol.Initialized && symbol.Type is { } ready)return ready;
        // Type queries run before all variable types have been prebound.
        // A preceding declaration with an explicit annotation is already
        // semantically known even when its initializer has not been checked.
        // Do not infer an unannotated, uninitialized or forward reference.
        if(symbol.File==file && symbol.Declaration is Variable
            {Annotation:{ } annotation,Initializer:not null})
            return TypeResolver.Resolve(annotation,symbol.DeclaringScope??scope,file,depth+1);
        return null;
    }

    // Cross-module exports must have their finite spread member types ready
    // before the module linker checks imports. Check only already initialized
    // named sources or independently inferable literal object operands;
    // unsupported expressions remain unresolved until normal checking.
    private static ObjectTypeSymbol? ResolveConstSpreadSource(
        ValueNode value,Scope scope,string file,int depth)
    {
        if(depth>64)return null;
        while(value is ValueParenthesized grouped)value=grouped.Expression;
        TypeSymbol? inferred=value is ValueAtom {Token.Kind:TokenKind.Identifier} atom
            ?ResolveConstAssertionIdentifier(atom.Token,scope,file,depth+1)
            :ResolveFiniteInitializerType(value,scope,file,false,depth+1);
        return CompilerTypes.TryFiniteObjectSpreadSource(inferred);
    }

    // A finite array spread has the same already-checked tuple identity in
    // cross-module prebinding and in ordinary expression checking. Unknown
    // length arrays remain unsupported; optional tuple spreads are safe only
    // when they are final elements of finite const arrays.
    private static ArrayTypeSymbol? ResolveConstTupleSpreadSource(
        ValueNode value,Scope scope,string file,int depth)
    {
        if(depth>64)return null;
        while(value is ValueParenthesized grouped)value=grouped.Expression;
        TypeSymbol? inferred=value is ValueAtom {Token.Kind:TokenKind.Identifier} atom
            ?ResolveConstAssertionIdentifier(atom.Token,scope,file,depth+1)
            :ResolveFiniteInitializerType(value,scope,file,false,depth+1);
        return inferred as ArrayTypeSymbol;
    }

    // Prebinding and typeof queries must infer the same finite initializer.
    // const primitive literals stay exact; mutable values and object members widen.
    public static TypeSymbol? ResolveFiniteInitializerType(
        ValueNode? initial,Scope scope,string file,bool constant,int depth=0)
    {
        // Recursive finite object/array assertions can contain nested type
        // queries. Bound the inference just like named type resolution.
        if(depth>64)return null;
        while(initial is ValueParenthesized group)initial=group.Expression;
        return initial switch
        {
            ValueTypeOperator { Keyword.Text:"as",Target:NamedTypeSyntax {Name.Kind:TokenKind.Const} } asserted =>
                CompilerTypes.FromFiniteConstAssertion(asserted.Expression,depth+1,
                    token=>ResolveConstAssertionIdentifier(token,scope,file,depth+1),
                    checkedObjectSpreadSource:entry=>
                        ResolveConstSpreadSource(entry.Value,scope,file,depth+1),
                    checkedTupleSpreadSource:entry=>
                        ResolveConstTupleSpreadSource(entry.Operand,scope,file,depth+1)),
            ValueTypeOperator { Keyword.Text:"as" } asserted =>
                TypeResolver.Resolve(asserted.Target,scope,file,depth+1),
            ValueTypeOperator { Keyword.Text:"satisfies" } satisfied =>
                ResolveFiniteInitializerType(satisfied.Expression,scope,file,false,depth+1) is { } source
                    ?CompilerTypes.PreserveSatisfiedLiteralTypes(satisfied.Expression,source,
                        TypeResolver.Resolve(satisfied.Target,scope,file,depth+1))
                    :null,
            ValueAtom atom when constant &&
                (atom.Token.Kind is TokenKind.Number or TokenKind.String or TokenKind.True or TokenKind.False) =>
                CompilerTypes.FromLiteralExact(atom.Token),
            ValueUnary { Operator.Kind:TokenKind.Minus,Operand:ValueAtom number }
                when constant && number.Token.Kind==TokenKind.Number =>
                CompilerTypes.FromLiteralExact(number.Token,negative:true),
            _=>initial is ValueArray or ValueObject
                ?ResolveFiniteCompositeInitializerType(initial,scope,file,depth+1) ??
                    CompilerTypes.FromInitializer(initial)
                :CompilerTypes.FromInitializer(initial)
        };
    }

    // A closed initializer may contain a nested explicit literal assertion.
    // The original structural fast path handles ordinary literals/spreads;
    // only fall back here when that inference cannot resolve the initializer.
    // Unknown references, arbitrary expressions and spreads remain fail-closed.
    private static TypeSymbol? ResolveFiniteCompositeInitializerType(
        ValueNode? initial,Scope scope,string file,int depth)
    {
        if(depth>64)return null;
        if(initial is ValueArray array)
        {
            TypeSymbol element=CompilerTypes.Never;
            foreach(var item in array.Elements)
            {
                if(item is ValueArraySpread)return null;
                if(ResolveFiniteInitializerType(item,scope,file,false,depth+1) is not { } next)
                    return null;
                if(element.Equals(CompilerTypes.Never))element=next;
                else if(!CompilerTypes.IsEquivalent(element,next))
                {
                    try {element=CompilerTypes.Union([element,next]);}
                    catch(InvalidOperationException) {return null;}
                }
            }
            return new ArrayTypeSymbol(element);
        }
        if(initial is not ValueObject obj || obj.Properties.Count>128)
            return null;
        var members=new Dictionary<string,TypeSymbol>(StringComparer.Ordinal);
        var numericMembers=new HashSet<string>(StringComparer.Ordinal);
        var computedNumericMembers=new HashSet<string>(StringComparer.Ordinal);
        var optionalMembers=new HashSet<string>(StringComparer.Ordinal);
        var mixedMembers=new HashSet<string>(StringComparer.Ordinal);
        foreach(var property in obj.Properties)
        {
            if(property.Spread)
            {
                // Resolve an already checked/annotated identifier in its own
                // scope. This does not execute the spread or guess unknown types.
                var source=ResolveConstSpreadSource(property.Value,scope,file,depth+1);
                if(source is null)return null;
                foreach(var (spreadName,type) in source.Members)
                {
                    // typeof inference must enforce the same presence, readonly
                    // and numeric-identity invariants as the expression checker.
                    if(members.ContainsKey(spreadName) &&
                       source.MixedRequiredOptionalMembers.Contains(spreadName) &&
                       source.NumericMembers.Contains(spreadName))return null;
                    if(source.MixedRequiredOptionalMembers.Contains(spreadName))
                        mixedMembers.Add(spreadName);
                    else if(!source.OptionalMembers.Contains(spreadName))
                        mixedMembers.Remove(spreadName);
                    try
                    {
                        if(!CompilerTypes.MergeFiniteSpreadMember(members,numericMembers,
                            optionalMembers,spreadName,type,source.NumericMembers.Contains(spreadName),
                            source.OptionalMembers.Contains(spreadName),computedNumericMembers,
                            source.ComputedNumericMembers.Contains(spreadName)))return null;
                    }
                    catch(InvalidOperationException) {return null;}
                }
                continue;
            }
            var member=ResolveFiniteInitializerType(property.Value,scope,file,false,depth+1)
                ?? (property.Value is ValueAtom {Token.Kind:TokenKind.Identifier} atom
                    ?ResolveConstAssertionIdentifier(atom.Token,scope,file,depth+1):null);
            if(member is null)return null;
            var name=Parser.PropertyName(property.Name);
            members[name]=member;
            optionalMembers.Remove(name);
            mixedMembers.Remove(name);
            if(property.Name.Kind==TokenKind.Number)numericMembers.Add(name);
            else numericMembers.Remove(name);
            if(property.Computed && property.Name.Kind==TokenKind.Number)computedNumericMembers.Add(name);
            else computedNumericMembers.Remove(name);
        }
        mixedMembers.IntersectWith(optionalMembers);
        return new ObjectTypeSymbol(members,numericMembers:numericMembers,
            optionalMembers:optionalMembers,computedNumericMembers:computedNumericMembers,
            mixedRequiredOptionalMembers:mixedMembers);
    }

    public static void InitializeDeclaredTypes(Scope scope)
    {
        // Validate all declarations even when nothing references their aliases.
        // The scope is complete before any named type is resolved.
        foreach(var binding in scope.Types.Values.Distinct())
        {
            // A non-generic intrinsic declaration is checked, not resolved:
            // only a use of it fails closed.
            if(binding is { Syntax:IntrinsicTypeSyntax,TypeParameters.Count:0 })TypeResolver.ValidateIntrinsic(binding);
            else if(binding is { TypeParameters.Count:0 } && HasIndexSignature(binding))
            {
                // Like intrinsic: an indexed declaration is checked, and only
                // a use of it fails closed.
                TypeResolver.RejectUnmodelledIndexSignatures(binding);
                TypeResolver.ValidateDeclarationBody(binding.Syntax,binding.Owner,binding.File,1);
                foreach(var merged in binding.MergedInterfaces)
                    TypeResolver.ValidateDeclarationBody(merged.Syntax,merged.Owner,merged.File,1);
            }
            else _ = TypeResolver.ResolveBinding(binding);
        }
        // TypeScript permits multiple ambient var declarations only when their
        // resolved types agree. Check every saved source position after aliases
        // have been bound; never silently use the first or last annotation.
        foreach(var symbol in scope.Symbols.Values)
        {
            if(symbol.AmbientVarMerges.Count==0)continue;
            var first=(Variable)symbol.Declaration;
            var expected=TypeResolver.Resolve(first.Annotation!,scope,symbol.File);
            foreach(var (file,merged) in symbol.AmbientVarMerges)
            {
                var actual=TypeResolver.Resolve(merged.Annotation!,scope,file);
                if(!DeclarationTypeIdentity.Same(first.Annotation!,scope,symbol.File,
                    merged.Annotation!,scope,file))
                    throw new CompilationFailure(Diagnostic.Error(file,merged.Name,"TS2403",
                        $"Subsequent variable declarations must have the same type. Variable '{merged.Name.Text}' must be of type '{expected.DisplayName}', but here has type '{actual.DisplayName}'."));
            }
        }
        // A function declaration is hoisted, including within a later
        // const-literal value. Its signature must be known before a preceding
        // variable's initializer tries to inspect that function value.
        foreach(var symbol in scope.Symbols.Values)
        {
            if(symbol.Imported || symbol.Declaration is not FunctionDeclaration function)continue;
            symbol.Type=ResolveFunctionSignature(function,scope,symbol.File);
            symbol.Initialized=true;
            symbol.CrossFileTypeReady=true;
            symbol.DeclaredType=symbol.Type;
        }
        foreach(var symbol in scope.Symbols.Values)
        {
            // Value imports are materialized by ModuleLinker after their
            // source declaration is actually ready.
            if(symbol.Imported || symbol.Declaration is FunctionDeclaration)continue;
            switch(symbol.Declaration)
            {
                case Variable declaration:
                    // Prebind cross-module assertion exports from their type,
                    // without changing the runtime initializer or skipping
                    // the later checked expression. Satisfies keeps inference.
                    TypeSymbol? literalType=ResolveFiniteInitializerType(
                        declaration.Initializer,scope,symbol.File,declaration.Constant);
                    symbol.Type=declaration.Annotation is { } annotation
                        ? TypeResolver.Resolve(annotation,scope,symbol.File) : literalType;
                    symbol.Initialized=declaration.Initializer!=null;
                    symbol.CrossFileTypeReady=symbol.Type!=null;
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
        if(scope.Symbols.TryAdd(name.Text,new Symbol(file,declaration,scope)))return;
        var previous=scope.Symbols[name.Text];
        if(previous.Declaration is Variable { Ambient:true,AmbientVar:true,Annotation:not null,Initializer:null,Exported:false } &&
           declaration is Variable { Ambient:true,AmbientVar:true,Annotation:not null,Initializer:null,Exported:false } merged)
        {
            previous.AmbientVarMerges.Add((file,merged));
            return;
        }
        throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",
            "Redeclaration diagnostics and declaration merging are not supported yet."));
    }

    private static bool HasIndexSignature(TypeBinding binding)=>
        binding.MergedInterfaces.Select(m=>m.Syntax).Prepend(binding.Syntax).Any(syntax=>syntax switch
        {
            ObjectTypeSyntax shape=>shape.HasIndexSignatures,
            InterfaceExtensionTypeSyntax inherited=>inherited.Body.HasIndexSignatures,
            _=>false
        });
}

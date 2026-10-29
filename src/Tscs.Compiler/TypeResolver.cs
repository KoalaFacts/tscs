namespace Tscs;

// An alias carries its declaring environment, not the importing module's.
// The same binding object is reused when imported under a different name.
internal sealed class TypeBinding(string file,Token name,TypeSyntax syntax,Scope owner)
{
    public string File { get; }=file;
    public Token Name { get; }=name;
    public TypeSyntax Syntax { get; }=syntax;
    public Scope Owner { get; }=owner;
    public TypeSymbol? Resolved { get; set; }
    public bool Resolving { get; set; }
}

internal static class TypeResolver
{
    public static TypeSymbol ResolveBinding(TypeBinding binding,int depth=0)
    {
        if(binding.Resolved is { } cached)return cached;
        if(binding.Resolving || depth>64)
            throw new CompilationFailure(Diagnostic.Error(binding.File,binding.Name,"TS2456",
                $"Type alias '{binding.Name.Text}' circularly references itself or exceeds the supported depth."));
        binding.Resolving=true;
        try {return binding.Resolved=Resolve(binding.Syntax,binding.Owner,binding.File,depth+1);}
        finally {binding.Resolving=false;}
    }

    public static TypeSymbol Resolve(TypeSyntax syntax,Scope scope,string file,int depth=0)
    {
        if(depth>64)
            throw new CompilationFailure(Diagnostic.Error(file,Location(syntax),"TSCS1001",
                "Type resolution exceeds the bounded compiler subset."));
        switch(syntax)
        {
            case PrimitiveTypeSyntax primitive: return CompilerTypes.FromAnnotation(primitive.Name.Text);
            case LiteralTypeSyntax literal: return CompilerTypes.FromLiteralExact(literal.Literal,literal.Negative);
            case FunctionTypeSyntax function:
            {
                var parameters=function.Parameters.Select(p=>Resolve(p.Type,scope,file,depth+1)).ToArray();
                var last=function.Parameters.LastOrDefault();
                return new FunctionTypeSymbol(parameters,
                    Resolve(function.ReturnType,scope,file,depth+1),
                    function.Parameters.TakeWhile(p=>!p.Optional && !p.Rest).Count(),
                    last is { Rest:true,Type:ArrayTypeSyntax array }
                        ?Resolve(array.Element,scope,file,depth+1):null);
            }
            case NamedTypeSyntax named:
                if(scope.FindType(named.Name.Text) is not { } binding)
                    throw new CompilationFailure(Diagnostic.Error(file,named.Name,"TS2304",
                        $"Cannot find name '{named.Name.Text}'."));
                return ResolveBinding(binding,depth+1);
            case TypeQuerySyntax query:
            {
                if(scope.Find(query.Root.Text) is not { } symbol)
                    throw new CompilationFailure(Diagnostic.Error(file,query.Root,"TS2304",
                        $"Cannot find name '{query.Root.Text}'."));
                // Imported aliases point at the exporting declaration, not the
                // consumer's type namespace. Reading its annotation here is safe
                // even before the import's runtime type is hydrated.
                var declaringScope=symbol.DeclaringScope??scope;
                TypeSymbol target=symbol.Declaration switch
                {
                    Variable { Annotation: { } annotation } =>
                        Resolve(annotation,declaringScope,symbol.File,depth+1),
                    FunctionDeclaration function =>
                        Binder.ResolveFunctionSignature(function,declaringScope,symbol.File,depth+1),
                    _=>throw new CompilationFailure(Diagnostic.Error(file,query.Root,"TSCS1001",
                        "typeof currently requires an explicitly annotated value or typed function."))
                };
                foreach(var member in query.Members)
                {
                    var resolved=CompilerTypes.TryGetProperty(target,Parser.PropertyName(member));
                    if(resolved is null)
                        throw new CompilationFailure(Diagnostic.Error(file,member,"TS2339",
                            $"Property '{Parser.PropertyName(member)}' does not exist on type '{target.DisplayName}'."));
                    target=resolved;
                }
                return target;
            }
            case KeyofTypeSyntax keyed:
            {
                var operand=Resolve(keyed.Target,scope,file,depth+1);
                // keyof on a union can use only names present in every
                // structural alternative, regardless of their value types.
                IReadOnlyList<ObjectTypeSymbol> alternatives=operand switch
                {
                    ObjectTypeSymbol shape=>[shape],
                    UnionTypeSymbol union when union.Alternatives.All(type=>type is ObjectTypeSymbol)=>
                        union.Alternatives.Cast<ObjectTypeSymbol>().ToArray(),
                    _=>throw new CompilationFailure(Diagnostic.Error(file,keyed.Keyword,"TSCS1001",
                        "keyof requires a known structural object or union of structural objects."))
                };
                try
                {
                    var first=alternatives[0];
                    // JavaScript lookup aliases 0 and "0"; keyof must not.
                    // Each union arm must agree on both runtime name and key identity.
                    var shared=first.Members.Keys.Where(name=>
                        alternatives.Skip(1).All(shape=>shape.Members.ContainsKey(name) &&
                            shape.NumericMembers.Contains(name)==first.NumericMembers.Contains(name)));
                    return CompilerTypes.Union(shared.Select(name=>
                        (TypeSymbol)new LiteralTypeSymbol(first.NumericMembers.Contains(name)
                            ?PrimitiveKind.Number:PrimitiveKind.String,name)));
                }
                catch(InvalidOperationException failure)
                {
                    throw new CompilationFailure(Diagnostic.Error(file,keyed.Keyword,"TSCS1001",failure.Message));
                }
            }
            case IndexedAccessTypeSyntax indexed:
            {
                var receiver=Resolve(indexed.Target,scope,file,depth+1);
                var selector=Resolve(indexed.Index,scope,file,depth+1);
                if(receiver is not ObjectTypeSymbol &&
                    !(receiver is UnionTypeSymbol objectUnion &&
                      objectUnion.Alternatives.All(type=>type is ObjectTypeSymbol)))
                    throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                        "Indexed access types require a known structural object or union of structural objects."));
                // Exact literal keys only; unbounded index signatures are not implemented.
                LiteralTypeSymbol[] keys=selector switch
                {
                    LiteralTypeSymbol { Kind:PrimitiveKind.String or PrimitiveKind.Number } literal=>[literal],
                    UnionTypeSymbol union when union.Alternatives.All(type=>
                        type is LiteralTypeSymbol { Kind:PrimitiveKind.String or PrimitiveKind.Number })=>
                        union.Alternatives.Cast<LiteralTypeSymbol>().ToArray(),
                    PrimitiveTypeSymbol { Kind:PrimitiveKind.Never }=>[],
                    _=>throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                        "Indexed access requires a literal string/number key or finite union of such keys."))
                };
                var values=new List<TypeSymbol>(keys.Length);
                foreach(var key in keys)
                {
                    if(key.Kind==PrimitiveKind.Number &&
                        (!long.TryParse(key.Value,System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture,out var number) ||
                         number>9007199254740991L))
                        throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                            "Numeric indexed-access keys must be non-negative safe integers."));
                    var member=CompilerTypes.TryGetProperty(receiver,key.Value);
                    if(member==null)
                        throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TS2339",
                            $"Property '{key.Value}' does not exist on type '{receiver.DisplayName}'."));
                    values.Add(member);
                }
                try {return CompilerTypes.Union(values);}
                catch(InvalidOperationException failure)
                {
                    throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",failure.Message));
                }
            }
            case ArrayTypeSyntax array:return new ArrayTypeSymbol(Resolve(array.Element,scope,file,depth+1));
            case InterfaceExtensionTypeSyntax inherited:
            {
                // Never erase missing or incompatible inherited members. Each
                // imported base resolves in this declaration's original scope.
                var members=new Dictionary<string,TypeSymbol>(StringComparer.Ordinal);
                var readonlyKeys=new HashSet<string>(StringComparer.Ordinal);
                var numericKeys=new HashSet<string>(StringComparer.Ordinal);
                // Own members can resolve otherwise different inherited types,
                // but each explicit member must satisfy EVERY base constraint.
                var declared=new HashSet<string>(StringComparer.Ordinal);
                foreach(var property in inherited.Body.Properties)
                {
                    var name=Parser.PropertyName(property.Name);
                    members.Add(name,Resolve(property.Type,scope,file,depth+1));
                    declared.Add(name);
                    if(property.Name.Kind==TokenKind.Number)numericKeys.Add(name);
                    if(property.Readonly)readonlyKeys.Add(name);
                    if(members.Count>128)
                        throw new CompilationFailure(Diagnostic.Error(file,property.Name,"TSCS1001",
                            "Extended interface exceeds the finite 128-member limit."));
                }
                foreach(var named in inherited.Bases)
                {
                    if(Resolve(named,scope,file,depth+1) is not ObjectTypeSymbol parent)
                        throw new CompilationFailure(Diagnostic.Error(file,named.Name,"TSCS1001",
                            "Interface heritage requires a finite structural object base."));
                    foreach(var member in parent.Members)
                    {
                        bool numeric=parent.NumericMembers.Contains(member.Key);
                        bool isReadonly=parent.ReadonlyMembers.Contains(member.Key);
                        if(members.TryGetValue(member.Key,out var previous))
                        {
                            if(declared.Contains(member.Key))
                            {
                                if(numericKeys.Contains(member.Key)!=numeric ||
                                   !CompilerTypes.IsAssignable(previous,member.Value))
                                    throw new CompilationFailure(Diagnostic.Error(file,inherited.Name,"TS2430",
                                        $"Interface '{inherited.Name.Text}' incorrectly extends its base interface at '{member.Key}'."));
                            }
                            else if(numericKeys.Contains(member.Key)!=numeric ||
                                    readonlyKeys.Contains(member.Key)!=isReadonly ||
                                    !CompilerTypes.IsEquivalent(previous,member.Value))
                                throw new CompilationFailure(Diagnostic.Error(file,named.Name,"TSCS1001",
                                    $"Inherited member '{member.Key}' has incompatible base declarations."));
                        }
                        else
                        {
                            members.Add(member.Key,member.Value);
                            if(numeric)numericKeys.Add(member.Key);
                            if(isReadonly)readonlyKeys.Add(member.Key);
                        }
                        if(members.Count>128)
                            throw new CompilationFailure(Diagnostic.Error(file,named.Name,"TSCS1001",
                                "Interface heritage exceeds the finite 128-member limit."));
                    }
                }
                return new ObjectTypeSymbol(members,readonlyKeys,numericKeys);
            }
            case ObjectTypeSyntax shape:return new ObjectTypeSymbol(shape.Properties.Select(p=>
                new KeyValuePair<string,TypeSymbol>(Parser.PropertyName(p.Name),Resolve(p.Type,scope,file,depth+1))),
                shape.Properties.Where(p=>p.Readonly).Select(p=>Parser.PropertyName(p.Name)),
                shape.Properties.Where(p=>p.Name.Kind==TokenKind.Number).Select(p=>Parser.PropertyName(p.Name)));
            case IntersectionTypeSyntax intersection:
            {
                // Finite, required structural shapes can be safely flattened and
                // checked by the existing binder/checker without a new type escape hatch.
                // Unions/primitive operands, conflicting keys, methods and index
                // signatures stay explicitly unsupported rather than guessed.
                var properties=new Dictionary<string,TypeSymbol>(StringComparer.Ordinal);
                var numeric=new HashSet<string>(StringComparer.Ordinal);
                var readonlyKeys=new HashSet<string>(StringComparer.Ordinal);
                foreach(var part in intersection.Members)
                {
                    var shape=Resolve(part,scope,file,depth+1) as ObjectTypeSymbol;
                    if(shape is null)
                        throw new CompilationFailure(Diagnostic.Error(file,Location(part),"TSCS1001",
                            "Intersections currently require finite structural object operands."));
                    foreach(var member in shape.Members)
                    {
                        var name=member.Key;
                        bool isNumeric=shape.NumericMembers.Contains(name);
                        bool isReadonly=shape.ReadonlyMembers.Contains(name);
                        if(properties.TryGetValue(name,out var previous))
                        {
                            // Runtime names may alias 0 and "0" while keyof does
                            // not. Do not erase that distinction during flattening.
                            if(numeric.Contains(name)!=isNumeric)
                                throw new CompilationFailure(Diagnostic.Error(file,Location(part),"TSCS1001",
                                    $"Intersected property '{name}' has incompatible numeric/string key identities."));
                            if(CompilerTypes.IsEquivalent(previous,member.Value))
                            {
                                // Equivalent required member types stay unchanged.
                            }
                            else if(previous is LiteralTypeSymbol &&
                                member.Value is PrimitiveTypeSymbol &&
                                CompilerTypes.IsAssignable(previous,member.Value))
                            {
                                // Exact member constraint already narrows the primitive.
                            }
                            else if(member.Value is LiteralTypeSymbol &&
                                previous is PrimitiveTypeSymbol &&
                                CompilerTypes.IsAssignable(member.Value,previous))
                                properties[name]=member.Value;
                            else
                                throw new CompilationFailure(Diagnostic.Error(file,Location(part),"TSCS1001",
                                    $"Intersected property '{name}' has unsupported incompatible member types."));
                            // A mutable occurrence permits assignment to an
                            // otherwise readonly overlapping required member.
                            if(!isReadonly)readonlyKeys.Remove(name);
                        }
                        else
                        {
                            properties.Add(name,member.Value);
                            if(isNumeric)numeric.Add(name);
                            if(isReadonly)readonlyKeys.Add(name);
                            if(properties.Count>128)
                                throw new CompilationFailure(Diagnostic.Error(file,Location(part),"TSCS1001",
                                    "Intersected object exceeds the supported property bound."));
                        }
                    }
                }
                return new ObjectTypeSymbol(properties,readonlyKeys,numeric);
            }
            case UnionTypeSyntax union:
                try {return CompilerTypes.Union(union.Members.Select(member=>Resolve(member,scope,file,depth+1)));}
                catch(InvalidOperationException failure)
                {
                    throw new CompilationFailure(Diagnostic.Error(file,Location(union),"TSCS1001",failure.Message));
                }
            default:throw new InvalidOperationException("Unknown type annotation AST.");
        }
    }

    private static Token Location(TypeSyntax syntax)=>syntax switch
    {
        PrimitiveTypeSyntax value=>value.Name,
        LiteralTypeSyntax value=>value.Literal,
        FunctionTypeSyntax value=>value.Open,
        NamedTypeSyntax value=>value.Name,
        KeyofTypeSyntax value=>value.Keyword,
        TypeQuerySyntax value=>value.Keyword,
        IndexedAccessTypeSyntax value=>value.Open,
        ObjectTypeSyntax value=>value.Open,
        InterfaceExtensionTypeSyntax value=>value.Name,
        ArrayTypeSyntax value=>value.Open,
        IntersectionTypeSyntax value=>Location(value.Members[0]),
        UnionTypeSyntax value=>Location(value.Members[0]),
        _=>default
    };
}

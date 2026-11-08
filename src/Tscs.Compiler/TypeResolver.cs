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
    // Whether the generic declaration body has been checked once (ValidateGeneric).
    public bool Validated { get; set; }
    public bool IsInterface { get; init; }
    // Whether the first declaration carries 'export' (merging requires agreement, TS2395).
    public bool ExportedDeclaration { get; init; }
    // Plain type parameters of a generic alias or interface. Each reference instantiates
    // the alias body in a fresh scope; generic bindings are never cached.
    public IReadOnlyList<Token> TypeParameters { get; init; }=[];
    // Reopened interfaces retain source positions and declaring scopes.
    // No type alias or imported binding is silently merged with an interface.
    public List<(string File,Token Name,TypeSyntax Syntax,Scope Owner)> MergedInterfaces { get; }=[];
}

internal static class TypeResolver
{
    public static TypeSymbol ResolveBinding(TypeBinding binding,int depth=0)
    {
        if(binding.Resolved is { } cached)return cached;
        if(binding.TypeParameters.Count!=0)
        {
            ValidateGeneric(binding,depth);
            return CompilerTypes.Unknown;
        }
        if(binding.Resolving || depth>64)
            throw new CompilationFailure(Diagnostic.Error(binding.File,binding.Name,"TS2456",
                $"Type alias '{binding.Name.Text}' circularly references itself or exceeds the supported depth."));
        binding.Resolving=true;
        try
        {
            return binding.Resolved=Merge(binding,null,depth);
        }
        finally {binding.Resolving=false;}
    }

    // Merges reopened interface declarations. 'instance' is the scope of one
    // generic instantiation (all declarations share identical parameter
    // lists), or null for a non-generic interface.
    private static TypeSymbol Merge(TypeBinding binding,Scope? instance,int depth)
    {
        var first=Resolve(binding.Syntax,instance??binding.Owner,binding.File,depth+1);
        if(binding.MergedInterfaces.Count==0)return first;
        if(first is not ObjectTypeSymbol original)
            throw new CompilationFailure(Diagnostic.Error(binding.File,binding.Name,"TSCS1001",
                "Only finite structural interfaces can be merged."));

        var members=original.Members.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        var readonlyKeys=new HashSet<string>(original.ReadonlyMembers,StringComparer.Ordinal);
        var numericKeys=new HashSet<string>(original.NumericMembers,StringComparer.Ordinal);
        var optionalKeys=new HashSet<string>(original.OptionalMembers,StringComparer.Ordinal);
        // Keep original member annotation and scope for source-identity
        // checks; Resolve(IntersectionTypeSyntax) flattens this information.
        var origins=new Dictionary<string,(TypeSyntax Syntax,Scope Owner,string File)>(StringComparer.Ordinal);
        foreach(var key in original.Members.Keys)
            if(DeclarationTypeIdentity.DirectProperty(binding.Syntax,key) is { } property)
                origins.Add(key,(property.Type,instance??binding.Owner,binding.File));
        foreach(var (file,name,syntax,declaredOwner) in binding.MergedInterfaces)
        {
            // Identical type parameter lists: every declaration's body is
            // read in the same instantiation scope.
            var owner=instance??declaredOwner;
            if(Resolve(syntax,owner,file,depth+1) is not ObjectTypeSymbol next)
                throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",
                    "Only finite structural interfaces can be merged."));
            foreach(var (key,type) in next.Members)
            {
                bool incomingReadonly=next.ReadonlyMembers.Contains(key);
                bool incomingNumeric=next.NumericMembers.Contains(key);
                bool incomingOptional=next.OptionalMembers.Contains(key);
                if(members.TryGetValue(key,out var previous))
                {
                    if(readonlyKeys.Contains(key)!=incomingReadonly ||
                       numericKeys.Contains(key)!=incomingNumeric ||
                       optionalKeys.Contains(key)!=incomingOptional)
                        throw new CompilationFailure(Diagnostic.Error(file,name,"TS2687",
                            $"All declarations of '{key}' must have identical modifiers."));
                    var incoming=DeclarationTypeIdentity.DirectProperty(syntax,key);
                    bool same=origins.TryGetValue(key,out var prior) && incoming is not null
                        ?DeclarationTypeIdentity.Same(prior.Syntax,prior.Owner,prior.File,
                            incoming.Type,owner,file)
                        :CompilerTypes.SameStructuralInput(previous,type) &&
                            !DeclarationTypeIdentity.HasIntersection(binding.Syntax,instance??binding.Owner) &&
                            !DeclarationTypeIdentity.HasIntersection(syntax,owner);
                    if(!same)
                        throw new CompilationFailure(Diagnostic.Error(file,name,"TS2717",
                            $"Subsequent property declarations must have the same type. Property '{key}' must be of type '{previous.DisplayName}', but here has type '{type.DisplayName}'."));
                    continue;
                }
                if(members.Count>=128)
                    throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",
                        "Merged interface exceeds the finite 128-member limit."));
                members.Add(key,type);
                if(DeclarationTypeIdentity.DirectProperty(syntax,key) is { } declaredProperty)
                    origins.Add(key,(declaredProperty.Type,owner,file));
                if(incomingReadonly)readonlyKeys.Add(key);
                if(incomingNumeric)numericKeys.Add(key);
                if(incomingOptional)optionalKeys.Add(key);
            }
        }
        return new ObjectTypeSymbol(members,readonlyKeys,numericKeys,optionalKeys);
    }

    // An unreferenced generic alias body is still checked: every name must
    // resolve. Its parameters stand for 'unknown' here, so a body that needs
    // known arguments (keyof T, T["k"]) fails closed as unsupported rather
    // than reporting a diagnostic the reference would not report.
    private static void ValidateGeneric(TypeBinding binding,int depth)
    {
        if(binding.Resolving || depth>64)
            throw new CompilationFailure(Diagnostic.Error(binding.File,binding.Name,"TS2456",
                $"Type alias '{binding.Name.Text}' circularly references itself or exceeds the supported depth."));
        binding.Resolving=true;
        binding.Validated=true;
        try
        {
            var placeholder=new Scope(binding.Owner);
            foreach(var parameter in binding.TypeParameters)
                placeholder.Types.Add(parameter.Text,new TypeBinding(binding.File,parameter,
                    new PrimitiveTypeSyntax(parameter with {Text="unknown"}),binding.Owner)
                {
                    Resolved=CompilerTypes.Unknown
                });
            _=Resolve(binding.Syntax,placeholder,binding.File,depth+1);
            // A generic interface's heritage is checked with opaque type
            // parameters in the reference (TS2430 / TS2320): 'x:string' does
            // not satisfy a base 'x:T', yet it does for the 'unknown'
            // placeholder. Members the declaration and its bases share, or two
            // bases share, would need that comparison; fail closed instead.
            if(binding.Syntax is InterfaceExtensionTypeSyntax inherited)
            {
                var keys=new HashSet<string>(inherited.Body.Properties.Select(p=>Parser.PropertyName(p.Name)),
                    StringComparer.Ordinal);
                foreach(var named in inherited.Bases)
                    if(Resolve(named,placeholder,binding.File,depth+1) is ObjectTypeSymbol parent &&
                       parent.Members.Keys.Any(key=>!keys.Add(key)))
                        throw new CompilationFailure(Diagnostic.Error(binding.File,named.Name,"TSCS1001",
                            "A member shared by a generic interface and its bases is not supported yet."));
            }
            // A member redeclared by a reopened generic interface must have the
            // same type for every instantiation (TS2717 / TS2687 otherwise);
            // that needs opaque type parameters, so fail closed instead of
            // comparing only the instantiations that happen to be referenced.
            if(binding.MergedInterfaces.Count!=0)
            {
                var seen=new HashSet<string>(StringComparer.Ordinal);
                foreach(var (file,name,syntax,_) in binding.MergedInterfaces.Prepend(
                            (binding.File,binding.Name,binding.Syntax,binding.Owner)))
                    if(syntax is not ObjectTypeSyntax body ||
                       body.Properties.Any(p=>!seen.Add(Parser.PropertyName(p.Name))))
                        throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",
                            "Redeclaring a member of a merged generic interface is not supported yet."));
            }
            foreach(var merged in binding.MergedInterfaces)
                _=Resolve(merged.Syntax,placeholder,merged.File,depth+1);
        }
        catch(CompilationFailure failure) when (failure.Diagnostic.Code!="TS2304")
        {
            throw new CompilationFailure(Diagnostic.Error(binding.File,binding.Name,"TSCS1001",
                "This generic declaration body is not supported without known type arguments yet."));
        }
        finally {binding.Resolving=false;}
    }

    // The scope in which a generic alias body denotes one instantiation: each
    // type parameter is bound to its argument's syntax in the referencing
    // scope, so declaration identity can follow argument provenance too.
    // Arguments are resolved eagerly, so an unused argument is still checked.
    public static Scope Instantiate(TypeBinding binding,NamedTypeSyntax reference,Scope scope,string file,int depth=0)
    {
        var arguments=reference.TypeArguments??[];
        if(binding.TypeParameters.Count==0)
            throw new CompilationFailure(Diagnostic.Error(file,reference.Name,"TS2315",
                $"Type '{reference.Name.Text}' is not generic."));
        if(arguments.Count!=binding.TypeParameters.Count)
            throw new CompilationFailure(Diagnostic.Error(file,reference.Name,"TS2314",
                $"Generic type '{binding.Name.Text}' requires {binding.TypeParameters.Count} type argument(s)."));
        var instance=new Scope(binding.Owner);
        for(var i=0;i<arguments.Count;i++)
            instance.Types.Add(binding.TypeParameters[i].Text,
                new TypeBinding(file,binding.TypeParameters[i],arguments[i],scope)
                {
                    Resolved=Resolve(arguments[i],scope,file,depth+1)
                });
        return instance;
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
            {
                if(scope.FindType(named.Name.Text) is not { } binding)
                    throw new CompilationFailure(Diagnostic.Error(file,named.Name,"TS2304",
                        $"Cannot find name '{named.Name.Text}'."));
                if(binding.TypeParameters.Count==0 && named.TypeArguments is null)
                    return ResolveBinding(binding,depth+1);
                // Heritage diagnostics belong to the declaration, so check it
                // before any instantiation can report them at a use site.
                if(!binding.Validated && !binding.Resolving && binding.TypeParameters.Count!=0 &&
                   (binding.Syntax is InterfaceExtensionTypeSyntax ||
                    binding.MergedInterfaces.Any(merged=>merged.Syntax is InterfaceExtensionTypeSyntax)))
                    ValidateGeneric(binding,depth+1);
                var instance=Instantiate(binding,named,scope,file,depth+1);
                return binding.MergedInterfaces.Count==0
                    ? Resolve(binding.Syntax,instance,binding.File,depth+1)
                    : Merge(binding,instance,depth+1);
            }
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
                    Variable declaration when Binder.ResolveFiniteInitializerType(
                        declaration.Initializer,declaringScope,symbol.File,
                        declaration.Constant,depth+1) is { } inferred => inferred,
                    FunctionDeclaration function =>
                        Binder.ResolveFunctionSignature(function,declaringScope,symbol.File,depth+1),
                    _=>throw new CompilationFailure(Diagnostic.Error(file,query.Root,"TSCS1001",
                        "typeof requires a finite inferred initializer, annotation or typed function."))
                };
                foreach(var member in query.Members)
                {
                    TypeSymbol? resolved;
                    try {resolved=CompilerTypes.TryGetProperty(target,Parser.PropertyName(member));}
                    catch(InvalidOperationException failure)
                    {
                        throw new CompilationFailure(Diagnostic.Error(file,member,"TSCS1001",failure.Message));
                    }
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

                // A primitive or literal string has a UTF-16 code-unit index
                // signature and a numeric length in the pinned String ambient.
                // The type of a code unit is string, not the literal's character:
                // even a literal's numeric index is not a tuple-style selection.
                bool IsKnownString(TypeSymbol part)=>
                    part.Equals(CompilerTypes.String) ||
                    part is LiteralTypeSymbol { Kind:PrimitiveKind.String };
                if(IsKnownString(receiver) ||
                   receiver is UnionTypeSymbol stringUnion &&
                   stringUnion.Alternatives.All(IsKnownString))
                {
                    TypeSymbol StringIndex(TypeSymbol key)
                    {
                        if(key is UnionTypeSymbol keys)
                            return CompilerTypes.Union(keys.Alternatives.Select(StringIndex));
                        if(key.Equals(CompilerTypes.Never))return CompilerTypes.Never;
                        if(key.Equals(CompilerTypes.Number))return CompilerTypes.String;
                        if(key is LiteralTypeSymbol { Kind:PrimitiveKind.Number } numeric)
                        {
                            if(!long.TryParse(numeric.Value,System.Globalization.NumberStyles.None,
                                   System.Globalization.CultureInfo.InvariantCulture,out var value) ||
                               value>9007199254740991L)
                                throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                                    "String indexed-access numeric keys must be non-negative safe integers."));
                            return CompilerTypes.String;
                        }
                        if(key is LiteralTypeSymbol { Kind:PrimitiveKind.String,Value:"length" })
                            return CompilerTypes.Number;
                        throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                            "String indexed access requires a numeric key or the exact 'length' property."));
                    }
                    try {return StringIndex(selector);}
                    catch(InvalidOperationException failure)
                    {
                        throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",failure.Message));
                    }
                }
                // Array indexed-access types expose their element for numeric keys
                // and their length for the exact string key. A union of arrays
                // is safe when every alternative is an array; never silently
                // accept unknown keys or a mixed object/array union.
                if(receiver is ArrayTypeSymbol ||
                   receiver is UnionTypeSymbol arrayUnion &&
                   arrayUnion.Alternatives.All(part=>part is ArrayTypeSymbol))
                {
                    TypeSymbol ArrayIndex(TypeSymbol target,TypeSymbol key)
                    {
                        if(target is UnionTypeSymbol alternatives)
                            return CompilerTypes.Union(alternatives.Alternatives.Select(part=>ArrayIndex(part,key)));
                        if(key is UnionTypeSymbol keys)
                            return CompilerTypes.Union(keys.Alternatives.Select(part=>ArrayIndex(target,part)));
                        if(key.Equals(CompilerTypes.Never))return CompilerTypes.Never;
                        var arrayType=(ArrayTypeSymbol)target;
                        if(key.Equals(CompilerTypes.Number))return arrayType.ElementType;
                        if(key is LiteralTypeSymbol { Kind:PrimitiveKind.Number } numeric)
                        {
                            if(!long.TryParse(numeric.Value,System.Globalization.NumberStyles.None,
                                   System.Globalization.CultureInfo.InvariantCulture,out var value) ||
                               value>9007199254740991L)
                                throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                                    "Array indexed-access numeric keys must be non-negative safe integers."));
                            if(arrayType.TupleElements is { } slots)
                            {
                                if(value>=slots.Count)
                                    throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TS2493",
                                        $"Tuple type '{arrayType.DisplayName}' of length '{slots.Count}' has no element at index '{value}'."));
                                return CompilerTypes.TupleSlot(arrayType,(int)value);
                            }
                            return arrayType.ElementType;
                        }
                        if(key is LiteralTypeSymbol { Kind:PrimitiveKind.String,Value:"length" })
                            return arrayType.TupleElements is not null
                                ?CompilerTypes.TupleLength(arrayType):CompilerTypes.Number;
                        throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                            "Array indexed access requires a numeric key or the exact 'length' property."));
                    }
                    try {return ArrayIndex(receiver,selector);}
                    catch(InvalidOperationException failure)
                    {
                        throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",failure.Message));
                    }
                }
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
            case TupleTypeSyntax tuple:
            {
                var slots=new List<TypeSymbol>();
                int optionalStart=-1;
                for(int i=0;i<tuple.Elements.Count;i++)
                {
                    if(tuple.Elements[i] is TupleRestTypeSyntax spread)
                    {
                        // Resolve a named tail in its declaring type scope.
                        // Preserve exact positions, optional suffix and length.
                        var resolved=Resolve(spread.Target,scope,file,depth+1);
                        if(resolved is not ArrayTypeSymbol { TupleElements: { } tail })
                            throw new CompilationFailure(Diagnostic.Error(file,spread.Ellipsis,"TSCS1001",
                                "A trailing rest must expand a finite tuple type, not an unbounded array."));
                        if(optionalStart>=0 && tail.Count>0)
                            throw new CompilationFailure(Diagnostic.Error(file,spread.Ellipsis,"TSCS1001",
                                "A finite rest cannot follow an optional tuple element."));
                        if(((ArrayTypeSymbol)resolved).TupleOptionalStart>=0)
                            optionalStart=slots.Count+((ArrayTypeSymbol)resolved).TupleOptionalStart;
                        slots.AddRange(tail);
                    }
                    else
                    {
                        if(i==tuple.OptionalStart)optionalStart=slots.Count;
                        slots.Add(Resolve(tuple.Elements[i],scope,file,depth+1));
                    }
                    if(slots.Count>32)
                        throw new CompilationFailure(Diagnostic.Error(file,tuple.Open,"TSCS1001",
                            "Expanded finite tuple types with over 32 elements are unsupported."));
                }
                try {return CompilerTypes.FiniteTuple(slots,tuple.Readonly,optionalStart);}
                catch(InvalidOperationException failure)
                {
                    throw new CompilationFailure(Diagnostic.Error(file,tuple.Open,"TSCS1001",failure.Message));
                }
            }
            case ArrayTypeSyntax array:
                if(array.GenericName is { } genericName && scope.FindType(genericName.Text) is not null)
                    throw new CompilationFailure(Diagnostic.Error(file,genericName,"TS2315",
                        $"Type '{genericName.Text}' is not generic in the supported subset."));
                return new ArrayTypeSymbol(Resolve(array.Element,scope,file,depth+1),array.Readonly);
            case InterfaceExtensionTypeSyntax inherited:
            {
                // Never erase missing or incompatible inherited members. Each
                // imported base resolves in this declaration's original scope.
                var members=new Dictionary<string,TypeSymbol>(StringComparer.Ordinal);
                var readonlyKeys=new HashSet<string>(StringComparer.Ordinal);
                var numericKeys=new HashSet<string>(StringComparer.Ordinal);
                var optionalKeys=new HashSet<string>(StringComparer.Ordinal);
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
                    if(property.Optional)optionalKeys.Add(name);
                    if(members.Count>128)
                        throw new CompilationFailure(Diagnostic.Error(file,property.Name,"TSCS1001",
                            "Extended interface exceeds the finite 128-member limit."));
                }
                // Two bases sharing a member must agree on its declared type
                // (TS2320); flattened intersections cannot show that.
                bool provenance=inherited.Bases.Count>1 &&
                    inherited.Bases.Any(b=>DeclarationTypeIdentity.HasIntersection(b,scope));
                foreach(var named in inherited.Bases)
                {
                    if(Resolve(named,scope,file,depth+1) is not ObjectTypeSymbol parent)
                        throw new CompilationFailure(Diagnostic.Error(file,named.Name,"TSCS1001",
                            "Interface heritage requires a finite structural object base."));
                    foreach(var member in parent.Members)
                    {
                        bool numeric=parent.NumericMembers.Contains(member.Key);
                        bool isReadonly=parent.ReadonlyMembers.Contains(member.Key);
                        bool isOptional=parent.OptionalMembers.Contains(member.Key);
                        if(members.TryGetValue(member.Key,out var previous))
                        {
                            if(declared.Contains(member.Key))
                            {
                                if(numericKeys.Contains(member.Key)!=numeric ||
                                   (!isOptional && optionalKeys.Contains(member.Key)) ||
                                   !CompilerTypes.IsAssignable(previous,member.Value))
                                    throw new CompilationFailure(Diagnostic.Error(file,inherited.Name,"TS2430",
                                        $"Interface '{inherited.Name.Text}' incorrectly extends its base interface at '{member.Key}'."));
                            }
                            else if(provenance)
                                throw new CompilationFailure(Diagnostic.Error(file,named.Name,"TSCS1001",
                                    $"Inherited member '{member.Key}' with intersection provenance is not supported yet."));
                            else if(numericKeys.Contains(member.Key)!=numeric ||
                                    readonlyKeys.Contains(member.Key)!=isReadonly ||
                                    optionalKeys.Contains(member.Key)!=isOptional ||
                                    !CompilerTypes.IsEquivalent(previous,member.Value))
                                throw new CompilationFailure(Diagnostic.Error(file,named.Name,"TSCS1001",
                                    $"Inherited member '{member.Key}' has incompatible base declarations."));
                        }
                        else
                        {
                            members.Add(member.Key,member.Value);
                            if(numeric)numericKeys.Add(member.Key);
                            if(isReadonly)readonlyKeys.Add(member.Key);
                            if(isOptional)optionalKeys.Add(member.Key);
                        }
                        if(members.Count>128)
                            throw new CompilationFailure(Diagnostic.Error(file,named.Name,"TSCS1001",
                                "Interface heritage exceeds the finite 128-member limit."));
                    }
                }
                return new ObjectTypeSymbol(members,readonlyKeys,numericKeys,optionalKeys);
            }
            case ObjectTypeSyntax shape:return new ObjectTypeSymbol(shape.Properties.Select(p=>
                new KeyValuePair<string,TypeSymbol>(Parser.PropertyName(p.Name),Resolve(p.Type,scope,file,depth+1))),
                shape.Properties.Where(p=>p.Readonly).Select(p=>Parser.PropertyName(p.Name)),
                shape.Properties.Where(p=>p.Name.Kind==TokenKind.Number).Select(p=>Parser.PropertyName(p.Name)),
                shape.Properties.Where(p=>p.Optional).Select(p=>Parser.PropertyName(p.Name)));
            case IntersectionTypeSyntax intersection:
            {
                // Finite, required structural shapes can be safely flattened and
                // checked by the existing binder/checker without a new type escape hatch.
                // Unions/primitive operands, conflicting keys, methods and index
                // signatures stay explicitly unsupported rather than guessed.
                var properties=new Dictionary<string,TypeSymbol>(StringComparer.Ordinal);
                var numeric=new HashSet<string>(StringComparer.Ordinal);
                var computedNumeric=new HashSet<string>(StringComparer.Ordinal);
                var readonlyKeys=new HashSet<string>(StringComparer.Ordinal);
                var optionalKeys=new HashSet<string>(StringComparer.Ordinal);
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
                        bool isOptional=shape.OptionalMembers.Contains(name);
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
                            if(!isOptional)optionalKeys.Remove(name);
                            if(!shape.ComputedNumericMembers.Contains(name))
                                computedNumeric.Remove(name);
                        }
                        else
                        {
                            properties.Add(name,member.Value);
                            if(isNumeric)numeric.Add(name);
                            if(shape.ComputedNumericMembers.Contains(name))computedNumeric.Add(name);
                            if(isReadonly)readonlyKeys.Add(name);
                            if(isOptional)optionalKeys.Add(name);
                            if(properties.Count>128)
                                throw new CompilationFailure(Diagnostic.Error(file,Location(part),"TSCS1001",
                                    "Intersected object exceeds the supported property bound."));
                        }
                    }
                }
                return new ObjectTypeSymbol(properties,readonlyKeys,numeric,optionalKeys,computedNumeric);
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

    internal static Token Location(TypeSyntax syntax)=>syntax switch
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
        TupleTypeSyntax value=>value.Open,
        TupleRestTypeSyntax value=>value.Ellipsis,
        IntersectionTypeSyntax value=>Location(value.Members[0]),
        UnionTypeSyntax value=>Location(value.Members[0]),
        _=>default
    };
}

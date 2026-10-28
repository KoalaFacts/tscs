namespace Tscs;

// Semantic types are independent of the source spelling in the syntax tree.
// Future function, object, union and type-parameter nodes extend this identity
// model; binder, module linking and checker consume the same type objects.
internal abstract record TypeSymbol
{
    public abstract string DisplayName { get; }
}

internal enum PrimitiveKind { Number, String, Boolean, Any, Never }

internal sealed record PrimitiveTypeSymbol(PrimitiveKind Kind) : TypeSymbol
{
    public override string DisplayName => Kind switch
    {
        PrimitiveKind.Number => "number",
        PrimitiveKind.String => "string",
        PrimitiveKind.Boolean => "boolean",
        PrimitiveKind.Any => "any",
        PrimitiveKind.Never => "never",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };
}

internal sealed record ArrayTypeSymbol(TypeSymbol ElementType) : TypeSymbol
{
    public override string DisplayName=>TypeDisplay.Format(this);
}

internal sealed record UnionTypeSymbol(IReadOnlyList<TypeSymbol> Alternatives) : TypeSymbol
{
    public override string DisplayName=>TypeDisplay.Format(this);
}

internal sealed record FunctionTypeSymbol(IReadOnlyList<TypeSymbol> Parameters,TypeSymbol ReturnType) : TypeSymbol
{
    public override string DisplayName=>TypeDisplay.Format(this);
}

// These relations cover primitives, any and required structural object data.
// Unsupported TypeScript type forms remain parser/compiler errors; an unknown
// type is represented by null, never silently treated as 'any'.
internal static class CompilerTypes
{
    public static readonly TypeSymbol Number=new PrimitiveTypeSymbol(PrimitiveKind.Number);
    public static readonly TypeSymbol String=new PrimitiveTypeSymbol(PrimitiveKind.String);
    public static readonly TypeSymbol Boolean=new PrimitiveTypeSymbol(PrimitiveKind.Boolean);
    public static readonly TypeSymbol Any=new PrimitiveTypeSymbol(PrimitiveKind.Any);
    // Internal inference for an empty literal. 'never' annotations and
    // evolving array inference remain outside this supported subset.
    public static readonly TypeSymbol Never=new PrimitiveTypeSymbol(PrimitiveKind.Never);

    public static TypeSymbol Union(IEnumerable<TypeSymbol> variants)
    {
        var options=new List<TypeSymbol>();
        foreach(var variant in variants)
        {
            foreach(var type in variant is UnionTypeSymbol nested ? nested.Alternatives : [variant])
            {
                if(type.Equals(Any))return Any;
                if(type.Equals(Never))continue;
                if(options.All(existing=>!IsEquivalent(existing,type)))options.Add(type);
                if(options.Count>64)throw new InvalidOperationException("Too many union variants for the bounded compiler.");
            }
        }
        return options.Count switch {0=>Never,1=>options[0],_=>new UnionTypeSymbol(options)};
    }

    public static TypeSymbol FromAnnotation(string name) => name switch
    {
        "number" => Number,
        "string" => String,
        "boolean" => Boolean,
        "any" => Any,
        _ => throw new ArgumentOutOfRangeException(nameof(name),
            "Only parser-validated primitive annotations may reach the semantic type model.")
    };

    public static TypeSymbol FromAnnotation(TypeSyntax syntax)=>syntax switch
    {
        PrimitiveTypeSyntax primitive=>FromAnnotation(primitive.Name.Text),
        ArrayTypeSyntax array=>new ArrayTypeSymbol(FromAnnotation(array.Element)),
        ObjectTypeSyntax shape=>new ObjectTypeSymbol(shape.Properties.Select(p=>
            new KeyValuePair<string,TypeSymbol>(Parser.PropertyName(p.Name),FromAnnotation(p.Type)))),
        _=>throw new ArgumentOutOfRangeException(nameof(syntax))
    };

    // Prebinding may infer closed literal shapes without consulting another
    // symbol. Other cross-file initializers still need an explicit annotation.
    public static TypeSymbol? FromInitializer(ValueNode? value)
    {
        while(value is ValueParenthesized group)value=group.Expression;
        if(value is ValueAtom atom)return FromLiteral(atom.Token);
        if(value is ValueArray array)
        {
            TypeSymbol? element=Never;
            foreach(var item in array.Elements)
            {
                if(FromInitializer(item) is not { } actual)return null;
                if(element==Never)element=actual;
                else if(!IsEquivalent(element,actual))
                {
                    try {element=Union([element,actual]);}
                    catch(InvalidOperationException) {return null;}
                }
            }
            return new ArrayTypeSymbol(element!);
        }
        if(value is not ValueObject literal)return null;
        var members=new List<KeyValuePair<string,TypeSymbol>>();
        foreach(var property in literal.Properties)
        {
            if(FromInitializer(property.Value) is not { } type)return null;
            members.Add(new(Parser.PropertyName(property.Name),type));
        }
        return new ObjectTypeSymbol(members);
    }

    public static TypeSymbol? FromLiteral(Token token) => token.Kind switch
    {
        TokenKind.Number => Number,
        TokenKind.String => String,
        TokenKind.True or TokenKind.False => Boolean,
        _ => null
    };

    public static bool IsEquivalent(TypeSymbol a,TypeSymbol b)=>
        IsAssignable(a,b) && IsAssignable(b,a);

    // A read through a union is safe only when every alternative has the
    // requested property. Its result type is the union of member types.
    public static TypeSymbol? TryGetProperty(TypeSymbol receiver,string name)
    {
        if(receiver.Equals(Any))return Any;
        if(receiver is ArrayTypeSymbol && name=="length")return Number;
        if(receiver is ObjectTypeSymbol shape)return shape.Members.GetValueOrDefault(name);
        if(receiver is UnionTypeSymbol union)
        {
            var found=new List<TypeSymbol>(union.Alternatives.Count);
            foreach(var variant in union.Alternatives)
            {
                var member=TryGetProperty(variant,name);
                if(member==null)return null;
                found.Add(member);
            }
            return Union(found);
        }
        return null;
    }

    public static bool IsNumber(TypeSymbol? type) => type is PrimitiveTypeSymbol { Kind: PrimitiveKind.Number };
    public static bool IsBoolean(TypeSymbol? type) => type is PrimitiveTypeSymbol { Kind: PrimitiveKind.Boolean };

    // This is only the bounded, same-primitive/any equality subset.
    // Do not infer an overlap between unrelated or structural type forms.
    public static bool CanCompareEquality(TypeSymbol left,TypeSymbol right) =>
        left is PrimitiveTypeSymbol && right is PrimitiveTypeSymbol &&
        (left.Equals(right) || left.Equals(Any) || right.Equals(Any));

    public static bool IsAssignable(TypeSymbol source,TypeSymbol target)
    {
        // Union sources are universally quantified; union targets require an
        // assignable alternative. Member/array relations use the same engine.
        // Memoize by reference identity and bound work on malformed broad graphs.
        int budget=16384;
        var active=new HashSet<(TypeSymbol Source,TypeSymbol Target)>(TypePairComparer.Instance);
        bool Check(TypeSymbol actual,TypeSymbol required)
        {
            if(--budget<0)return false; // fail closed on combinatorial input
            if(ReferenceEquals(actual,required) || actual.Equals(Never) ||
               actual.Equals(Any) || required.Equals(Any))return true;
            var pair=(actual,required);
            if(!active.Add(pair))return false; // circular structural cases are outside this subset
            try
            {
                if(actual is UnionTypeSymbol sourceUnion)
                    return sourceUnion.Alternatives.All(variant=>Check(variant,required));
                if(required is UnionTypeSymbol targetUnion)
                    return targetUnion.Alternatives.Any(variant=>Check(actual,variant));
                if(required is ArrayTypeSymbol requiredArray)
                    return actual is ArrayTypeSymbol actualArray &&
                        Check(actualArray.ElementType,requiredArray.ElementType);
                if(required is ObjectTypeSymbol shape)
                {
                    if(shape.Members.Count==0)return true;
                    if(actual is not ObjectTypeSymbol data)return false;
                    return shape.Members.All(property=>data.Members.TryGetValue(property.Key,out var member) &&
                        Check(member,property.Value));
                }
                return actual.Equals(required);
            }
            finally {active.Remove(pair);}
        }
        return Check(source,target);
    }
    private sealed class TypePairComparer : IEqualityComparer<(TypeSymbol Source,TypeSymbol Target)>
    {
        public static readonly TypePairComparer Instance=new();
        public bool Equals((TypeSymbol Source,TypeSymbol Target) x,(TypeSymbol Source,TypeSymbol Target) y)=>
            ReferenceEquals(x.Source,y.Source) && ReferenceEquals(x.Target,y.Target);
        public int GetHashCode((TypeSymbol Source,TypeSymbol Target) pair)=>HashCode.Combine(
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Source),
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Target));
    }
}

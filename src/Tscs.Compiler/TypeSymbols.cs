namespace Tscs;

// Semantic types are independent of the source spelling in the syntax tree.
// Future function, object, union and type-parameter nodes extend this identity
// model; binder, module linking and checker consume the same type objects.
internal abstract record TypeSymbol
{
    public abstract string DisplayName { get; }
}

internal enum PrimitiveKind { Number, String, Boolean, Any }

internal sealed record PrimitiveTypeSymbol(PrimitiveKind Kind) : TypeSymbol
{
    public override string DisplayName => Kind switch
    {
        PrimitiveKind.Number => "number",
        PrimitiveKind.String => "string",
        PrimitiveKind.Boolean => "boolean",
        PrimitiveKind.Any => "any",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };
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

    public static bool IsNumber(TypeSymbol? type) => type is PrimitiveTypeSymbol { Kind: PrimitiveKind.Number };
    public static bool IsBoolean(TypeSymbol? type) => type is PrimitiveTypeSymbol { Kind: PrimitiveKind.Boolean };

    // This is only the bounded, same-primitive/any equality subset.
    // Do not infer an overlap between unrelated or structural type forms.
    public static bool CanCompareEquality(TypeSymbol left,TypeSymbol right) =>
        left is PrimitiveTypeSymbol && right is PrimitiveTypeSymbol &&
        (left.Equals(right) || left.Equals(Any) || right.Equals(Any));

    public static bool IsAssignable(TypeSymbol source,TypeSymbol target)
    {
        // Width subtyping walks target requirements. Fresh-literal excess
        // checks are performed separately against the originating expression.
        var pending=new Stack<(TypeSymbol Source,TypeSymbol Target)>();
        var seen=new HashSet<(TypeSymbol Source,TypeSymbol Target)>(TypePairComparer.Instance);
        pending.Push((source,target));
        while(pending.Count>0)
        {
            var (actual,required)=pending.Pop();
            if(!seen.Add((actual,required)))continue;
            if(ReferenceEquals(actual,required) || actual.Equals(Any) || required.Equals(Any))continue;
            if(required is ObjectTypeSymbol shape)
            {
                // Within this non-nullish subset, {} accepts every value type.
                if(shape.Members.Count==0)continue;
                if(actual is not ObjectTypeSymbol data)return false;
                foreach(var (name,type) in shape.Members)
                {
                    if(!data.Members.TryGetValue(name,out var member))return false;
                    pending.Push((member,type));
                }
            }
            else if(!actual.Equals(required))return false;
        }
        return true;
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

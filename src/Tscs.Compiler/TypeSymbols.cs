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
    public override string DisplayName =>
        $"({string.Join(", ",Parameters.Select(p=>p.DisplayName))}) => {ReturnType.DisplayName}";
}

// Required named properties share the same semantic model as primitives.
// Optional/readonly/index signatures and unions remain unsupported rather
// than being silently widened to any.
internal sealed record ObjectPropertySymbol(string Name,TypeSymbol Type);
internal sealed record ObjectTypeSymbol : TypeSymbol
{
    public ObjectTypeSymbol(IEnumerable<ObjectPropertySymbol> properties)
    {
        Properties=properties.ToArray();
        Members=Properties.ToDictionary(p=>p.Name,p=>p.Type,StringComparer.Ordinal);
    }
    public IReadOnlyList<ObjectPropertySymbol> Properties { get; }
    public IReadOnlyDictionary<string,TypeSymbol> Members { get; }
    public override string DisplayName => "{ " +
        string.Join("; ",Properties.Select(p=>$"{p.Name}: {p.Type.DisplayName}")) + " }";
    public TypeSymbol? Member(string name)=>Members.GetValueOrDefault(name);
}

// An unknown type is null, never implicitly any.
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

    public static TypeSymbol FromAnnotation(TypeSyntax annotation) => annotation switch
    {
        PrimitiveTypeSyntax primitive => FromAnnotation(primitive.Name.Text),
        ObjectTypeSyntax shape => new ObjectTypeSymbol(shape.Properties.Select(
            property=>new ObjectPropertySymbol(property.Name.Text,FromAnnotation(property.Type)))),
        _ => throw new ArgumentOutOfRangeException(nameof(annotation))
    };

    // Prebind only literal members whose semantic type is known without
    // evaluating another declaration. This makes structural exports stable
    // across the existing module-linking pass.
    public static TypeSymbol? InferLiteral(ValueNode value,int depth=0)
    {
        if(depth>16)return null;
        switch(value)
        {
            case ValueAtom atom: return FromLiteral(atom.Token);
            case ValueParenthesized grouped: return InferLiteral(grouped.Expression,depth+1);
            case ValueObject literal:
                var members=new List<ObjectPropertySymbol>();
                foreach(var property in literal.Properties)
                {
                    var type=InferLiteral(property.Value,depth+1);
                    if(type==null)return null;
                    members.Add(new(property.Name.Text,type));
                }
                return new ObjectTypeSymbol(members);
            default: return null;
        }
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

    public static bool IsAssignable(TypeSymbol source,TypeSymbol target) =>
        IsAssignable(source,target,0);

    private static bool IsAssignable(TypeSymbol source,TypeSymbol target,int depth)
    {
        if(source.Equals(Any)||target.Equals(Any))return true;
        if(source is ObjectTypeSymbol objectSource && target is ObjectTypeSymbol objectTarget)
        {
            if(depth>16)return false;
            foreach(var requirement in objectTarget.Properties)
            {
                var present=objectSource.Member(requirement.Name);
                if(present==null || !IsAssignable(present,requirement.Type,depth+1))
                    return false;
            }
            return true;
        }
        if(source is ObjectTypeSymbol || target is ObjectTypeSymbol)return false;
        return source.Equals(target);
    }
}

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

// These relations cover only the implemented primitive + any subset.
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

    public static TypeSymbol? FromLiteral(Token token) => token.Kind switch
    {
        TokenKind.Number => Number,
        TokenKind.String => String,
        TokenKind.True or TokenKind.False => Boolean,
        _ => null
    };

    public static bool IsNumber(TypeSymbol? type) => type is PrimitiveTypeSymbol { Kind: PrimitiveKind.Number };

    public static bool IsAssignable(TypeSymbol source,TypeSymbol target) =>
        source.Equals(target) || source.Equals(Any) || target.Equals(Any);
}

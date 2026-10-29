using System.Collections.ObjectModel;

namespace Tscs;

// Required data properties. Runtime lookup uses canonical names, while numeric
// source-key identity survives for keyof (0 and "0" have distinct key types).
// Freshness remains a syntax/checking concern.
internal sealed record ObjectTypeSymbol : TypeSymbol
{
    public IReadOnlyDictionary<string,TypeSymbol> Members { get; }
    public IReadOnlySet<string> ReadonlyMembers { get; }
    public IReadOnlySet<string> NumericMembers { get; }

    public ObjectTypeSymbol(IEnumerable<KeyValuePair<string,TypeSymbol>> members,
        IEnumerable<string>? readonlyMembers=null,IEnumerable<string>? numericMembers=null)
    {
        Members=new ReadOnlyDictionary<string,TypeSymbol>(
            new Dictionary<string,TypeSymbol>(members,StringComparer.Ordinal));
        ReadonlyMembers=new HashSet<string>(readonlyMembers??Array.Empty<string>(),StringComparer.Ordinal);
        NumericMembers=new HashSet<string>(numericMembers??Array.Empty<string>(),StringComparer.Ordinal);
        if(NumericMembers.Any(name=>!Members.ContainsKey(name)))
            throw new ArgumentException("Numeric member identities must belong to the object.",nameof(numericMembers));
    }

    public override string DisplayName=>TypeDisplay.Format(this);
}

// Shared object graphs can be exponentially larger when printed as trees.
// Bound diagnostic display without changing the actual member/type graph.
internal static class TypeDisplay
{
    private const int MaximumLength=4096;
    public static string Format(TypeSymbol type)
    {
        var output=new System.Text.StringBuilder();
        var pending=new Stack<(TypeSymbol? Type,string? Text)>();
        void Text(string text)=>pending.Push((null,text));
        void Type(TypeSymbol item)=>pending.Push((item,null));
        Type(type);
        while(pending.Count>0 && output.Length<MaximumLength)
        {
            var item=pending.Pop();
            if(item.Text is { } text)
            {
                int count=Math.Min(text.Length,MaximumLength-output.Length);
                output.Append(text.AsSpan(0,count));
                if(count<text.Length)Text(text[count..]);
                continue;
            }
            switch(item.Type)
            {
                case ObjectTypeSymbol shape:
                    if(shape.Members.Count==0) { Text("{}"); break; }
                    Text(" }");
                    foreach(var member in shape.Members.Reverse())
                    {
                        Text(";");
                        Type(member.Value);
                        Text((shape.ReadonlyMembers.Contains(member.Key)?" readonly ":" ")+member.Key+": ");
                    }
                    Text("{");
                    break;
                case ArrayTypeSymbol array:
                    if(array.ElementType is UnionTypeSymbol)
                    {
                        Text(")[]");
                        Type(array.ElementType);
                        Text("(");
                    }
                    else
                    {
                        Text("[]");
                        Type(array.ElementType);
                    }
                    break;
                case UnionTypeSymbol union:
                    for(int i=union.Alternatives.Count-1;i>=0;i--)
                    {
                        Type(union.Alternatives[i]);
                        if(i>0)Text(" | ");
                    }
                    break;
                case FunctionTypeSymbol function:
                    Type(function.ReturnType);
                    Text(") => ");
                    for(int i=function.Parameters.Count-1;i>=0;i--)
                    {
                        if(i>=function.MinimumArguments)Text("?");
                        Type(function.Parameters[i]);
                        if(i>0)Text(", ");
                    }
                    Text("(");
                    break;
                case LiteralTypeSymbol literal:
                    Text(literal.DisplayName);
                    break;
                case PrimitiveTypeSymbol primitive:
                    Text(primitive.DisplayName);
                    break;
                default: throw new InvalidOperationException("Unknown semantic type in diagnostic display.");
            }
        }
        if(pending.Count>0)output.Append('…');
        return output.ToString();
    }
}

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
            case NamedTypeSyntax named:
                if(scope.FindType(named.Name.Text) is not { } binding)
                    throw new CompilationFailure(Diagnostic.Error(file,named.Name,"TS2304",
                        $"Cannot find name '{named.Name.Text}'."));
                return ResolveBinding(binding,depth+1);
            case ArrayTypeSyntax array:return new ArrayTypeSymbol(Resolve(array.Element,scope,file,depth+1));
            case ObjectTypeSyntax shape:return new ObjectTypeSymbol(shape.Properties.Select(p=>
                new KeyValuePair<string,TypeSymbol>(Parser.PropertyName(p.Name),Resolve(p.Type,scope,file,depth+1))),
                shape.Properties.Where(p=>p.Readonly).Select(p=>Parser.PropertyName(p.Name)));
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
        NamedTypeSyntax value=>value.Name,
        ObjectTypeSyntax value=>value.Open,
        ArrayTypeSyntax value=>value.Open,
        UnionTypeSyntax value=>Location(value.Members[0]),
        _=>default
    };
}

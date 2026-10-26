namespace Tscs;

// Supported paths follow parseVariableDeclarationWorker and block/statement parsing upstream.
internal sealed class Parser(string file, List<Token> tokens)
{
    private int position;
    private Token Current => tokens[position];
    public List<Diagnostic> Diagnostics { get; } = [];
    private Token Take() => tokens[position++];
    private bool Eat(TokenKind kind) { if(Current.Kind!=kind)return false; position++;return true; }
    public Block Parse(int depth=0)
    {
        if(depth>128) Unsupported(Current,"Block nesting exceeds the bootstrap limit.");
        var statements=new List<Statement>();
        while(Current.Kind!=TokenKind.End && !(depth>0 && Current.Kind==TokenKind.Close))
        {
            if(Current.Kind==TokenKind.Close) { Diagnostics.Add(Diagnostic.Error(file,Take(),"TS1128","Declaration or statement expected.")); continue; }
            if(Eat(TokenKind.Semi))continue;
            if(Eat(TokenKind.Open))
            {
                statements.Add(Parse(depth+1));
                if(!Eat(TokenKind.Close)) Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
                continue;
            }
            if(Current.Kind is TokenKind.Let or TokenKind.Const)
            {
                bool constant=Take().Kind==TokenKind.Const;
                while(true)
                {
                    var name=Current;
                    if(name.Kind!=TokenKind.Identifier) Unsupported(name,"Only named variable declarations are supported.");
                    Take(); string? annotation=null;
                    if(Eat(TokenKind.Colon))
                    {
                        if(Current.Kind!=TokenKind.Identifier || Current.Text is not ("number" or "string" or "boolean" or "any")) Unsupported(Current,"Only primitive type annotations are supported.");
                        annotation=Take().Text;
                    }
                    ValueNode? initializer=Eat(TokenKind.Equal)?ParseValue():null;
                    if(annotation==null && initializer==null) Unsupported(name,"Variables without a type or initializer are not supported yet.");
                    statements.Add(new Variable(name,constant,annotation,initializer));
                    if(Eat(TokenKind.Comma))continue;
                    // Preserve upstream missing-comma recovery in variable declaration lists.
                    if(!Current.LineBreak && Current.Kind is TokenKind.Identifier or TokenKind.Let)
                    {
                        Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","',' expected."));
                        if(Current.Kind==TokenKind.Let) { Take(); Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","',' expected.")); }
                        continue;
                    }
                    EndStatement();break;
                }
                continue;
            }
            var value=ParseValue();
            if(Current.Kind==TokenKind.PlusPlus)
            {
                var update=Take();
                // Line terminators are forbidden between the operand and ++.
                // Expressions such as (x)++, a.b++, x++ + 1 and x++ as an
                // initializer remain unsupported until their grammar is ported.
                if(!update.LineBreak && value is ValueAtom atom && atom.Token.Kind==TokenKind.Identifier)
                    statements.Add(new PostfixUpdate(atom.Token,update));
                else Unsupported(update,"Only same-line identifier postfix '++' is supported yet.");
                EndStatement();
                continue;
            }
            if(Eat(TokenKind.Equal))
            {
                if(value is ValueAtom atom && atom.Token.Kind==TokenKind.Identifier)
                    statements.Add(new Assignment(atom.Token,ParseValue()));
                else Unsupported(Current,"Only identifier assignment targets are supported.");
            }
            else statements.Add(new Expression(value));
            EndStatement();
        }
        return new Block(statements);
    }
    // Bounded numeric arithmetic: unary '+'/'-' bind more tightly than '*',
    // which binds more tightly than additive '+'/'-'. Other unary forms,
    // exponentiation or nonnumeric arithmetic is claimed as supported.
    private ValueNode ParseValue()
    {
        ValueNode left=ParseProduct();
        while(Current.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            var op=Take();
            left=new ValueBinary(left,op,ParseProduct());
        }
        return left;
    }
    private ValueNode ParseProduct()
    {
        ValueNode left=ParseUnary();
        while(Current.Kind==TokenKind.Star)
        {
            var op=Take();
            left=new ValueBinary(left,op,ParseUnary());
        }
        return left;
    }
    private ValueNode ParseUnary()
    {
        // Avoid recursive parsing on input such as 20,000 whitespace-separated
        // prefix '+'/'-' operators; the TypeChecker also evaluates iteratively.
        var prefixes=new List<Token>();
        while(Current.Kind is TokenKind.Minus or TokenKind.Plus)prefixes.Add(Take());
        ValueNode value=ParsePrimary();
        for(int i=prefixes.Count-1;i>=0;i--)value=new ValueUnary(prefixes[i],value);
        return value;
    }
    private ValueNode ParsePrimary()
    {
        var token=Current;
        if(token.Kind is TokenKind.Identifier or TokenKind.Number or TokenKind.String or TokenKind.True or TokenKind.False)
            return new ValueAtom(Take());
        if(token.Kind is TokenKind.Semi or TokenKind.End or TokenKind.Close)
        {
            Diagnostics.Add(Diagnostic.Error(file,token,"TS1109","Expression expected."));
            return new ValueAtom(new(TokenKind.Number,"0",token.Start,0));
        }
        Unsupported(token,"Only primitive literals and identifier expressions are supported.");
        return default!;
    }
    private void EndStatement()
    {
        if(Eat(TokenKind.Semi)||Current.Kind is TokenKind.End or TokenKind.Close||Current.LineBreak)return;
        Unsupported(Current,"This expression or statement form is not supported yet.");
    }
    private void Unsupported(Token token,string message)=>throw new CompilationFailure(Diagnostic.Error(file,token,"TSCS1001",message));
}

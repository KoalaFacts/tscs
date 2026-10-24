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
                        if(Current.Kind!=TokenKind.Identifier || Current.Text is not ("number" or "string" or "boolean")) Unsupported(Current,"Only primitive type annotations are supported.");
                        annotation=Take().Text;
                    }
                    Token? initializer=Eat(TokenKind.Equal)?ParseValue():null;
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
            if(Eat(TokenKind.Equal))
            {
                if(value.Kind!=TokenKind.Identifier) Unsupported(value,"Only identifier assignment targets are supported.");
                statements.Add(new Assignment(value,ParseValue()));
            }
            else statements.Add(new Expression(value));
            EndStatement();
        }
        return new Block(statements);
    }
    private Token ParseValue()
    {
        var token=Current;
        if(token.Kind is TokenKind.Identifier or TokenKind.Number or TokenKind.String or TokenKind.True or TokenKind.False) return Take();
        if(token.Kind is TokenKind.Semi or TokenKind.End or TokenKind.Close)
        { Diagnostics.Add(Diagnostic.Error(file,token,"TS1109","Expression expected.")); return new(TokenKind.Number,"0",token.Start,0); }
        Unsupported(token,"Only primitive literals and identifier expressions are supported."); return default;
    }
    private void EndStatement()
    {
        if(Eat(TokenKind.Semi)||Current.Kind is TokenKind.End or TokenKind.Close||Current.LineBreak)return;
        Unsupported(Current,"This expression or statement form is not supported yet.");
    }
    private void Unsupported(Token token,string message)=>throw new CompilationFailure(Diagnostic.Error(file,token,"TSCS1001",message));
}

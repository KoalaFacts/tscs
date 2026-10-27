namespace Tscs;

// Supported paths follow parseVariableDeclarationWorker and block/statement parsing upstream.
internal sealed class Parser(string file, List<Token> tokens)
{
    private int position;
    private int callNesting;
    private int conditionalNesting;
    private int groupNesting;
    private int objectNesting;
    private Token Current => tokens[position];
    public List<Diagnostic> Diagnostics { get; } = [];
    private Token Take() => tokens[position++];
    private bool Eat(TokenKind kind) { if(Current.Kind!=kind)return false; position++;return true; }
    public Block Parse(int depth=0,bool inFunction=false)
    {
        if(depth>128) Unsupported(Current,"Block nesting exceeds the bootstrap limit.");
        var statements=new List<Statement>();
        while(Current.Kind!=TokenKind.End && !(depth>0 && Current.Kind==TokenKind.Close))
        {
            if(Current.Kind==TokenKind.Close) { Diagnostics.Add(Diagnostic.Error(file,Take(),"TS1128","Declaration or statement expected.")); continue; }
            if(Eat(TokenKind.Semi)) { statements.Add(new EmptyStatement()); continue; }
            if(Eat(TokenKind.Open))
            {
                statements.Add(Parse(depth+1,inFunction));
                if(!Eat(TokenKind.Close)) Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
                continue;
            }
            if(Current.Kind==TokenKind.Import)
            {
                if(depth!=0)Unsupported(Current,"Module imports must be at source-file scope.");
                Take();
                if(!Eat(TokenKind.Open))Unsupported(Current,"Only named module imports are supported yet.");
                var specifiers=new List<ImportSpecifier>();
                while(true)
                {
                    if(Current.Kind!=TokenKind.Identifier)
                        Unsupported(Current,"Expected an imported identifier.");
                    var imported=Take();
                    var local=imported;
                    if(Current.Kind==TokenKind.Identifier && Current.Text=="as")
                    {
                        Take();
                        if(Current.Kind!=TokenKind.Identifier)
                            Unsupported(Current,"Expected a local alias after 'as'.");
                        local=Take();
                    }
                    specifiers.Add(new(imported,local));
                    if(!Eat(TokenKind.Comma))break;
                }
                if(!Eat(TokenKind.Close))Unsupported(Current,"Expected '}' after named imports.");
                if(Current.Kind!=TokenKind.Identifier || Current.Text!="from")
                    Unsupported(Current,"Expected 'from' in an import declaration.");
                Take();
                if(Current.Kind!=TokenKind.String)
                    Unsupported(Current,"Expected a string module specifier.");
                var module=Take();
                statements.Add(new ImportDeclaration(module,specifiers));
                EndStatement();
                continue;
            }
            bool exported=false;
            if(Current.Kind==TokenKind.Export)
            {
                if(depth!=0)Unsupported(Current,"Module exports must be at source-file scope.");
                Take();
                exported=true;
                if(Eat(TokenKind.Open))
                {
                    // Named local exports share the module's already-bound symbols.
                    // Re-exports from another module require a separate graph phase.
                    var exports=new List<ExportSpecifier>();
                    if(Current.Kind!=TokenKind.Close)
                    {
                        while(true)
                        {
                            if(Current.Kind!=TokenKind.Identifier)
                                Unsupported(Current,"Expected a local name in a named export.");
                            var local=Take();
                            var exportedName=local;
                            if(Current.Kind==TokenKind.Identifier && Current.Text=="as")
                            {
                                Take();
                                if(Current.Kind!=TokenKind.Identifier)
                                    Unsupported(Current,"Expected an exported alias after 'as'.");
                                exportedName=Take();
                            }
                            exports.Add(new ExportSpecifier(local,exportedName));
                            if(!Eat(TokenKind.Comma))break;
                            if(Current.Kind==TokenKind.Close)break;
                        }
                    }
                    if(!Eat(TokenKind.Close))Unsupported(Current,"Expected '}' after named exports.");
                    Token? from=null;
                    if(Current.Kind==TokenKind.Identifier && Current.Text=="from")
                    {
                        Take();
                        if(Current.Kind!=TokenKind.String)
                            Unsupported(Current,"Expected a string module specifier in a re-export.");
                        from=Take();
                        if(exports.Count==0)
                            Unsupported(from.Value,"An empty re-export list is not supported.");
                    }
                    statements.Add(new ExportDeclaration(exports,from));
                    EndStatement();
                    continue;
                }
                if(Current.Kind is not (TokenKind.Let or TokenKind.Const or TokenKind.Function))
                    Unsupported(Current,"Only exported variables, functions or named local exports are supported yet.");
            }
            if(Current.Kind==TokenKind.Function)
            {
                if(depth!=0)Unsupported(Current,"Function declarations are currently supported only at source-file scope.");
                Take();
                var name=Current;
                if(name.Kind!=TokenKind.Identifier)Unsupported(name,"Expected a named function declaration.");
                Take();
                if(!Eat(TokenKind.OpenParen))Unsupported(Current,"Expected '(' after function name.");
                var parameters=new List<ParameterDeclaration>();
                if(Current.Kind!=TokenKind.CloseParen)
                {
                    while(true)
                    {
                        var parameter=Current;
                        if(parameter.Kind!=TokenKind.Identifier)Unsupported(parameter,"Expected a named parameter.");
                        Take();
                        if(!Eat(TokenKind.Colon))Unsupported(Current,"Function parameters require an explicit primitive type.");
                        parameters.Add(new ParameterDeclaration(parameter,ReadTypeAnnotation()));
                        if(!Eat(TokenKind.Comma))break;
                    }
                }
                if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after function parameters.");
                if(!Eat(TokenKind.Colon))Unsupported(Current,"Function return types must be explicit in this compiler subset.");
                var result=ReadTypeAnnotation();
                if(!Eat(TokenKind.Open))Unsupported(Current,"Expected '{' before function body.");
                var body=Parse(depth+1,true);
                if(!Eat(TokenKind.Close))Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
                statements.Add(new FunctionDeclaration(name,parameters,result,body,exported));
                continue;
            }
            if(Current.Kind==TokenKind.If)
            {
                statements.Add(ParseConditional(depth,inFunction));
                continue;
            }
            if(Current.Kind==TokenKind.Return)
            {
                var keyword=Take();
                if(!inFunction)Unsupported(keyword,"Return statements are supported only inside functions.");
                if(Current.LineBreak || Current.Kind is TokenKind.Semi or TokenKind.End or TokenKind.Close)
                    Unsupported(keyword,"Only value-returning functions are supported yet.");
                statements.Add(new ReturnStatement(keyword,ParseValue()));
                EndStatement();
                continue;
            }
            if(Current.Kind is TokenKind.Let or TokenKind.Const)
            {
                bool constant=Take().Kind==TokenKind.Const;
                while(true)
                {
                    var name=Current;
                    if(name.Kind!=TokenKind.Identifier) Unsupported(name,"Only named variable declarations are supported.");
                    Take(); TypeSyntax? annotation=null;
                    if(Eat(TokenKind.Colon))
                    {
                        annotation=ReadTypeAnnotation();
                    }
                    ValueNode? initializer=Eat(TokenKind.Equal)?ParseValue():null;
                    if(annotation==null && initializer==null) Unsupported(name,"Variables without a type or initializer are not supported yet.");
                    statements.Add(new Variable(name,constant,annotation,initializer,exported));
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
                else if(value is ValueMember member)
                    statements.Add(new MemberAssignment(member.Target,member.Member,ParseValue()));
                else Unsupported(Current,"Only identifier and named-property assignment targets are supported.");
            }
            else statements.Add(new Expression(value));
            EndStatement();
        }
        return new Block(statements);
    }
    // Preserve distinct branch ASTs for later control flow and symbol analysis.
    // Single-statement arms remain unsupported until the parser can represent them.
    private ConditionalStatement ParseConditional(int depth,bool inFunction)
    {
        if(++conditionalNesting>128)
            Unsupported(Current,"Conditional nesting exceeds the bootstrap limit.");
        try
        {
            var keyword=Take();
            if(!Eat(TokenKind.OpenParen))
                Unsupported(Current,"Expected '(' after 'if'.");
            var condition=ParseValue();
            if(!Eat(TokenKind.CloseParen))
                Unsupported(Current,"Expected ')' after the condition.");
            if(!Eat(TokenKind.Open))
                Unsupported(Current,"Conditional branches require a braced block.");
            var consequent=Parse(depth+1,inFunction);
            if(!Eat(TokenKind.Close))
                Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
            Statement? alternate=null;
            if(Eat(TokenKind.Else))
            {
                if(Current.Kind==TokenKind.If)
                    alternate=ParseConditional(depth+1,inFunction);
                else
                {
                    if(!Eat(TokenKind.Open))
                        Unsupported(Current,"Conditional branches require a braced block.");
                    alternate=Parse(depth+1,inFunction);
                    if(!Eat(TokenKind.Close))
                        Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
                }
            }
            return new ConditionalStatement(keyword,condition,consequent,alternate);
        }
        finally { conditionalNesting--; }
    }

    private TypeSyntax ReadTypeAnnotation(int depth=0)
    {
        if(depth>16)Unsupported(Current,"Object type nesting exceeds the supported limit.");
        if(Eat(TokenKind.Open))
        {
            var properties=new List<ObjectTypePropertySyntax>();
            var names=new HashSet<string>(StringComparer.Ordinal);
            while(Current.Kind!=TokenKind.Close)
            {
                if(properties.Count>=128)Unsupported(Current,"Object types are limited to 128 named members.");
                var name=Current;
                if(name.Kind!=TokenKind.Identifier)Unsupported(name,"Object types require named members.");
                Take();
                if(!names.Add(name.Text))Unsupported(name,"Duplicate object type members are not supported.");
                if(!Eat(TokenKind.Colon))Unsupported(Current,"Expected ':' after object type member.");
                properties.Add(new(name,ReadTypeAnnotation(depth+1)));
                if(Current.Kind==TokenKind.Close)break;
                if(!Eat(TokenKind.Semi) && !Eat(TokenKind.Comma))
                    Unsupported(Current,"Expected ';' or ',' between object type members.");
            }
            Take();
            return new ObjectTypeSyntax(properties);
        }
        if(Current.Kind!=TokenKind.Identifier ||
           Current.Text is not ("number" or "string" or "boolean" or "any"))
            Unsupported(Current,"Only primitive and required named object type annotations are supported.");
        return new PrimitiveTypeSyntax(Take());
    }

    // Single precedence ladder shared by variable, call, return, assignment
    // and conditional expression sites. The bounded type checker must reject
    // unsupported operand families rather than pretending JS coercion works.
    private ValueNode ParseValue()=>ParseLogicalOr();
    private ValueNode ParseLogicalOr()
    {
        ValueNode left=ParseLogicalAnd();
        while(Current.Kind==TokenKind.BarBar)
        {
            var op=Take();
            left=new ValueBinary(left,op,ParseLogicalAnd());
        }
        return left;
    }
    private ValueNode ParseLogicalAnd()
    {
        ValueNode left=ParseEquality();
        while(Current.Kind==TokenKind.AmpAmp)
        {
            var op=Take();
            left=new ValueBinary(left,op,ParseEquality());
        }
        return left;
    }
    private ValueNode ParseEquality()
    {
        ValueNode left=ParseRelational();
        while(Current.Kind is TokenKind.EqualEqual or TokenKind.BangEqual or TokenKind.StrictEqual or TokenKind.StrictNotEqual)
        {
            var op=Take();
            left=new ValueBinary(left,op,ParseRelational());
        }
        return left;
    }
    private ValueNode ParseRelational()
    {
        ValueNode left=ParseAdditive();
        while(Current.Kind is TokenKind.LessThan or TokenKind.LessEqual or TokenKind.GreaterThan or TokenKind.GreaterEqual)
        {
            var op=Take();
            left=new ValueBinary(left,op,ParseAdditive());
        }
        return left;
    }
    private ValueNode ParseAdditive()
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
        while(Current.Kind is TokenKind.Minus or TokenKind.Plus or TokenKind.Bang)prefixes.Add(Take());
        ValueNode value=ParsePostfix();
        for(int i=prefixes.Count-1;i>=0;i--)value=new ValueUnary(prefixes[i],value);
        return value;
    }
    private ValueNode ParsePostfix()
    {
        var value=ParsePrimary();
        while(true)
        {
            if(Eat(TokenKind.Dot))
            {
                if(Current.Kind!=TokenKind.Identifier)
                    Unsupported(Current,"Expected a named property after '.'.");
                value=new ValueMember(value,Take());
                continue;
            }
            if(Current.Kind!=TokenKind.OpenParen)break;
            if(value is not ValueAtom { Token.Kind: TokenKind.Identifier })
                Unsupported(Current,"Only named function calls are supported yet.");
            var callee=((ValueAtom)value).Token;
            Take();
            if(++callNesting>64)Unsupported(callee,"Nested calls exceed the compiler subset depth limit.");
            try
            {
                var arguments=new List<ValueNode>();
                if(Current.Kind!=TokenKind.CloseParen)
                {
                    while(true)
                    {
                        arguments.Add(ParseValue());
                        if(!Eat(TokenKind.Comma))break;
                    }
                }
                if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after function arguments.");
                value=new ValueCall(callee,arguments);
            }
            finally { callNesting--; }
        }
        return value;
    }

    private ValueNode ParsePrimary()
    {
        var token=Current;
        if(token.Kind==TokenKind.OpenParen)
        {
            Take();
            if(++groupNesting>128)Unsupported(token,"Parenthesized expression nesting exceeds the bootstrap limit.");
            try
            {
                var inner=ParseValue();
                if(Current.Kind!=TokenKind.CloseParen)Unsupported(Current,"Expected ')' to close a parenthesized expression.");
                return new ValueParenthesized(token,inner,Take());
            }
            finally { groupNesting--; }
        }
        if(token.Kind==TokenKind.Open)
        {
            Take();
            if(++objectNesting>16)Unsupported(token,"Object literal nesting exceeds the supported limit.");
            try
            {
                var properties=new List<ObjectValueProperty>();
                var names=new HashSet<string>(StringComparer.Ordinal);
                while(Current.Kind!=TokenKind.Close)
                {
                    if(properties.Count>=128)Unsupported(Current,"Object literals are limited to 128 named properties.");
                    var name=Current;
                    if(name.Kind!=TokenKind.Identifier)
                        Unsupported(name,"Object literals require named properties.");
                    Take();
                    if(!names.Add(name.Text))
                        throw new CompilationFailure(Diagnostic.Error(file,name,"TS1117",
                            "An object literal cannot have multiple properties with the same name."));
                    if(!Eat(TokenKind.Colon))Unsupported(Current,"Expected ':' after object property.");
                    properties.Add(new(name,ParseValue()));
                    if(Current.Kind==TokenKind.Close)break;
                    if(!Eat(TokenKind.Comma))Unsupported(Current,"Expected ',' between object properties.");
                    if(Current.Kind==TokenKind.Close)break;
                }
                var close=Take();
                return new ValueObject(token,properties,close);
            }
            finally { objectNesting--; }
        }
        if(token.Kind is TokenKind.Identifier or TokenKind.Number or TokenKind.String or TokenKind.True or TokenKind.False)
            return new ValueAtom(Take());
        if(token.Kind is TokenKind.Semi or TokenKind.End or TokenKind.Close)
        {
            Diagnostics.Add(Diagnostic.Error(file,token,"TS1109","Expression expected."));
            return new ValueAtom(new(TokenKind.Number,"0",token.Start,0));
        }
        Unsupported(token,"Only implemented primitive, object, parenthesized and named-call expressions are supported.");
        return default!;
    }
    private void EndStatement()
    {
        if(Eat(TokenKind.Semi)||Current.Kind is TokenKind.End or TokenKind.Close||Current.LineBreak)return;
        Unsupported(Current,"This expression or statement form is not supported yet.");
    }
    private void Unsupported(Token token,string message)=>throw new CompilationFailure(Diagnostic.Error(file,token,"TSCS1001",message));
}

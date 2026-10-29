namespace Tscs;

// Supported paths follow parseVariableDeclarationWorker and block/statement parsing upstream.
internal sealed class Parser(string file, List<Token> tokens)
{
    private int position;
    private int callNesting;
    private int conditionalNesting;
    private int conditionalValueNesting;
    private int groupNesting;
    private int objectNesting;
    private int arrayNesting;
    private int exponentNesting;
    // Classic-for initializers use the ECMAScript NoIn grammar; grouping restores In.
    private bool disallowIn;
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
                bool typeOnly=Eat(TokenKind.Type);
                if(!Eat(TokenKind.Open))Unsupported(Current,"Only named module imports and import type are supported yet.");
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
                statements.Add(new ImportDeclaration(module,specifiers,typeOnly));
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
                if(Current.Kind is not (TokenKind.Let or TokenKind.Const or TokenKind.Function or TokenKind.Type or TokenKind.Interface))
                    Unsupported(Current,"Only exported variables, functions, type aliases and interfaces are supported yet.");
            }
            if(Current.Kind is TokenKind.Type or TokenKind.Interface)
            {
                if(depth!=0)Unsupported(Current,"Type declarations require source-file scope.");
                bool alias=Take().Kind==TokenKind.Type;
                var name=Current;
                if(name.Kind!=TokenKind.Identifier)Unsupported(name,"Expected a named type declaration.");
                Take();
                if(alias)
                {
                    if(!Eat(TokenKind.Equal))Unsupported(Current,"Expected '=' in a type alias.");
                    statements.Add(new TypeAliasDeclaration(name,ReadTypeAnnotation(),exported));
                    EndStatement();
                }
                else
                {
                    // Named, non-generic finite-object interface bases only.
                    // 'extends' is contextual, not a reserved value token.
                    var bases=new List<NamedTypeSyntax>();
                    if(Current.Kind==TokenKind.Identifier && Current.Text=="extends")
                    {
                        Take();
                        while(true)
                        {
                            if(bases.Count>=16)Unsupported(Current,"Interface heritage exceeds the 16-base limit.");
                            if(Current.Kind!=TokenKind.Identifier)
                                Unsupported(Current,"Interface heritage requires named structural types.");
                            bases.Add(new NamedTypeSyntax(Take()));
                            if(!Eat(TokenKind.Comma))break;
                        }
                    }
                    if(Current.Kind!=TokenKind.Open)
                        Unsupported(Current,"Only named, non-generic interface heritage and required properties are supported.");
                    var body=ReadTypeAnnotation();
                    if(body is not ObjectTypeSyntax)
                        Unsupported(Current,"Interfaces require a required-member object body.");
                    statements.Add(new InterfaceDeclaration(name,(ObjectTypeSyntax)body,exported,bases));
                    Eat(TokenKind.Semi);
                }
                continue;
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
                bool sawOptionalParameter=false;
                bool sawDefaultParameter=false;
                bool sawRestParameter=false;
                if(Current.Kind!=TokenKind.CloseParen)
                {
                    while(true)
                    {
                        if(sawRestParameter)
                            Diagnostics.Add(Diagnostic.Error(file,Current,"TS1014",
                                "A rest parameter must be last in a parameter list."));
                        bool rest=Eat(TokenKind.Ellipsis);
                        var parameter=Current;
                        if(parameter.Kind!=TokenKind.Identifier)Unsupported(parameter,"Expected a named parameter.");
                        Take();
                        bool questionOptional=Eat(TokenKind.Question);
                        if(!Eat(TokenKind.Colon))Unsupported(Current,"Function parameters require an explicit supported type.");
                        var annotation=ReadTypeAnnotation();
                        var defaultValue=Eat(TokenKind.Equal)?ParseValue():null;
                        if(rest)
                        {
                            if(annotation is not ArrayTypeSyntax)
                                Diagnostics.Add(Diagnostic.Error(file,parameter,"TS2370",
                                    "A rest parameter must be of an array type."));
                            if(questionOptional)
                                Diagnostics.Add(Diagnostic.Error(file,parameter,"TS1047",
                                    "A rest parameter cannot be optional."));
                            if(defaultValue is not null)
                                Diagnostics.Add(Diagnostic.Error(file,parameter,"TS1048",
                                    "A rest parameter cannot have an initializer."));
                            sawRestParameter=true;
                        }
                        if(questionOptional && defaultValue is not null)
                            Diagnostics.Add(Diagnostic.Error(file,parameter,"TS1015",
                                "Parameter cannot have question mark and initializer."));
                        bool optional=questionOptional||defaultValue is not null||rest;
                        // TypeScript permits a default before a required parameter,
                        // but this bounded implementation does not model its required
                        // call-site positional slot. Reject instead of miscompiling.
                        if(sawDefaultParameter && !optional)
                            Unsupported(parameter,"Required parameters after defaults are not supported yet.");
                        if(sawOptionalParameter && !optional)
                            Diagnostics.Add(Diagnostic.Error(file,parameter,"TS1016",
                                "A required parameter cannot follow an optional parameter."));
                        sawOptionalParameter|=optional;
                        sawDefaultParameter|=defaultValue is not null;
                        parameters.Add(new ParameterDeclaration(parameter,annotation,optional,defaultValue,rest));
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
            if(Current.Kind==TokenKind.For)
            {
                statements.Add(ParseFor(depth,inFunction));
                continue;
            }
            if(Current.Kind==TokenKind.While)
            {
                statements.Add(ParseWhile(depth,inFunction));
                continue;
            }
            if(Current.Kind==TokenKind.Do)
            {
                statements.Add(ParseDoWhile(depth,inFunction));
                continue;
            }
            if(Current.Kind==TokenKind.Switch)
            {
                statements.Add(ParseSwitch(depth,inFunction));
                continue;
            }
            if(Current.Kind==TokenKind.Break)
            {
                var keyword=Take();
                EndStatement();
                statements.Add(new BreakStatement(keyword));
                continue;
            }
            if(Current.Kind==TokenKind.Continue)
            {
                var keyword=Take();
                EndStatement();
                statements.Add(new ContinueStatement(keyword));
                continue;
            }
            if(Current.Kind==TokenKind.Try)
            {
                statements.Add(ParseTry(depth,inFunction));
                continue;
            }
            if(Current.Kind==TokenKind.Throw)
            {
                var keyword=Take();
                if(Current.LineBreak)
                    throw new CompilationFailure(Diagnostic.Error(file,keyword,"TS1142",
                        "Line break not permitted here."));
                if(Current.Kind is TokenKind.Semi or TokenKind.End or TokenKind.Close)
                    throw new CompilationFailure(Diagnostic.Error(file,Current,"TS1109","Expression expected."));
                statements.Add(new ThrowStatement(keyword,ParseValue()));
                EndStatement();
                continue;
            }
            if(Current.Kind==TokenKind.Return)
            {
                var keyword=Take();
                if(!inFunction)Unsupported(keyword,"Return statements are supported only inside functions.");
                // A line terminator after return ends the statement (ASI).
                // The checker validates a bare return against the declared result type.
                var returnExpression=Current.LineBreak || Current.Kind is TokenKind.Semi or TokenKind.End or TokenKind.Close
                    ? null : ParseValue();
                statements.Add(new ReturnStatement(keyword,returnExpression));
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
            if(Current.Kind is TokenKind.PlusEqual or TokenKind.MinusEqual or TokenKind.StarEqual or TokenKind.PowerEqual or TokenKind.SlashEqual or TokenKind.PercentEqual or TokenKind.AmpEqual or TokenKind.BarEqual or TokenKind.CaretEqual or TokenKind.LeftShiftEqual or TokenKind.RightShiftEqual or TokenKind.UnsignedRightShiftEqual)
            {
                var op=Take();
                if(value is not (ValueAtom { Token.Kind:TokenKind.Identifier } or ValuePropertyAccess or ValueElementAccess))
                    Unsupported(op,"Compound assignment requires an identifier, named property or array element.");
                statements.Add(new CompoundAssignment(value,op,ParseValue()));
            }
            else if(Eat(TokenKind.Equal))
            {
                if(value is ValueAtom atom && atom.Token.Kind==TokenKind.Identifier)
                    statements.Add(new Assignment(atom.Token,ParseValue()));
                else if(value is ValuePropertyAccess property)
                    statements.Add(new PropertyAssignment(property,ParseValue()));
                else if(value is ValueElementAccess element)
                    statements.Add(new ElementAssignment(element,ParseValue()));
                else Unsupported(Current,"Only identifier, named-property or array-element assignment targets are supported.");
            }
            else statements.Add(new Expression(value));
            EndStatement();
        }
        return new Block(statements);
    }

    // ES2015 braced try/catch/finally. Catch accepts a single identifier
    // or an omitted binding. Typed/destructured bindings remain unsupported.
    private TryStatement ParseTry(int depth,bool inFunction)
    {
        var keyword=Take();
        if(!Eat(TokenKind.Open))Unsupported(Current,"try requires a braced block.");
        var body=Parse(depth+1,inFunction);
        if(!Eat(TokenKind.Close))
            Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
        Token? binding=null;
        Block? caught=null;
        if(Eat(TokenKind.Catch))
        {
            if(Eat(TokenKind.OpenParen))
            {
                if(Current.Kind!=TokenKind.Identifier)
                    Unsupported(Current,"Catch binding must be a single identifier.");
                binding=Take();
                if(!Eat(TokenKind.CloseParen))
                    Unsupported(Current,"Typed or destructured catch bindings are not supported.");
            }
            if(!Eat(TokenKind.Open))Unsupported(Current,"catch requires a braced block.");
            caught=Parse(depth+1,inFunction);
            if(!Eat(TokenKind.Close))
                Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
        }
        Block? cleanup=null;
        if(Eat(TokenKind.Finally))
        {
            if(!Eat(TokenKind.Open))Unsupported(Current,"finally requires a braced block.");
            cleanup=Parse(depth+1,inFunction);
            if(!Eat(TokenKind.Close))
                Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
        }
        if(caught==null && cleanup==null)
            Unsupported(Current,"A try statement requires catch or finally.");
        return new TryStatement(keyword,body,binding,caught,cleanup);
    }

    // Literal-labelled, braced clauses provide separate lexical scopes.
    // Normal completion falls through to the next case; break exits the
    // switch. Non-braced clauses and dynamic labels still fail closed.
    private SwitchStatement ParseSwitch(int depth,bool inFunction)
    {
        var keyword=Take();
        if(!inFunction)Unsupported(keyword,"Returning switch statements are currently supported only inside functions.");
        if(!Eat(TokenKind.OpenParen))Unsupported(Current,"Expected '(' after 'switch'.");
        var selector=ParseValue();
        if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after switch selector.");
        if(!Eat(TokenKind.Open))Unsupported(Current,"Expected '{' before switch clauses.");
        var clauses=new List<SwitchClause>();
        bool hasDefault=false;
        while(Current.Kind!=TokenKind.Close)
        {
            if(Current.Kind==TokenKind.End)Unsupported(Current,"Expected '}' after switch clauses.");
            if(clauses.Count>=64)Unsupported(Current,"Switches with over 64 clauses are not supported yet.");
            var keywordClause=Current;
            if(keywordClause.Kind is not (TokenKind.Case or TokenKind.Default))
                Unsupported(keywordClause,"Expected 'case' or 'default' in switch.");
            Take();
            ValueNode? label=null;
            if(keywordClause.Kind==TokenKind.Case)
            {
                label=ParseValue();
                if(label is not ValueAtom { Token.Kind:TokenKind.String or TokenKind.Number or TokenKind.True or TokenKind.False } &&
                   label is not ValueUnary { Operator.Kind:TokenKind.Minus, Operand:ValueAtom { Token.Kind:TokenKind.Number } })
                    Unsupported(keywordClause,"Only primitive literal switch case labels are supported yet.");
            }
            else if(hasDefault)Unsupported(keywordClause,"Duplicate switch defaults are not supported.");
            else hasDefault=true;
            if(!Eat(TokenKind.Colon))Unsupported(Current,"Expected ':' after switch clause.");
            if(!Eat(TokenKind.Open))
                Unsupported(Current,"Each switch clause currently requires a braced block.");
            var body=Parse(depth+1,inFunction);
            if(!Eat(TokenKind.Close))
                Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
            clauses.Add(new SwitchClause(keywordClause,label,body));
        }
        if(!Eat(TokenKind.Close))Unsupported(Current,"Expected '}' after switch clauses.");
        if(clauses.Count==0)Unsupported(keyword,"An empty switch has no supported returning control flow.");
        return new SwitchStatement(keyword,selector,clauses);
    }

    // Both loop styles share the checked expression AST and lexically scoped
    // bindings. Only one mutable let initializer and supported mutations are
    // admitted for classic for; unsupported forms fail closed.
    private Statement ParseFor(int depth,bool inFunction)
    {
        var keyword=Take();
        if(!Eat(TokenKind.OpenParen))Unsupported(Current,"Expected '(' after 'for'.");
        Statement? initializer=null;
        if(Current.Kind is TokenKind.Let or TokenKind.Const)
        {
            bool constant=Take().Kind==TokenKind.Const;
            var name=Current;
            if(name.Kind!=TokenKind.Identifier)Unsupported(name,"for requires a named iteration variable.");
            Take();
            if(Current.Kind==TokenKind.Identifier && Current.Text=="of")
            {
                Take();
                var source=ParseValue();
                if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after for..of source.");
                if(!Eat(TokenKind.Open))Unsupported(Current,"for..of bodies must be braced.");
                var body=Parse(depth+1,inFunction);
                if(!Eat(TokenKind.Close))Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
                return new ForOfStatement(keyword,name,constant,source,body);
            }
            if(Eat(TokenKind.In))
            {
                var source=ParseValue();
                if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after for..in source.");
                if(!Eat(TokenKind.Open))Unsupported(Current,"for..in bodies must be braced.");
                var body=Parse(depth+1,inFunction);
                if(!Eat(TokenKind.Close))Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
                return new ForInStatement(keyword,name,constant,source,body);
            }
            if(constant)Unsupported(name,"Classic for currently requires a mutable let binding.");
            TypeSyntax? annotation=Eat(TokenKind.Colon)?ReadTypeAnnotation():null;
            ValueNode? initial=Eat(TokenKind.Equal)?ParseWithInDisallowed(ParseValue):null;
            if(annotation==null && initial==null)
                Unsupported(name,"Classic for declarations need an initializer or explicit type.");
            initializer=new Variable(name,false,annotation,initial);
        }
        else if(Current.Kind!=TokenKind.Semi)
            initializer=ParseWithInDisallowed(ParseForHeaderMutation);
        if(!Eat(TokenKind.Semi))Unsupported(Current,"Expected first ';' in classic for header.");
        ValueNode? condition=Current.Kind==TokenKind.Semi?null:ParseValue();
        if(!Eat(TokenKind.Semi))Unsupported(Current,"Expected second ';' in classic for header.");
        Statement? update=Current.Kind==TokenKind.CloseParen?null:ParseForHeaderMutation();
        if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after classic for header.");
        if(!Eat(TokenKind.Open))Unsupported(Current,"Classic for bodies must be braced.");
        var statements=Parse(depth+1,inFunction);
        if(!Eat(TokenKind.Close))Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
        return new ClassicForStatement(keyword,initializer,condition,update,statements);
    }

    private T ParseWithInDisallowed<T>(Func<T> parse)
    {
        var old=disallowIn;
        disallowIn=true;
        try { return parse(); }
        finally { disallowIn=old; }
    }

    // Nested assignment-expression contexts admit `in` even inside a NoIn
    // classic-for initializer. Restore NoIn for the enclosing expression.
    private T ParseWithInAllowed<T>(Func<T> parse)
    {
        var old=disallowIn;
        disallowIn=false;
        try { return parse(); }
        finally { disallowIn=old; }
    }

    private Statement ParseForHeaderMutation()
    {
        var expression=ParseValue();
        if(Current.Kind is TokenKind.PlusEqual or TokenKind.MinusEqual or TokenKind.StarEqual or TokenKind.PowerEqual or TokenKind.SlashEqual or TokenKind.PercentEqual or TokenKind.AmpEqual or TokenKind.BarEqual or TokenKind.CaretEqual or TokenKind.LeftShiftEqual or TokenKind.RightShiftEqual or TokenKind.UnsignedRightShiftEqual)
        {
            var op=Take();
            if(expression is not (ValueAtom { Token.Kind:TokenKind.Identifier } or ValuePropertyAccess or ValueElementAccess))
                Unsupported(op,"Compound for update requires an identifier, named property or array element.");
            return new CompoundAssignment(expression,op,ParseValue());
        }
        if(Eat(TokenKind.Equal))
        {
            var right=ParseValue();
            return expression switch
            {
                ValueAtom { Token.Kind:TokenKind.Identifier } name=>new Assignment(name.Token,right),
                ValuePropertyAccess property=>new PropertyAssignment(property,right),
                ValueElementAccess element=>new ElementAssignment(element,right),
                _=>throw new CompilationFailure(Diagnostic.Error(file,Current,"TSCS1001",
                    "Classic for assignments require an identifier or named property or indexed element."))
            };
        }
        if(expression is ValuePrefixUpdate or ValuePostfixUpdate)return new Expression(expression);
        Unsupported(Current,"Only assignment and prefix/postfix updates are supported in classic for headers.");
        return default!;
    }

    // Braced while loops share the same checked condition and exit model as
    // if/else, switch and for..of. Unbraced bodies remain outside this subset.
    private WhileStatement ParseWhile(int depth,bool inFunction)
    {
        var keyword=Take();
        if(!Eat(TokenKind.OpenParen))Unsupported(Current,"Expected '(' after 'while'.");
        var condition=ParseValue();
        if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after while condition.");
        if(!Eat(TokenKind.Open))Unsupported(Current,"while bodies must be braced.");
        var body=Parse(depth+1,inFunction);
        if(!Eat(TokenKind.Close))Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
        return new WhileStatement(keyword,condition,body);
    }

    // The body executes at least once. The condition is checked after the body,
    // including when a continue skips to the next condition test.
    private DoWhileStatement ParseDoWhile(int depth,bool inFunction)
    {
        var keyword=Take();
        if(!Eat(TokenKind.Open))Unsupported(Current,"do/while bodies must be braced.");
        var body=Parse(depth+1,inFunction);
        if(!Eat(TokenKind.Close))Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
        if(!Eat(TokenKind.While))Unsupported(Current,"Expected 'while' after do body.");
        if(!Eat(TokenKind.OpenParen))Unsupported(Current,"Expected '(' after do/while.");
        var condition=ParseValue();
        if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after do/while condition.");
        EndStatement();
        return new DoWhileStatement(keyword,body,condition);
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

    // The same bounded grammar handles aliases, interfaces, variables, function
    // signatures and nested members: postfix [] binds tighter than union |.
    private TypeSyntax ReadTypeAnnotation(int depth=0)
    {
        if(depth>64)Unsupported(Current,"Type nesting exceeds the compiler subset limit.");
        var alternatives=new List<TypeSyntax>{ ReadTypeIntersection(depth+1) };
        while(Eat(TokenKind.Bar))
        {
            if(alternatives.Count>=64)Unsupported(Current,"Unions with over 64 alternatives are not supported yet.");
            alternatives.Add(ReadTypeIntersection(depth+1));
        }
        return alternatives.Count==1?alternatives[0]:new UnionTypeSyntax(alternatives);
    }
    // Postfix [] and keyof bind before &, while & binds before union |.
    // Bitwise & in value expressions continues to use the existing expression grammar.
    private TypeSyntax ReadTypeIntersection(int depth)
    {
        if(depth>64)Unsupported(Current,"Type nesting exceeds the compiler subset limit.");
        var parts=new List<TypeSyntax>{ReadTypePart(depth+1)};
        while(Eat(TokenKind.Amp))
        {
            if(parts.Count>=64)Unsupported(Current,"Intersections with over 64 operands are not supported yet.");
            parts.Add(ReadTypePart(depth+1));
        }
        return parts.Count==1?parts[0]:new IntersectionTypeSyntax(parts);
    }
    private TypeSyntax ReadTypePart(int depth)
    {
        if(depth>64)Unsupported(Current,"Type nesting exceeds the compiler subset limit.");
        TypeSyntax type;
        if(Current.Kind==TokenKind.Identifier && Current.Text=="keyof")
        {
            var keyword=Take();
            type=new KeyofTypeSyntax(keyword,ReadTypePart(depth+1));
        }
        else if(Current.Kind==TokenKind.Typeof)
        {
            var keyword=Take();
            if(Current.Kind!=TokenKind.Identifier)
                Unsupported(Current,"A typeof type query requires a named value.");
            var root=Take();
            var members=new List<Token>();
            while(Eat(TokenKind.Dot))
            {
                if(members.Count>=32)Unsupported(Current,"Type query property path exceeds the supported limit.");
                members.Add(ReadPropertyName(allowString:false));
            }
            type=new TypeQuerySyntax(keyword,root,members);
        }
        else if(Current.Kind is TokenKind.Identifier or TokenKind.Void)
        {
            var name=Take();
            type=name.Text is "number" or "string" or "boolean" or "any" or "void"
                ?new PrimitiveTypeSyntax(name):new NamedTypeSyntax(name);
        }
        else if(Current.Kind is TokenKind.String or TokenKind.Number or TokenKind.True or TokenKind.False)
        {
            type=new LiteralTypeSyntax(Take());
        }
        else if(Current.Kind==TokenKind.Minus)
        {
            var sign=Take();
            if(Current.Kind!=TokenKind.Number)
                Unsupported(sign,"Only negative numeric literal annotations are supported after '-'.");
            type=new LiteralTypeSyntax(Take(),Negative:true);
        }
        else if(Eat(TokenKind.OpenParen))
        {
            type=ReadTypeAnnotation(depth+1);
            if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' in parenthesized type.");
        }
        else if(Current.Kind==TokenKind.Open)
        {
            var open=Take();
            var properties=new List<ObjectTypeProperty>();
            var names=new HashSet<string>(StringComparer.Ordinal);
            while(Current.Kind!=TokenKind.Close)
            {
                if(Current.Kind==TokenKind.End)Unsupported(Current,"Expected '}' in object type.");
                // Contextual readonly; "readonly: number" remains an ordinary property name.
                bool readonlyMember=Current.Kind==TokenKind.Identifier &&
                    Current.Text=="readonly" && position+1<tokens.Count &&
                    tokens[position+1].Kind!=TokenKind.Colon;
                if(readonlyMember)Take();
                var name=ReadPropertyName(allowString:true,allowNumber:true);
                if(!names.Add(PropertyName(name)))
                    Diagnostics.Add(Diagnostic.Error(file,name,"TS2300",$"Duplicate identifier '{PropertyName(name)}'."));
                if(!Eat(TokenKind.Colon))
                    Unsupported(Current,"Required object type members must have ':' and a type.");
                properties.Add(new ObjectTypeProperty(name,ReadTypeAnnotation(depth+1),readonlyMember));
                if(Eat(TokenKind.Semi)||Eat(TokenKind.Comma))continue;
                if(Current.Kind==TokenKind.Close)break;
                if(!Current.LineBreak)Unsupported(Current,"Expected a separator between object type members.");
            }
            type=new ObjectTypeSyntax(open,properties,Take());
        }
        else
        {
            Unsupported(Current,"Only named, primitive, literal, object, array and union types are supported.");
            return default!;
        }
        while(Current.Kind==TokenKind.OpenBracket)
        {
            if(++depth>64)Unsupported(Current,"Array type nesting exceeds the compiler subset limit.");
            var open=Take();
            if(Current.Kind==TokenKind.CloseBracket)
                type=new ArrayTypeSyntax(type,open,Take());
            else
            {
                // A finite type-level key expression: T["name"], T[K] or T[keyof T].
                var index=ReadTypeAnnotation(depth+1);
                if(Current.Kind!=TokenKind.CloseBracket)
                    Unsupported(Current,"Expected ']' after indexed access type.");
                type=new IndexedAccessTypeSyntax(type,open,index,Take());
            }
        }
        return type;
    }

    internal static string PropertyName(Token token)=>token.Kind==TokenKind.String
        ? token.Value??token.Text : token.Text;

    private Token ReadPropertyName(bool allowString,bool allowNumber=false)
    {
        // Only non-negative safe integers have an unambiguous static
        // property identity. Fractional/exponent keys are not in this subset.
        if(allowNumber && Current.Kind==TokenKind.Number &&
           long.TryParse(Current.Text,System.Globalization.NumberStyles.None,
               System.Globalization.CultureInfo.InvariantCulture,out var integerKey) &&
           integerKey is >= 0 and <= 9007199254740991L)
            return Take();
        if(Current.Kind is TokenKind.Identifier or TokenKind.Let or TokenKind.Const or
            TokenKind.Import or TokenKind.Export or TokenKind.Type or TokenKind.Interface or TokenKind.Function or TokenKind.Return or TokenKind.Void or TokenKind.Throw or TokenKind.Try or TokenKind.Catch or TokenKind.Finally or
            TokenKind.If or TokenKind.Else or TokenKind.For or TokenKind.While or TokenKind.Do or TokenKind.Break or TokenKind.Continue or TokenKind.In or TokenKind.Typeof or TokenKind.True or TokenKind.False ||
            (allowString && Current.Kind==TokenKind.String))return Take();
        Unsupported(Current,"Expected a supported named object property.");
        return default;
    }

    private ValueArray ParseArray()
    {
        var open=Take();
        if(++arrayNesting>64)Unsupported(open,"Array literal nesting exceeds the compiler subset limit.");
        try
        {
            var values=new List<ValueNode>();
            while(Current.Kind!=TokenKind.CloseBracket)
            {
                if(Current.Kind==TokenKind.End)Unsupported(Current,"Expected ']' after array elements.");
                if(Current.Kind==TokenKind.Comma)
                    Unsupported(Current,"Sparse array holes are not supported yet.");
                if(Current.Kind==TokenKind.Ellipsis)
                {
                    var spread=Take();
                    values.Add(new ValueArraySpread(spread,ParseWithInAllowed(ParseValue)));
                }
                else values.Add(ParseWithInAllowed(ParseValue));
                if(!Eat(TokenKind.Comma))break;
            }
            if(Current.Kind!=TokenKind.CloseBracket)Unsupported(Current,"Expected ']' after array elements.");
            return new ValueArray(open,values,Take());
        }
        finally { arrayNesting--; }
    }

    private ValueObject ParseObject()
    {
        var open=Take();
        if(++objectNesting>64)Unsupported(open,"Object literal nesting exceeds the compiler subset limit.");
        try
        {
            var properties=new List<ObjectProperty>();
            var names=new HashSet<string>(StringComparer.Ordinal);
            while(Current.Kind!=TokenKind.Close)
            {
                if(Current.Kind==TokenKind.Ellipsis)
                {
                    var spread=Take();
                    properties.Add(new ObjectProperty(spread,ParseWithInAllowed(ParseValue),Spread:true));
                }
                else
                {
                    var name=ReadPropertyName(allowString:true,allowNumber:true);
                    if(PropertyName(name)=="__proto__")
                        Unsupported(name,"Object prototype setter syntax is not supported.");
                    if(!names.Add(PropertyName(name)))
                        Diagnostics.Add(Diagnostic.Error(file,name,"TS1117","An object literal cannot have multiple properties with the same name."));
                    if(Eat(TokenKind.Colon))
                        properties.Add(new ObjectProperty(name,ParseWithInAllowed(ParseValue)));
                    else if(name.Kind==TokenKind.Identifier &&
                        (Current.Kind is TokenKind.Comma or TokenKind.Close))
                        properties.Add(new ObjectProperty(name,new ValueAtom(name,Shorthand:true)));
                    else
                        Unsupported(Current,"Object shorthand requires an identifier; other object members need ':' and a value.");
                }
                if(!Eat(TokenKind.Comma))break;
            }
            if(Current.Kind!=TokenKind.Close)Unsupported(Current,"Expected '}' after object properties.");
            return new ValueObject(open,properties,Take());
        }
        finally { objectNesting--; }
    }

    // Single precedence ladder shared by variable, call, return, assignment
    // and conditional expression sites. The bounded type checker must reject
    // unsupported operand families rather than pretending JS coercion works.
    private ValueNode ParseValue()=>ParseConditionalValue();
    // Lower precedence than ||; the false arm is right-associative.
    private ValueNode ParseConditionalValue()
    {
        var condition=ParseLogicalOr();
        if(Current.Kind!=TokenKind.Question)return condition;
        var question=Take();
        if(++conditionalValueNesting>64)Unsupported(question,"Conditional expression nesting exceeds the compiler subset limit.");
        try
        {
            var whenTrue=ParseWithInAllowed(ParseValue);
            if(Current.Kind!=TokenKind.Colon)Unsupported(Current,"Expected ':' after conditional expression branch.");
            var colon=Take();
            var whenFalse=ParseConditionalValue();
            return new ValueConditional(condition,question,whenTrue,colon,whenFalse);
        }
        finally { conditionalValueNesting--; }
    }
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
        ValueNode left=ParseBitwiseOr();
        while(Current.Kind==TokenKind.AmpAmp)
        {
            var op=Take();
            left=new ValueBinary(left,op,ParseBitwiseOr());
        }
        return left;
    }
    // JavaScript/TypeScript precedence: logical && > bitwise | > ^ > & > equality.
    // Keep type-annotation union parsing separate from the expression grammar.
    private ValueNode ParseBitwiseOr()
    {
        ValueNode left=ParseBitwiseXor();
        while(Current.Kind==TokenKind.Bar)
        {
            var op=Take();
            left=new ValueBinary(left,op,ParseBitwiseXor());
        }
        return left;
    }
    private ValueNode ParseBitwiseXor()
    {
        ValueNode left=ParseBitwiseAnd();
        while(Current.Kind==TokenKind.Caret)
        {
            var op=Take();
            left=new ValueBinary(left,op,ParseBitwiseAnd());
        }
        return left;
    }
    private ValueNode ParseBitwiseAnd()
    {
        ValueNode left=ParseEquality();
        while(Current.Kind==TokenKind.Amp)
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
        ValueNode left=ParseTypeOperator();
        while(Current.Kind is TokenKind.LessThan or TokenKind.LessEqual or TokenKind.GreaterThan or TokenKind.GreaterEqual ||
              (Current.Kind==TokenKind.In && !disallowIn))
        {
            var op=Take();
            left=new ValueBinary(left,op,ParseTypeOperator());
        }
        return left;
    }

    // 'as' and 'satisfies' are contextual identifiers. Do not reserve them in
    // the scanner: import/export aliases and ordinary properties still work.
    // The bounded grammar allows subsequent arithmetic via explicit grouping.
    private ValueNode ParseTypeOperator()
    {
        ValueNode value=ParseShift();
        int count=0;
        while(Current.Kind==TokenKind.Identifier && (Current.Text is "as" or "satisfies"))
        {
            var keyword=Take();
            if(++count>64)Unsupported(keyword,"Type assertion nesting exceeds the compiler subset limit.");
            value=new ValueTypeOperator(value,keyword,ReadTypeAnnotation());
        }
        return value;
    }
    // Shifts bind more weakly than addition but more tightly than comparisons.
    private ValueNode ParseShift()
    {
        ValueNode left=ParseAdditive();
        while(Current.Kind is TokenKind.LeftShift or TokenKind.RightShift or TokenKind.UnsignedRightShift)
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
        while(Current.Kind is TokenKind.Star or TokenKind.Slash or TokenKind.Percent)
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
        while(Current.Kind is TokenKind.Minus or TokenKind.Plus or TokenKind.Bang or TokenKind.Typeof or TokenKind.Tilde or TokenKind.PlusPlus or TokenKind.MinusMinus)prefixes.Add(Take());
        ValueNode value=ParseExponentiation();
        // An unparenthesized unary left operand of ** is invalid in TS/JS.
        if(prefixes.Count>0 && value is ValueBinary { Operator.Kind:TokenKind.Power } exponent)
            Unsupported(exponent.Operator,"Unary left operands of ** must be parenthesized.");
        for(int i=prefixes.Count-1;i>=0;i--)
            value=prefixes[i].Kind is TokenKind.PlusPlus or TokenKind.MinusMinus
                ?new ValuePrefixUpdate(prefixes[i],value)
                :new ValueUnary(prefixes[i],value);
        return value;
    }
    private ValueNode ParseExponentiation()
    {
        var left=ParsePrimary();
        if(Current.Kind!=TokenKind.Power)return left;
        var op=Take();
        if(++exponentNesting>64)Unsupported(op,"Exponentiation nesting exceeds the compiler subset limit.");
        try { return new ValueBinary(left,op,ParseUnary()); }
        finally { exponentNesting--; }
    }
    private ValueNode ParsePrimary()
    {
        ValueNode value=ParsePrimaryValue();
        // Postfix access binds more tightly than unary/binary operators. Named
        // calls and grouped expressions can both produce object receivers.
        while(true)
        {
            if(Eat(TokenKind.Dot))
                value=new ValuePropertyAccess(value,ReadPropertyName(allowString:false));
            else if(Current.Kind==TokenKind.OpenBracket)
            {
                var open=Take();
                var index=ParseWithInAllowed(ParseValue);
                if(Current.Kind!=TokenKind.CloseBracket)
                    Unsupported(Current,"Expected ']' after indexed access.");
                value=new ValueElementAccess(value,open,index,Take());
            }
            else break;
        }
        // Same-line postfix binds after property/index access and before unary/binary.
        // A line break invokes statement separation instead.
        if(!Current.LineBreak && Current.Kind is TokenKind.PlusPlus or TokenKind.MinusMinus)
            value=new ValuePostfixUpdate(value,Take());
        return value;
    }

    private ValueNode ParsePrimaryValue()
    {
        var token=Current;
        if(token.Kind==TokenKind.Open)return ParseObject();
        if(token.Kind==TokenKind.OpenBracket)return ParseArray();
        if(token.Kind==TokenKind.OpenParen)
        {
            Take();
            if(++groupNesting>128)Unsupported(token,"Parenthesized expression nesting exceeds the bootstrap limit.");
            try
            {
                var inner=ParseWithInAllowed(ParseValue);
                if(Current.Kind!=TokenKind.CloseParen)Unsupported(Current,"Expected ')' to close a parenthesized expression.");
                return new ValueParenthesized(token,inner,Take());
            }
            finally { groupNesting--; }
        }
        if(token.Kind==TokenKind.Identifier)
        {
            Take();
            if(!Eat(TokenKind.OpenParen))return new ValueAtom(token);
            if(++callNesting>64)Unsupported(token,"Nested calls exceed the compiler subset depth limit.");
            try
            {
                var arguments=new List<ValueNode>();
                int spreadIndex=-1;
                if(Current.Kind!=TokenKind.CloseParen)
                {
                    while(true)
                    {
                        if(Current.Kind==TokenKind.Ellipsis)
                        {
                            Take();
                            spreadIndex=arguments.Count;
                        }
                        arguments.Add(ParseWithInAllowed(ParseValue));
                        // General interleaved, repeated and trailing-comma spread
                        // forms are not in this bounded call grammar.
                        if(spreadIndex>=0 && Current.Kind==TokenKind.Comma)
                            Unsupported(Current,"Only one trailing array spread argument is supported yet.");
                        if(!Eat(TokenKind.Comma))break;
                    }
                }
                if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after function arguments.");
                return new ValueCall(token,arguments,spreadIndex);
            }
            finally { callNesting--; }
        }
        if(token.Kind is TokenKind.Number or TokenKind.String or TokenKind.True or TokenKind.False)
            return new ValueAtom(Take());
        if(token.Kind is TokenKind.Semi or TokenKind.End or TokenKind.Close)
        {
            Diagnostics.Add(Diagnostic.Error(file,token,"TS1109","Expression expected."));
            return new ValueAtom(new(TokenKind.Number,"0",token.Start,0));
        }
        Unsupported(token,"Only primitive, object, named-property, parenthesized and named-call expressions are supported.");
        return default!;
    }
    private void EndStatement()
    {
        if(Eat(TokenKind.Semi)||Current.Kind is TokenKind.End or TokenKind.Close||Current.LineBreak)return;
        Unsupported(Current,"This expression or statement form is not supported yet.");
    }
    private void Unsupported(Token token,string message)=>throw new CompilationFailure(Diagnostic.Error(file,token,"TSCS1001",message));
}

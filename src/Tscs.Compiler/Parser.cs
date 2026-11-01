namespace Tscs;

// Supported paths follow parseVariableDeclarationWorker and block/statement parsing upstream.
internal sealed class Parser(string file, List<Token> tokens, Scanner? sourceScanner=null)
{
    private int position;
    private int callNesting;
    private int conditionalNesting;
    private int conditionalValueNesting;
    private int groupNesting;
    private int objectNesting;
    private int arrayNesting;
    private int exponentNesting;
    private int arrowNesting;
    // Classic-for initializers use the ECMAScript NoIn grammar; grouping restores In.
    private bool disallowIn;
    private Token Current => tokens[position];
    public List<Diagnostic> Diagnostics { get; } = [];
    private Token Take() => tokens[position++];
    // Split shift/punctuation only while closing a type argument, retaining
    // the original source offsets and the JS scanner's expression tokens.
    private Token TakeArrayTypeClose()
    {
        if(Current.Kind==TokenKind.GreaterThan)return Take();
        var original=Current;
        var tail=original.Kind switch
        {
            TokenKind.RightShift=>TokenKind.GreaterThan,
            TokenKind.UnsignedRightShift=>TokenKind.RightShift,
            TokenKind.GreaterEqual=>TokenKind.Equal,
            TokenKind.RightShiftEqual=>TokenKind.GreaterEqual,
            TokenKind.UnsignedRightShiftEqual=>TokenKind.RightShiftEqual,
            _=>TokenKind.Unsupported
        };
        if(tail==TokenKind.Unsupported)
        {
            Unsupported(Current,"Expected '>' after the single array type argument.");
            return default;
        }
        tokens[position]=original with {Kind=TokenKind.GreaterThan,Text=">",Length=1};
        tokens.Insert(position+1,original with
        {
            Kind=tail,Text=original.Text[1..],Start=original.Start+1,
            Length=original.Length-1,LineBreak=false
        });
        var closing=Take();
        // After consuming the leading '>' of the scanner's '>=' token,
        // its remaining '=' may join a contiguous next '>' to form '=>'.
        // This matters for arrow returns spelled Array<T>=> without spaces.
        if(tail==TokenKind.Equal && position+1<tokens.Count &&
           tokens[position].Kind==TokenKind.Equal &&
           tokens[position+1].Kind==TokenKind.GreaterThan &&
           !tokens[position+1].LineBreak &&
           tokens[position+1].Start==tokens[position].Start+1)
        {
            var equals=tokens[position];
            tokens[position]=equals with {Kind=TokenKind.Arrow,Text="=>",Length=2};
            tokens.RemoveAt(position+1);
        }
        return closing;
    }
    private bool Eat(TokenKind kind) { if(Current.Kind!=kind)return false; position++;return true; }

    // Lower finite binding patterns to ordinary checked variable declarations.
    // The generated read-only temporary evaluates the source exactly once;
    // nested temporaries likewise preserve each nested receiver evaluation.
    // Every user-visible binding still passes Binder, TypeChecker and emitter.
    private abstract record BindingPattern;
    private sealed record BoundName(Token Name) : BindingPattern;
    private sealed record BoundDefault(BindingPattern Target,ValueNode Fallback,Token Location) : BindingPattern;
    private sealed record BoundObject(IReadOnlyList<(Token Key,BindingPattern Target)> Members) : BindingPattern;
    private sealed record BoundArray(Token Open,IReadOnlyList<BindingPattern?> Elements) : BindingPattern;
    private int bindingTemporaryOrdinal;

    private static void CollectParameterBindingNames(BindingPattern pattern,List<Token> names)
    {
        switch(pattern)
        {
            case BoundName bound: names.Add(bound.Name);break;
            case BoundDefault fallback:
                CollectParameterBindingNames(fallback.Target,names);break;
            case BoundObject obj:
                foreach(var member in obj.Members)
                    CollectParameterBindingNames(member.Target,names);
                break;
            case BoundArray array:
                foreach(var element in array.Elements)
                    if(element is not null)CollectParameterBindingNames(element,names);
                break;
        }
    }

    private static bool PatternContainsDefault(BindingPattern pattern) => pattern switch
    {
        BoundDefault _ => true,
        BoundObject obj => obj.Members.Any(member=>PatternContainsDefault(member.Target)),
        BoundArray array => array.Elements.Any(element=>
            element is not null && PatternContainsDefault(element)),
        _ => false
    };

    private static void CollectBindingDefaults(BindingPattern pattern,List<BoundDefault> defaults)
    {
        switch(pattern)
        {
            case BoundDefault fallback:
                defaults.Add(fallback);
                CollectBindingDefaults(fallback.Target,defaults);
                break;
            case BoundObject obj:
                foreach(var member in obj.Members)
                    CollectBindingDefaults(member.Target,defaults);
                break;
            case BoundArray array:
                foreach(var element in array.Elements)
                    if(element is not null)CollectBindingDefaults(element,defaults);
                break;
        }
    }

    // Analyse actual expression references, not scanner token spellings:
    // interpolated templates contain parsed substitution nodes, optional
    // property names are not variable reads, and nested callables bind locals.
    // The finite parameter lowering uses this to reject only unsafe captures
    // of the pattern's own bindings or declarations in the function body.
    private static bool ReferencesName(Token token,HashSet<string> names,HashSet<string> scoped) =>
        token.Kind==TokenKind.Identifier && names.Contains(token.Text) &&
        !scoped.Contains(token.Text);

    private static bool DefaultReferencesAny(ValueNode expression,HashSet<string> names) =>
        DefaultReferencesAny(expression,names,new HashSet<string>(StringComparer.Ordinal));

    private static bool DefaultReferencesAny(ValueNode expression,HashSet<string> names,HashSet<string> scoped)
    {
        if(names.Count==0)return false;
        // TypeScript binary chains can be hundreds of thousands of nodes deep.
        // Walk values on the heap; recursive left-spine traversal would turn
        // a valid source file into a process-fatal native stack overflow.
        var pending=new Stack<(ValueNode Node,HashSet<string> Scope)>();
        pending.Push((expression,scoped));
        while(pending.Count>0)
        {
            var (node,active)=pending.Pop();
            switch(node)
            {
                case ValueAtom atom:
                    if(ReferencesName(atom.Token,names,active))return true;
                    break;
                case ValueTemplate template:
                    foreach(var item in template.Expressions)pending.Push((item,active));
                    break;
                case ValueBinary binary:
                    pending.Push((binary.Left,active));
                    pending.Push((binary.Right,active));
                    break;
                case ValueConditional conditional:
                    pending.Push((conditional.Condition,active));
                    pending.Push((conditional.WhenTrue,active));
                    pending.Push((conditional.WhenFalse,active));
                    break;
                case ValueTypeOperator typeOp:
                    pending.Push((typeOp.Expression,active));
                    break;
                case ValueNonNull asserted:
                    pending.Push((asserted.Expression,active));
                    break;
                case ValueUnary unary:
                    pending.Push((unary.Operand,active));
                    break;
                case ValuePrefixUpdate update:
                    pending.Push((update.Target,active));
                    break;
                case ValuePostfixUpdate update:
                    pending.Push((update.Target,active));
                    break;
                case ValueParenthesized group:
                    pending.Push((group.Expression,active));
                    break;
                case ValueArrow arrow:
                {
                    var callableScope=new HashSet<string>(active,StringComparer.Ordinal);
                    foreach(var parameter in arrow.Parameters)
                        callableScope.Add(parameter.Name.Text);
                    if(arrow.FunctionName is { } functionName)
                        callableScope.Add(functionName.Text);
                    foreach(var parameter in arrow.Parameters)
                        if(parameter.DefaultValue is { } value)
                            pending.Push((value,callableScope));
                    if(arrow.Body is { } body)
                        pending.Push((body,callableScope));
                    if(arrow.BlockBody is { } block &&
                       DefaultReferencesAny(block,names,callableScope))return true;
                    break;
                }
                case ValueCall call:
                    pending.Push((call.Callee,active));
                    foreach(var arg in call.Arguments)pending.Push((arg,active));
                    break;
                case ValueObject obj:
                    foreach(var property in obj.Properties)
                    {
                        if(property.Computed && ReferencesName(property.Name,names,active))
                            return true;
                        pending.Push((property.Value,active));
                    }
                    break;
                case ValueArray array:
                    foreach(var element in array.Elements)pending.Push((element,active));
                    break;
                case ValueArraySpread spread:
                    pending.Push((spread.Operand,active));
                    break;
                case ValueElementAccess index:
                    pending.Push((index.Receiver,active));
                    pending.Push((index.Index,active));
                    break;
                case ValuePropertyAccess property:
                    // Both .member and ?.member name a property, not a binding.
                    pending.Push((property.Receiver,active));
                    break;
                default:
                    // A new AST node cannot silently bypass scope validation.
                    return true;
            }
        }
        return false;
    }

    private static bool DefaultReferencesAny(Block block,HashSet<string> names,HashSet<string> scoped)
    {
        var blockScope=new HashSet<string>(scoped,StringComparer.Ordinal);
        foreach(var statement in block.Statements)
        {
            if(statement is Variable variable)blockScope.Add(variable.Name.Text);
            if(statement is FunctionDeclaration function)blockScope.Add(function.Name.Text);
        }
        return block.Statements.Any(statement=>DefaultReferencesAny(statement,names,blockScope));
    }

    private static bool DefaultReferencesAny(Statement statement,HashSet<string> names,HashSet<string> scoped)
    {
        switch(statement)
        {
            case EmptyStatement:
            case BreakStatement:
            case ContinueStatement:
            case TypeAliasDeclaration:
            case InterfaceDeclaration:
            case ImportDeclaration:
            case ExportDeclaration:
                return false;
            case Block block:
                return DefaultReferencesAny(block,names,scoped);
            case Variable variable:
                return variable.Initializer is { } initializer &&
                       DefaultReferencesAny(initializer,names,scoped);
            case Expression expression:
                return DefaultReferencesAny(expression.Value,names,scoped);
            case ReturnStatement result:
                return result.Value is { } returned && DefaultReferencesAny(returned,names,scoped);
            case ThrowStatement thrown:
                return DefaultReferencesAny(thrown.Value,names,scoped);
            case Assignment assignment:
                return ReferencesName(assignment.Name,names,scoped) ||
                       DefaultReferencesAny(assignment.Value,names,scoped);
            case PostfixUpdate update:
                return ReferencesName(update.Name,names,scoped);
            case PropertyAssignment assignment:
                return DefaultReferencesAny(assignment.Target,names,scoped) ||
                       DefaultReferencesAny(assignment.Value,names,scoped);
            case ElementAssignment assignment:
                return DefaultReferencesAny(assignment.Target,names,scoped) ||
                       DefaultReferencesAny(assignment.Value,names,scoped);
            case CompoundAssignment assignment:
                return DefaultReferencesAny(assignment.Target,names,scoped) ||
                       DefaultReferencesAny(assignment.Value,names,scoped);
            case ParameterDeclaration parameter:
                return parameter.DefaultValue is { } value &&
                       DefaultReferencesAny(value,names,scoped);
            case FunctionDeclaration function:
            {
                var functionScope=new HashSet<string>(scoped,StringComparer.Ordinal) {function.Name.Text};
                foreach(var parameter in function.Parameters)
                    functionScope.Add(parameter.Name.Text);
                return function.Parameters.Any(parameter=>
                           parameter.DefaultValue is { } value &&
                           DefaultReferencesAny(value,names,functionScope)) ||
                       DefaultReferencesAny(function.Body,names,functionScope);
            }
            case ConditionalStatement conditional:
                return DefaultReferencesAny(conditional.Condition,names,scoped) ||
                       DefaultReferencesAny(conditional.Consequent,names,scoped) ||
                       (conditional.Alternate is { } alternate &&
                        DefaultReferencesAny(alternate,names,scoped));
            case TryStatement attempt:
            {
                var catchScope=new HashSet<string>(scoped,StringComparer.Ordinal);
                if(attempt.CatchBinding is { } binding)catchScope.Add(binding.Text);
                return DefaultReferencesAny(attempt.Body,names,scoped) ||
                       (attempt.CatchBody is { } caught &&
                        DefaultReferencesAny(caught,names,catchScope)) ||
                       (attempt.FinallyBody is { } cleanup &&
                        DefaultReferencesAny(cleanup,names,scoped));
            }
            case SwitchStatement selection:
                return DefaultReferencesAny(selection.Selector,names,scoped) ||
                       selection.Clauses.Any(clause=>
                           (clause.Label is { } label &&
                            DefaultReferencesAny(label,names,scoped)) ||
                           DefaultReferencesAny(clause.Body,names,scoped));
            case WhileStatement loop:
                return DefaultReferencesAny(loop.Condition,names,scoped) ||
                       DefaultReferencesAny(loop.Body,names,scoped);
            case DoWhileStatement loop:
                return DefaultReferencesAny(loop.Body,names,scoped) ||
                       DefaultReferencesAny(loop.Condition,names,scoped);
            case ForOfStatement loop:
            {
                var inner=new HashSet<string>(scoped,StringComparer.Ordinal) {loop.Name.Text};
                return DefaultReferencesAny(loop.Source,names,inner) ||
                       DefaultReferencesAny(loop.Body,names,inner);
            }
            case ForInStatement loop:
            {
                var inner=new HashSet<string>(scoped,StringComparer.Ordinal) {loop.Name.Text};
                return DefaultReferencesAny(loop.Source,names,inner) ||
                       DefaultReferencesAny(loop.Body,names,inner);
            }
            case ClassicForStatement loop:
            {
                var inner=new HashSet<string>(scoped,StringComparer.Ordinal);
                if(loop.Initializer is Variable local)inner.Add(local.Name.Text);
                return (loop.Initializer is { } init &&
                        DefaultReferencesAny(init,names,inner)) ||
                       (loop.Condition is { } condition &&
                        DefaultReferencesAny(condition,names,inner)) ||
                       (loop.Update is { } update &&
                        DefaultReferencesAny(update,names,inner)) ||
                       DefaultReferencesAny(loop.Body,names,inner);
            }
            default:
                return true;
        }
    }

    private Token NewBindingTemporary(Token location)
    {
        // Distinguish files compiled in shared script scopes, and never capture
        // a user identifier even when it resembles our generated prefix.
        var fileId=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(file)))[..12];
        string name;
        do { name="__tscs_binding_"+fileId+"_"+(++bindingTemporaryOrdinal); }
        while(tokens.Any(token=>token.Kind==TokenKind.Identifier && token.Text==name));
        return new Token(TokenKind.Identifier,name,location.Start,location.Length);
    }

    // Declaration initializers and nested defaults have distinct '=' roles.
    private BindingPattern ReadBindingPattern(int depth,bool allowDefault=false)
    {
        var pattern=ReadBindingPatternCore(depth);
        if(allowDefault && Eat(TokenKind.Equal))
        {
            var location=Current;
            pattern=new BoundDefault(pattern,ParseValue(),location);
        }
        return pattern;
    }

    private BindingPattern ReadBindingPatternCore(int depth)
    {
        if(depth>32)Unsupported(Current,"Binding pattern nesting exceeds the supported limit.");
        if(Eat(TokenKind.Open))
        {
            var members=new List<(Token Key,BindingPattern Target)>();
            while(Current.Kind!=TokenKind.Close)
            {
                if(members.Count>=64)Unsupported(Current,"Binding pattern has too many members.");
                var key=Current;
                if(key.Kind!=TokenKind.Identifier)
                    Unsupported(key,"Only static identifier object binding keys are supported.");
                Take();
                bool renamed=Eat(TokenKind.Colon);
                if(!renamed && depth>=32)
                    Unsupported(key,"Binding pattern nesting exceeds the supported limit.");
                BindingPattern binding;
                if(renamed)
                    binding=ReadBindingPattern(depth+1,allowDefault:true);
                else
                {
                    binding=new BoundName(key);
                    if(Eat(TokenKind.Equal))
                    {
                        binding=new BoundDefault(binding,ParseValue(),key);
                    }
                }
                members.Add((key,binding));
                if(!Eat(TokenKind.Comma))break;
            }
            if(members.Count==0)Unsupported(Current,"Empty binding patterns are not supported.");
            if(!Eat(TokenKind.Close))
                Unsupported(Current,"Expected '}' after object binding pattern.");
            return new BoundObject(members);
        }
        if(Current.Kind==TokenKind.OpenBracket)
        {
            var open=Take();
            var elements=new List<BindingPattern?>();
            while(Current.Kind!=TokenKind.CloseBracket)
            {
                if(elements.Count>=64)Unsupported(Current,"Array binding pattern has too many elements.");
                if(Eat(TokenKind.Comma)) { elements.Add(null);continue; }
                elements.Add(ReadBindingPattern(depth+1,allowDefault:true));
                if(!Eat(TokenKind.Comma))break;
            }
            if(elements.All(item=>item is null))
                Unsupported(Current,"Empty array binding patterns are not supported.");
            if(!Eat(TokenKind.CloseBracket))
                Unsupported(Current,"Expected ']' after array binding pattern.");
            return new BoundArray(open,elements);
        }
        if(Current.Kind!=TokenKind.Identifier)
            Unsupported(Current,"Only finite identifier, object and array bindings are supported.");
        return new BoundName(Take());
    }

    private void LowerBindingPattern(BindingPattern pattern,ValueNode source,bool constant,
        bool exported,List<Statement> target,int depth=0,bool annotatedBinding=false,
        ValueNode? freshSource=null)
    {
        if(depth>32)throw new InvalidOperationException("Validated binding pattern depth exceeded.");
        switch(pattern)
        {
            case BoundName name:
                target.Add(new Variable(name.Name,constant,null,source,exported));
                return;
            case BoundDefault defaulted:
                // Property/index access is evaluated once, even with getters.
                // Only undefined triggers the lazy fallback. 'void 0' cannot
                // be shadowed and existing branch checking narrows the read.
                var candidate=NewBindingTemporary(defaulted.Location);
                target.Add(new Variable(candidate,true,null,source));
                var read=new ValueAtom(candidate with {Start=candidate.Start+1});
                var undefined=new ValueUnary(
                    new Token(TokenKind.Void,"void",defaulted.Location.Start,4),
                    new ValueAtom(new Token(TokenKind.Number,"0",defaulted.Location.Start,1)));
                var missing=new ValueBinary(read,
                    new Token(TokenKind.StrictEqual,"===",defaulted.Location.Start,3),undefined,
                    GeneratedUndefinedCheck:true);
                var selected=new ValueConditional(missing,
                    new Token(TokenKind.Question,"?",defaulted.Location.Start,1),
                    defaulted.Fallback,
                    new Token(TokenKind.Colon,":",defaulted.Location.Start,1),
                    new ValueAtom(candidate with {Start=candidate.Start+1}),BindingDefault:true,
                    BindingDefaultConstrained:annotatedBinding);
                if(defaulted.Target is BoundName)
                    LowerBindingPattern(defaulted.Target,selected,constant,exported,target,depth,annotatedBinding);
                else
                {
                    // Do not re-evaluate a lazy fallback for each nested child.
                    var receiver=NewBindingTemporary(defaulted.Location);
                    target.Add(new Variable(receiver,true,null,selected));
                    LowerBindingPattern(defaulted.Target,
                        new ValueAtom(receiver with {Start=receiver.Start+1}),constant,exported,target,depth,annotatedBinding);
                }
                return;
            case BoundObject objectPattern:
                // A directly fresh initializer contextually supplies omitted
                // defaulted keys; a previously stored receiver does not.
                var fresh=freshSource;
                while(fresh is ValueParenthesized grouped)fresh=grouped.Expression;
                var freshObject=fresh as ValueObject;
                bool reliableFresh=freshObject is not null &&
                    freshObject.Properties.All(member=>!member.Spread && !member.Computed);
                foreach(var (key,binding) in objectPattern.Members)
                {
                    var written=reliableFresh
                        ?freshObject!.Properties.LastOrDefault(member=>PropertyName(member.Name)==key.Text)
                        :null;
                    bool omitted=reliableFresh && !annotatedBinding &&
                        written is null && binding is BoundDefault;
                    LowerBindingChild(binding,
                        new ValuePropertyAccess(source,key,BindingFreshOmittedDefault:omitted),
                        key,constant,exported,target,depth+1,annotatedBinding,written?.Value);
                }
                return;
            case BoundArray arrayPattern:
                var direct=freshSource;
                while(direct is ValueParenthesized grouped)direct=grouped.Expression;
                var directArray=direct as ValueArray;
                bool reliableArray=directArray is not null &&
                    directArray.Elements.All(element=>element is not ValueArraySpread);
                for(var index=0;index<arrayPattern.Elements.Count;index++)
                {
                    var binding=arrayPattern.Elements[index];
                    if(binding is null)continue;
                    var number=index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var numberToken=new Token(TokenKind.Number,number,arrayPattern.Open.Start,number.Length);
                    var closing=new Token(TokenKind.CloseBracket,"]",arrayPattern.Open.Start,1);
                    LowerBindingChild(binding,
                        new ValueElementAccess(source,arrayPattern.Open,new ValueAtom(numberToken),closing,BindingArrayElement:true),
                        arrayPattern.Open,constant,exported,target,depth+1,annotatedBinding,
                        reliableArray && index<directArray!.Elements.Count?directArray.Elements[index]:null);
                }
                return;
            default:
                throw new InvalidOperationException("Unexpected validated binding pattern.");
        }
    }

    private void LowerBindingChild(BindingPattern binding,ValueNode read,Token location,
        bool constant,bool exported,List<Statement> target,int depth,bool annotatedBinding,
        ValueNode? freshSource=null)
    {
        if(binding is BoundName or BoundDefault)
        {
            LowerBindingPattern(binding,read,constant,exported,target,depth,annotatedBinding,freshSource);
            return;
        }
        var temporary=NewBindingTemporary(location);
        target.Add(new Variable(temporary,true,null,read));
        LowerBindingPattern(binding,new ValueAtom(temporary with {Start=temporary.Start+1}),constant,exported,target,depth,annotatedBinding,freshSource);
    }
    public Block Parse(int depth=0,bool inFunction=false,bool switchClauseBody=false,bool singleStatementBody=false)
    {
        if(depth>128) Unsupported(Current,"Block nesting exceeds the bootstrap limit.");
        var statements=new List<Statement>();
        while(Current.Kind!=TokenKind.End && !(depth>0 && Current.Kind==TokenKind.Close) &&
              !(switchClauseBody && (Current.Kind is TokenKind.Case or TokenKind.Default)) &&
              (!singleStatementBody || statements.Count==0))
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
                if(Current.Kind!=TokenKind.String || IsNoSubstitutionTemplate(Current))
                    Unsupported(Current,"Expected a quoted string module specifier.");
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
                // Distinguish "export type { T }" from "export type T = ...".
                bool typeOnlyExport=Current.Kind==TokenKind.Type &&
                    position+1<tokens.Count && tokens[position+1].Kind==TokenKind.Open;
                if(typeOnlyExport)Take();
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
                        if(Current.Kind!=TokenKind.String || IsNoSubstitutionTemplate(Current))
                            Unsupported(Current,"Expected a quoted string module specifier in a re-export.");
                        from=Take();
                        if(exports.Count==0)
                            Unsupported(from.Value,"An empty re-export list is not supported.");
                    }
                    statements.Add(new ExportDeclaration(exports,from,typeOnlyExport));
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
                    if(Current.Kind==TokenKind.Extends)
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
                // Bind finite typed pattern parameters through ordinary hidden
                // positional parameters and checked function-entry locals.
                var parameterBindings=new List<Statement>();
                var parameterElementDefaults=new List<BoundDefault>();
                var userParameterNames=new HashSet<string>(StringComparer.Ordinal);
                var patternedParameterNames=new HashSet<string>(StringComparer.Ordinal);
                bool patternWithDefaultBeforeNext=false;
                bool sawOptionalParameter=false;
                bool sawDefaultParameter=false;
                bool sawRestParameter=false;
                if(Current.Kind!=TokenKind.CloseParen)
                {
                    while(true)
                    {
                        // Pattern element defaults execute during parameter binding
                        // in TypeScript, but the finite lowering occurs at body entry.
                        // Reject subsequent parameters instead of reordering effects
                        // or observing a later parameter outside its TDZ.
                        if(patternWithDefaultBeforeNext)
                            Unsupported(Current,"Parameters after a binding pattern with defaults are not supported.");
                        if(sawRestParameter)
                            Diagnostics.Add(Diagnostic.Error(file,Current,"TS1014",
                                "A rest parameter must be last in a parameter list."));
                        bool rest=Eat(TokenKind.Ellipsis);
                        BindingPattern? parameterPattern=null;
                        Token parameter;
                        if(Current.Kind is TokenKind.Open or TokenKind.OpenBracket)
                        {
                            if(rest)Unsupported(Current,"Rest binding-pattern parameters are not supported.");
                            var location=Current;
                            parameterPattern=ReadBindingPattern(0);
                            parameter=NewBindingTemporary(location);
                        }
                        else
                        {
                            parameter=Current;
                            if(parameter.Kind!=TokenKind.Identifier)
                                Unsupported(parameter,"Expected a named or finite binding-pattern parameter.");
                            Take();
                        }
                        bool questionOptional=Eat(TokenKind.Question);
                        if(parameterPattern is not null && questionOptional)
                            Unsupported(parameter,"Binding-pattern parameters cannot be marked optional; use an explicit default.");
                        var exposedNames=new List<Token>();
                        if(parameterPattern is not null)
                            CollectParameterBindingNames(parameterPattern,exposedNames);
                        else
                            exposedNames.Add(parameter);
                        if(parameterPattern is not null)
                        {
                            var defaults=new List<BoundDefault>();
                            CollectBindingDefaults(parameterPattern,defaults);
                            var localNames=exposedNames.Select(name=>name.Text)
                                .ToHashSet(StringComparer.Ordinal);
                            foreach(var fallback in defaults)
                            {
                                if(DefaultReferencesAny(fallback.Fallback,localNames))
                                    Unsupported(fallback.Location,
                                        "A parameter element default cannot reference names bound by its own destructuring pattern.");
                            }
                            parameterElementDefaults.AddRange(defaults);
                        }
                        foreach(var boundName in exposedNames)
                        {
                            if(!userParameterNames.Add(boundName.Text) &&
                               (parameterPattern is not null || patternedParameterNames.Contains(boundName.Text)))
                                Diagnostics.Add(Diagnostic.Error(file,boundName,"TS2300",
                                    $"Duplicate identifier '{boundName.Text}'."));
                            if(parameterPattern is not null)
                                patternedParameterNames.Add(boundName.Text);
                        }
                        if(!Eat(TokenKind.Colon))Unsupported(Current,"Function parameters require an explicit supported type.");
                        var annotation=ReadTypeAnnotation();
                        ValueNode? defaultValue=null;
                        if(Eat(TokenKind.Equal))
                        {
                            defaultValue=ParseValue();
                            if(parameterPattern is not null &&
                               DefaultReferencesAny(defaultValue,exposedNames.Select(name=>name.Text)
                                   .ToHashSet(StringComparer.Ordinal)))
                                Unsupported(parameter,
                                    "A whole-parameter default cannot reference its own destructured names.");
                        }
                        // An earlier parameter initializer executes before this
                        // pattern binds its names; body-entry lowering cannot
                        // reproduce an outer-name capture or TDZ dependency.
                        if(parameterPattern is not null && sawDefaultParameter)
                            Unsupported(parameter,"Binding patterns after a defaulted parameter are not supported.");
                        if(parameterBindings.Count>0 && defaultValue is not null)
                            Unsupported(parameter,"Later parameter defaults after a destructured parameter are not supported.");
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
                        if(parameterPattern is not null)
                        {
                            // Parameter bindings are mutable (not const). Reuse
                            // the ordinary finite binding/default semantics.
                            LowerBindingPattern(parameterPattern,
                                new ValueAtom(parameter with {Start=parameter.Start+1}),
                                false,false,parameterBindings,annotatedBinding:true);
                            patternWithDefaultBeforeNext|=
                                PatternContainsDefault(parameterPattern) || defaultValue is not null;
                        }
                        if(!Eat(TokenKind.Comma))break;
                    }
                }
                if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after function parameters.");
                if(!Eat(TokenKind.Colon))Unsupported(Current,"Function return types must be explicit in this compiler subset.");
                var result=ReadTypeAnnotation();
                if(!Eat(TokenKind.Open))Unsupported(Current,"Expected '{' before function body.");
                var body=Parse(depth+1,true);
                if(!Eat(TokenKind.Close))Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
                if(parameterElementDefaults.Count>0)
                {
                    // Body declarations have no scope in native parameter
                    // initializers, but would shadow the same names after our
                    // body-entry lowering. Reject that unsupported dependency
                    // instead of silently calling/reading a different symbol.
                    var bodyNames=new HashSet<string>(StringComparer.Ordinal);
                    foreach(var statement in body.Statements)
                    {
                        switch(statement)
                        {
                            case Variable local: bodyNames.Add(local.Name.Text);break;
                            case FunctionDeclaration localFunction:
                                bodyNames.Add(localFunction.Name.Text);break;
                        }
                    }
                    foreach(var fallback in parameterElementDefaults)
                    {
                        if(DefaultReferencesAny(fallback.Fallback,bodyNames))
                            Unsupported(fallback.Location,
                                "Parameter element defaults cannot reference function-body declarations.");
                    }
                }
                if(parameterBindings.Count>0)
                    body=new Block([..parameterBindings,..body.Statements]);
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
                EndStatement(rejectTemplateContinuation:true);
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
                EndStatement(rejectTemplateContinuation:returnExpression is not null);
                continue;
            }
            if(Current.Kind is TokenKind.Let or TokenKind.Const)
            {
                bool constant=Take().Kind==TokenKind.Const;
                while(true)
                {
                    if(Current.Kind is TokenKind.Open or TokenKind.OpenBracket)
                    {
                        var location=Current;
                        var pattern=ReadBindingPattern(0);
                        var patternType=Eat(TokenKind.Colon)?ReadTypeAnnotation():null;
                        if(!Eat(TokenKind.Equal))
                            Unsupported(Current,"Variable binding patterns require an initializer.");
                        var source=ParseValue();
                        var temporary=NewBindingTemporary(location);
                        statements.Add(new Variable(temporary,true,patternType,source));
                        LowerBindingPattern(pattern,new ValueAtom(temporary with {Start=temporary.Start+1}),constant,exported,statements,
                             annotatedBinding:patternType is not null,
                             freshSource:patternType is null?source:null);
                        if(Eat(TokenKind.Comma))continue;
                        EndStatement(rejectTemplateContinuation:true);
                        break;
                    }
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
                    EndStatement(rejectTemplateContinuation:initializer is not null);break;
                }
                continue;
            }
            var value=ParseValue();
            if(Current.Kind is TokenKind.PlusEqual or TokenKind.MinusEqual or TokenKind.StarEqual or TokenKind.PowerEqual or TokenKind.SlashEqual or TokenKind.PercentEqual or TokenKind.AmpEqual or TokenKind.BarEqual or TokenKind.CaretEqual or TokenKind.LeftShiftEqual or TokenKind.RightShiftEqual or TokenKind.UnsignedRightShiftEqual or TokenKind.AmpAmpEqual or TokenKind.BarBarEqual or TokenKind.QuestionQuestionEqual)
            {
                var op=Take();
                if(value is not (ValueAtom { Token.Kind:TokenKind.Identifier } or ValuePropertyAccess or ValueElementAccess) ||
                   HasOptionalAccess(value))
                    Unsupported(op,"An optional chain cannot be a compound assignment target.");
                statements.Add(new CompoundAssignment(value,op,ParseValue()));
            }
            else if(Eat(TokenKind.Equal))
            {
                if(HasOptionalAccess(value))Unsupported(Current,"An optional chain cannot be an assignment target.");
                if(value is ValueAtom atom && atom.Token.Kind==TokenKind.Identifier)
                    statements.Add(new Assignment(atom.Token,ParseValue()));
                else if(value is ValuePropertyAccess property)
                    statements.Add(new PropertyAssignment(property,ParseValue()));
                else if(value is ValueElementAccess element)
                    statements.Add(new ElementAssignment(element,ParseValue()));
                else Unsupported(Current,"Only identifier, named-property or array-element assignment targets are supported.");
            }
            else statements.Add(new Expression(value));
            EndStatement(rejectTemplateContinuation:true);
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

    // Both braced arms and a bounded unbraced statement sequence use the
    // same checked flow. Unbraced lexical declarations need a shared switch
    // scope that is not yet represented; reject those instead of misbinding.
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
            bool braced=Eat(TokenKind.Open);
            var body=Parse(depth+1,inFunction,switchClauseBody:!braced);
            if(braced)
            {
                if(!Eat(TokenKind.Close))
                    Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
            }
            else if(body.Statements.Any(statement=>statement is Variable or FunctionDeclaration or
                            TypeAliasDeclaration or InterfaceDeclaration))
                Unsupported(keywordClause,
                    "Unbraced switch clauses with lexical declarations are not supported yet.");
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
                var body=ParseLoopBody(depth,inFunction);
                return new ForOfStatement(keyword,name,constant,source,body);
            }
            if(Eat(TokenKind.In))
            {
                var source=ParseValue();
                if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after for..in source.");
                var body=ParseLoopBody(depth,inFunction);
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
        var statements=ParseLoopBody(depth,inFunction);
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
        if(Current.Kind is TokenKind.PlusEqual or TokenKind.MinusEqual or TokenKind.StarEqual or TokenKind.PowerEqual or TokenKind.SlashEqual or TokenKind.PercentEqual or TokenKind.AmpEqual or TokenKind.BarEqual or TokenKind.CaretEqual or TokenKind.LeftShiftEqual or TokenKind.RightShiftEqual or TokenKind.UnsignedRightShiftEqual or TokenKind.AmpAmpEqual or TokenKind.BarBarEqual or TokenKind.QuestionQuestionEqual)
        {
            var op=Take();
            if(expression is not (ValueAtom { Token.Kind:TokenKind.Identifier } or ValuePropertyAccess or ValueElementAccess) ||
               HasOptionalAccess(expression))
                Unsupported(op,"An optional chain cannot be an assignment target.");
            return new CompoundAssignment(expression,op,ParseValue());
        }
        if(Eat(TokenKind.Equal))
        {
            if(HasOptionalAccess(expression))Unsupported(Current,"An optional chain cannot be an assignment target.");
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

    // One loop body is either a braced block or one statement. The synthetic
    // Block reuses normal lexical checking, backedge flow and ES2015 emission.
    private Block ParseLoopBody(int depth,bool inFunction)
    {
        if(Eat(TokenKind.Open))
        {
            var braced=Parse(depth+1,inFunction);
            if(!Eat(TokenKind.Close))
                Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
            return braced;
        }
        if(Current.Kind is TokenKind.Let or TokenKind.Const)
            Unsupported(Current,"Unbraced loop bodies cannot contain lexical declarations.");
        var single=Parse(depth+1,inFunction,singleStatementBody:true);
        if(single.Statements.Count!=1)
            Unsupported(Current,"Expected one statement after loop header.");
        return single;
    }

    // Both braced and single-statement while loops share checked flow.
    private WhileStatement ParseWhile(int depth,bool inFunction)
    {
        var keyword=Take();
        if(!Eat(TokenKind.OpenParen))Unsupported(Current,"Expected '(' after 'while'.");
        var condition=ParseValue();
        if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after while condition.");
        var body=ParseLoopBody(depth,inFunction);
        return new WhileStatement(keyword,condition,body);
    }

    // The body executes at least once. The condition is checked after the body,
    // including when a continue skips to the next condition test.
    private DoWhileStatement ParseDoWhile(int depth,bool inFunction)
    {
        var keyword=Take();
        var body=ParseLoopBody(depth,inFunction);
        if(!Eat(TokenKind.While))Unsupported(Current,"Expected 'while' after do body.");
        if(!Eat(TokenKind.OpenParen))Unsupported(Current,"Expected '(' after do/while.");
        var condition=ParseValue();
        if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after do/while condition.");
        EndStatement();
        return new DoWhileStatement(keyword,body,condition);
    }

    // One unbraced statement forms one branch; the nearest unmatched if
    // consumes its own else. A synthetic Block preserves checked branch scopes
    // and native ES2015 emission without a separate semantic pipeline.
    private Block ParseConditionalArm(int depth,bool inFunction,bool ifConsequent=false)
    {
        if(Eat(TokenKind.Open))
        {
            var body=Parse(depth+1,inFunction);
            if(!Eat(TokenKind.Close))
                Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
            return body;
        }
        if(Current.Kind is TokenKind.Let or TokenKind.Const)
            Unsupported(Current,"Lexical declarations in if/else arms require braces.");
        // TS1313 applies to a bare empty if consequent, not an else arm.
        if(ifConsequent && Current.Kind==TokenKind.Semi)
            Diagnostics.Add(Diagnostic.Error(file,Current,"TS1313",
                "The body of an 'if' statement cannot be the empty statement."));
        var single=Parse(depth+1,inFunction,singleStatementBody:true);
        if(single.Statements.Count!=1)
            Unsupported(Current,"Expected a statement after an if/else condition.");
        return single;
    }

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
            var consequent=ParseConditionalArm(depth,inFunction,ifConsequent:true);
            Statement? alternate=null;
            if(Eat(TokenKind.Else))
                alternate=Current.Kind==TokenKind.If
                    ?ParseConditional(depth+1,inFunction)
                    :ParseConditionalArm(depth,inFunction);
            return new ConditionalStatement(keyword,condition,consequent,alternate);
        }
        finally { conditionalNesting--; }
    }

    // The same bounded grammar handles aliases, interfaces, variables, function
    // signatures and nested members: postfix [] binds tighter than union |.
    // Disambiguate typed call signatures from parenthesized types.
    private bool IsFunctionTypeStart()
    {
        int parentheses=0;
        for(int i=position;i<tokens.Count;i++)
        {
            if(tokens[i].Kind==TokenKind.OpenParen)parentheses++;
            else if(tokens[i].Kind==TokenKind.CloseParen && --parentheses==0)
                return i+1<tokens.Count && tokens[i+1].Kind==TokenKind.Arrow;
            else if(tokens[i].Kind==TokenKind.End)break;
        }
        return false;
    }

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
        // A prefix readonly modifier applies to the outermost array only.
        // Other readonly type operators (including tuples) remain unsupported.
        if(Current.Kind==TokenKind.Identifier && Current.Text=="readonly")
        {
            var keyword=Take();
            var operand=ReadTypePart(depth+1);
            if(operand is not ArrayTypeSyntax array)
            {
                Unsupported(keyword,"The readonly type modifier currently requires an array type.");
                return default!;
            }
            if(array.GenericName is not null)
                Unsupported(keyword,"A readonly prefix requires T[] syntax, not Array<T> or ReadonlyArray<T>.");
            if(array.Readonly)
                Unsupported(keyword,"Repeated readonly array modifiers are not supported.");
            type=array with {Readonly=true};
        }
        else if(Current.Kind==TokenKind.Identifier && Current.Text=="keyof")
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
        else if(Current.Kind is TokenKind.Identifier or TokenKind.Void or TokenKind.Null)
        {
            var name=Take();
            if(name.Kind==TokenKind.Identifier &&
               name.Text is "Array" or "ReadonlyArray" &&
               Current.Kind==TokenKind.LessThan)
            {
                var open=Take();
                if(Current.Kind is TokenKind.GreaterThan or TokenKind.Comma)
                    Unsupported(Current,"Array<T> and ReadonlyArray<T> require exactly one type argument.");
                var element=ReadTypeAnnotation(depth+1);
                var close=TakeArrayTypeClose();
                type=new ArrayTypeSyntax(element,open,close,name.Text=="ReadonlyArray",name);
            }
            else
                type=name.Text is "number" or "string" or "boolean" or "any" or "void" or "undefined" or "null"
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
        else if(Current.Kind==TokenKind.OpenParen && IsFunctionTypeStart())
        {
            var open=Take();
            var parameters=new List<FunctionTypeParameter>();
            bool optionalSeen=false;
            while(Current.Kind!=TokenKind.CloseParen)
            {
                if(parameters.Count>=32)Unsupported(Current,"Function types with over 32 parameters are not supported yet.");
                bool rest=Eat(TokenKind.Ellipsis);
                if(Current.Kind!=TokenKind.Identifier)
                    Unsupported(Current,"Function type parameters require a name and explicit type.");
                var name=Take();
                bool optional=Eat(TokenKind.Question);
                if(rest && optional)Unsupported(name,"A rest parameter cannot be optional.");
                if(optionalSeen && !optional && !rest)
                    Unsupported(name,"A required parameter cannot follow an optional parameter.");
                if(!Eat(TokenKind.Colon))
                    Unsupported(Current,"Function type parameters require ':'.");
                var parameterType=ReadTypeAnnotation(depth+1);
                if(rest && parameterType is not ArrayTypeSyntax)
                    Unsupported(name,"Rest function type parameters require an array element type.");
                parameters.Add(new FunctionTypeParameter(name,parameterType,optional,rest));
                if(optional)optionalSeen=true;
                if(rest && Current.Kind!=TokenKind.CloseParen)
                    Unsupported(Current,"A rest parameter must be the final function type parameter.");
                if(!Eat(TokenKind.Comma))break;
            }
            if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after function type parameters.");
            if(!Eat(TokenKind.Arrow))Unsupported(Current,"Expected '=>' in function type.");
            type=new FunctionTypeSyntax(open,parameters,ReadTypeAnnotation(depth+1));
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
                    tokens[position+1].Kind is not (TokenKind.Colon or TokenKind.OpenParen);
                if(readonlyMember)Take();
                var name=ReadPropertyName(allowString:true,allowNumber:true);
                bool optionalMember=Eat(TokenKind.Question);
                if(!names.Add(PropertyName(name)))
                    Diagnostics.Add(Diagnostic.Error(file,name,"TS2300",$"Duplicate identifier '{PropertyName(name)}'."));
                // Unsupported(...) throws; seed definite assignment for C# flow analysis.
                TypeSyntax memberType = null!;
                if(Eat(TokenKind.Colon))
                    memberType=ReadTypeAnnotation(depth+1);
                else if(Current.Kind==TokenKind.OpenParen)
                {
                    if(readonlyMember)
                        Unsupported(name,"Readonly modifiers on method signatures are not supported.");
                    memberType=ReadObjectMethodType(depth+1);
                }
                else
                    Unsupported(Current,"Object type members require ':' or a typed method signature.");
                properties.Add(new ObjectTypeProperty(name,memberType,readonlyMember,optionalMember));
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


    // Method signatures are required or optional callable contracts, not executable
    // declarations. Reuse FunctionTypeSyntax so Binder, TypeResolver, structural
    // relations and cross-module callbacks share one semantic representation.
    private TypeSyntax ReadObjectMethodType(int depth)
    {
        if(depth>64)Unsupported(Current,"Method signature nesting exceeds the supported limit.");
        var open=Take();
        var parameters=new List<FunctionTypeParameter>();
        bool sawOptional=false;
        while(Current.Kind!=TokenKind.CloseParen)
        {
            if(parameters.Count>=32)Unsupported(Current,"Method signatures with over 32 parameters are unsupported.");
            bool rest=Eat(TokenKind.Ellipsis);
            if(Current.Kind!=TokenKind.Identifier)
                Unsupported(Current,"Method parameters require named explicit types.");
            var name=Take();
            bool optional=Eat(TokenKind.Question);
            if(rest && optional)
                Unsupported(name,"Rest method parameters cannot be optional.");
            if(sawOptional && !optional && !rest)
                Unsupported(name,"Required method parameters cannot follow optional parameters.");
            if(!Eat(TokenKind.Colon))
                Unsupported(Current,"Method parameters require a type annotation.");
            var type=ReadTypeAnnotation(depth+1);
            if(rest && type is not ArrayTypeSyntax)
                Unsupported(name,"Method rest parameters require an array type.");
            parameters.Add(new FunctionTypeParameter(name,type,optional,rest));
            if(optional)sawOptional=true;
            if(rest && Current.Kind!=TokenKind.CloseParen)
                Unsupported(Current,"Rest method parameters must be final.");
            if(!Eat(TokenKind.Comma))break;
        }
        if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' in method signature.");
        if(!Eat(TokenKind.Colon))Unsupported(Current,"Method signature requires a return type.");
        return new FunctionTypeSyntax(open,parameters,ReadTypeAnnotation(depth+1));
    }

    internal static string PropertyName(Token token)=>token.Kind is TokenKind.String or TokenKind.Number
        ? token.Value??token.Text : token.Text;

    private Token ReadPropertyName(bool allowString,bool allowNumber=false)
    {
        // Only non-negative safe integers have an unambiguous static
        // property identity. Fractional/exponent keys are not in this subset.
        if(allowNumber && Current.Kind==TokenKind.Number &&
           long.TryParse(Current.Value??Current.Text,System.Globalization.NumberStyles.None,
               System.Globalization.CultureInfo.InvariantCulture,out var integerKey) &&
           integerKey is >= 0 and <= 9007199254740991L)
            return Take();
        if(Current.Kind is TokenKind.Identifier or TokenKind.Let or TokenKind.Const or
            TokenKind.Import or TokenKind.Export or TokenKind.Type or TokenKind.Interface or TokenKind.Extends or TokenKind.Function or TokenKind.Return or TokenKind.Void or TokenKind.Delete or TokenKind.Throw or TokenKind.Try or TokenKind.Catch or TokenKind.Finally or
            TokenKind.If or TokenKind.Else or TokenKind.For or TokenKind.While or TokenKind.Do or TokenKind.Break or TokenKind.Continue or TokenKind.In or TokenKind.Typeof or TokenKind.True or TokenKind.False ||
            (allowString && Current.Kind==TokenKind.String && !IsNoSubstitutionTemplate(Current)))return Take();
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
                    bool computed=Eat(TokenKind.OpenBracket);
                    if(computed && Current.Kind is not (TokenKind.String or TokenKind.Number))
                        Unsupported(Current,"Only static string or integer computed object keys are supported.");
                    var name=ReadPropertyName(allowString:true,allowNumber:true);
                    if(computed && !Eat(TokenKind.CloseBracket))
                        Unsupported(Current,"Expected ']' after a computed object key.");
                    if(PropertyName(name)=="__proto__")
                        Unsupported(name,"Object prototype setter syntax is not supported.");
                    if(!names.Add(PropertyName(name)))
                        Diagnostics.Add(Diagnostic.Error(file,name,"TS1117","An object literal cannot have multiple properties with the same name."));
                    if(Eat(TokenKind.Colon))
                        properties.Add(new ObjectProperty(name,ParseWithInAllowed(ParseValue),Computed:computed));
                    else if(Current.Kind==TokenKind.OpenParen)
                        properties.Add(new ObjectProperty(name,ParseObjectMethod(name),Method:true,Computed:computed));
                    else if(!computed && name.Kind==TokenKind.Identifier &&
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

    // Method shorthand has its own ES2015 runtime form: it is not an
    // arrow and does not acquire a function-valued property's prototype.
    // The checked body reuses the ordinary typed function-expression contract.
    private ValueArrow ParseObjectMethod(Token name)
    {
        if(++arrowNesting>32)
            Unsupported(name,"Object method nesting exceeds the compiler subset limit.");
        try
        {
            if(!Eat(TokenKind.OpenParen))
                Unsupported(Current,"Expected '(' after object method name.");
            var bindings=new ExpressionParameterBindings();
            var parameters=ParseTypedExpressionParameters(bindings);
            TypeSyntax? result=Eat(TokenKind.Colon)?ReadTypeAnnotation():null;
            if(!Eat(TokenKind.Open))
                Unsupported(Current,"Object methods require a braced body.");
            var body=Parse(1,true);
            if(!Eat(TokenKind.Close))
                Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
            return new ValueArrow(name,parameters,result,null,
                FinishExpressionParameterBindings(body,bindings),FunctionExpression:true);
        }
        finally { arrowNesting--; }
    }

    // Single precedence ladder shared by variable, call, return, assignment
    // and conditional expression sites. The bounded type checker must reject
    // unsupported operand families rather than pretending JS coercion works.
    private ValueNode ParseValue()=>LooksLikeTypedArrow()?ParseTypedArrow():ParseConditionalValue();

    // Distinguish a typed arrow from a parenthesized value; never infer an
    // implicit-any parameter from an unsupported JavaScript lambda.
    private bool LooksLikeTypedArrow()
    {
        if(Current.Kind!=TokenKind.OpenParen)return false;
        int nesting=0;
        for(int i=position;i<tokens.Count && i-position<1024;i++)
        {
            if(tokens[i].Kind==TokenKind.OpenParen)nesting++;
            else if(tokens[i].Kind==TokenKind.CloseParen && --nesting==0)
            {
                if(i+1>=tokens.Count || tokens[i+1].Kind is not (TokenKind.Arrow or TokenKind.Colon))
                    return false;
                // A ':' after an ordinary parenthesized conditional is its
                // *outer* ternary separator, not an arrow return annotation.
                // Require an empty list or a named typed parameter; optional
                // parameters have '?' before ':'. Do not confuse ternaries
                // with return-type annotations.
                return i==position+1 || LooksLikeTypedBindingArrowParameter(i) ||
                    (position+2<i && tokens[position+1].Kind==TokenKind.Identifier &&
                     (tokens[position+2].Kind==TokenKind.Colon ||
                      (position+3<i && tokens[position+2].Kind==TokenKind.Question &&
                       tokens[position+3].Kind==TokenKind.Colon))) ||
                    (position+3<i && tokens[position+1].Kind==TokenKind.Ellipsis &&
                     tokens[position+2].Kind==TokenKind.Identifier &&
                     (tokens[position+3].Kind==TokenKind.Colon ||
                      (position+4<i && tokens[position+3].Kind==TokenKind.Question &&
                       tokens[position+4].Kind==TokenKind.Colon)));
            }
            else if(tokens[i].Kind==TokenKind.End)return false;
        }
        return false;
    }


    // Recognise only an explicitly typed finite binding-pattern parameter.
    // A parenthesized value followed by a ternary ':' is not an arrow.
    private bool LooksLikeTypedBindingArrowParameter(int closingParen)
    {
        if(position+1>=closingParen ||
           tokens[position+1].Kind is not (TokenKind.Open or TokenKind.OpenBracket))
            return false;
        int depth=0;
        for(int i=position+1;i<closingParen;i++)
        {
            if(tokens[i].Kind is TokenKind.Open or TokenKind.OpenBracket)depth++;
            else if(tokens[i].Kind is TokenKind.Close or TokenKind.CloseBracket)
            {
                if(--depth==0)
                    return i+1<closingParen && tokens[i+1].Kind==TokenKind.Colon;
            }
        }
        return false;
    }

    private sealed class ExpressionParameterBindings
    {
        public List<Statement> Locals { get; } = [];
        public List<BoundDefault> ElementDefaults { get; } = [];
    }

    private Block FinishExpressionParameterBindings(Block body,ExpressionParameterBindings bindings)
    {
        if(bindings.ElementDefaults.Count>0)
        {
            // Native parameter initializers cannot capture function-body locals.
            var bodyNames=new HashSet<string>(StringComparer.Ordinal);
            foreach(var statement in body.Statements)
            {
                switch(statement)
                {
                    case Variable local:bodyNames.Add(local.Name.Text);break;
                    case FunctionDeclaration localFunction:bodyNames.Add(localFunction.Name.Text);break;
                }
            }
            foreach(var fallback in bindings.ElementDefaults)
                if(DefaultReferencesAny(fallback.Fallback,bodyNames))
                    Unsupported(fallback.Location,
                        "Parameter element defaults cannot reference function-body declarations.");
        }
        return bindings.Locals.Count==0?body:new Block([..bindings.Locals,..body.Statements]);
    }

    // Typed arrows, function expressions and concise object methods use the
    // same parameter checking and finite destructuring lowering.
    private List<ParameterDeclaration> ParseTypedExpressionParameters(ExpressionParameterBindings bindings)
    {
        var parameters=new List<ParameterDeclaration>();
        var names=new HashSet<string>(StringComparer.Ordinal);
        bool sawOptionalParameter=false;
        bool sawDefaultParameter=false;
        bool sawRestParameter=false;
        bool patternedDefaultBeforeNext=false;
        while(Current.Kind!=TokenKind.CloseParen)
        {
            if(patternedDefaultBeforeNext)
                Unsupported(Current,"Parameters after a binding pattern with defaults are not supported.");
            if(sawRestParameter)
                Diagnostics.Add(Diagnostic.Error(file,Current,"TS1014",
                    "A rest parameter must be last in a parameter list."));
            if(parameters.Count>=32)Unsupported(Current,"Arrow parameter count exceeds the supported limit.");
            bool rest=Eat(TokenKind.Ellipsis);
            BindingPattern? pattern=null;
            Token name;
            if(Current.Kind is TokenKind.Open or TokenKind.OpenBracket)
            {
                if(rest)Unsupported(Current,"Rest binding-pattern parameters are not supported.");
                var location=Current;
                pattern=ReadBindingPattern(0);
                name=NewBindingTemporary(location);
            }
            else
            {
                if(Current.Kind!=TokenKind.Identifier)
                    Unsupported(Current,"Arrow parameters require explicitly named type annotations.");
                name=Take();
            }
            var exposedNames=new List<Token>();
            if(pattern is not null)
            {
                CollectParameterBindingNames(pattern,exposedNames);
                var defaults=new List<BoundDefault>();
                CollectBindingDefaults(pattern,defaults);
                var localNames=exposedNames.Select(item=>item.Text).ToHashSet(StringComparer.Ordinal);
                foreach(var fallback in defaults)
                    if(DefaultReferencesAny(fallback.Fallback,localNames))
                        Unsupported(fallback.Location,
                            "A parameter element default cannot reference names bound by its own destructuring pattern.");
                bindings.ElementDefaults.AddRange(defaults);
            }
            else exposedNames.Add(name);
            foreach(var exposed in exposedNames)
                if(!names.Add(exposed.Text))
                    Diagnostics.Add(Diagnostic.Error(file,exposed,"TS2300",
                        $"Duplicate identifier '{exposed.Text}'."));
            bool questionOptional=Eat(TokenKind.Question);
            if(pattern is not null && questionOptional)
                Unsupported(name,"Binding-pattern parameters cannot be marked optional; use an explicit default.");
            if(!Eat(TokenKind.Colon))
                Unsupported(Current,"Arrow parameters require explicit type annotations.");
            var annotation=ReadTypeAnnotation();
            var defaultValue=Eat(TokenKind.Equal)?ParseValue():null;
            if(pattern is not null && defaultValue is not null &&
               DefaultReferencesAny(defaultValue,exposedNames.Select(item=>item.Text)
                   .ToHashSet(StringComparer.Ordinal)))
                Unsupported(name,"A whole-parameter default cannot reference its own destructured names.");
            if(pattern is not null && sawDefaultParameter)
                Unsupported(name,"Binding patterns after a defaulted parameter are not supported.");
            if(bindings.Locals.Count>0 && defaultValue is not null)
                Unsupported(name,"Later parameter defaults after a destructured parameter are not supported.");
            if(rest)
            {
                if(annotation is not ArrayTypeSyntax || questionOptional)
                    Diagnostics.Add(Diagnostic.Error(file,name,"TS2370",
                        "A rest parameter must be of an array type."));
                if(questionOptional)
                    Diagnostics.Add(Diagnostic.Error(file,name,"TS1047",
                        "A rest parameter cannot be optional."));
                if(defaultValue is not null)
                    Diagnostics.Add(Diagnostic.Error(file,name,"TS1048",
                        "A rest parameter cannot have an initializer."));
                sawRestParameter=true;
            }
            if(questionOptional && defaultValue is not null)
                Diagnostics.Add(Diagnostic.Error(file,name,"TS1015",
                    "Parameter cannot have question mark and initializer."));
            bool optional=questionOptional||defaultValue is not null||rest;
            if(sawDefaultParameter && !optional)
                Unsupported(name,"Required arrow parameters after defaults are not supported yet.");
            if(sawOptionalParameter && !optional && !sawRestParameter)
                Diagnostics.Add(Diagnostic.Error(file,name,"TS1016",
                    "A required parameter cannot follow an optional parameter."));
            sawOptionalParameter|=optional;
            sawDefaultParameter|=defaultValue is not null;
            parameters.Add(new ParameterDeclaration(name,annotation,optional,defaultValue,rest));
            if(pattern is not null)
            {
                LowerBindingPattern(pattern,
                    new ValueAtom(name with {Start=name.Start+1}),
                    false,false,bindings.Locals,annotatedBinding:true);
                patternedDefaultBeforeNext|=
                    PatternContainsDefault(pattern)||defaultValue is not null;
            }
            if(!Eat(TokenKind.Comma))break;
        }
        if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after arrow parameters.");
        return parameters;
    }

    // Function expressions reuse typed parameter grammar. Named forms bind a
    // private self name; recursive references require an explicit return type.
    private ValueNode ParseFunctionExpression()
    {
        var keyword=Take();
        if(++arrowNesting>32)
            Unsupported(keyword,"Callable expression nesting exceeds the compiler subset limit.");
        try
        {
            Token? name=Current.Kind==TokenKind.Identifier?Take():null;
            if(!Eat(TokenKind.OpenParen))
                Unsupported(Current,"Function expressions require a typed parameter list.");
            var bindings=new ExpressionParameterBindings();
            var parameters=ParseTypedExpressionParameters(bindings);
            TypeSyntax? returns=Eat(TokenKind.Colon)?ReadTypeAnnotation():null;
            if(!Eat(TokenKind.Open))
                Unsupported(Current,"Function expressions require a braced body.");
            var body=Parse(1,true);
            if(!Eat(TokenKind.Close))
                Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
            return new ValueArrow(keyword,parameters,returns,null,
                FinishExpressionParameterBindings(body,bindings),FunctionExpression:true,FunctionName:name);
        }
        finally {arrowNesting--;}
    }

    private ValueNode ParseTypedArrow()
    {
        var open=Take();
        if(++arrowNesting>32)Unsupported(open,"Arrow expression nesting exceeds the compiler subset limit.");
        try
        {
            var bindings=new ExpressionParameterBindings();
            var parameters=ParseTypedExpressionParameters(bindings);
            TypeSyntax? returns=Eat(TokenKind.Colon)?ReadTypeAnnotation():null;
            if(!Eat(TokenKind.Arrow))Unsupported(Current,"Expected '=>' after arrow signature.");
            if(Eat(TokenKind.Open))
            {
                // A typed parameter list is enough to infer a bounded braced
                // return contract in the checker; do not require an annotation.
                var body=Parse(1,true);
                if(!Eat(TokenKind.Close))
                    Diagnostics.Add(Diagnostic.Error(file,Current,"TS1005","'}' expected."));
                return new ValueArrow(open,parameters,returns,null,
                    FinishExpressionParameterBindings(body,bindings));
            }
            var expression=ParseWithInAllowed(ParseValue);
            if(bindings.Locals.Count==0)
                return new ValueArrow(open,parameters,returns,expression);
            // Keep the native lexical arrow; checked entry-local destructuring
            // runs before the expression's single return.
            var returned=new ReturnStatement(
                new Token(TokenKind.Return,"return",open.Start,6),expression);
            var expressionBody=new Block([returned]);
            return new ValueArrow(open,parameters,returns,null,
                FinishExpressionParameterBindings(expressionBody,bindings));
        }
        finally {arrowNesting--;}
    }
    // Lower precedence than ||; the false arm is right-associative.
    private ValueNode ParseConditionalValue()
    {
        var condition=ParseNullishCoalescing();
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
    // Coalescing binds below logical OR and above the conditional operator.
    // TS forbids unparenthesized mixing with && and ||.
    private ValueNode ParseNullishCoalescing()
    {
        var left=ParseLogicalOr();
        while(Current.Kind==TokenKind.QuestionQuestion)
        {
            var op=Take();
            if(IsBareLogical(left))
                Unsupported(op,"Nullish coalescing cannot mix with && or || without parentheses.");
            var right=ParseLogicalOr();
            if(IsBareLogical(right))
                Unsupported(op,"Nullish coalescing cannot mix with && or || without parentheses.");
            left=new ValueBinary(left,op,right);
        }
        return left;
    }
    private static bool IsBareLogical(ValueNode value)=>
        value is ValueBinary { Operator.Kind:TokenKind.AmpAmp or TokenKind.BarBar };

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
        while(Current.Kind is TokenKind.Minus or TokenKind.Plus or TokenKind.Bang or TokenKind.Typeof or TokenKind.Void or TokenKind.Delete or TokenKind.Tilde or TokenKind.PlusPlus or TokenKind.MinusMinus)prefixes.Add(Take());
        ValueNode value=ParseExponentiation();
        // An unparenthesized unary left operand of ** is invalid in TS/JS.
        if(prefixes.Count>0 && value is ValueBinary { Operator.Kind:TokenKind.Power } exponent)
            Unsupported(exponent.Operator,"Unary left operands of ** must be parenthesized.");
        if(prefixes.Any(p=>p.Kind is TokenKind.PlusPlus or TokenKind.MinusMinus) &&
           HasOptionalAccess(value))
            Unsupported(prefixes[0],"An optional chain cannot be an update target.");
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
    // Calls retain the callee expression, not just a named binding token.
    // Existing bounded spread/arity syntax stays unchanged.
    private ValueCall ParseCall(ValueNode callee,bool optional=false,bool chainContinuation=false)
    {
        var open=Take();
        if(++callNesting>64)Unsupported(open,"Nested calls exceed the compiler subset depth limit.");
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
                    if(spreadIndex>=0 && Current.Kind==TokenKind.Comma)
                        Unsupported(Current,"Only one trailing array spread argument is supported yet.");
                    if(!Eat(TokenKind.Comma))break;
                }
            }
            if(!Eat(TokenKind.CloseParen))Unsupported(Current,"Expected ')' after function arguments.");
            return new ValueCall(callee,open,arguments,spreadIndex,optional,chainContinuation);
        }
        finally {callNesting--;}
    }

    private static bool HasOptionalAccess(ValueNode node)
    {
        // Optional chains are never writable targets, even when a later
        // ordinary member follows an optional segment.
        while(true)
        {
            switch(node)
            {
                case ValuePropertyAccess property:
                    if(property.Optional)return true;
                    node=property.Receiver;
                    break;
                case ValueElementAccess element:
                    if(element.Optional)return true;
                    node=element.Receiver;
                    break;
                case ValueCall call:
                    if(call.Optional)return true;
                    node=call.Callee;
                    break;
                case ValueParenthesized grouped:node=grouped.Expression;break;
                case ValueTypeOperator typed:node=typed.Expression;break;
                case ValueNonNull asserted:node=asserted.Expression;break;
                default:return false;
            }
        }
    }

    private ValueNode ParsePrimary()
    {
        ValueNode value=ParsePrimaryValue();
        // A finite optional chain can continue through checked required properties,
        // indices and calls. The emitter guards the *whole* unparenthesized
        // continuation: the receiver and any later indices/arguments are skipped
        // when the optional segment short-circuits.
        // Grouping starts a new chain with ordinary JavaScript semantics.
        bool activeOptionalChain=false;
        bool continuedOptionalChain=false;
        bool optionalCallRoot=false;
        while(true)
        {
            if(Current.Kind==TokenKind.Dot)
            {
                if(activeOptionalChain && (optionalCallRoot || value is ValueNonNull))
                    Unsupported(Current,"This optional-chain continuation is not implemented.");
                Take();
                value=new ValuePropertyAccess(value,ReadPropertyName(allowString:false),
                    ChainContinuation:activeOptionalChain);
                continuedOptionalChain|=activeOptionalChain;
            }
            else if(Current.Kind==TokenKind.QuestionDot)
            {
                var optional=Take();
                if(continuedOptionalChain || value is ValueNonNull)
                    Unsupported(optional,"Multiple optional segments after a continuation are not implemented.");
                if(Current.Kind==TokenKind.OpenBracket)
                {
                    var open=Take();
                    var index=ParseWithInAllowed(ParseValue);
                    if(Current.Kind!=TokenKind.CloseBracket)
                        Unsupported(Current,"Expected ']' after optional indexed access.");
                    value=new ValueElementAccess(value,open,index,Take(),true);
                }
                else if(Current.Kind==TokenKind.Identifier)
                    value=new ValuePropertyAccess(value,ReadPropertyName(allowString:false),true);
                else if(Current.Kind==TokenKind.OpenParen)
                {
                    // A second optional segment after an optional property/call
                    // requires additional receiver semantics; fail closed.
                    if(activeOptionalChain || HasOptionalAccess(value))
                        Unsupported(optional,"Optional-call continuations after an optional access are not implemented.");
                    value=ParseCall(value,optional:true);
                    optionalCallRoot=true;
                }
                else Unsupported(optional,"Expected a property, index or call after '?.'.");
                activeOptionalChain=true;
            }
            else if(Current.Kind==TokenKind.OpenBracket)
            {
                if(activeOptionalChain && (optionalCallRoot || value is ValueNonNull))
                    Unsupported(Current,"This optional-chain continuation is not implemented.");
                var open=Take();
                var index=ParseWithInAllowed(ParseValue);
                if(Current.Kind!=TokenKind.CloseBracket)
                    Unsupported(Current,"Expected ']' after indexed access.");
                value=new ValueElementAccess(value,open,index,Take(),
                    ChainContinuation:activeOptionalChain);
                continuedOptionalChain|=activeOptionalChain;
            }
            else if(Current.Kind==TokenKind.OpenParen)
            {
                if(activeOptionalChain && (optionalCallRoot || value is ValueNonNull))
                    Unsupported(Current,"This optional-chain continuation is not implemented.");
                value=ParseCall(value,chainContinuation:activeOptionalChain);
                continuedOptionalChain|=activeOptionalChain;
            }
            else if(Current.Kind==TokenKind.Bang && !Current.LineBreak)
                value=new ValueNonNull(value,Take());
            else break;
        }
        // Same-line postfix binds after property/index access and before unary/binary.
        // A line break invokes statement separation instead.
        if(!Current.LineBreak && Current.Kind is TokenKind.PlusPlus or TokenKind.MinusMinus)
        {
            if(HasOptionalAccess(value))Unsupported(Current,"An optional chain cannot be an update target.");
            value=new ValuePostfixUpdate(value,Take());
        }
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
        if(token.Kind==TokenKind.Function)return ParseFunctionExpression();
        if(token.Kind==TokenKind.Template)return ParseInterpolatedTemplate();
        if(token.Kind==TokenKind.Identifier)return new ValueAtom(Take());
        if(token.Kind is TokenKind.Number or TokenKind.String or TokenKind.True or TokenKind.False or TokenKind.Null)
            return new ValueAtom(Take());
        if(token.Kind is TokenKind.Semi or TokenKind.End or TokenKind.Close)
        {
            Diagnostics.Add(Diagnostic.Error(file,token,"TS1109","Expression expected."));
            return new ValueAtom(new(TokenKind.Number,"0",token.Start,0));
        }
        Unsupported(token,"Only primitive, object, named-property, parenthesized and named-call expressions are supported.");
        return default!;
    }
    // Parse substitutions as genuine expression AST nodes. Only the token
    // boundary is special: no string matching bypasses normal type checking.
    private ValueTemplate ParseInterpolatedTemplate()
    {
        var literal=Take();
        var raw=literal.Text;
        var chunks=new List<string>();
        var expressions=new List<ValueNode>();
        int chunkStart=1;
        for(int i=1;i<raw.Length-1;i++)
        {
            if(raw[i]=='\\'){i++;continue;}
            if(raw[i]!='$' || i+1>=raw.Length-1 || raw[i+1]!='{')continue;
            chunks.Add(raw[chunkStart..i]);
            int begin=i+2;
            int closing=TemplateInterpolation.FindClose(raw,begin);
            if(closing<0 || closing>=raw.Length-1)
                Unsupported(literal,"Template substitution is not properly closed.");
            if(expressions.Count>=32)
                Unsupported(literal,"Template substitution count exceeds the bounded limit.");
            string embedded=raw[begin..closing];
            if(string.IsNullOrWhiteSpace(embedded))
                Unsupported(literal,"Template substitution requires an expression.");
            int offset=literal.Start+begin;
            var scanner=new Scanner(file,embedded);
            List<Token> nested;
            try {nested=scanner.Scan();}
            catch(CompilationFailure failure)
            {
                throw new CompilationFailure(failure.Diagnostic with
                    {Start=failure.Diagnostic.Start+offset});
            }
            if(scanner.Diagnostics.Count>0)
                throw new CompilationFailure(scanner.Diagnostics[0] with
                    {Start=scanner.Diagnostics[0].Start+offset});
            sourceScanner?.ImportSuppressedLines(scanner,offset);
            for(int j=0;j<nested.Count;j++)
                nested[j]=nested[j] with {Start=nested[j].Start+offset};
            // Keep the original source scanner for directives inside nested templates.
            var nestedParser=new Parser(file,nested,sourceScanner);
            expressions.Add(nestedParser.ParseEmbeddedExpression());
            i=closing;
            chunkStart=closing+1;
        }
        chunks.Add(raw[chunkStart..^1]);
        if(expressions.Count==0)
            Unsupported(literal,"Expected a template substitution.");
        return new ValueTemplate(literal,chunks,expressions);
    }

    private ValueNode ParseEmbeddedExpression()
    {
        var expression=ParseValue();
        if(Diagnostics.Count>0)
            throw new CompilationFailure(Diagnostics[0]);
        if(Current.Kind!=TokenKind.End)
            Unsupported(Current,"Template substitution must contain one complete expression.");
        return expression;
    }

    // Untagged templates are legal expressions; tag continuations cannot be
    // split by automatic semicolon insertion, even across a newline.
    private static bool IsNoSubstitutionTemplate(Token token) =>
        (token.Kind is TokenKind.String or TokenKind.Template) &&
        token.Text.Length>0 && token.Text[0]=='`';

    private void EndStatement(bool rejectTemplateContinuation=false)
    {
        if(rejectTemplateContinuation && IsNoSubstitutionTemplate(Current))
            Unsupported(Current,"Tagged template continuations are not supported.");
        if(Eat(TokenKind.Semi)||Current.Kind is TokenKind.End or TokenKind.Close||Current.LineBreak)return;
        Unsupported(Current,"This expression or statement form is not supported yet.");
    }
    private void Unsupported(Token token,string message)=>throw new CompilationFailure(Diagnostic.Error(file,token,"TSCS1001",message));
}

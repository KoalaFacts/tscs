namespace Tscs;

// Primitive branches of checkVariableLikeDeclaration/checkIdentifier/checkBinaryLikeExpression.
internal sealed class TypeChecker(string file,bool strictModule=false)
{
    private readonly List<Diagnostic> diagnostics=[];
    public IReadOnlyList<Diagnostic> Check(Block block) { CheckBlock(block,null);return diagnostics; }
    public IReadOnlyList<Diagnostic> CheckBound(Block block,Scope global)
    {
        CheckBlock(block,global,true);
        return diagnostics;
    }
    private void CheckBlock(Block block,Scope? parent,bool rootAlreadyBound=false,TypeSymbol? expectedReturn=null,bool allowBreak=false,bool allowContinue=false,bool preboundLocal=false)
    {
        var scope=rootAlreadyBound?parent!:Binder.Bind(file,block,parent);
        // Branch facts are lexical and never escape this block, including
        // exits through malformed-source error diagnostics.
        var originalFlowTypes=new Dictionary<Symbol,TypeSymbol?>();
        try
        {
            foreach(var statement in block.Statements)
            {
                switch(statement)
                {
                    case TypeAliasDeclaration or InterfaceDeclaration:
                        // Checked as a separate type namespace during binding.
                        break;
                    case Block nested: CheckBlock(nested,scope,false,expectedReturn,allowBreak,allowContinue);break;
                    case FunctionDeclaration function:
                        CheckStrictModuleBinding(function.Name);
                        var functionSymbol=scope.Symbols[function.Name.Text];
                        var functionType=(FunctionTypeSymbol)functionSymbol.Type!;
                        if(!FunctionControlFlow.DefinitelyReturnsValue(function.Body))
                            throw new CompilationFailure(Diagnostic.Error(file,function.Name,"TSCS1001",
                                "Only function bodies with a definite value return are supported yet."));
                        var parameters=new Scope(scope);
                        foreach(var parameter in function.Parameters)
                        {
                            CheckStrictModuleBinding(parameter.Name);
                            Binder.DeclareParameter(file,parameters,parameter);
                        }
                        CheckBlock(function.Body,parameters,false,functionType.ReturnType);
                        break;
                    case ImportDeclaration import:
                        // The imported name is an export property; only the local
                        // alias introduces a strict-mode module binding.
                        foreach(var specifier in import.Specifiers)
                            CheckStrictModuleBinding(specifier.Local);
                        break;
                    case ConditionalStatement conditional:
                        var continuation=CheckConditional(conditional,scope,expectedReturn,allowBreak,allowContinue);
                        if(continuation is { } afterGuard)
                        {
                            // Preserve original lexical type across all sibling
                            // statements; subsequent guards can refine it further.
                            originalFlowTypes.TryAdd(afterGuard.Symbol,afterGuard.Symbol.Type);
                            afterGuard.Symbol.Type=afterGuard.FallthroughType;
                        }
                        break;
                    case ForOfStatement loop:
                        CheckForOf(loop,scope,expectedReturn);
                        break;
                    case WhileStatement whileLoop:
                        CheckWhile(whileLoop,scope,expectedReturn);
                        break;
                    case ClassicForStatement classicLoop:
                        CheckClassicFor(classicLoop,scope,expectedReturn);
                        break;
                    case BreakStatement breaking:
                        if(!allowBreak)
                            throw new CompilationFailure(Diagnostic.Error(file,breaking.Keyword,"TSCS1001",
                                "Break statements require an enclosing switch or loop."));
                        break;
                    case ContinueStatement continuing:
                        if(!allowContinue)
                            throw new CompilationFailure(Diagnostic.Error(file,continuing.Keyword,"TSCS1001",
                                "Continue statements require an enclosing loop."));
                        break;
                    case SwitchStatement switching:
                        CheckSwitch(switching,scope,expectedReturn,allowContinue);
                        break;
                    case ReturnStatement returned:
                        if(expectedReturn==null)
                            throw new CompilationFailure(Diagnostic.Error(file,returned.Keyword,"TSCS1001",
                                "Return statements outside functions are not supported."));
                        var returnType=TypeOf(returned.Value,scope);
                        if(returnType!=null && CheckContextualLiteral(returned.Value,expectedReturn,returnType) &&
                           !IsAssignableContextually(returned.Value,returnType,expectedReturn))
                            Mismatch(returned.Keyword,returnType,expectedReturn);
                        break;
                    case Variable v:
                        if(v.Name.Text is "eval" or "arguments") Error(v.Name,"TS1100",$"Invalid use of '{v.Name.Text}' in strict mode.");
                        if((parent==null || (rootAlreadyBound && !preboundLocal)) && v.Name.Text=="globalThis") Error(v.Name,"TS2397","Declaration name conflicts with built-in global identifier 'globalThis'.");
                        var symbol=scope.Symbols[v.Name.Text];
                        if(v.Annotation==null && v.Initializer is ValueArray { Elements.Count:0 })
                            throw new CompilationFailure(Diagnostic.Error(file,v.Name,"TSCS1001",
                                "Empty array inference requires an explicit element type in this compiler subset."));
                        var source=v.Initializer is {} init?TypeOf(init,scope):null;
                        var annotatedType=v.Annotation is { } annotation
                            ? TypeResolver.Resolve(annotation,scope,file) : null;
                        symbol.Type=annotatedType??source;
                        symbol.Initialized=v.Initializer!=null;
                        if(v.Constant&&v.Initializer==null) Error(v.Name,"TS1155","'const' declarations must be initialized.");
                        if(annotatedType!=null && source!=null &&
                           CheckContextualLiteral(v.Initializer!,annotatedType,source) &&
                           !IsAssignableContextually(v.Initializer!,source,annotatedType))
                            Mismatch(v.Name,source,annotatedType);
                        break;
                    case Assignment a:
                        var target=Resolve(a.Name,scope);var assigned=TypeOf(a.Value,scope);
                        if(target==null)break;
                        if(target.Imported) Error(a.Name,"TS2632",$"Cannot assign to '{a.Name.Text}' because it is an import.");
                        else if(target.IsConstant) Error(a.Name,"TS2588",$"Cannot assign to '{a.Name.Text}' because it is a constant.");
                        else if(target.Type!=null && assigned!=null)
                        {
                            if(CheckContextualLiteral(a.Value,target.Type,assigned) &&
                               !IsAssignableContextually(a.Value,assigned,target.Type))Mismatch(a.Name,assigned,target.Type);
                            else target.Initialized=true;
                        }
                        else target.Initialized=true;
                        break;
                    case ElementAssignment elementAssignment:
                        var elementType=TypeOf(elementAssignment.Target,scope);
                        var elementValueType=TypeOf(elementAssignment.Value,scope);
                        if(elementType!=null && elementValueType!=null &&
                           CheckContextualLiteral(elementAssignment.Value,elementType,elementValueType) &&
                           !IsAssignableContextually(elementAssignment.Value,elementValueType,elementType))
                            Mismatch(elementAssignment.Target.Open,elementValueType,elementType);
                        break;
                    case PropertyAssignment assignment:
                        // Reading common members of a union is safe; writing one
                        // requires intersection-like relations not yet represented.
                        // Reject such writes rather than accepting an unsafe arm.
                        if(TypeOf(assignment.Target.Receiver,scope) is UnionTypeSymbol)
                            throw new CompilationFailure(Diagnostic.Error(file,assignment.Target.Name,"TSCS1001",
                                "Writes through union-typed receivers are not supported yet."));
                        // Mutating a property does not rebind a const or an import.
                        // TypeOf still enforces initialization of the receiver.
                        var propertyType=TypeOf(assignment.Target,scope);
                        var valueType=TypeOf(assignment.Value,scope);
                        if(propertyType!=null && valueType!=null &&
                           CheckContextualLiteral(assignment.Value,propertyType,valueType) &&
                           !IsAssignableContextually(assignment.Value,valueType,propertyType))
                            Mismatch(assignment.Target.Name,valueType,propertyType);
                        break;
                    case PostfixUpdate update:
                        var updated=Resolve(update.Name,scope);
                        if(updated==null)break;
                        if(!updated.Initialized)
                            throw new CompilationFailure(Diagnostic.Error(file,update.Name,"TSCS1001","Reads before initialization are not supported yet."));
                        if(updated.Imported)
                            Error(update.Name,"TS2632",$"Cannot assign to '{update.Name.Text}' because it is an import.");
                        else if(updated.IsConstant)
                            Error(update.Name,"TS2588",$"Cannot assign to '{update.Name.Text}' because it is a constant.");
                        else if(!CompilerTypes.IsNumber(updated.Type))
                            throw new CompilationFailure(Diagnostic.Error(file,update.Operator,"TSCS1001","Only number postfix updates are supported yet."));
                        else updated.Initialized=true;
                        break;
                    case Expression e: TypeOf(e.Value,scope);break;
                }
            }

        }
        finally
        {
            foreach(var (symbol,originalType) in originalFlowTypes)
                symbol.Type=originalType;
        }
    }
    
    // Case-local types view the existing symbol. A normally completing
    // clause can fall into the next one, so its effective incoming union
    // must be joined with the next case's direct-entry variants.
    private void CheckSwitch(SwitchStatement switching,Scope scope,TypeSymbol? expectedReturn,bool allowContinue)
    {
        var selectorType=TypeOf(switching.Selector,scope);
        var labels=new Dictionary<SwitchClause,LiteralTypeSymbol>();
        var selected=new HashSet<LiteralTypeSymbol>();
        foreach(var clause in switching.Clauses)
        {
            if(clause.Label is not { } label)continue;
            var type=TryReadLiteral(label);
            if(type==null)
                throw new CompilationFailure(Diagnostic.Error(file,clause.Keyword,"TSCS1001",
                    "Only primitive literal switch case labels are supported."));
            if(!selected.Add(type))
                throw new CompilationFailure(Diagnostic.Error(file,clause.Keyword,"TSCS1001",
                    "Duplicate switch case labels are not supported yet."));
            labels.Add(clause,type);
            if(selectorType!=null && !CompilerTypes.IsAssignable(type,selectorType))
                Error(clause.Keyword,"TS2678",
                    $"Type '{type.DisplayName}' is not comparable to type '{selectorType.DisplayName}'.");
        }

        Symbol? subject=null;
        UnionTypeSymbol? variants=null;
        string? memberName=null;
        // Share the same parenthesis-aware discriminant recognizer as if/else.
        // The selector's grouping must not erase the union branch information.
        if(TryReadDiscriminant(switching.Selector,out var variable,out var property))
        {
            var symbol=scope.Find(variable.Text);
            if(symbol?.Type is UnionTypeSymbol union &&
               union.Alternatives.All(candidate=>candidate is ObjectTypeSymbol shape &&
                   shape.Members.TryGetValue(property,out var member) &&
                   member is LiteralTypeSymbol))
            {
                subject=symbol;variants=union;memberName=property;
            }
        }

        TypeSymbol? incomingFallthrough=null;
        foreach(var clause in switching.Clauses)
        {
            var before=CaptureInitialized(scope);
            var originalType=subject?.Type;
            try
            {
                TypeSymbol? effectiveType=null;
                if(subject!=null && variants!=null && memberName!=null)
                {
                    var matching=new List<TypeSymbol>();
                    foreach(var candidate in variants.Alternatives)
                    {
                        var literal=(LiteralTypeSymbol)((ObjectTypeSymbol)candidate).Members[memberName];
                        if(clause.Label==null ? !selected.Contains(literal) : literal.Equals(labels[clause]))
                            matching.Add(candidate);
                    }
                    if(incomingFallthrough is { } flowing)matching.Add(flowing);
                    if(matching.Count>0)
                    {
                        effectiveType=CompilerTypes.Union(matching);
                        subject.Type=effectiveType;
                    }
                    // An unreachable clause retains the broad type, rather
                    // than inventing 'never' or silently accepting bad reads.
                }
                CheckBlock(clause.Body,scope,false,expectedReturn,true,allowContinue);
                // Only a normal exit enters the next clause. Return and break
                // do not leak their branch-local symbol types into that path.
                incomingFallthrough=FunctionControlFlow.CanCompleteNormally(clause.Body)
                    ? effectiveType : null;
            }
            finally
            {
                RestoreInitialized(before);
                if(subject!=null)subject.Type=originalType;
            }
        }
    }

    // for..of may execute zero times; assignments in its body cannot establish
    // definite initialization after the loop. One element binding is scoped
    // to the loop and is always initialized during an iteration.
    private void CheckForOf(ForOfStatement loop,Scope scope,TypeSymbol? expectedReturn)
    {
        var sourceType=TypeOf(loop.Source,scope);
        if(sourceType is not ArrayTypeSymbol array)
            throw new CompilationFailure(Diagnostic.Error(file,loop.Keyword,"TSCS1001",
                "Only known array values are supported in for..of iteration."));
        CheckStrictModuleBinding(loop.Name);
        var before=CaptureInitialized(scope);
        var iteration=new Scope(scope);
        Binder.DeclareIterationVariable(file,iteration,loop,array.ElementType);
        try { CheckBlock(loop.Body,iteration,false,expectedReturn,true,true); }
        finally { RestoreInitialized(before); }
    }

    // A while body can run zero times: no definite-initialisation effects
    // escape into following statements. Loops retain the existing restricted
    // boolean/any condition policy rather than inferring truthiness.
    private void CheckWhile(WhileStatement loop,Scope scope,TypeSymbol? expectedReturn)
    {
        var conditionType=TypeOf(loop.Condition,scope);
        if(conditionType!=null && !CompilerTypes.IsAssignable(conditionType,CompilerTypes.Boolean))
            throw new CompilationFailure(Diagnostic.Error(file,loop.Keyword,"TSCS1001",
                "Only boolean or any while conditions are supported by the current flow checker."));
        var before=CaptureInitialized(scope);
        try { CheckBlock(loop.Body,scope,false,expectedReturn,true,true); }
        finally { RestoreInitialized(before); }
    }

    // The initializer always runs, but body and increment effects do not
    // establish definite initialization after a possibly empty loop.
    private void CheckClassicFor(ClassicForStatement loop,Scope scope,TypeSymbol? expectedReturn)
    {
        Scope header=loop.Initializer is Variable declaration
            ? Binder.Bind(file,new Block([declaration]),scope)
            : new Scope(scope);
        if(loop.Initializer is { } initializer)
            CheckBlock(new Block([initializer]),header,true,expectedReturn,preboundLocal:true);
        if(loop.Condition is { } condition)
        {
            var conditionType=TypeOf(condition,header);
            if(conditionType!=null && !CompilerTypes.IsAssignable(conditionType,CompilerTypes.Boolean))
                throw new CompilationFailure(Diagnostic.Error(file,loop.Keyword,"TSCS1001",
                    "Classic for conditions currently require boolean or any."));
        }
        var before=CaptureInitialized(header);
        try
        {
            // The update runs after the body (and after a continue), never
            // before its first iteration. Check its types independently, then
            // restore ALL initialization facts, including header locals, before
            // checking the body. Do not let the update fabricate a first-read.
            if(loop.Update is { } update)
                CheckBlock(new Block([update]),header,true,expectedReturn);
            RestoreInitialized(before);
            CheckBlock(loop.Body,header,false,expectedReturn,true,true);
        }
        finally { RestoreInitialized(before); }
    }

    // Each branch runs against the same lexical parent, but writes to symbols
    // are joined conservatively; an unexecuted arm cannot initialize a value.
    private DiscriminantContinuation? CheckConditional(ConditionalStatement conditional,Scope scope,TypeSymbol? expectedReturn,bool allowBreak,bool allowContinue)
    {
        var conditionType=TypeOf(conditional.Condition,scope);
        if(conditionType!=null && !CompilerTypes.IsAssignable(conditionType,CompilerTypes.Boolean))
            throw new CompilationFailure(Diagnostic.Error(file,conditional.Keyword,"TSCS1001",
                "Only boolean or any conditions are supported by the current flow checker."));
        // Discriminant types are a branch-local view of one existing symbol.
        // Never mutate the AST or permanently narrow the declaration type.
        var discriminant=TryNarrowDiscriminant(conditional.Condition,scope);
        var originalType=discriminant?.Symbol.Type;
        var before=CaptureInitialized(scope);
        var whenTrue=before;
        var whenFalse=before;
        // A returning arm cannot reach the statement after this conditional.
        // Only paths that can fall through contribute to definite assignment.
        bool thenFallsThrough=FunctionControlFlow.CanCompleteNormally(conditional.Consequent);
        bool elseFallsThrough=conditional.Alternate is null ||
            FunctionControlFlow.CanCompleteNormally(conditional.Alternate);
        try
        {
            if(discriminant is { } narrowing) narrowing.Symbol.Type=narrowing.WhenTrue;
            CheckBlock(conditional.Consequent,scope,false,expectedReturn,allowBreak,allowContinue);
            whenTrue=CaptureInitialized(scope);
            RestoreInitialized(before);
            if(discriminant is { } narrowingFalse) narrowingFalse.Symbol.Type=narrowingFalse.WhenFalse;
            switch(conditional.Alternate)
            {
                case Block alternate:
                    CheckBlock(alternate,scope,false,expectedReturn,allowBreak,allowContinue);
                    break;
                case ConditionalStatement chained:
                    CheckConditional(chained,scope,expectedReturn,allowBreak,allowContinue);
                    break;
            }
            whenFalse=CaptureInitialized(scope);
        }
        finally
        {
            RestoreInitialized(before);
            if(discriminant is { } narrowingRestored)
                narrowingRestored.Symbol.Type=originalType;
        }
        foreach(var (symbol,initialized) in before)
            symbol.Initialized=(thenFallsThrough,elseFallsThrough) switch
            {
                (true,true) => whenTrue[symbol] && whenFalse[symbol],
                (true,false) => whenTrue[symbol],
                (false,true) => whenFalse[symbol],
                // No path continues: retain the incoming state rather than
                // fabricating an initialized value in unreachable code.
                _ => initialized
            };
        // Facts survive to the next statement only if one branch
        // definitely exits. Two fallthrough branches retain the union.
        if(discriminant is not { } narrowed || thenFallsThrough==elseFallsThrough)
            return null;
        return new DiscriminantContinuation(narrowed.Symbol,
            thenFallsThrough?narrowed.WhenTrue:narrowed.WhenFalse);
    }

    private readonly record struct DiscriminantContinuation(
        Symbol Symbol,TypeSymbol FallthroughType);

    // Bounded discriminated unions: a simple identifier's shared named member
    // must be a known literal in every object alternative. This explicitly
    // excludes broad/string/optional members and arbitrary boolean expressions.
    // Both branch types must be nonempty to avoid inventing unreachable-flow
    // handling before the full control-flow graph is implemented.
    private readonly record struct DiscriminantBranches(
        Symbol Symbol,TypeSymbol WhenTrue,TypeSymbol WhenFalse);

    private static DiscriminantBranches? TryNarrowDiscriminant(ValueNode condition,Scope scope)
    {
        while(condition is ValueParenthesized grouped)condition=grouped.Expression;
        if(condition is not ValueBinary comparison ||
           comparison.Operator.Kind is not (TokenKind.StrictEqual or TokenKind.StrictNotEqual))
            return null;

        Token variable=default;
        string property="";
        LiteralTypeSymbol? expected=null;
        if(TryReadDiscriminant(comparison.Left,out variable,out property))
            expected=TryReadLiteral(comparison.Right);
        if(expected==null && TryReadDiscriminant(comparison.Right,out variable,out property))
            expected=TryReadLiteral(comparison.Left);
        if(expected==null)return null;
        var symbol=scope.Find(variable.Text);
        if(symbol?.Type is not UnionTypeSymbol union)return null;

        var equal=new List<TypeSymbol>();
        var different=new List<TypeSymbol>();
        foreach(var candidate in union.Alternatives)
        {
            if(candidate is not ObjectTypeSymbol shape ||
               !shape.Members.TryGetValue(property,out var discriminator) ||
               discriminator is not LiteralTypeSymbol literal)
                return null; // no guessed narrowing of broad or unknown variants
            if(literal.Equals(expected))equal.Add(candidate);
            else different.Add(candidate);
        }
        if(equal.Count==0 || different.Count==0)return null;
        var onEqual=CompilerTypes.Union(equal);
        var onDifferent=CompilerTypes.Union(different);
        return comparison.Operator.Kind==TokenKind.StrictEqual
            ?new DiscriminantBranches(symbol,onEqual,onDifferent)
            :new DiscriminantBranches(symbol,onDifferent,onEqual);
    }

    private static bool TryReadDiscriminant(ValueNode source,out Token variable,out string member)
    {
        while(source is ValueParenthesized group)source=group.Expression;
        if(source is ValuePropertyAccess { Receiver:ValueAtom atom } access &&
           atom.Token.Kind==TokenKind.Identifier)
        {
            variable=atom.Token;
            member=access.Name.Text;
            return true;
        }
        variable=default;
        member="";
        return false;
    }

    private static LiteralTypeSymbol? TryReadLiteral(ValueNode source)
    {
        while(source is ValueParenthesized group)source=group.Expression;
        if(source is ValueAtom atom && atom.Token.Kind is
                (TokenKind.String or TokenKind.Number or TokenKind.True or TokenKind.False))
            return CompilerTypes.FromLiteralExact(atom.Token);
        if(source is ValueUnary { Operator.Kind:TokenKind.Minus,Operand:ValueAtom number } &&
           number.Token.Kind==TokenKind.Number)
            return CompilerTypes.FromLiteralExact(number.Token,negative:true);
        return null;
    }

    private static Dictionary<Symbol,bool> CaptureInitialized(Scope scope)
    {
        var values=new Dictionary<Symbol,bool>();
        for(Scope? current=scope;current!=null;current=current.Parent)
            foreach(var symbol in current.Symbols.Values)
                values.TryAdd(symbol,symbol.Initialized);
        return values;
    }

    private static void RestoreInitialized(IReadOnlyDictionary<Symbol,bool> values)
    {
        foreach(var (symbol,initialized) in values) symbol.Initialized=initialized;
    }

    private TypeSymbol? TypeOf(ValueNode value,Scope scope)
    {
        // The parser builds left-deep chains for numeric additions. Do not
        // recursively descend them: supported source can contain hundreds of
        // thousands of '+' operators under the source-size limit.
        // Post-order evaluation preserves left-to-right binding diagnostics
        // and reports the first unsupported operand at the same operator.
        var pending=new Stack<(ValueNode Node,bool Combine)>();
        var types=new Stack<TypeSymbol?>();
        pending.Push((value,false));
        while(pending.Count>0)
        {
            var (node,combine)=pending.Pop();
            if(node is ValueAtom atom)
            {
                types.Push(TypeOfAtom(atom.Token,scope));
                continue;
            }
            if(node is ValueArray arrayLiteral)
            {
                if(!combine)
                {
                    pending.Push((arrayLiteral,true));
                    for(int i=arrayLiteral.Elements.Count-1;i>=0;i--)
                        pending.Push((arrayLiteral.Elements[i],false));
                    continue;
                }
                TypeSymbol element=CompilerTypes.Never;
                bool known=true;
                for(int i=arrayLiteral.Elements.Count-1;i>=0;i--)
                {
                    var next=types.Pop();
                    if(next==null)known=false;
                    else if(element==CompilerTypes.Never)element=next;
                    else if(!CompilerTypes.IsEquivalent(element,next))
                    {
                        try {element=CompilerTypes.Union([element,next]);}
                        catch(InvalidOperationException failure)
                        {
                            throw new CompilationFailure(Diagnostic.Error(file,arrayLiteral.Open,"TSCS1001",failure.Message));
                        }
                    }
                }
                types.Push(known?new ArrayTypeSymbol(element):null);
                continue;
            }
            if(node is ValueElementAccess indexed)
            {
                if(!combine)
                {
                    pending.Push((indexed,true));
                    pending.Push((indexed.Index,false));
                    pending.Push((indexed.Receiver,false));
                    continue;
                }
                var indexType=types.Pop();
                var receiverType=types.Pop();
                if(indexType!=null && !CompilerTypes.IsNumber(indexType))
                    throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                        "Array indices currently require a number expression."));
                if(receiverType is ArrayTypeSymbol array)types.Push(array.ElementType);
                else if(receiverType==null || indexType==null)types.Push(null);
                else throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                    "Index access currently supports arrays only; object index signatures are not implemented."));
                continue;
            }
            if(node is ValueObject literal)
            {
                if(!combine)
                {
                    pending.Push((literal,true));
                    for(int i=literal.Properties.Count-1;i>=0;i--)
                        pending.Push((literal.Properties[i].Value,false));
                    continue;
                }
                var members=new KeyValuePair<string,TypeSymbol>[literal.Properties.Count];
                bool known=true;
                for(int i=members.Length-1;i>=0;i--)
                {
                    var memberType=types.Pop();
                    if(memberType==null)known=false;
                    else members[i]=new(Parser.PropertyName(literal.Properties[i].Name),memberType);
                }
                // Do not turn an unresolved property into an implicit any.
                types.Push(known?new ObjectTypeSymbol(members):null);
                continue;
            }
            if(node is ValuePropertyAccess property)
            {
                if(!combine)
                {
                    pending.Push((property,true));
                    pending.Push((property.Receiver,false));
                    continue;
                }
                var receiver=types.Pop();
                if(receiver==null)types.Push(null);
                else if(receiver.Equals(CompilerTypes.Any))types.Push(CompilerTypes.Any);
                else if(CompilerTypes.TryGetProperty(receiver,property.Name.Text) is { } member)
                    types.Push(member);
                else
                {
                    Error(property.Name,"TS2339",$"Property '{property.Name.Text}' does not exist on type '{receiver.DisplayName}'.");
                    types.Push(null);
                }
                continue;
            }
            if(node is ValueParenthesized grouped)
            {
                // Preserve grouping in the AST for eventual output, but its
                // semantic type is exactly that of the enclosed expression.
                pending.Push((grouped.Expression,false));
                continue;
            }
            if(node is ValueUnary unary)
            {
                if(!combine)
                {
                    pending.Push((unary,true));
                    pending.Push((unary.Operand,false));
                    continue;
                }
                var operand=types.Pop();
                if(operand==null)types.Push(null);
                else if(unary.Operator.Kind==TokenKind.Bang &&
                    (CompilerTypes.IsBoolean(operand)||operand.Equals(CompilerTypes.Any)))
                    types.Push(CompilerTypes.Boolean);
                else if((unary.Operator.Kind is TokenKind.Plus or TokenKind.Minus) &&
                    CompilerTypes.IsNumber(operand))
                    types.Push(CompilerTypes.Number);
                else throw new CompilationFailure(Diagnostic.Error(file,unary.Operator,"TSCS1001",
                    unary.Operator.Kind switch
                    {
                        TokenKind.Plus => "Only numeric unary plus is supported yet.",
                        TokenKind.Minus => "Only numeric unary minus is supported yet.",
                        _ => "Only boolean or any logical negation is supported yet."
                    }));
                continue;
            }
            if(node is ValueCall call)
            {
                if(!combine)
                {
                    pending.Push((call,true));
                    for(int i=call.Arguments.Count-1;i>=0;i--)
                        pending.Push((call.Arguments[i],false));
                    continue;
                }
                var arguments=new TypeSymbol?[call.Arguments.Count];
                for(int i=arguments.Length-1;i>=0;i--)arguments[i]=types.Pop();
                var callee=Resolve(call.Callee,scope);
                if(callee==null) { types.Push(null); continue; }
                if(callee.Type is not FunctionTypeSymbol signature)
                    throw new CompilationFailure(Diagnostic.Error(file,call.Callee,"TSCS1001",
                        "Calls currently require a declared function with an explicit signature."));
                if(signature.Parameters.Count!=arguments.Length)
                    Error(call.Callee,"TS2554",$"Expected {signature.Parameters.Count} arguments, but got {arguments.Length}.");
                else
                    for(int i=0;i<arguments.Length;i++)
                        if(arguments[i] is { } actual &&
                           CheckContextualLiteral(call.Arguments[i],signature.Parameters[i],actual) &&
                           !IsAssignableContextually(call.Arguments[i],actual,signature.Parameters[i]))
                            Error(call.Callee,"TS2345",
                                $"Argument of type '{actual.DisplayName}' is not assignable to parameter of type '{signature.Parameters[i].DisplayName}'.");
                types.Push(signature.ReturnType);
                continue;
            }
            var binary=(ValueBinary)node;
            if(!combine)
            {
                pending.Push((binary,true));
                pending.Push((binary.Right,false));
                pending.Push((binary.Left,false));
                continue;
            }
            var right=types.Pop();
            var left=types.Pop();
            if(left==null||right==null)types.Push(null);
            else if(binary.Operator.Kind is TokenKind.Plus or TokenKind.Minus or TokenKind.Star)
            {
                if(CompilerTypes.IsNumber(left)&&CompilerTypes.IsNumber(right))
                    types.Push(CompilerTypes.Number);
                else throw new CompilationFailure(Diagnostic.Error(file,binary.Operator,"TSCS1001",
                    binary.Operator.Kind switch
                    {
                        TokenKind.Star => "Only numeric multiplication expressions are supported yet.",
                        TokenKind.Minus => "Only numeric subtraction expressions are supported yet.",
                        _ => "Only numeric addition expressions are supported yet."
                    }));
            }
            else if(binary.Operator.Kind is TokenKind.LessThan or TokenKind.LessEqual or TokenKind.GreaterThan or TokenKind.GreaterEqual)
            {
                if(CompilerTypes.IsNumber(left)&&CompilerTypes.IsNumber(right))
                    types.Push(CompilerTypes.Boolean);
                else throw new CompilationFailure(Diagnostic.Error(file,binary.Operator,"TSCS1001",
                    "Relational comparisons currently require two number operands."));
            }
            else if(binary.Operator.Kind is TokenKind.EqualEqual or TokenKind.BangEqual or TokenKind.StrictEqual or TokenKind.StrictNotEqual)
            {
                if(CompilerTypes.CanCompareEquality(left,right))
                    types.Push(CompilerTypes.Boolean);
                else throw new CompilationFailure(Diagnostic.Error(file,binary.Operator,"TSCS1001",
                    "Equality comparisons currently require matching primitive or any operands."));
            }
            else if(binary.Operator.Kind is TokenKind.AmpAmp or TokenKind.BarBar)
            {
                if(CompilerTypes.IsBoolean(left)&&CompilerTypes.IsBoolean(right))
                    types.Push(CompilerTypes.Boolean);
                else throw new CompilationFailure(Diagnostic.Error(file,binary.Operator,"TSCS1001",
                    "Logical binary expressions currently require boolean operands."));
            }
            else throw new InvalidOperationException("Unrecognized binary expression operator.");
        }
        return types.Pop();
    }
    private TypeSymbol? TypeOfAtom(Token token,Scope scope)
    {
        var literal=CompilerTypes.FromLiteral(token);
        if(literal!=null)return literal;
        var symbol=Resolve(token,scope);
        if(symbol!=null&&!symbol.Initialized)throw new CompilationFailure(Diagnostic.Error(file,token,"TSCS1001","Reads before initialization are not supported yet."));
        return symbol?.Type;
    }
    private Symbol? Resolve(Token name,Scope scope)
    {
        var symbol=scope.Find(name.Text);
        // Lexical declarations take precedence over the intrinsic, including TDZ checks.
        if(symbol==null&&name.Text=="globalThis")
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001","The built-in globalThis type is not supported yet."));
        if(symbol==null) Error(name,"TS2304",$"Cannot find name '{name.Text}'.");
        else if(symbol.File!=file && !symbol.CrossFileTypeReady)
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001","Cross-file inference requires an explicit annotation or primitive literal initializer."));
        else if(symbol.File==file && symbol.Declaration is not FunctionDeclaration && name.Start<=symbol.DeclarationStart)
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001","References before declaration are not supported yet."));
        return symbol;
    }
    private void CheckStrictModuleBinding(Token name)
    {
        // ES modules are always strict, including nested function scopes.
        // Do not impose module-only restrictions on sloppy script parameters.
        if(strictModule && (name.Text is "eval" or "arguments"))
            Error(name,"TS1100",$"Invalid use of '{name.Text}' in strict mode.");
    }
    // Literal values are widened during general expression inference, but a
    // syntactic literal can inhabit an explicitly required literal type. Never
    // narrow an already-stored string/number/boolean variable by guessing.
    private static bool IsAssignableContextually(ValueNode expression,TypeSymbol actual,TypeSymbol expected,int depth=0)
    {
        if(depth>64)return false;
        // A stored union remains assignable to itself; only fresh source
        // literals need contextual refinement beyond ordinary type relations.
        if(CompilerTypes.IsAssignable(actual,expected))return true;
        while(expression is ValueParenthesized group)expression=group.Expression;
        if(expected is UnionTypeSymbol union)
            return union.Alternatives.Any(part=>IsAssignableContextually(expression,actual,part,depth+1));
        if(expected is LiteralTypeSymbol literal)
        {
            if(CompilerTypes.IsAssignable(actual,literal))return true;
            if(expression is ValueAtom atom && atom.Token.Kind is
                    (TokenKind.String or TokenKind.Number or TokenKind.True or TokenKind.False))
                return CompilerTypes.FromLiteralExact(atom.Token).Equals(literal);
            if(expression is ValueUnary { Operator.Kind:TokenKind.Minus,Operand:ValueAtom number } &&
               number.Token.Kind==TokenKind.Number)
                return CompilerTypes.FromLiteralExact(number.Token,negative:true).Equals(literal);
            return false;
        }
        if(expression is ValueObject fresh && actual is ObjectTypeSymbol data &&
           expected is ObjectTypeSymbol shape)
        {
            foreach(var (name,required) in shape.Members)
            {
                if(!data.Members.TryGetValue(name,out var member))return false;
                var property=fresh.Properties.FirstOrDefault(p=>Parser.PropertyName(p.Name)==name);
                if(property is null)
                {
                    if(!CompilerTypes.IsAssignable(member,required))return false;
                }
                else if(!IsAssignableContextually(property.Value,member,required,depth+1))
                    return false;
            }
            return true;
        }
        if(expression is ValueArray elements && actual is ArrayTypeSymbol sourceArray &&
           expected is ArrayTypeSymbol targetArray)
        {
            // The fixed TS7 Go compiler also widens the primitive-tagged
            // members of freshly inferred heterogeneous object arrays.
            // A union of distinct object shapes is not an individual element's
            // contextual shape. For that case use ordinary source-array
            // assignability, not per-element fresh-literal narrowing. An
            // array of already-typed union values remains assignable.
            if(targetArray.ElementType is UnionTypeSymbol objectUnion &&
               objectUnion.Alternatives.All(part=>part is ObjectTypeSymbol))
                return CompilerTypes.IsAssignable(actual,expected);
            // The fixed TS7 Go compiler under the pinned --noLib/minimal
            // profile widens directly inferred primitive array elements:
            // ["ready","done"] has string[], not ("ready"|"done")[].
            // Literal-only element requirements therefore need an already
            // narrow source type. Preserve independent contextual object
            // checking for arrays of structured object literals.
            bool onlyLiteralMembers=targetArray.ElementType is LiteralTypeSymbol ||
                targetArray.ElementType is UnionTypeSymbol options &&
                options.Alternatives.All(option=>option is LiteralTypeSymbol);
            if(onlyLiteralMembers && sourceArray.ElementType is PrimitiveTypeSymbol)
                return CompilerTypes.IsAssignable(actual,expected);
            return elements.Elements.All(element=>IsAssignableContextually(element,
                CompilerTypes.FromInitializer(element)??sourceArray.ElementType,
                targetArray.ElementType,depth+1));
        }
        return CompilerTypes.IsAssignable(actual,expected);
    }

    // Fresh literal checks are use-site rules, not properties of stored object
    // types: a wider value in a variable remains structurally assignable.
    private bool CheckContextualLiteral(ValueNode value,TypeSymbol expected,TypeSymbol actual)
    {
        while(value is ValueParenthesized group)value=group.Expression;
        if(value is ValueObject fresh && expected is UnionTypeSymbol alternatives &&
           actual is ObjectTypeSymbol)
        {
            // TS's fresh literal check considers the set of properties from
            // each object alternative; a property unknown to *all* arms is
            // excess even if ordinary structural width would accept it.
            var expectedObjects=alternatives.Alternatives.OfType<ObjectTypeSymbol>().ToArray();
            if(expectedObjects.Length>0)
            {
                bool unionFreshValid=true;
                foreach(var property in fresh.Properties)
                {
                    var name=Parser.PropertyName(property.Name);
                    if(expectedObjects.All(shape=>!shape.Members.ContainsKey(name)))
                    {
                        Error(property.Name,"TS2353",$"Object literal may only specify known properties, and '{name}' does not exist in type '{expected.DisplayName}'.");
                        unionFreshValid=false;
                    }
                }
                return unionFreshValid;
            }
        }
        if(value is ValueArray literalArray && expected is ArrayTypeSymbol targetArray &&
           actual is ArrayTypeSymbol sourceArray)
        {
            // Do not apply contextual literal refinement when the expected
            // element is a union of distinct object alternatives. The pinned
            // Go source profile widens raw array member discriminants before
            // the outer structural assignment. Defer this one relation to
            // IsAssignableContextually so it reports one true TS2322.
            if(targetArray.ElementType is UnionTypeSymbol objectUnion &&
               objectUnion.Alternatives.All(part=>part is ObjectTypeSymbol))
                return true;
            bool arrayValid=true;
            foreach(var element in literalArray.Elements)
            {
                // A heterogeneous array's aggregate union is not the type of
                // each fresh element. Retain that element's own inferred
                // shape when checking contextual literal properties.
                var elementType=CompilerTypes.FromInitializer(element)??sourceArray.ElementType;
                if(!CheckContextualLiteral(element,targetArray.ElementType,elementType))arrayValid=false;
                else if(!IsAssignableContextually(element,elementType,targetArray.ElementType))
                {
                    Mismatch(literalArray.Open,elementType,targetArray.ElementType);
                    arrayValid=false;
                }
            }
            return arrayValid;
        }
        if(value is not ValueObject literal || expected is not ObjectTypeSymbol shape ||
           actual is not ObjectTypeSymbol data || shape.Members.Count==0)return true;
        bool valid=true;
        foreach(var property in literal.Properties)
        {
            var name=Parser.PropertyName(property.Name);
            if(!shape.Members.TryGetValue(name,out var required))
            {
                Error(property.Name,"TS2353",$"Object literal may only specify known properties, and '{name}' does not exist in type '{shape.DisplayName}'.");
                valid=false;
            }
            else if(data.Members.TryGetValue(name,out var member))
            {
                // Contextual checking diagnoses a fresh literal's property,
                // not the whole function argument. Reuse its computed type so
                // nested name-resolution errors are not evaluated twice.
                if(!CheckContextualLiteral(property.Value,required,member))valid=false;
                else if(!IsAssignableContextually(property.Value,member,required))
                {
                    Mismatch(property.Name,member,required);
                    valid=false;
                }
            }
        }
        return valid;
    }

    private void Mismatch(Token token,TypeSymbol source,TypeSymbol target)
    {
        if(source is ObjectTypeSymbol data && target is ObjectTypeSymbol shape)
        {
            var missing=shape.Members.Keys.Where(name=>!data.Members.ContainsKey(name)).ToArray();
            if(missing.Length==1)
            {
                Error(token,"TS2741",$"Property '{missing[0]}' is missing in type '{source.DisplayName}' but required in type '{target.DisplayName}'.");
                return;
            }
        }
        Error(token,"TS2322",$"Type '{source.DisplayName}' is not assignable to type '{target.DisplayName}'.");
    }
    private void Error(Token token,string code,string message)=>diagnostics.Add(Diagnostic.Error(file,token,code,message));
}

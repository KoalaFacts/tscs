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
    private void CheckBlock(Block block,Scope? parent,bool rootAlreadyBound=false,TypeSymbol? expectedReturn=null)
    {
        var scope=rootAlreadyBound?parent!:Binder.Bind(file,block,parent);
        foreach(var statement in block.Statements)
        {
            switch(statement)
            {
                case Block nested: CheckBlock(nested,scope,false,expectedReturn);break;
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
                    CheckConditional(conditional,scope,expectedReturn);
                    break;
                case ReturnStatement returned:
                    if(expectedReturn==null)
                        throw new CompilationFailure(Diagnostic.Error(file,returned.Keyword,"TSCS1001",
                            "Return statements outside functions are not supported."));
                    var returnType=TypeOf(returned.Value,scope);
                    if(returnType!=null && CheckContextualLiteral(returned.Value,expectedReturn) &&
                       !CompilerTypes.IsAssignable(returnType,expectedReturn))
                        Mismatch(returned.Keyword,returnType,expectedReturn);
                    break;
                case Variable v:
                    if(v.Name.Text is "eval" or "arguments") Error(v.Name,"TS1100",$"Invalid use of '{v.Name.Text}' in strict mode.");
                    if((parent==null || rootAlreadyBound) && v.Name.Text=="globalThis") Error(v.Name,"TS2397","Declaration name conflicts with built-in global identifier 'globalThis'.");
                    var symbol=scope.Symbols[v.Name.Text];
                    var source=v.Initializer is {} init?TypeOf(init,scope):null;
                    var annotatedType=v.Annotation is { } annotation
                        ? CompilerTypes.FromAnnotation(annotation) : null;
                    symbol.Type=annotatedType??source;
                    symbol.Initialized=v.Initializer!=null;
                    if(v.Constant&&v.Initializer==null) Error(v.Name,"TS1155","'const' declarations must be initialized.");
                    if(annotatedType!=null && source!=null &&
                       CheckContextualLiteral(v.Initializer!,annotatedType) &&
                       !CompilerTypes.IsAssignable(source,annotatedType))
                        Mismatch(v.Name,source,annotatedType);
                    break;
                case Assignment a:
                    var target=Resolve(a.Name,scope);var assigned=TypeOf(a.Value,scope);
                    if(target==null)break;
                    if(target.Imported) Error(a.Name,"TS2632",$"Cannot assign to '{a.Name.Text}' because it is an import.");
                    else if(target.IsConstant) Error(a.Name,"TS2588",$"Cannot assign to '{a.Name.Text}' because it is a constant.");
                    else if(target.Type!=null && assigned!=null)
                    {
                        if(CheckContextualLiteral(a.Value,target.Type) &&
                           !CompilerTypes.IsAssignable(assigned,target.Type))Mismatch(a.Name,assigned,target.Type);
                        else target.Initialized=true;
                    }
                    else target.Initialized=true;
                    break;
                case PropertyAssignment assignment:
                    // Mutating a property does not rebind a const or an import.
                    // TypeOf still enforces initialization of the receiver.
                    var propertyType=TypeOf(assignment.Target,scope);
                    var valueType=TypeOf(assignment.Value,scope);
                    if(propertyType!=null && valueType!=null &&
                       CheckContextualLiteral(assignment.Value,propertyType) &&
                       !CompilerTypes.IsAssignable(valueType,propertyType))
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
    // Each branch runs against the same lexical parent, but writes to symbols
    // are joined conservatively; an unexecuted arm cannot initialize a value.
    private void CheckConditional(ConditionalStatement conditional,Scope scope,TypeSymbol? expectedReturn)
    {
        var conditionType=TypeOf(conditional.Condition,scope);
        if(conditionType!=null && !CompilerTypes.IsAssignable(conditionType,CompilerTypes.Boolean))
            throw new CompilationFailure(Diagnostic.Error(file,conditional.Keyword,"TSCS1001",
                "Only boolean or any conditions are supported by the current flow checker."));
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
            CheckBlock(conditional.Consequent,scope,false,expectedReturn);
            whenTrue=CaptureInitialized(scope);
            RestoreInitialized(before);
            switch(conditional.Alternate)
            {
                case Block alternate:
                    CheckBlock(alternate,scope,false,expectedReturn);
                    break;
                case ConditionalStatement chained:
                    CheckConditional(chained,scope,expectedReturn);
                    break;
            }
            whenFalse=CaptureInitialized(scope);
        }
        finally { RestoreInitialized(before); }
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
                else if(receiver is ObjectTypeSymbol shape &&
                        shape.Members.TryGetValue(property.Name.Text,out var member))types.Push(member);
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
                           CheckContextualLiteral(call.Arguments[i],signature.Parameters[i]) &&
                           !CompilerTypes.IsAssignable(actual,signature.Parameters[i]))
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
    // Fresh literal checks are use-site rules, not properties of stored object
    // types: a wider value in a variable remains structurally assignable.
    private bool CheckContextualLiteral(ValueNode value,TypeSymbol expected)
    {
        while(value is ValueParenthesized group)value=group.Expression;
        if(value is not ValueObject literal || expected is not ObjectTypeSymbol shape ||
           shape.Members.Count==0)return true;
        bool valid=true;
        foreach(var property in literal.Properties)
        {
            var name=Parser.PropertyName(property.Name);
            if(!shape.Members.TryGetValue(name,out var required))
            {
                Error(property.Name,"TS2353",$"Object literal may only specify known properties, and '{name}' does not exist in type '{shape.DisplayName}'.");
                valid=false;
            }
            else valid=CheckContextualLiteral(property.Value,required) && valid;
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

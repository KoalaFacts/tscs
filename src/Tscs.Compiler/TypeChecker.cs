namespace Tscs;

// Primitive branches of checkVariableLikeDeclaration/checkIdentifier/checkBinaryLikeExpression.
internal sealed class TypeChecker(string file,bool strictModule=false)
{
    private readonly List<Diagnostic> diagnostics=[];
    private readonly Dictionary<ValueConditional,(TypeSymbol? True,TypeSymbol? False)> conditionalResults = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ValueBinary,LogicalValueOutcome> logicalResults = new(ReferenceEqualityComparer.Instance);
    // Logical expressions use bounded recursion only to check the RHS in the
    // appropriate short-circuit flow scope; other expression chains remain iterative.
    private int logicalEvaluationDepth;
    public IReadOnlyList<Diagnostic> Check(Block block) { CheckBlock(block,null);return diagnostics; }
    public IReadOnlyList<Diagnostic> CheckBound(Block block,Scope global)
    {
        CheckBlock(block,global,true);
        return diagnostics;
    }
    // A valid, bounded element union may become too wide when a method adds
    // undefined to its return type. Surface this as a compiler diagnostic,
    // rather than leaking an unchecked InvalidOperationException to CLI hosts.
    private TypeSymbol? CheckedProperty(TypeSymbol receiver,Token name)
    {
        try {return CompilerTypes.TryGetProperty(receiver,name.Text);}
        catch(InvalidOperationException failure)
        {
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",failure.Message));
        }
    }

    private void CheckBlock(Block block,Scope? parent,bool rootAlreadyBound=false,TypeSymbol? expectedReturn=null,bool allowBreak=false,bool allowContinue=false,bool preboundLocal=false)
    {
        var scope=rootAlreadyBound?parent!:Binder.Bind(file,block,parent);
        // Branch facts are lexical and never escape this block, including
        // exits through malformed-source error diagnostics.
        var originalFlowTypes=new Dictionary<Symbol,(TypeSymbol? Type,int WriteVersion)>();
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
                        // Only value-returning functions need definite exit.
                        if(!functionType.ReturnType.Equals(CompilerTypes.Void) &&
                           !FunctionControlFlow.DefinitelyReturnsValue(function.Body))
                            throw new CompilationFailure(Diagnostic.Error(file,function.Name,"TSCS1001",
                                "Only function bodies with a definite value return are supported yet."));
                        var parameters=new Scope(scope);
                        foreach(var parameter in function.Parameters)
                        {
                            CheckStrictModuleBinding(parameter.Name);
                            // Initializers can see preceding parameters, not future
                            // ones. Check in declaration order before binding self.
                            if(parameter.DefaultValue is { } initializer)
                            {
                                var requiredType=TypeResolver.Resolve(parameter.Annotation,parameters,file);
                                var initialType=TypeOf(initializer,parameters);
                                if(initialType is { } actual &&
                                   CheckContextualLiteral(initializer,requiredType,actual) &&
                                   !IsAssignableContextually(initializer,actual,requiredType))
                                    Mismatch(parameter.Name,actual,requiredType);
                            }
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
                            originalFlowTypes.TryAdd(afterGuard.Symbol,(afterGuard.Symbol.Type,afterGuard.Symbol.FlowWriteVersion));
                            afterGuard.Symbol.Type=afterGuard.FallthroughType;
                        }
                        break;
                    case ForOfStatement loop:
                        CheckForOf(loop,scope,expectedReturn);
                        break;
                    case ForInStatement enumerator:
                        CheckForIn(enumerator,scope,expectedReturn);
                        break;
                    case WhileStatement whileLoop:
                        CheckWhile(whileLoop,scope,expectedReturn);
                        break;
                    case DoWhileStatement doLoop:
                        CheckDoWhile(doLoop,scope,expectedReturn);
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
                    case ThrowStatement thrown:
                        TypeOf(thrown.Value,scope);
                        break;
                    case TryStatement guarded:
                        CheckTry(guarded,scope,expectedReturn,allowBreak,allowContinue);
                        break;
                    case ReturnStatement returned:
                        if(expectedReturn is null && inferredArrowReturns is { } inference)
                        {
                            // Collect return types in the actual lexical checker,
                            // after local bindings, imports and branch narrowing.
                            // A nested named function has its own expectedReturn,
                            // while nested arrow inference replaces this collector.
                            inference.Add(returned.Value is { } inferredValue
                                ?TypeOf(inferredValue,scope):CompilerTypes.Void);
                            break;
                        }
                        if(expectedReturn==null)
                            throw new CompilationFailure(Diagnostic.Error(file,returned.Keyword,"TSCS1001",
                                "Return statements outside functions are not supported."));
                        if(returned.Value is not { } returnValue)
                        {
                            if(!expectedReturn.Equals(CompilerTypes.Void))
                                throw new CompilationFailure(Diagnostic.Error(file,returned.Keyword,"TSCS1001",
                                    "A bare return requires a void-returning function in this compiler subset."));
                            break;
                        }
                        var returnType=TypeOf(returnValue,scope);
                        if(returnType!=null && CheckContextualLiteral(returnValue,expectedReturn,returnType) &&
                           !IsAssignableContextually(returnValue,returnType,expectedReturn))
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
                        symbol.DeclaredType=symbol.Type;
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
                        else if((target.DeclaredType??target.Type) is { } assignmentType && assigned!=null)
                        {
                            // A narrowed read is NOT the declared assignment constraint.
                            // Rebinding can change the flow type, but must still satisfy
                            // the original declaration (including literal contextual rules).
                            if(CheckContextualLiteral(a.Value,assignmentType,assigned) &&
                               !IsAssignableContextually(a.Value,assigned,assignmentType))
                                Mismatch(a.Name,assigned,assignmentType);
                            else
                            {
                                target.Initialized=true;
                                target.FlowWriteVersion++;
                                if(target.DeclaredType!=null &&
                                   !ReferenceEquals(target.Type,target.DeclaredType))
                                {
                                    // An exact primitive assignment may refine a
                                    // primitive union; other writes conservatively
                                    // invalidate the old branch fact.
                                    bool primitiveUnion=target.DeclaredType is UnionTypeSymbol union &&
                                        union.Alternatives.All(member=>member is PrimitiveTypeSymbol or LiteralTypeSymbol);
                                    target.Type=primitiveUnion &&
                                        CompilerTypes.IsAssignable(assigned,target.DeclaredType)
                                        ?assigned:target.DeclaredType;
                                }
                            }
                        }
                        else
                        {
                            target.Initialized=true;
                            target.FlowWriteVersion++;
                        }
                        break;
                    case ElementAssignment elementAssignment:
                        EnsureWritableIndex(elementAssignment.Target,TypeOf(elementAssignment.Target.Receiver,scope));
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
                        var propertyReceiver=TypeOf(assignment.Target.Receiver,scope);
                        if(propertyReceiver is ArrayTypeSymbol {Readonly:true} &&
                           assignment.Target.Name.Text=="length")
                            throw new CompilationFailure(Diagnostic.Error(file,assignment.Target.Name,"TS2540",
                                "Cannot assign to 'length' because it is a read-only property."));
                        if(CompilerTypes.IsStringFamily(propertyReceiver) && assignment.Target.Name.Text=="length")
                            throw new CompilationFailure(Diagnostic.Error(file,assignment.Target.Name,"TS2540",
                                "Cannot assign to 'length' because it is a read-only property."));
                        if(propertyReceiver is ObjectTypeSymbol readonlyShape &&
                           readonlyShape.ReadonlyMembers.Contains(assignment.Target.Name.Text))
                            throw new CompilationFailure(Diagnostic.Error(file,assignment.Target.Name,"TS2540",
                                $"Cannot assign to '{assignment.Target.Name.Text}' because it is a read-only property."));
                        if(propertyReceiver is UnionTypeSymbol writableUnion)
                            RequireCommonWritableUnionProperty(writableUnion,assignment.Target.Name.Text,assignment.Target.Name);
                        // Mutating a property does not rebind a const or an import.
                        // TypeOf still enforces initialization of the receiver.
                        var propertyType=TypeOf(assignment.Target,scope);
                        var valueType=TypeOf(assignment.Value,scope);
                        if(propertyType!=null && valueType!=null &&
                           CheckContextualLiteral(assignment.Value,propertyType,valueType) &&
                           !IsAssignableContextually(assignment.Value,valueType,propertyType))
                            Mismatch(assignment.Target.Name,valueType,propertyType);
                        break;
                    case CompoundAssignment compound:
                        bool logicalAssignment=compound.Operator.Kind is TokenKind.AmpAmpEqual
                            or TokenKind.BarBarEqual or TokenKind.QuestionQuestionEqual;
                        // Member and indexed logical assignments use the same checked
                        // writable-target constraints as ordinary compound writes.
                        // The emitter captures their receiver/key once and evaluates
                        // the RHS only on the branch that actually writes.
                        Symbol? logicalTarget=null;
                        TypeSymbol? targetType;
                        switch(compound.Target)
                        {
                            case ValueAtom { Token.Kind:TokenKind.Identifier } targetName:
                                var targetSymbol=Resolve(targetName.Token,scope);
                                if(targetSymbol is null) { targetType=null; break; }
                                if(!targetSymbol.Initialized)
                                    throw new CompilationFailure(Diagnostic.Error(file,targetName.Token,"TSCS1001",
                                        "Reads before initialization are not supported yet."));
                                if(targetSymbol.Imported)
                                    Error(targetName.Token,"TS2632",$"Cannot assign to '{targetName.Token.Text}' because it is an import.");
                                else if(targetSymbol.IsConstant)
                                    Error(targetName.Token,"TS2588",$"Cannot assign to '{targetName.Token.Text}' because it is a constant.");
                                targetType=targetSymbol.Type;
                                if(logicalAssignment)logicalTarget=targetSymbol;
                                break;
                            case ValuePropertyAccess propertyTarget:
                                var compoundReceiver=TypeOf(propertyTarget.Receiver,scope);
                                if(compoundReceiver is ArrayTypeSymbol {Readonly:true} &&
                                   propertyTarget.Name.Text=="length")
                                    throw new CompilationFailure(Diagnostic.Error(file,propertyTarget.Name,"TS2540",
                                        "Cannot assign to 'length' because it is a read-only property."));
                                if(CompilerTypes.IsStringFamily(compoundReceiver) && propertyTarget.Name.Text=="length")
                                    throw new CompilationFailure(Diagnostic.Error(file,propertyTarget.Name,"TS2540",
                                        "Cannot assign to 'length' because it is a read-only property."));
                                if(compoundReceiver is ObjectTypeSymbol readonlyCompoundShape &&
                                   readonlyCompoundShape.ReadonlyMembers.Contains(propertyTarget.Name.Text))
                                    throw new CompilationFailure(Diagnostic.Error(file,propertyTarget.Name,"TS2540",
                                        $"Cannot assign to '{propertyTarget.Name.Text}' because it is a read-only property."));
                                if(compoundReceiver is UnionTypeSymbol compoundUnion)
                                    RequireCommonWritableUnionProperty(compoundUnion,propertyTarget.Name.Text,propertyTarget.Name);
                                targetType=TypeOf(propertyTarget,scope);
                                break;
                            case ValueElementAccess elementTarget:
                                EnsureWritableIndex(elementTarget,TypeOf(elementTarget.Receiver,scope));
                                targetType=TypeOf(elementTarget,scope);
                                break;
                            default:
                                throw new InvalidOperationException("Only parser-checked compound assignment targets are allowed.");
                        }
                        var operandType=TypeOf(compound.Value,scope);
                        if(targetType!=null && operandType!=null)
                        {
                            if(logicalAssignment)
                            {
                                var declared=logicalTarget?.DeclaredType??targetType;
                                // Check writes against their declared type, while preserving JS
                                // truthiness for numeric, string, object and nullish lvalues.
                                if(CheckContextualLiteral(compound.Value,declared,operandType) &&
                                   !IsAssignableContextually(compound.Value,operandType,declared))
                                    Mismatch(compound.Operator,operandType,declared);
                                else if(logicalTarget is not null)
                                {
                                    // A conditional rebind invalidates previous branch
                                    // narrowings rather than claiming an unconditional type.
                                    logicalTarget.FlowWriteVersion++;
                                    logicalTarget.Type=logicalTarget.DeclaredType??logicalTarget.Type;
                                }
                            }
                            else if(compound.Operator.Kind==TokenKind.PlusEqual)
                            {
                                // Unlike numeric-only operators, += can concatenate,
                                // but its resulting string must still fit the target.
                                var resultType=CompilerTypes.AdditionResult(targetType,operandType);
                                if(resultType==null)
                                    throw new CompilationFailure(Diagnostic.Error(file,compound.Operator,"TSCS1001",
                                        "Addition requires compatible numeric, string or any operands."));
                                // TypeScript checks compound addition against the
                                // primitive family of a literal target: a literal
                                // may be changed by += while ordinary = remains exact.
                                // Keep number += string rejected as TS2322.
                                TypeSymbol required=targetType is LiteralTypeSymbol literal
                                    ? literal.Kind switch
                                    {
                                        PrimitiveKind.Number=>CompilerTypes.Number,
                                        PrimitiveKind.String=>CompilerTypes.String,
                                        PrimitiveKind.Boolean=>CompilerTypes.Boolean,
                                        _=>targetType
                                    }
                                    : targetType;
                                if(!CompilerTypes.IsAssignable(resultType,required))
                                    Mismatch(compound.Operator,resultType,required);
                            }
                            else if(!(IsNumericMutationOperand(targetType) && IsNumericMutationOperand(operandType)))
                                throw new CompilationFailure(Diagnostic.Error(file,compound.Operator,"TSCS1001",
                                    "Compound arithmetic assignments currently require number or any operands."));
                        }
                        break;
                    case PostfixUpdate update:
                        // Legacy statement AST shares the same checked lvalue path.
                        CheckNumericUpdate(new ValueAtom(update.Name),update.Operator,scope);
                        break;
                    case Expression e: TypeOf(e.Value,scope);break;
                }
            }

        }
        finally
        {
            foreach(var (symbol,(originalType,writeVersion)) in originalFlowTypes)
                symbol.Type=symbol.FlowWriteVersion!=writeVersion
                    ?symbol.DeclaredType??originalType
                    :originalType;
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
        // Native ES2015 for..of iterates strings by Unicode code point.
        // Other unknown or nullable sources remain unsupported.
        TypeSymbol? elementType;
        try { elementType=CompilerTypes.TryGetIterationElementType(sourceType); }
        catch(InvalidOperationException failure)
        {
            throw new CompilationFailure(Diagnostic.Error(file,loop.Keyword,"TSCS1001",failure.Message));
        }
        if(elementType is null)
            throw new CompilationFailure(Diagnostic.Error(file,loop.Keyword,"TSCS1001",
                "Only checked array and string values are supported in for..of iteration."));
        CheckStrictModuleBinding(loop.Name);
        var before=CaptureInitialized(scope);
        var writesBefore=CaptureFlowWriteVersions(scope);
        var iteration=new Scope(scope);
        Binder.DeclareIterationVariable(file,iteration,loop,elementType);
        // Backedges can observe a write from a previous iteration. A
        // narrower pre-loop read type is not a loop invariant.
        InvalidateLoopBackedgeFlowTypes(scope,loop.Body,iterationName:loop.Name.Text);
        try { CheckBlock(loop.Body,iteration,false,expectedReturn,true,true); }
        finally
        {
            RestoreInitialized(before);
            InvalidateLoopWrittenFlowTypes(writesBefore);
        }
    }

    // Finite object/array unions retain native for..in semantics. Nullish
    // alternatives are harmless no-ops, but a nullish-only or primitive
    // source has no checked object alternative and must be rejected.
    private static bool CanEnumerateForInKeys(TypeSymbol? source)
    {
        if(source is null)return false;
        if(source.Equals(CompilerTypes.Any))return true;
        if(source is ObjectTypeSymbol or ArrayTypeSymbol)return true;
        if(source is not UnionTypeSymbol union)return false;
        bool hasObject=false;
        foreach(var alternative in union.Alternatives)
        {
            if(alternative is ObjectTypeSymbol or ArrayTypeSymbol)
            {
                hasObject=true;
                continue;
            }
            if(alternative.Equals(CompilerTypes.Null)||alternative.Equals(CompilerTypes.Undefined))
                continue;
            return false;
        }
        return hasObject;
    }

    // Keys, not values, are bound inside the lexical loop scope. A for..in
    // body may execute zero times, so its writes cannot initialize outer locals.
    private void CheckForIn(ForInStatement loop,Scope scope,TypeSymbol? expectedReturn)
    {
        var sourceType=TypeOf(loop.Source,scope);
        if(!CanEnumerateForInKeys(sourceType))
            throw new CompilationFailure(Diagnostic.Error(file,loop.Keyword,"TS2407",
                "The right-hand side of a 'for...in' statement must have an object or any type."));
        CheckStrictModuleBinding(loop.Name);
        var before=CaptureInitialized(scope);
        var writesBefore=CaptureFlowWriteVersions(scope);
        var iteration=new Scope(scope);
        Binder.DeclareIterationVariable(file,iteration,loop);
        InvalidateLoopBackedgeFlowTypes(scope,loop.Body,iterationName:loop.Name.Text);
        try { CheckBlock(loop.Body,iteration,false,expectedReturn,true,true); }
        finally
        {
            RestoreInitialized(before);
            InvalidateLoopWrittenFlowTypes(writesBefore);
        }
    }

    // A while body can run zero times: no definite-initialisation effects
    // escape into following statements. Runtime expression truthiness never implies
    // definite assignment or unsafe property-path narrowing.
    private void CheckWhile(WhileStatement loop,Scope scope,TypeSymbol? expectedReturn)
    {
        // Both the body and the condition may be evaluated after a backedge.
        InvalidateLoopBackedgeFlowTypes(scope,loop.Body);
        var conditionType=TypeOf(loop.Condition,scope);
        CheckRuntimeCondition(loop.Condition,conditionType,loop.Keyword);
        var before=CaptureInitialized(scope);
        var writesBefore=CaptureFlowWriteVersions(scope);
        try { CheckBlock(loop.Body,scope,false,expectedReturn,true,true); }
        finally
        {
            RestoreInitialized(before);
            InvalidateLoopWrittenFlowTypes(writesBefore);
        }
    }

    // Numeric literals name an existing integer object member, never a
    // general object[number] index signature. Strings retain prior behavior.
    private static bool TryStaticNamedIndex(ValueNode index,TypeSymbol? receiver,out string name)
    {
        while(index is ValueParenthesized group)index=group.Expression;
        if(index is ValueAtom atom)
        {
            if(atom.Token.Kind==TokenKind.String)
            {
                name=Parser.PropertyName(atom.Token);
                return true;
            }
            if(atom.Token.Kind==TokenKind.Number &&
               receiver is ObjectTypeSymbol shape &&
               shape.Members.ContainsKey(Parser.PropertyName(atom.Token)))
            {
                name=Parser.PropertyName(atom.Token);
                return true;
            }
        }
        name=string.Empty;
        return false;
    }

    // Writes to a union must be valid for every runtime alternative. The
    // bounded implementation admits shared required, mutable properties only;
    // unlike a union read, a write cannot use a union of property value types.
    // Assignment compatibility is too permissive for writable alternatives:
    // any must NOT match concrete types, even inside arrays/nested objects.
    // Unknown callable/recursive relationships fail closed rather than
    // accepting a write that is invalid for one union branch.
    private static bool SameWritableType(TypeSymbol left,TypeSymbol right)
    {
        // Shared alias graphs can otherwise cause exponentially many repeated
        // comparisons. Use reference identity rather than record equality,
        // which itself can recursively traverse structural type children.
        var comparisons=new Dictionary<TypeSymbol,Dictionary<TypeSymbol,bool>>(ReferenceEqualityComparer.Instance);
        int budget=16384;
        bool Check(TypeSymbol a,TypeSymbol b,int depth)
        {
            if(ReferenceEquals(a,b))return true;
            if(depth>=64)return false;
            if(comparisons.TryGetValue(a,out var row) &&
               row.TryGetValue(b,out var previous))return previous;
            if(--budget<0)return false;
            row ??=new Dictionary<TypeSymbol,bool>(ReferenceEqualityComparer.Instance);
            comparisons[a]=row;
            row[b]=false; // Recursive structural graphs fail closed.
            bool result=(a,b) switch
            {
                (PrimitiveTypeSymbol x,PrimitiveTypeSymbol y)=>x.Kind==y.Kind,
                (LiteralTypeSymbol x,LiteralTypeSymbol y)=>x.Kind==y.Kind && x.Value==y.Value,
                (ArrayTypeSymbol x,ArrayTypeSymbol y)=>x.Readonly==y.Readonly &&
                    Check(x.ElementType,y.ElementType,depth+1),
                (ObjectTypeSymbol x,ObjectTypeSymbol y)=>
                    x.Members.Count==y.Members.Count &&
                    x.ReadonlyMembers.SetEquals(y.ReadonlyMembers) &&
                    x.OptionalMembers.SetEquals(y.OptionalMembers) &&
                    x.NumericMembers.SetEquals(y.NumericMembers) &&
                    x.Members.All(kv=>y.Members.TryGetValue(kv.Key,out var value) &&
                        Check(kv.Value,value,depth+1)),
                (UnionTypeSymbol x,UnionTypeSymbol y)=>
                    x.Alternatives.Count==y.Alternatives.Count &&
                    x.Alternatives.Select((value,i)=>Check(value,y.Alternatives[i],depth+1)).All(ok=>ok),
                // Only identical canonical callable references are admitted.
                _=>false
            };
            row[b]=result;
            return result;
        }
        return Check(left,right,0);
    }

    private TypeSymbol RequireCommonWritableUnionProperty(UnionTypeSymbol union,string name,Token location)
    {
        TypeSymbol? common=null;
        foreach(var alternative in union.Alternatives)
        {
            if(alternative is not ObjectTypeSymbol shape ||
               !shape.Members.TryGetValue(name,out var member))
                throw new CompilationFailure(Diagnostic.Error(file,location,"TS2339",
                    $"Property '{name}' does not exist on type '{union.DisplayName}'."));
            if(shape.ReadonlyMembers.Contains(name))
                throw new CompilationFailure(Diagnostic.Error(file,location,"TS2540",
                    $"Cannot assign to '{name}' because it is a read-only property."));
            if(shape.OptionalMembers.Contains(name))
                throw new CompilationFailure(Diagnostic.Error(file,location,"TSCS1001",
                    "Writes to optional members of union receivers are not supported yet."));
            if(common is not null && !SameWritableType(common,member))
                throw new CompilationFailure(Diagnostic.Error(file,location,"TSCS1001",
                    "Union writes require an equivalent property type in every alternative."));
            common=member;
        }
        return common??throw new CompilationFailure(Diagnostic.Error(file,location,"TSCS1001",
            "A union write requires at least one checked object alternative."));
    }

    private void EnsureWritableIndex(ValueElementAccess target,TypeSymbol? receiver)
    {
        if(receiver is ArrayTypeSymbol {Readonly:true} readonlyArray)
            throw new CompilationFailure(Diagnostic.Error(file,target.Open,"TS2542",
                $"Index signature in type '{readonlyArray.DisplayName}' only permits reading."));
        if(CompilerTypes.IsStringFamily(receiver))
            throw new CompilationFailure(Diagnostic.Error(file,target.Open,"TS2542",
                "Index signature in type 'String' only permits reading."));
        if(!TryStaticNamedIndex(target.Index,receiver,out var key))return;
        if(receiver is ObjectTypeSymbol shape && shape.ReadonlyMembers.Contains(key))
            throw new CompilationFailure(Diagnostic.Error(file,target.Open,"TS2540",
                $"Cannot assign to '{key}' because it is a read-only property."));
        if(receiver is UnionTypeSymbol union)
            RequireCommonWritableUnionProperty(union,key,target.Open);
    }

    // Do not type-check a receiver/index twice: a computed index can itself mutate state.
    private TypeSymbol? CheckNumericUpdate(ValueNode target,Token op,Scope scope)
    {
        TypeSymbol? current;
        Symbol? binding=null;
        switch(target)
        {
            case ValueAtom { Token.Kind:TokenKind.Identifier } atom:
                binding=Resolve(atom.Token,scope);
                if(binding==null)return null;
                if(!binding.Initialized)
                    throw new CompilationFailure(Diagnostic.Error(file,atom.Token,"TSCS1001",
                        "Reads before initialization are not supported yet."));
                if(binding.Imported)
                    Error(atom.Token,"TS2632",$"Cannot assign to '{atom.Token.Text}' because it is an import.");
                else if(binding.IsConstant)
                    Error(atom.Token,"TS2588",$"Cannot assign to '{atom.Token.Text}' because it is a constant.");
                current=binding.Type;
                break;
            case ValuePropertyAccess property:
                var propertyReceiver=TypeOf(property.Receiver,scope);
                if(propertyReceiver==null)return null;
                if(propertyReceiver is ArrayTypeSymbol {Readonly:true} && property.Name.Text=="length")
                    throw new CompilationFailure(Diagnostic.Error(file,property.Name,"TS2540",
                        "Cannot assign to 'length' because it is a read-only property."));
                if(CompilerTypes.IsStringFamily(propertyReceiver) && property.Name.Text=="length")
                    throw new CompilationFailure(Diagnostic.Error(file,property.Name,"TS2540",
                        "Cannot assign to 'length' because it is a read-only property."));
                if(propertyReceiver is ObjectTypeSymbol shape && shape.ReadonlyMembers.Contains(property.Name.Text))
                    throw new CompilationFailure(Diagnostic.Error(file,property.Name,"TS2540",
                        $"Cannot assign to '{property.Name.Text}' because it is a read-only property."));
                if(propertyReceiver is UnionTypeSymbol updateUnion)
                    RequireCommonWritableUnionProperty(updateUnion,property.Name.Text,property.Name);
                current=CompilerTypes.TryGetProperty(propertyReceiver,property.Name.Text);
                if(current==null)
                    Error(property.Name,"TS2339",
                        $"Property '{property.Name.Text}' does not exist on type '{propertyReceiver.DisplayName}'.");
                break;
            case ValueElementAccess element:
                var elementReceiver=TypeOf(element.Receiver,scope);
                var indexType=TypeOf(element.Index,scope);
                bool namedObjectIndex=TryStaticNamedIndex(element.Index,elementReceiver,out var propertyName) &&
                    (elementReceiver is ObjectTypeSymbol || elementReceiver is UnionTypeSymbol union &&
                     union.Alternatives.All(part=>part is ObjectTypeSymbol));
                if(indexType!=null && !CompilerTypes.IsNumber(indexType) && !namedObjectIndex)
                    throw new CompilationFailure(Diagnostic.Error(file,element.Open,"TSCS1001",
                        "Indexed updates currently require a number expression."));
                if(CompilerTypes.IsStringFamily(elementReceiver))
                {
                    Error(op,"TS2356",
                        "An arithmetic operand must be of type 'any', 'number', 'bigint' or an enum type.");
                    Error(element.Open,"TS2542",
                        "Index signature in type 'String' only permits reading.");
                    return null;
                }
                EnsureWritableIndex(element,elementReceiver);
                if(namedObjectIndex && elementReceiver is { } namedObject)
                {
                    current=CompilerTypes.TryGetProperty(namedObject,propertyName);
                    if(current==null)
                        Error(element.Open,"TS2339",
                            $"Property '{propertyName}' does not exist on type '{namedObject.DisplayName}'.");
                }
                else if(elementReceiver is ArrayTypeSymbol array)
                    current=array.ElementType;
                else if(elementReceiver==null || indexType==null)
                    current=null;
                else throw new CompilationFailure(Diagnostic.Error(file,element.Open,"TSCS1001",
                    "Indexed updates currently support arrays or literal-key object properties only."));
                break;
            default:
                throw new CompilationFailure(Diagnostic.Error(file,op,"TSCS1001",
                    "Numeric updates require an identifier, property or indexed array element."));
        }
        if(current==null)return null;
        if(!IsNumericMutationOperand(current))
            throw new CompilationFailure(Diagnostic.Error(file,op,"TSCS1001",
                "Numeric updates require a number or any operand."));
        if(binding is { Imported:false,IsConstant:false })
        {
            binding.Initialized=true;
            binding.FlowWriteVersion++;
            binding.Type=binding.DeclaredType??binding.Type;
        }
        return CompilerTypes.Number;
    }

    private static bool IsNumericMutationOperand(TypeSymbol type)=>
        CompilerTypes.IsNumber(type)||type.Equals(CompilerTypes.Any);


    // Delete checks finite mutable optional members of typed receiver
    // expressions. Nullish guarded receivers return true; unsafe targets fail closed.
    private void CheckDeleteOptionalMember(ValueNode operand,Scope scope,Token keyword)
    {
        while(operand is ValueParenthesized grouped)operand=grouped.Expression;
        ValueNode receiver;
        ValueNode? index=null;
        string? name=null;
        bool guarded=false;
        switch(operand)
        {
            case ValuePropertyAccess member:
                receiver=member.Receiver;
                name=member.Name.Text;
                guarded=member.Optional || member.ChainContinuation;
                break;
            case ValueElementAccess element:
                receiver=element.Receiver;
                index=element.Index;
                guarded=element.Optional || element.ChainContinuation;
                break;
            default:
                throw new CompilationFailure(Diagnostic.Error(file,keyword,"TSCS1001",
                    "Delete currently requires a finite optional object member target."));
        }
        // Reuse the type checker for nested properties, array elements and
        // typed function results. No implicit any or dynamic index signatures.
        // A continued optional chain checks only its present-path receiver here.
        // The ordinary chain TypeOf pass still rejects unsafe nullable
        // intermediate members; emission guards the original root once.
        var receiverType=TypeOf(receiver,scope);
        var presentType=receiverType is null?null:guarded
            ?CompilerTypes.NonNullAssertionResult(receiverType):receiverType;
        if(presentType is not ObjectTypeSymbol shape)
            throw new CompilationFailure(Diagnostic.Error(file,keyword,"TSCS1001",
                "Delete currently requires a finite object receiver."));
        if(index is not null && !TryStaticNamedIndex(index,shape,out name))
            throw new CompilationFailure(Diagnostic.Error(file,keyword,"TSCS1001",
                "Delete currently requires a statically named object index."));
        if(name is null || !shape.Members.ContainsKey(name))
            throw new CompilationFailure(Diagnostic.Error(file,keyword,"TSCS1001",
                "Delete currently requires a declared optional object member."));
        if(shape.ReadonlyMembers.Contains(name))
            throw new CompilationFailure(Diagnostic.Error(file,keyword,"TS2704",
                "The operand of a 'delete' operator cannot be a read-only property."));
        if(!shape.OptionalMembers.Contains(name))
            throw new CompilationFailure(Diagnostic.Error(file,keyword,"TS2790",
                "The operand of a 'delete' operator must be optional."));
    }

    private void CheckDoWhile(DoWhileStatement loop,Scope scope,TypeSymbol? expectedReturn)
    {
        // The condition and body can run again after a rebinding on the
        // previous iteration, so guard facts from before the loop are not
        // reliable read types for their later evaluations.
        InvalidateLoopBackedgeFlowTypes(scope,loop.Body);
        // Check the condition with entry-state facts. This deliberately
        // rejects a do body that only *may* initialize a condition variable:
        // a continue or break may bypass its write on the first iteration.
        var conditionType=TypeOf(loop.Condition,scope);
        CheckRuntimeCondition(loop.Condition,conditionType,loop.Keyword);
        var before=CaptureInitialized(scope);
        var writesBefore=CaptureFlowWriteVersions(scope);
        try
        {
            CheckBlock(loop.Body,scope,false,expectedReturn,true,true);
        }
        finally
        {
            // In the bounded analysis, neither a loop iteration nor its
            // continue/break paths may fabricate definite post-loop writes.
            RestoreInitialized(before);
            InvalidateLoopWrittenFlowTypes(writesBefore);
        }
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
        // A for-update is speculative when checking the first body entry:
        // join possible body/update rebindings before checking both, then
        // restore that same read view after inspecting the update.
        InvalidateLoopBackedgeFlowTypes(header,loop.Body,loop.Update);
        if(loop.Condition is { } condition)
        {
            var conditionType=TypeOf(condition,header);
            CheckRuntimeCondition(condition,conditionType,loop.Keyword);
        }
        var before=CaptureInitialized(header);
        var writesBefore=CaptureFlowWriteVersions(header);
        try
        {
            // The update runs after the body (and after a continue), never
            // before its first iteration. Check its types independently, then
            // restore ALL initialization facts, including header locals, before
            // checking the body. Do not let the update fabricate a first-read.
            var entryTypes=CaptureFlowTypes(header);
            if(loop.Update is { } update)
                CheckBlock(new Block([update]),header,true,expectedReturn);
            RestoreInitialized(before);
            RestoreFlowTypes(entryTypes);
            CheckBlock(loop.Body,header,false,expectedReturn,true,true);
        }
        finally
        {
            RestoreInitialized(before);
            InvalidateLoopWrittenFlowTypes(writesBefore);
        }
    }

    // Each branch runs against the same lexical parent, but writes to symbols
    // are joined conservatively; an unexecuted arm cannot initialize a value.
    private DiscriminantContinuation? CheckConditional(ConditionalStatement conditional,Scope scope,TypeSymbol? expectedReturn,bool allowBreak,bool allowContinue)
    {
        var conditionType=TypeOf(conditional.Condition,scope);
        CheckRuntimeCondition(conditional.Condition,conditionType,conditional.Keyword);
        // Discriminant types are a branch-local view of one existing symbol.
        // Never mutate the AST or permanently narrow the declaration type.
        var discriminant=TryNarrowDiscriminant(conditional.Condition,scope);
        var before=CaptureInitialized(scope);
        var incomingTypes=CaptureFlowTypes(scope);
        var writesBefore=CaptureFlowWriteVersions(scope);
        var whenTrueWrites=writesBefore;
        var whenFalseWrites=writesBefore;
        var whenTrue=before;
        var whenFalse=before;
        var typesWhenTrue=incomingTypes;
        var typesWhenFalse=incomingTypes;
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
            typesWhenTrue=CaptureFlowTypes(scope);
            whenTrueWrites=CaptureFlowWriteVersions(scope);
            RestoreInitialized(before);
            RestoreFlowTypes(incomingTypes);
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
            typesWhenFalse=CaptureFlowTypes(scope);
            whenFalseWrites=CaptureFlowWriteVersions(scope);
        }
        finally
        {
            RestoreInitialized(before);
            RestoreFlowTypes(incomingTypes);
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
        // Flow writes are may-effects, even if a nested block has restored
        // its temporary narrowing. A possible surviving write invalidates
        // the incoming read refinement; returning arms have no such effect.
        var invalidated=new HashSet<Symbol>();
        foreach(var (symbol,version) in writesBefore)
        {
            bool trueWrites=whenTrueWrites[symbol]!=version;
            bool falseWrites=whenFalseWrites[symbol]!=whenTrueWrites[symbol];
            if((thenFallsThrough && trueWrites)||(elseFallsThrough && falseWrites))
            {
                symbol.Type=symbol.DeclaredType??incomingTypes[symbol];
                invalidated.Add(symbol);
            }
        }
        if(discriminant is not { } narrowed || thenFallsThrough==elseFallsThrough)
            return null;
        // A changed branch discriminant must not be restored to an obsolete
        // matched arm. Otherwise preserve the exact returning-guard refinement.
        var retained=thenFallsThrough?typesWhenTrue[narrowed.Symbol]:typesWhenFalse[narrowed.Symbol];
        return new DiscriminantContinuation(narrowed.Symbol,
            invalidated.Contains(narrowed.Symbol)
                ?(narrowed.Symbol.DeclaredType??incomingTypes[narrowed.Symbol]??retained!)
                :retained!);
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

    private static DiscriminantBranches? TryNarrowDiscriminant(ValueNode condition,Scope scope,int depth=0)
    {
        // A deeply nested boolean expression must never overflow the managed
        // stack. Beyond this bound no refinement is inferred (never guess).
        if(depth>=64)return null;
        while(condition is ValueParenthesized grouped)condition=grouped.Expression;
        if(condition is ValueUnary { Operator.Kind:TokenKind.Bang } inverted)
        {
            var inner=TryNarrowDiscriminant(inverted.Operand,scope,depth+1);
            return inner is { } branches
                ?new DiscriminantBranches(branches.Symbol,branches.WhenFalse,branches.WhenTrue)
                :null;
        }
        if(condition is ValueBinary logical &&
           logical.Operator.Kind is TokenKind.AmpAmp or TokenKind.BarBar)
        {
            var first=TryNarrowDiscriminant(logical.Left,scope,depth+1);
            if(first is not { } left)return null;
            var original=left.Symbol.Type!;
            DiscriminantBranches? next;
            try
            {
                left.Symbol.Type=logical.Operator.Kind==TokenKind.AmpAmp
                    ?left.WhenTrue:left.WhenFalse;
                next=TryNarrowDiscriminant(logical.Right,scope,depth+1);
            }
            finally {left.Symbol.Type=original;}
            // Right-only facts contribute only on the path that evaluates the
            // RHS. With an unrelated/unknown RHS, leave the other exit broad.
            if(next is { } right && ReferenceEquals(right.Symbol,left.Symbol))
            {
                try
                {
                    return logical.Operator.Kind==TokenKind.AmpAmp
                        ?new DiscriminantBranches(left.Symbol,right.WhenTrue,
                            CompilerTypes.Union([left.WhenFalse,right.WhenFalse]))
                        :new DiscriminantBranches(left.Symbol,
                            CompilerTypes.Union([left.WhenTrue,right.WhenTrue]),right.WhenFalse);
                }
                catch(InvalidOperationException) {return null;}
            }
            return logical.Operator.Kind==TokenKind.AmpAmp
                ?new DiscriminantBranches(left.Symbol,left.WhenTrue,original)
                :new DiscriminantBranches(left.Symbol,original,left.WhenFalse);
        }
        if(TryNarrowTruthyIdentifier(condition,scope) is { } truthy)
            return truthy;
        if(condition is not ValueBinary comparison)return null;
        if(comparison.Operator.Kind==TokenKind.In)
            return TryNarrowInProperty(comparison,scope);
        if(comparison.Operator.Kind is not (TokenKind.StrictEqual or TokenKind.StrictNotEqual or
               TokenKind.EqualEqual or TokenKind.BangEqual))
            return null;

        // The null comparison partition is operator-sensitive: strict equality
        // removes only null, while ==/!= null also matches undefined.
        if(TryNarrowNullishComparison(comparison,scope) is { } nullGuard)
            return nullGuard;
        if(comparison.Operator.Kind is not (TokenKind.StrictEqual or TokenKind.StrictNotEqual))
            return null;

        // typeof guards share existing branch-local and returning-guard flow.
        if(TryNarrowPrimitiveTypeof(comparison,scope) is { } primitive)
            return primitive;

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


    // Only the existing stable guards narrow symbols. A general checked
    // JavaScript expression may be tested at runtime without gaining flow facts.
    // TypeScript diagnoses an explicit void expression as always falsy
    // (TS2873), separately from testing a void-typed function result (TS1345).
    private void CheckRuntimeCondition(ValueNode condition,TypeSymbol? value,Token location)
    {
        // Unary void evaluates its operand but has the semantic value
        // 'undefined'. Keep that value type intact for assignments and
        // nullish operators; detect forbidden condition syntax separately.
        while(condition is ValueParenthesized group)condition=group.Expression;
        if(condition is ValueUnary { Operator.Kind:TokenKind.Void })
            throw new CompilationFailure(Diagnostic.Error(file,location,"TS2873",
                "This kind of expression is always falsy."));
        if(value is PrimitiveTypeSymbol { Kind:PrimitiveKind.Void })
            throw new CompilationFailure(Diagnostic.Error(file,location,"TS1345",
                "An expression of type 'void' cannot be tested for truthiness."));
    }

    // Only direct identifier reads have stable flow identity in this subset.
    private static bool IsBoundedTruthyIdentifier(ValueNode condition)
    {
        while(condition is ValueParenthesized group)condition=group.Expression;
        return condition is ValueAtom { Token.Kind:TokenKind.Identifier };
    }

    // Falsy number/string values are possible even when their types exclude
    // nullish values. Retain broad numeric/string/boolean types in both arms.
    // Only exact literal values and known always-truthy object/function shapes
    // are partitioned; any/unknown and unhandled types never gain assumptions.
    private static DiscriminantBranches? TryNarrowTruthyIdentifier(ValueNode condition,Scope scope)
    {
        if(!IsBoundedTruthyIdentifier(condition))return null;
        while(condition is ValueParenthesized group)condition=group.Expression;
        var atom=(ValueAtom)condition;
        var symbol=scope.Find(atom.Token.Text);
        if(symbol?.Type is not UnionTypeSymbol union)return null;
        var truthy=new List<TypeSymbol>();
        var falsy=new List<TypeSymbol>();
        foreach(var alternative in union.Alternatives)
        {
            bool mayBeTruthy,mayBeFalsy;
            switch(alternative)
            {
                case PrimitiveTypeSymbol { Kind:PrimitiveKind.Null or PrimitiveKind.Undefined }:
                    mayBeTruthy=false;mayBeFalsy=true;
                    break;
                case PrimitiveTypeSymbol { Kind:PrimitiveKind.Void }:
                    // A void-typed callback may still return a runtime value.
                    // Never assume this constituent is always falsy.
                    mayBeTruthy=true;mayBeFalsy=true;
                    break;
                case PrimitiveTypeSymbol { Kind:PrimitiveKind.Number or PrimitiveKind.String or PrimitiveKind.Boolean }:
                    mayBeTruthy=true;mayBeFalsy=true;
                    break;
                case LiteralTypeSymbol { Kind:PrimitiveKind.Boolean } literalBoolean:
                    mayBeTruthy=literalBoolean.Value=="true";
                    mayBeFalsy=!mayBeTruthy;
                    break;
                case LiteralTypeSymbol { Kind:PrimitiveKind.String } literalString:
                    mayBeTruthy=literalString.Value.Length>0;
                    mayBeFalsy=!mayBeTruthy;
                    break;
                case LiteralTypeSymbol { Kind:PrimitiveKind.Number } literalNumber:
                    if(!double.TryParse(literalNumber.Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,out var numeric))
                        return null;
                    mayBeTruthy=numeric!=0 && !double.IsNaN(numeric);
                    mayBeFalsy=!mayBeTruthy;
                    break;
                case ObjectTypeSymbol shape:
                    // The empty structural {} contract also accepts 0, "" and
                    // false. It is not proof of a non-falsy runtime object.
                    mayBeTruthy=true;mayBeFalsy=shape.Members.Count==0;
                    break;
                case ArrayTypeSymbol or FunctionTypeSymbol:
                    mayBeTruthy=true;mayBeFalsy=false;
                    break;
                default:
                    return null;
            }
            if(mayBeTruthy)truthy.Add(alternative);
            if(mayBeFalsy)falsy.Add(alternative);
        }
        // No unreachable-path modeling: both arms must have a live type.
        if(truthy.Count==0 || falsy.Count==0)return null;
        return new DiscriminantBranches(symbol,
            CompilerTypes.Union(truthy),CompilerTypes.Union(falsy));
    }

    // A literal, singleton-typed variable, or directly initialized immutable
    // literal variable has a known runtime property key. Widened/mutable keys
    // cannot safely partition object union alternatives.
    private static bool TryReadStaticInKey(ValueNode expression,Scope scope,out string key)
    {
        while(expression is ValueParenthesized group)expression=group.Expression;
        if(expression is ValueAtom atom)
        {
            if(atom.Token.Kind==TokenKind.String)
            {
                key=Parser.PropertyName(atom.Token);
                return true;
            }
            if(atom.Token.Kind==TokenKind.Number)
                return TrySafeInIntegerKey(atom.Token.Value??atom.Token.Text,out key);
            if(atom.Token.Kind==TokenKind.Identifier)
            {
                var symbol=scope.Find(atom.Token.Text);
                if(symbol is { Initialized:true })
                {
                    if(symbol.Type is LiteralTypeSymbol singleton)
                    {
                        if(singleton.Kind==PrimitiveKind.String)
                        {
                            key=singleton.Value;
                            return true;
                        }
                        if(singleton.Kind==PrimitiveKind.Number)
                            return TrySafeInIntegerKey(singleton.Value,out key);
                    }
                    // Bounded inference widens unannotated const primitives.
                    // Their literal initializers remain stable, even via imports.
                    // Explicit broad annotations must never be bypassed.
                    if(symbol.IsConstant &&
                       symbol.Declaration is Variable { Annotation:null,Initializer:{ } initializer })
                    {
                        while(initializer is ValueParenthesized parenthesized)
                            initializer=parenthesized.Expression;
                        if(initializer is ValueAtom initial)
                        {
                            if(initial.Token.Kind==TokenKind.String)
                            {
                                key=Parser.PropertyName(initial.Token);
                                return true;
                            }
                            if(initial.Token.Kind==TokenKind.Number)
                                return TrySafeInIntegerKey(initial.Token.Value??initial.Token.Text,out key);
                        }
                    }
                }
            }
        }
        key="";
        return false;
    }

    // Match finite, non-negative numeric member spelling. Do not guess
    // JavaScript ToPropertyKey for fractional or unsafe large numbers.
    private static bool TrySafeInIntegerKey(string text,out string key)
    {
        if(long.TryParse(text,System.Globalization.NumberStyles.None,
               System.Globalization.CultureInfo.InvariantCulture,out var value) &&
           value is >= 0 and <= 9007199254740991L)
        {
            key=value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        key="";
        return false;
    }

    // Required keys partition into present/absent; optional keys contribute
    // to both arms because the member may be absent at runtime.
    private static DiscriminantBranches? TryNarrowInProperty(ValueBinary comparison,Scope scope)
    {
        if(!TryReadStaticInKey(comparison.Left,scope,out var key))return null;
        var receiver=comparison.Right;
        while(receiver is ValueParenthesized receiverGroup)receiver=receiverGroup.Expression;
        if(receiver is not ValueAtom { Token.Kind:TokenKind.Identifier } name)return null;
        var symbol=scope.Find(name.Token.Text);
        if(symbol?.Type is not UnionTypeSymbol union)return null;

        var present=new List<TypeSymbol>();
        var absent=new List<TypeSymbol>();
        foreach(var candidate in union.Alternatives)
        {
            if(candidate is not ObjectTypeSymbol shape)return null;
            var hasMember=shape.Members.ContainsKey(key);
            if(hasMember)present.Add(candidate);
            if(!hasMember || shape.OptionalMembers.Contains(key))absent.Add(candidate);
        }
        if(present.Count==0 || absent.Count==0)return null;
        return new DiscriminantBranches(symbol,CompilerTypes.Union(present),CompilerTypes.Union(absent));
    }

    // A simple local/parameter can be narrowed against literal null, the
    // unshadowed undefined intrinsic, or the side-effect-free 'void 0' sentinel.
    // Strict comparisons partition only the named sentinel; loose comparisons
    // match both nullish constituents. Lexical bindings named undefined must
    // never be mistaken for the intrinsic. Complex member paths and broad
    // any/unknown types are not inferred as safe.
    private static DiscriminantBranches? TryNarrowNullishComparison(ValueBinary comparison,Scope scope)
    {
        static bool TrySentinel(ValueNode source,Scope scope,out bool isNull)
        {
            while(source is ValueParenthesized grouped)source=grouped.Expression;
            if(source is ValueAtom { Token.Kind:TokenKind.Null })
            {
                isNull=true;
                return true;
            }
            if(source is ValueAtom atom && atom.Token.Kind==TokenKind.Identifier &&
               atom.Token.Text=="undefined" && scope.Find("undefined") is null)
            {
                isNull=false;
                return true;
            }
            if(source is ValueUnary { Operator.Kind:TokenKind.Void } voidExpr)
            {
                var operand=voidExpr.Operand;
                while(operand is ValueParenthesized grouped)operand=grouped.Expression;
                if(operand is ValueAtom number && number.Token.Kind==TokenKind.Number &&
                   (number.Token.Value??number.Token.Text)=="0")
                {
                    isNull=false;
                    return true;
                }
            }
            isNull=false;
            return false;
        }

        static bool TryIdentifier(ValueNode source,out Token name)
        {
            while(source is ValueParenthesized grouped)source=grouped.Expression;
            if(source is ValueAtom { Token.Kind:TokenKind.Identifier } atom)
            {
                name=atom.Token;
                return true;
            }
            name=default;
            return false;
        }

        Token variable;
        bool isNullSentinel;
        if(TrySentinel(comparison.Left,scope,out isNullSentinel))
        {
            if(!TryIdentifier(comparison.Right,out variable))return null;
        }
        else if(TrySentinel(comparison.Right,scope,out isNullSentinel))
        {
            if(!TryIdentifier(comparison.Left,out variable))return null;
        }
        else return null;

        var symbol=scope.Find(variable.Text);
        if(symbol?.Type is not UnionTypeSymbol union)return null;
        bool loose=comparison.Operator.Kind is TokenKind.EqualEqual or TokenKind.BangEqual;
        var matching=new List<TypeSymbol>();
        var different=new List<TypeSymbol>();
        foreach(var alternative in union.Alternatives)
        {
            if(alternative.Equals(CompilerTypes.Any) || alternative.Equals(CompilerTypes.Unknown))
                return null;
            bool matches=loose
                ?alternative.Equals(CompilerTypes.Null) || alternative.Equals(CompilerTypes.Undefined)
                :isNullSentinel
                    ?alternative.Equals(CompilerTypes.Null)
                    :alternative.Equals(CompilerTypes.Undefined);
            (matches?matching:different).Add(alternative);
        }
        if(matching.Count==0 || different.Count==0)return null;
        var equal=CompilerTypes.Union(matching);
        var other=CompilerTypes.Union(different);
        return comparison.Operator.Kind is TokenKind.StrictEqual or TokenKind.EqualEqual
            ?new DiscriminantBranches(symbol,equal,other)
            :new DiscriminantBranches(symbol,other,equal);
    }

    // Match only runtime families known from finite TypeScript types.
    // typeof null is "object"; a declared function value is "function".
    // Arbitrary structural objects, any, unknown and void do not gain
    // unsound flow refinements.
    private static DiscriminantBranches? TryNarrowPrimitiveTypeof(ValueBinary comparison,Scope scope)
    {
        Token variable=default;
        string? expected=null;
        if(TryReadTypeofVariable(comparison.Left,out variable))
            expected=TryReadTypeofTag(comparison.Right);
        if(expected==null && TryReadTypeofVariable(comparison.Right,out variable))
            expected=TryReadTypeofTag(comparison.Left);
        if(expected==null)return null;
        var symbol=scope.Find(variable.Text);
        if(symbol?.Type is not UnionTypeSymbol union)return null;

        var matching=new List<TypeSymbol>();
        var remaining=new List<TypeSymbol>();
        foreach(var alternative in union.Alternatives)
        {
            string? family=alternative switch
            {
                PrimitiveTypeSymbol { Kind:PrimitiveKind.Number } => "number",
                PrimitiveTypeSymbol { Kind:PrimitiveKind.String } => "string",
                PrimitiveTypeSymbol { Kind:PrimitiveKind.Boolean } => "boolean",
                PrimitiveTypeSymbol { Kind:PrimitiveKind.Undefined } => "undefined",
                PrimitiveTypeSymbol { Kind:PrimitiveKind.Null } => "object",
                LiteralTypeSymbol { Kind:PrimitiveKind.Number } => "number",
                LiteralTypeSymbol { Kind:PrimitiveKind.String } => "string",
                LiteralTypeSymbol { Kind:PrimitiveKind.Boolean } => "boolean",
                FunctionTypeSymbol => "function",
                _=>null
            };
            if(family==null)return null;
            (family==expected?matching:remaining).Add(alternative);
        }
        if(matching.Count==0 || remaining.Count==0)return null;
        var equal=CompilerTypes.Union(matching);
        var other=CompilerTypes.Union(remaining);
        return comparison.Operator.Kind==TokenKind.StrictEqual
            ?new DiscriminantBranches(symbol,equal,other)
            :new DiscriminantBranches(symbol,other,equal);
    }

    private static bool TryReadTypeofVariable(ValueNode source,out Token variable)
    {
        while(source is ValueParenthesized group)source=group.Expression;
        if(source is ValueUnary { Operator.Kind:TokenKind.Typeof } expression)
        {
            var operand=expression.Operand;
            while(operand is ValueParenthesized nested)operand=nested.Expression;
            if(operand is ValueAtom atom && atom.Token.Kind==TokenKind.Identifier)
            {
                variable=atom.Token;
                return true;
            }
        }
        variable=default;
        return false;
    }

    private static string? TryReadTypeofTag(ValueNode source)
    {
        while(source is ValueParenthesized group)source=group.Expression;
        if(source is not ValueAtom { Token.Kind:TokenKind.String } literal)return null;
        return literal.Token.Value switch
        {
            "number" or "string" or "boolean" or "undefined" or "function" or "object"
                =>literal.Token.Value,
            _=>null
        };
    }

    private static bool TryReadDiscriminant(ValueNode source,out Token variable,out string member)
    {
        while(source is ValueParenthesized group)source=group.Expression;
        if(source is ValuePropertyAccess { Receiver:ValueAtom atom } access &&
           !access.Optional && atom.Token.Kind==TokenKind.Identifier)
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

    private void CheckTry(TryStatement statement,Scope scope,
        TypeSymbol? expectedReturn,bool allowBreak,bool allowContinue)
    {
        // A throw can interrupt the body before a write. Neither the try nor
        // catch path may grant the other speculative definite assignment.
        var initialized=CaptureInitialized(scope);
        var types=CaptureFlowTypes(scope);
        var writes=CaptureFlowWriteVersions(scope);
        try
        {
            CheckBlock(statement.Body,scope,false,expectedReturn,allowBreak,allowContinue);
        }
        finally
        {
            RestoreInitialized(initialized);
            RestoreFlowTypes(types);
        }
        if(statement.CatchBody is { } caught)
        {
            try
            {
                // Prebind the catch body so its lexical declarations share the
                // binding's scope, rejecting duplicate let/const declarations.
                var catchScope=Binder.Bind(file,caught,scope,initialize:false);
                if(statement.CatchBinding is { } binding)
                {
                    CheckStrictModuleBinding(binding);
                    if(binding.Text is "eval" or "arguments")
                        throw new CompilationFailure(Diagnostic.Error(file,binding,"TS1100",
                            "Invalid catch binding in strict-mode output."));
                    // Match pinned TS7: a caught exception has unknown type
                    // until proven safe, never unsound implicit any.
                    Binder.DeclareCatchBinding(file,catchScope,binding);
                }
                Binder.InitializeDeclaredTypes(catchScope);
                CheckBlock(caught,catchScope,rootAlreadyBound:true,
                    expectedReturn:expectedReturn,allowBreak:allowBreak,
                    allowContinue:allowContinue,preboundLocal:true);
            }
            finally
            {
                RestoreInitialized(initialized);
                RestoreFlowTypes(types);
            }
        }
        InvalidateLoopWrittenFlowTypes(writes);
        // Cleanup executes for caught and uncaught exceptions, even when
        // return/break/continue is pending. Post-try joins are conservative.
        if(statement.FinallyBody is { } cleanup)
            CheckBlock(cleanup,scope,false,expectedReturn,allowBreak,allowContinue);
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

    private static Dictionary<Symbol,TypeSymbol?> CaptureFlowTypes(Scope scope)
    {
        var values=new Dictionary<Symbol,TypeSymbol?>();
        for(Scope? current=scope;current!=null;current=current.Parent)
            foreach(var symbol in current.Symbols.Values)
                values.TryAdd(symbol,symbol.Type);
        return values;
    }

    private static void RestoreFlowTypes(IReadOnlyDictionary<Symbol,TypeSymbol?> values)
    {
        foreach(var (symbol,type) in values) symbol.Type=type;
    }

    private static Dictionary<Symbol,int> CaptureFlowWriteVersions(Scope scope)
    {
        var versions=new Dictionary<Symbol,int>();
        for(Scope? current=scope;current!=null;current=current.Parent)
            foreach(var symbol in current.Symbols.Values)
                versions.TryAdd(symbol,symbol.FlowWriteVersion);
        return versions;
    }


    // A loop's backedge may repeat any checked rebinding in its body/update.
    // Discover possible external writes from the parsed AST *before* type
    // checking the first iteration, then use declared types to validate all
    // reads, including while/for conditions and classic-for increments. This
    // intentionally sacrifices precision for the bounded flow checker; it
    // cannot prove a particular write is unreachable or that break prevents
    // another iteration. Nested lexical declarations must not invalidate a
    // shadowed outer variable. Function bodies are not executed by declaring
    // a function, so this scan does not traverse them.
    private static void InvalidateLoopBackedgeFlowTypes(
        Scope scope,Block body,Statement? update=null,string? iterationName=null)
    {
        var mayWrite=new HashSet<Symbol>();
        void Record(Token name,HashSet<string> shadowed)
        {
            if(!shadowed.Contains(name.Text) && scope.Find(name.Text) is { } symbol)
                mayWrite.Add(symbol);
        }
        // Value-producing mutations can be nested inside array indices, calls,
        // initializers and returns. Inspect the AST without evaluating expressions.
        void RecordValueWrites(ValueNode root,HashSet<string> shadowed)
        {
            var pending=new Stack<ValueNode>();
            pending.Push(root);
            while(pending.Count>0)
            {
                switch(pending.Pop())
                {
                    case ValuePrefixUpdate prefix:
                        if(prefix.Target is ValueAtom { Token.Kind:TokenKind.Identifier } prefixAtom)
                            Record(prefixAtom.Token,shadowed);
                        pending.Push(prefix.Target);
                        break;
                    case ValuePostfixUpdate postfix:
                        if(postfix.Target is ValueAtom { Token.Kind:TokenKind.Identifier } postfixAtom)
                            Record(postfixAtom.Token,shadowed);
                        pending.Push(postfix.Target);
                        break;
                    case ValueBinary binary:
                        pending.Push(binary.Right);pending.Push(binary.Left);break;
                    case ValueConditional chosen:
                        pending.Push(chosen.WhenFalse);
                        pending.Push(chosen.WhenTrue);
                        pending.Push(chosen.Condition);
                        break;
                    case ValueUnary unary:pending.Push(unary.Operand);break;
                    case ValueParenthesized grouped:pending.Push(grouped.Expression);break;
                    case ValueTypeOperator typed:pending.Push(typed.Expression);break;
                    case ValueNonNull asserted:pending.Push(asserted.Expression);break;
                    case ValueCall call:
                        pending.Push(call.Callee);
                        foreach(var arg in call.Arguments)pending.Push(arg);
                        break;
                    case ValueElementAccess indexed:
                        pending.Push(indexed.Receiver);pending.Push(indexed.Index);break;
                    case ValuePropertyAccess property:pending.Push(property.Receiver);break;
                    case ValueArraySpread spread:
                        pending.Push(spread.Operand);
                        break;
                    case ValueArray array:
                        foreach(var entry in array.Elements)pending.Push(entry);
                        break;
                    case ValueObject literal:
                        foreach(var property in literal.Properties)pending.Push(property.Value);
                        break;
                }
            }
        }
        void Visit(Statement statement,HashSet<string> shadowed)
        {
            switch(statement)
            {
                case Block block:
                    var locals=new HashSet<string>(shadowed,StringComparer.Ordinal);
                    foreach(var child in block.Statements)
                    {
                        switch(child)
                        {
                            case Variable declaration: locals.Add(declaration.Name.Text);break;
                            case FunctionDeclaration function: locals.Add(function.Name.Text);break;
                        }
                    }
                    foreach(var child in block.Statements)Visit(child,locals);
                    break;
                case Variable { Initializer: { } initial }:
                    RecordValueWrites(initial,shadowed);
                    break;
                case Assignment assignment:
                    Record(assignment.Name,shadowed);
                    RecordValueWrites(assignment.Value,shadowed);
                    break;
                case PropertyAssignment propertyAssignment:
                    RecordValueWrites(propertyAssignment.Target,shadowed);
                    RecordValueWrites(propertyAssignment.Value,shadowed);
                    break;
                case ElementAssignment elementAssignment:
                    RecordValueWrites(elementAssignment.Target,shadowed);
                    RecordValueWrites(elementAssignment.Value,shadowed);
                    break;
                case CompoundAssignment compound:
                    if(compound.Target is ValueAtom { Token.Kind:TokenKind.Identifier } compoundAtom)
                        Record(compoundAtom.Token,shadowed);
                    RecordValueWrites(compound.Target,shadowed);
                    RecordValueWrites(compound.Value,shadowed);
                    break;
                case PostfixUpdate postfixStatement:
                    Record(postfixStatement.Name,shadowed);
                    break;
                case Expression expression:
                    RecordValueWrites(expression.Value,shadowed);
                    break;
                case ReturnStatement returned:
                    if(returned.Value is { } returnedValue)RecordValueWrites(returnedValue,shadowed);
                    break;
                case ThrowStatement thrown:
                    RecordValueWrites(thrown.Value,shadowed);
                    break;
                case TryStatement guarded:
                    Visit(guarded.Body,shadowed);
                    if(guarded.CatchBody is { } caught)
                    {
                        var catchShadowed=new HashSet<string>(shadowed,StringComparer.Ordinal);
                        if(guarded.CatchBinding is { } binding)
                            catchShadowed.Add(binding.Text);
                        Visit(caught,catchShadowed);
                    }
                    if(guarded.FinallyBody is { } cleanup)
                        Visit(cleanup,shadowed);
                    break;
                case ConditionalStatement branch:
                    RecordValueWrites(branch.Condition,shadowed);
                    Visit(branch.Consequent,shadowed);
                    if(branch.Alternate is { } alternate)Visit(alternate,shadowed);
                    break;
                case SwitchStatement switching:
                    RecordValueWrites(switching.Selector,shadowed);
                    foreach(var clause in switching.Clauses)
                    {
                        if(clause.Label is { } label)RecordValueWrites(label,shadowed);
                        Visit(clause.Body,shadowed);
                    }
                    break;
                case ForInStatement nestedForIn:
                    RecordValueWrites(nestedForIn.Source,shadowed);
                    var keyLocals=new HashSet<string>(shadowed,StringComparer.Ordinal)
                    {
                        nestedForIn.Name.Text
                    };
                    Visit(nestedForIn.Body,keyLocals);
                    break;
                case ForOfStatement nestedForOf:
                    RecordValueWrites(nestedForOf.Source,shadowed);
                    var iterationLocals=new HashSet<string>(shadowed,StringComparer.Ordinal)
                    {
                        nestedForOf.Name.Text
                    };
                    Visit(nestedForOf.Body,iterationLocals);
                    break;
                case WhileStatement nestedWhile:
                    RecordValueWrites(nestedWhile.Condition,shadowed);
                    Visit(nestedWhile.Body,shadowed);
                    break;
                case DoWhileStatement nestedDo:
                    RecordValueWrites(nestedDo.Condition,shadowed);
                    Visit(nestedDo.Body,shadowed);
                    break;
                case ClassicForStatement nestedClassic:
                    var headerLocals=new HashSet<string>(shadowed,StringComparer.Ordinal);
                    if(nestedClassic.Initializer is Variable loopLocal)
                        headerLocals.Add(loopLocal.Name.Text);
                    if(nestedClassic.Initializer is { } nestedInit)
                        Visit(nestedInit,headerLocals);
                    if(nestedClassic.Condition is { } nestedCondition)
                        RecordValueWrites(nestedCondition,headerLocals);
                    if(nestedClassic.Update is { } nestedUpdate)
                        Visit(nestedUpdate,headerLocals);
                    Visit(nestedClassic.Body,headerLocals);
                    break;
            }
        }
        var initialShadows=new HashSet<string>(StringComparer.Ordinal);
        if(iterationName!=null)initialShadows.Add(iterationName);
        Visit(body,initialShadows);
        if(update is not null)Visit(update,initialShadows);
        foreach(var symbol in mayWrite)
            if(symbol.DeclaredType is { } declared)
                symbol.Type=declared;
    }

    private static void InvalidateLoopWrittenFlowTypes(IReadOnlyDictionary<Symbol,int> before)
    {
        // A loop may run zero times, while nested conditions can also return
        // after zero successful writes. Neither case may manufacture a read
        // narrowing outside the loop by checking its body speculatively.
        foreach(var (symbol,version) in before)
            if(symbol.FlowWriteVersion!=version)
                symbol.Type=symbol.DeclaredType??symbol.Type;
    }

    // General atom inference widens literals, but a syntactically known
    // left literal determines whether &&/|| can evaluate their right side.
    // Never apply this override to a stored or mutable identifier.
    private static TypeSymbol LogicalLeftLiteralOrInferred(ValueNode source,TypeSymbol inferred)
    {
        while(source is ValueParenthesized grouped)source=grouped.Expression;
        if(source is ValueAtom { Token.Kind:TokenKind.True or TokenKind.False or TokenKind.Number or TokenKind.String } atom)
            return CompilerTypes.FromLiteralExact(atom.Token);
        if(source is ValueUnary { Operator.Kind:TokenKind.Minus,Operand:ValueAtom { Token.Kind:TokenKind.Number } number })
            return CompilerTypes.FromLiteralExact(number.Token,negative:true);
        return inferred;
    }

    private int arrowExpressionDepth;
    // Only active while checking a braced callable with an inferred return.
    // Its previous value is restored even when the nested checker fails.
    private List<TypeSymbol?>? inferredArrowReturns;
    private TypeSymbol? TypeOf(ValueNode value,Scope scope)
    {
        // The parser builds left-deep chains for numeric additions. Do not
        // recursively descend them: supported source can contain hundreds of
        // thousands of '+' operators under the source-size limit.
        // Post-order evaluation preserves left-to-right binding diagnostics
        // and reports the first unsupported operand at the same operator.
        var pending=new Stack<(ValueNode Node,bool Combine)>();
        var types=new Stack<TypeSymbol?>();
        // A chain continuation's run-time value may be undefined solely
        // because an earlier optional segment skipped. Keep its type on
        // the *present* execution path separate from that short-circuit.
        // This must not erase undefined from an actually optional property.
        var optionalPresent=new Dictionary<ValueNode,TypeSymbol?>();
        var optionalSkipped=new Dictionary<ValueNode,bool>();
        void PushOptionalResult(ValueNode node,TypeSymbol? present,bool isChain,bool maySkip)
        {
            types.Push(present!=null && maySkip
                ?CompilerTypes.Union([present,CompilerTypes.Undefined]):present);
            if(isChain)
            {
                optionalPresent[node]=present;
                optionalSkipped[node]=maySkip;
            }
        }
        pending.Push((value,false));
        while(pending.Count>0)
        {
            var (node,combine)=pending.Pop();
            if(!combine && node is ValueBinary logical &&
               logical.Operator.Kind is TokenKind.AmpAmp or TokenKind.BarBar)
            {
                if(++logicalEvaluationDepth>64)
                {
                    logicalEvaluationDepth--;
                    throw new CompilationFailure(Diagnostic.Error(file,logical.Operator,"TSCS1001",
                        "Compound guard nesting exceeds the compiler subset limit."));
                }
                try
                {
                    // For && the RHS executes only when the LHS is true; for ||
                    // only when it is false. In particular, typecheck a member
                    // read on the RHS under the precise discriminated union arm.
                    var leftType=TypeOf(logical.Left,scope);
                    var leftFacts=TryNarrowDiscriminant(logical.Left,scope);
                    var original=leftFacts?.Symbol.Type;
                    TypeSymbol? rightType;
                    try
                    {
                        if(leftFacts is { } facts)
                            facts.Symbol.Type=logical.Operator.Kind==TokenKind.AmpAmp
                                ?facts.WhenTrue:facts.WhenFalse;
                        rightType=TypeOf(logical.Right,scope);
                    }
                    finally
                    {
                        if(leftFacts is { } facts)facts.Symbol.Type=original;
                    }
                    if(leftType is null || rightType is null)
                    {
                        types.Push(null);
                        continue;
                    }
                    try
                    {
                        var effectiveLeft=LogicalLeftLiteralOrInferred(logical.Left,leftType);
                        var result=CompilerTypes.LogicalValueResult(effectiveLeft,rightType,
                            logical.Operator.Kind==TokenKind.AmpAmp);
                        if(result is null)
                            throw new CompilationFailure(Diagnostic.Error(file,logical.Operator,
                                "TSCS1001","Logical expression operand types are outside the supported finite subset."));
                        logicalResults[logical]=result;
                        types.Push(result.Result);
                    }
                    catch(InvalidOperationException failure)
                    {
                        throw new CompilationFailure(Diagnostic.Error(file,logical.Operator,"TSCS1001",failure.Message));
                    }
                }
                finally { logicalEvaluationDepth--; }
                continue;
            }
            if(node is ValueArrow arrow)
            {
                if(++arrowExpressionDepth>32)
                {
                    arrowExpressionDepth--;
                    throw new CompilationFailure(Diagnostic.Error(file,arrow.Open,"TSCS1001",
                        "Arrow expression checking exceeds the supported depth."));
                }
                try
                {
                    // A named function expression has a private immutable name
                    // scope; a separate parameter scope permits name shadowing.
                    var nameScope=arrow.FunctionName is null?scope:new Scope(scope);
                    var local=new Scope(nameScope);
                    if(arrow.FunctionName is { } functionName)
                    {
                        CheckStrictModuleBinding(functionName);
                        if(arrow.BlockBody is not { } functionBody)
                            throw new CompilationFailure(Diagnostic.Error(file,functionName,"TSCS1001",
                                "Named function expressions require a checked block."));
                        if(arrow.ReturnAnnotation is { } result)
                            Binder.DeclareFunctionExpressionName(file,nameScope,functionName,
                                arrow.Parameters,result,functionBody);
                        else
                            Binder.DeclareInferredFunctionExpressionName(file,nameScope,functionName);
                    }
                    foreach(var parameter in arrow.Parameters)
                    {
                        // Default initializers see earlier parameters and outer
                        // lexical bindings, never the parameter being declared
                        // or a later one. They are checked before JS emission.
                        if(parameter.DefaultValue is { } initializer)
                        {
                            var expected=TypeResolver.Resolve(parameter.Annotation,local,file);
                            var actual=TypeOf(initializer,local);
                            if(actual is { } resolved &&
                               CheckContextualLiteral(initializer,expected,resolved) &&
                               !IsAssignableContextually(initializer,resolved,expected))
                                Mismatch(parameter.Name,resolved,expected);
                        }
                        Binder.DeclareParameter(file,local,parameter);
                    }
                    var annotated=arrow.ReturnAnnotation is { } syntax
                        ?TypeResolver.Resolve(syntax,local,file):null;
                    TypeSymbol? returned;
                    if(arrow.BlockBody is { } body)
                    {
                        if(annotated is { } required)
                        {
                            if(!required.Equals(CompilerTypes.Void) && !FunctionControlFlow.DefinitelyReturnsValue(body))
                                throw new CompilationFailure(Diagnostic.Error(file,arrow.Open,"TSCS1001",
                                    "A value-returning braced arrow must definitely return a value."));
                            // Annotated arrows retain their original checking path.
                            CheckBlock(body,local,false,required);
                            returned=required;
                        }
                        else
                        {
                            var previous=inferredArrowReturns;
                            var inferred=new List<TypeSymbol?>();
                            inferredArrowReturns=inferred;
                            try { CheckBlock(body,local); }
                            finally { inferredArrowReturns=previous; }

                            if(inferred.Any(type=>type is null))
                                returned=null; // Preserve the actual unresolved diagnostic.
                            else if(inferred.Count==0)
                                returned=FunctionControlFlow.CanCompleteNormally(body)
                                    ?CompilerTypes.Void:CompilerTypes.Never;
                            else
                            {
                                bool hasVoid=inferred.Any(type=>type!.Equals(CompilerTypes.Void));
                                bool hasValue=inferred.Any(type=>!type!.Equals(CompilerTypes.Void));
                                if(hasVoid && hasValue)
                                    throw new CompilationFailure(Diagnostic.Error(file,arrow.Open,"TSCS1001",
                                        "Mixed bare and value returns require an explicit return contract."));
                                // The bounded implementation does not invent an
                                // implicit undefined member on a fallthrough path.
                                if(hasValue && !FunctionControlFlow.DefinitelyReturnsValue(body))
                                    throw new CompilationFailure(Diagnostic.Error(file,arrow.Open,"TSCS1001",
                                        "Inferred value-returning arrows must return on every path."));
                                try { returned=CompilerTypes.Union(inferred.Select(type=>type!)); }
                                catch(InvalidOperationException failure)
                                {
                                    throw new CompilationFailure(Diagnostic.Error(file,arrow.Open,"TSCS1001",failure.Message));
                                }
                            }
                        }
                    }
                    else
                    {
                        var expression=arrow.Body!;
                        returned=TypeOf(expression,local);
                        if(returned is { } actual && annotated is { } required &&
                           CheckContextualLiteral(expression,required,actual) &&
                           !IsAssignableContextually(expression,actual,required))
                            Mismatch(arrow.Open,actual,required);
                    }
                    var signatureReturn=annotated??returned;
                    types.Push(signatureReturn is null?null:new FunctionTypeSymbol(
                        arrow.Parameters.Select(p=>local.Symbols[p.Name.Text].Type!).ToArray(),
                        signatureReturn,
                        arrow.Parameters.TakeWhile(p=>!p.Optional && !p.Rest).Count(),
                        arrow.Parameters.LastOrDefault() is { Rest:true,Annotation:ArrayTypeSyntax restArray }
                            ?TypeResolver.Resolve(restArray.Element,local,file):null));
                }
                finally {arrowExpressionDepth--;}
                continue;
            }
            if(node is ValueTemplate template)
            {
                TypeSymbol? result=CompilerTypes.String;
                foreach(var interpolation in template.Expressions)
                    if(TypeOf(interpolation,scope)==null)result=null;
                types.Push(result);
                continue;
            }
            if(node is ValueAtom atom)
            {
                types.Push(TypeOfAtom(atom.Token,scope,atom.Shorthand));
                continue;
            }
            if(node is ValuePrefixUpdate prefix)
            {
                types.Push(CheckNumericUpdate(prefix.Target,prefix.Operator,scope));
                continue;
            }
            if(node is ValuePostfixUpdate postfix)
            {
                types.Push(CheckNumericUpdate(postfix.Target,postfix.Operator,scope));
                continue;
            }
            if(node is ValueConditional chosen)
            {
                var conditionType=TypeOf(chosen.Condition,scope);
                CheckRuntimeCondition(chosen.Condition,conditionType,chosen.Question);
                // Reuse statement-branch discriminant and typeof narrowing.
                // Both arms are independently checked; temporary read views
                // are restored even if an arm reports an error.
                var partition=TryNarrowDiscriminant(chosen.Condition,scope);
                var original=partition?.Symbol.Type;
                TypeSymbol? whenTrue,whenFalse;
                try
                {
                    if(partition is { } narrowed) narrowed.Symbol.Type=narrowed.WhenTrue;
                    whenTrue=TypeOf(chosen.WhenTrue,scope);
                    if(partition is { } narrowedFalse) narrowedFalse.Symbol.Type=narrowedFalse.WhenFalse;
                    whenFalse=TypeOf(chosen.WhenFalse,scope);
                }
                finally
                {
                    if(partition is { } narrowed) narrowed.Symbol.Type=original;
                }
                // Defaults must respect the original selection contract,
                // including undefined: a fallback of undefined is valid for
                // an optional number, but a string is not. If the selection
                // is *only* undefined, the fallback determines the type and
                // the impossible present arm contributes nothing.
                var selectedType=partition?.Symbol.DeclaredType??
                    partition?.Symbol.Type??whenFalse;
                bool undefinedOnly=chosen.BindingDefault &&
                    selectedType?.Equals(CompilerTypes.Undefined)==true;
                bool invalidBindingDefault=false;
                if(chosen.BindingDefaultConstrained &&
                   selectedType is { } expectedDefault &&
                   whenTrue is { } fallback)
                {
                    // A source annotation constrains defaults; an inferred
                    // selection does not. A literal fallback may have a
                    // different type from a non-optional inferred member.
                    // Preserve recursive fresh-object checking for an
                    // optional object instead of stopping at union keys.
                    TypeSymbol contextualTarget=expectedDefault;
                    if(chosen.WhenTrue is ValueObject &&
                       expectedDefault is UnionTypeSymbol optionalUnion &&
                       optionalUnion.Alternatives.Count(t=>t is ObjectTypeSymbol)==1 &&
                       optionalUnion.Alternatives.All(t=>t is ObjectTypeSymbol ||
                           t.Equals(CompilerTypes.Undefined) ||
                           t.Equals(CompilerTypes.Null)))
                        contextualTarget=optionalUnion.Alternatives.OfType<ObjectTypeSymbol>().Single();
                    if(CheckContextualLiteral(chosen.WhenTrue,contextualTarget,fallback) &&
                       !IsAssignableContextually(chosen.WhenTrue,fallback,expectedDefault))
                    {
                        Mismatch(chosen.Question,fallback,expectedDefault);
                        invalidBindingDefault=true;
                    }
                }
                // The invalid fallback has already produced its primary
                // diagnostic. Do not poison the synthetic binding's inferred
                // result type with the rejected value and then emit misleading
                // secondary assignment/return errors in user code.
                if(invalidBindingDefault && whenFalse is not null)
                    whenTrue=whenFalse;
                conditionalResults[chosen]=(whenTrue,
                    undefinedOnly?CompilerTypes.Never:whenFalse);
                if(conditionType==null || whenTrue==null || whenFalse==null)
                    types.Push(null);
                else if(undefinedOnly)
                    types.Push(whenTrue);
                else
                {
                    try {types.Push(CompilerTypes.Union([whenTrue,whenFalse]));}
                    catch(InvalidOperationException failure)
                    {
                        throw new CompilationFailure(Diagnostic.Error(file,chosen.Question,"TSCS1001",failure.Message));
                    }
                }
                continue;
            }
            if(node is ValueNonNull assertedNonNull)
            {
                if(!combine)
                {
                    pending.Push((assertedNonNull,true));
                    pending.Push((assertedNonNull.Expression,false));
                    continue;
                }
                var inferred=types.Pop();
                types.Push(inferred is null?null:CompilerTypes.NonNullAssertionResult(inferred));
                continue;
            }
            if(node is ValueTypeOperator typeOperator)
            {
                if(!combine)
                {
                    pending.Push((typeOperator,true));
                    pending.Push((typeOperator.Expression,false));
                    continue;
                }
                var actual=types.Pop();
                var expected=TypeResolver.Resolve(typeOperator.Target,scope,file);
                if(actual==null) { types.Push(null);continue; }
                if(typeOperator.Keyword.Text=="as")
                {
                    // One-direction assignability is enough for an assertion;
                    // unrelated primitive/structural forms must not silently
                    // become casts that TypeScript itself would reject.
                    if(!CompilerTypes.IsAssignable(actual,expected) &&
                       !CompilerTypes.IsAssignable(expected,actual))
                        Error(typeOperator.Keyword,"TS2352",
                            $"Conversion of type '{actual.DisplayName}' to type '{expected.DisplayName}' may be a mistake because neither type sufficiently overlaps with the other.");
                    types.Push(expected);
                }
                else
                {
                    // Discriminated object-union contextual inference has not
                    // been implemented. Do not widen the result and then allow
                    // an unsafe subsequent write through a lost literal type.
                    if(expected is UnionTypeSymbol objectUnion &&
                       objectUnion.Alternatives.OfType<ObjectTypeSymbol>().Any(shape=>
                           shape.Members.Values.Any(member=>member is LiteralTypeSymbol ||
                               member is UnionTypeSymbol nested && nested.Alternatives.Any(
                                   variant=>variant is LiteralTypeSymbol))))
                        throw new CompilationFailure(Diagnostic.Error(file,typeOperator.Keyword,"TSCS1001",
                            "Satisfies with discriminated object unions requires contextual union inference."));
                    if(CheckContextualLiteral(typeOperator.Expression,expected,actual) &&
                       !IsAssignableContextually(typeOperator.Expression,actual,expected))
                        Error(typeOperator.Keyword,"TS1360",
                            $"Type '{actual.DisplayName}' does not satisfy expected type '{expected.DisplayName}'.");
                    types.Push(CompilerTypes.PreserveSatisfiedLiteralTypes(typeOperator.Expression,actual,expected));
                }
                continue;
            }
            if(node is ValueArraySpread spread)
            {
                if(!combine)
                {
                    pending.Push((spread,true));
                    pending.Push((spread.Operand,false));
                    continue;
                }
                var operandType=types.Pop();
                if(operandType is ArrayTypeSymbol expanded)
                    types.Push(expanded.ElementType);
                else if(operandType==null)types.Push(null);
                else throw new CompilationFailure(Diagnostic.Error(file,spread.Keyword,"TSCS1001",
                    "Array spread requires a statically checked array operand."));
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
                // Native array destructuring iterates its receiver. A normal
                // numeric index on a string/object is not sufficient evidence:
                // those reads have different observable JavaScript semantics.
                if(indexed.BindingArrayElement && receiverType!=null &&
                   receiverType is not ArrayTypeSymbol)
                    throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                        "Binding array patterns require a statically checked array receiver."));
                var inheritedSkip=indexed.ChainContinuation &&
                    optionalSkipped.TryGetValue(indexed.Receiver,out var indexSkip) && indexSkip;
                var mayBeUndefined=(indexed.Optional && receiverType!=null &&
                    CompilerTypes.ContainsNullish(receiverType)) || inheritedSkip;
                TypeSymbol? presentReceiver;
                if(indexed.ChainContinuation)
                {
                    if(!optionalPresent.TryGetValue(indexed.Receiver,out presentReceiver))
                        throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                            "Unvalidated optional index continuation."));
                }
                else presentReceiver=indexed.Optional && receiverType!=null
                    ?CompilerTypes.NonNullAssertionResult(receiverType):receiverType;
                bool isChain=indexed.Optional || indexed.ChainContinuation;
                if(presentReceiver!=null && TryStaticNamedIndex(indexed.Index,presentReceiver,out var key) &&
                   (presentReceiver is ObjectTypeSymbol or UnionTypeSymbol ||
                    presentReceiver.Equals(CompilerTypes.Any)))
                {
                    if(CompilerTypes.TryGetProperty(presentReceiver,key) is { } member)
                        PushOptionalResult(indexed,member,isChain,mayBeUndefined);
                    else
                    {
                        Error(indexed.Open,"TS2339",
                            $"Property '{key}' does not exist on type '{presentReceiver.DisplayName}'.");
                        PushOptionalResult(indexed,null,isChain,mayBeUndefined);
                    }
                    continue;
                }
                if(indexType!=null && !CompilerTypes.IsNumber(indexType))
                    throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                        "Indexed reads currently require a number expression."));
                if(presentReceiver is ArrayTypeSymbol array)
                    PushOptionalResult(indexed,array.ElementType,isChain,mayBeUndefined);
                else if(CompilerTypes.IsStringFamily(presentReceiver))
                    PushOptionalResult(indexed,CompilerTypes.String,isChain,mayBeUndefined);
                else if(presentReceiver==null || indexType==null)
                    PushOptionalResult(indexed,null,isChain,mayBeUndefined);
                else throw new CompilationFailure(Diagnostic.Error(file,indexed.Open,"TSCS1001",
                    "Index access currently supports arrays and strings only; object index signatures are not implemented."));
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
                var inferred=new TypeSymbol?[literal.Properties.Count];
                bool known=true;
                for(int i=inferred.Length-1;i>=0;i--)
                {
                    inferred[i]=types.Pop();
                    if(inferred[i]==null)known=false;
                }
                var members=new Dictionary<string,TypeSymbol>(StringComparer.Ordinal);
                var numericMembers=new HashSet<string>(StringComparer.Ordinal);
                var optionalMembers=new HashSet<string>(StringComparer.Ordinal);
                for(int i=0;i<inferred.Length;i++)
                {
                    var entry=literal.Properties[i];
                    var memberType=inferred[i];
                    if(memberType==null)continue;
                    if(entry.Spread)
                    {
                        if(memberType is not ObjectTypeSymbol source)
                            throw new CompilationFailure(Diagnostic.Error(file,entry.Name,"TSCS1001",
                                "Object spread requires a statically checked object operand."));
                        foreach(var member in source.Members)
                        {
                            if(source.OptionalMembers.Contains(member.Key) && members.ContainsKey(member.Key))
                                throw new CompilationFailure(Diagnostic.Error(file,entry.Name,"TSCS1001",
                                    "Overlapping optional object spread keys require conditional type inference."));
                            members[member.Key]=member.Value;
                            if(source.NumericMembers.Contains(member.Key))numericMembers.Add(member.Key);
                            else numericMembers.Remove(member.Key);
                            if(source.OptionalMembers.Contains(member.Key))optionalMembers.Add(member.Key);
                            else optionalMembers.Remove(member.Key);
                        }
                    }
                    else
                    {
                        var name=Parser.PropertyName(entry.Name);
                        members[name]=memberType;
                        optionalMembers.Remove(name);
                        if(entry.Name.Kind==TokenKind.Number)numericMembers.Add(name);
                        else numericMembers.Remove(name);
                    }
                }
                // Do not turn an unresolved property into an implicit any.
                types.Push(known?new ObjectTypeSymbol(members,numericMembers:numericMembers,optionalMembers:optionalMembers):null);
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
                var inheritedSkip=property.ChainContinuation &&
                    optionalSkipped.TryGetValue(property.Receiver,out var propertySkip) && propertySkip;
                var mayBeUndefined=(property.Optional && receiver!=null &&
                    CompilerTypes.ContainsNullish(receiver)) || inheritedSkip;
                TypeSymbol? presentReceiver;
                if(property.ChainContinuation)
                {
                    if(!optionalPresent.TryGetValue(property.Receiver,out presentReceiver))
                        throw new CompilationFailure(Diagnostic.Error(file,property.Name,"TSCS1001",
                            "Unvalidated optional property continuation."));
                }
                else presentReceiver=property.Optional && receiver!=null
                    ?CompilerTypes.NonNullAssertionResult(receiver):receiver;
                TypeSymbol? presentMember;
                if(presentReceiver==null)presentMember=null;
                else if(presentReceiver.Equals(CompilerTypes.Any))presentMember=CompilerTypes.Any;
                else if(presentReceiver.Equals(CompilerTypes.Unknown))
                {
                    Error(property.Name,"TS18046","Caught value is of type 'unknown'.");
                    presentMember=null;
                }
                else if(CheckedProperty(presentReceiver,property.Name) is { } member)
                    presentMember=member;
                else if(property.BindingFreshOmittedDefault &&
                        presentReceiver is ObjectTypeSymbol)
                    // Missing own key in a directly fresh initializer with a
                    // default is contextually undefined. Runtime still reads it.
                    presentMember=CompilerTypes.Undefined;
                else
                {
                    // For a known member on the non-nullish alternatives,
                    // TypeScript reports the unsafe receiver (TS18047/48/49),
                    // not a missing member (TS2339). Preserve TS2339 when
                    // the property really is absent from the live shape.
                    var presentOnly=CompilerTypes.NonNullAssertionResult(presentReceiver);
                    if(CompilerTypes.ContainsNullish(presentReceiver) &&
                       (presentOnly.Equals(CompilerTypes.Never) ||
                        CheckedProperty(presentOnly,property.Name) is not null))
                    {
                        bool hasNull=presentReceiver.Equals(CompilerTypes.Null) ||
                            presentReceiver is UnionTypeSymbol nullable &&
                            nullable.Alternatives.Any(t=>t.Equals(CompilerTypes.Null));
                        bool hasUndefined=CompilerTypes.ContainsUndefined(presentReceiver);
                        string code=hasNull
                            ?hasUndefined?"TS18049":"TS18047"
                            :"TS18048";
                        string possible=hasNull
                            ?hasUndefined?"'null' or 'undefined'":"'null'"
                            :"'undefined'";
                        Error(property.Name,code,$"Value is possibly {possible}.");
                    }
                    else
                        Error(property.Name,"TS2339",
                            $"Property '{property.Name.Text}' does not exist on type '{presentReceiver.DisplayName}'.");
                    presentMember=null;
                }
                PushOptionalResult(property,presentMember,property.Optional||property.ChainContinuation,mayBeUndefined);
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
                else if(unary.Operator.Kind==TokenKind.Bang)
                    // ECMAScript ! consumes truthiness for every checked runtime value.
                    types.Push(CompilerTypes.Boolean);
                else if(unary.Operator.Kind==TokenKind.Typeof)
                    types.Push(CompilerTypes.String);
                else if(unary.Operator.Kind==TokenKind.Void)
                    // JavaScript void evaluates the operand but produces undefined.
                    types.Push(CompilerTypes.Undefined);
                else if(unary.Operator.Kind==TokenKind.Delete)
                {
                    CheckDeleteOptionalMember(unary.Operand,scope,unary.Operator);
                    types.Push(CompilerTypes.Boolean);
                }
                else if(unary.Operator.Kind==TokenKind.Tilde &&
                    (CompilerTypes.IsNumber(operand)||operand.Equals(CompilerTypes.Any)))
                    types.Push(CompilerTypes.Number);
                else if((unary.Operator.Kind is TokenKind.Plus or TokenKind.Minus) &&
                    CompilerTypes.IsNumber(operand))
                    types.Push(CompilerTypes.Number);
                else throw new CompilationFailure(Diagnostic.Error(file,unary.Operator,"TSCS1001",
                    unary.Operator.Kind switch
                    {
                        TokenKind.Plus => "Only numeric unary plus is supported yet.",
                        TokenKind.Minus => "Only numeric unary minus is supported yet.",
                        TokenKind.Tilde => "Bitwise complement requires a number or any operand.",
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
                    pending.Push((call.Callee,false));
                    continue;
                }
                var arguments=new TypeSymbol?[call.Arguments.Count];
                for(int i=arguments.Length-1;i>=0;i--)arguments[i]=types.Pop();
                var calleeType=types.Pop();
                if(calleeType==null) { types.Push(null); continue; }
                var inheritedSkip=call.ChainContinuation &&
                    optionalSkipped.TryGetValue(call.Callee,out var callSkip) && callSkip;
                bool optionalMissing=(call.Optional && CompilerTypes.ContainsNullish(calleeType)) || inheritedSkip;
                TypeSymbol? callableType;
                if(call.ChainContinuation)
                {
                    if(!optionalPresent.TryGetValue(call.Callee,out callableType))
                        throw new CompilationFailure(Diagnostic.Error(file,call.Open,"TSCS1001",
                            "Unvalidated optional call continuation."));
                }
                else callableType=call.Optional?CompilerTypes.NonNullAssertionResult(calleeType):calleeType;
                // Preserve the actual union type of callable values and
                // indexed-access properties; a call may use a shared explicit
                // input signature even when each alternative returns a
                // different type. Guard the bounded return-union limit here
                // for both dot and bracket calls, never let it crash Analyze.
                if(callableType is UnionTypeSymbol)
                {
                    try
                    {
                        callableType=CompilerTypes.TryCommonCallableSignature(callableType)??callableType;
                    }
                    catch(InvalidOperationException failure)
                    {
                        throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                            "TSCS1001",failure.Message));
                    }
                }
                if(callableType is not FunctionTypeSymbol signature)
                    throw new CompilationFailure(Diagnostic.Error(file,call.Open,"TSCS1001",
                        "Calls currently require a declared function with an explicit signature."));
                // A supplied initial accumulator permits a distinct explicit U.
                // Validate both callback contravariance and the initial value
                // through the existing function argument checker. Without an
                // initial value the older same-element fold contract remains.
                if(signature.NativeArrayReduceElementType is { } element &&
                   call.SpreadIndex<0 && arguments.Length==2)
                {
                    // A callable callback fixes U from its explicitly typed
                    // first parameter; if the callback itself is invalid,
                    // the checked seed still fixes U, so only the bad callback
                    // gets TS2345 (matching pinned TS7 diagnostics).
                    TypeSymbol? accumulator=arguments[0] is FunctionTypeSymbol fold &&
                        fold.Parameters.Count==2 ? fold.Parameters[0] : arguments[1];
                    if(accumulator is not null)
                        signature=new FunctionTypeSymbol(
                            [new FunctionTypeSymbol([accumulator,element],accumulator),accumulator],
                            accumulator,2,NativeArrayReduceElementType:element);
                }
                // The short toSpliced(start, deleteCount?) overload permits an
                // omitted/undefined delete count. Inserting items selects the
                // long overload, where deleteCount must be number. Preserve
                // the ordinary argument checker/diagnostics for both paths.
                if(signature.NativeArrayToSplicedElementType is { } splicedElement &&
                   arguments.Length>=3)
                    signature=signature with {Parameters=[CompilerTypes.Number,
                        CompilerTypes.Number,splicedElement]};
                var resultType=optionalMissing
                    ?CompilerTypes.Union([signature.ReturnType,CompilerTypes.Undefined])
                    :signature.ReturnType;
                // Native Array<T>.map<U> is a bounded intrinsic: callback input
                // checking still follows the ordinary function relation below.
                // Only the result element is inferred from the explicit callable
                // return type; no untyped-parameter or arbitrary generic inference.
                TypeSymbol presentResult=signature.ReturnType;
                if(signature.NativeArrayMapElementType is not null &&
                   call.SpreadIndex<0 && arguments.Length==1 &&
                   arguments[0] is FunctionTypeSymbol mapper)
                {
                    var mapped=new ArrayTypeSymbol(mapper.ReturnType);
                    presentResult=mapped;
                    resultType=optionalMissing
                        ?CompilerTypes.Union([mapped,CompilerTypes.Undefined])
                        :mapped;
                }
                if(call.Optional || call.ChainContinuation)
                {
                    optionalPresent[call]=presentResult;
                    optionalSkipped[call]=optionalMissing;
                }
                // A bounded call spread expands one statically typed array into
                // a trailing array rest parameter. Fixed parameters must be
                // explicitly supplied; unknown-length spreads cannot invent
                // mandatory positional arguments.
                var restElement=signature.RestElementType;
                int supplied=call.SpreadIndex>=0?call.SpreadIndex:arguments.Length;
                if(call.SpreadIndex>=0)
                {
                    if(restElement is null)
                    {
                        Error(call.Open,"TS2556",
                            "A spread argument must be passed to a rest parameter (tuple spreads are unsupported).");
                        types.Push(resultType);
                        continue;
                    }
                    if(call.SpreadIndex!=arguments.Length-1 ||
                       call.SpreadIndex<signature.Parameters.Count-1)
                        throw new CompilationFailure(Diagnostic.Error(file,call.Open,"TSCS1001",
                            "A spread must follow all fixed parameters and be the final argument."));
                    if(arguments[call.SpreadIndex] is ArrayTypeSymbol expanded)
                    {
                        if(!CompilerTypes.IsAssignable(expanded.ElementType,restElement))
                            Error(call.Open,"TS2345",
                                $"Argument of type '{expanded.ElementType.DisplayName}' is not assignable to parameter of type '{restElement.DisplayName}'.");
                    }
                    else if(arguments[call.SpreadIndex] is not null)
                        throw new CompilationFailure(Diagnostic.Error(file,call.Open,"TSCS1001",
                            "Spread arguments currently require a statically checked array type."));
                }
                if(supplied<signature.MinimumArguments ||
                   (restElement is null && supplied>signature.Parameters.Count))
                {
                    var expected=restElement is not null
                        ?$"at least {signature.MinimumArguments}"
                        :signature.MinimumArguments==signature.Parameters.Count
                            ?signature.Parameters.Count.ToString()
                            :$"{signature.MinimumArguments}-{signature.Parameters.Count}";
                    // For a native toSpliced call with no start argument,
                    // the pinned TS7 overload emits TS2555 (minimum arity),
                    // not TS2554. Preserve the real failure in both paths.
                    Error(call.Open,signature.NativeArrayToSplicedElementType is not null &&
                        supplied==0?"TS2555":"TS2554",
                        $"Expected {expected} arguments, but got {supplied}.");
                    // TS7 reports only the native reducing overload's arity
                    // error when the number of arguments is wrong. Keep the
                    // failure but avoid unrelated parameter mismatch noise.
                    if(signature.NativeArrayReduceElementType is not null)
                    {
                        types.Push(resultType);
                        continue;
                    }
                }
                for(int i=0;i<arguments.Length;i++)
                {
                    if(i==call.SpreadIndex)continue;
                    var required=restElement is not null && i>=signature.Parameters.Count-1
                        ?restElement
                        :i<signature.Parameters.Count?signature.Parameters[i]:null;
                    if(required is { } type && arguments[i] is { } actual &&
                       CheckContextualLiteral(call.Arguments[i],type,actual) &&
                       !IsAssignableContextually(call.Arguments[i],actual,type))
                        Error(call.Open,"TS2345",
                            $"Argument of type '{actual.DisplayName}' is not assignable to parameter of type '{type.DisplayName}'.");
                }
                types.Push(resultType);
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
            else if(binary.Operator.Kind==TokenKind.QuestionQuestion)
            {
                try {types.Push(CompilerTypes.CoalescingResult(left,right));}
                catch(InvalidOperationException failure)
                {
                    throw new CompilationFailure(Diagnostic.Error(file,binary.Operator,"TSCS1001",failure.Message));
                }
            }
            else if(binary.Operator.Kind==TokenKind.Plus)
            {
                var resultType=CompilerTypes.AdditionResult(left,right);
                if(resultType!=null)types.Push(resultType);
                else throw new CompilationFailure(Diagnostic.Error(file,binary.Operator,"TSCS1001",
                    "Addition requires compatible numeric, string or any operands."));
            }
            else if(binary.Operator.Kind is TokenKind.Minus or TokenKind.Star)
            {
                if(CompilerTypes.IsNumber(left)&&CompilerTypes.IsNumber(right))
                    types.Push(CompilerTypes.Number);
                else throw new CompilationFailure(Diagnostic.Error(file,binary.Operator,"TSCS1001",
                    binary.Operator.Kind==TokenKind.Star
                        ? "Only numeric multiplication expressions are supported yet."
                        : "Only numeric subtraction expressions are supported yet."));
            }
            else if(binary.Operator.Kind is TokenKind.Slash or TokenKind.Percent or TokenKind.Power)
            {
                // Division and remainder evaluate to a number, including when
                // a bounded 'any' operand participates. Preserve JavaScript
                // floating-point quotient and signed-remainder runtime behavior.
                if((CompilerTypes.IsNumber(left)||left.Equals(CompilerTypes.Any)) &&
                   (CompilerTypes.IsNumber(right)||right.Equals(CompilerTypes.Any)))
                    types.Push(CompilerTypes.Number);
                else throw new CompilationFailure(Diagnostic.Error(file,binary.Operator,"TSCS1001",
                    "Division, remainder and exponentiation require number or any operands."));
            }
            else if(binary.Operator.Kind is TokenKind.Amp or TokenKind.Bar or TokenKind.Caret
                or TokenKind.LeftShift or TokenKind.RightShift or TokenKind.UnsignedRightShift)
            {
                // TS numeric bitwise operators convert to 32-bit integers at
                // runtime, but their static result type is always 'number'.
                // Do not model JavaScript coercion of strings/booleans as TS support.
                if((CompilerTypes.IsNumber(left)||left.Equals(CompilerTypes.Any)) &&
                   (CompilerTypes.IsNumber(right)||right.Equals(CompilerTypes.Any)))
                    types.Push(CompilerTypes.Number);
                else throw new CompilationFailure(Diagnostic.Error(file,binary.Operator,"TSCS1001",
                    "Bitwise and shift operators require number or any operands."));
            }
            else if(binary.Operator.Kind==TokenKind.In)
            {
                // Missing names can return false at runtime: this is not a
                // bracket read and does not imply flow narrowing.
                if(!CompilerTypes.CanUseInLeft(left))
                    Error(binary.Operator,"TS2322",
                        $"Type '{left.DisplayName}' is not assignable to type 'string | number | symbol'.");
                if(!CompilerTypes.CanUseInRight(right))
                    Error(binary.Operator,"TS2322",
                        $"Type '{right.DisplayName}' is not assignable to type 'object'.");
                types.Push(CompilerTypes.Boolean);
            }
            else if(binary.Operator.Kind is TokenKind.LessThan or TokenKind.LessEqual or TokenKind.GreaterThan or TokenKind.GreaterEqual)
            {
                if(CompilerTypes.CanCompareRelational(left,right))
                    types.Push(CompilerTypes.Boolean);
                else throw new CompilationFailure(Diagnostic.Error(file,binary.Operator,"TSCS1001",
                    "Relational comparisons require two number operands or two string operands."));
            }
            else if(binary.Operator.Kind is TokenKind.EqualEqual or TokenKind.BangEqual or TokenKind.StrictEqual or TokenKind.StrictNotEqual)
            {
                if(!binary.GeneratedUndefinedCheck && !CompilerTypes.CanCompareEquality(left,right))
                {
                    // Keep the comparison invalid, but continue semantic traversal
                    // so unsafe reads in either branch still produce their own
                    // diagnostics instead of being hidden by this first error.
                    Error(binary.Operator,"TSCS1001",
                        "Equality comparisons currently require matching primitive or any operands.");
                }
                types.Push(CompilerTypes.Boolean);
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
    private TypeSymbol? TypeOfAtom(Token token,Scope scope,bool shorthand=false)
    {
        var literal=CompilerTypes.FromLiteral(token);
        if(literal!=null)return literal;
        // Lexical declarations named undefined take precedence over the intrinsic.
        if(token.Kind==TokenKind.Identifier && token.Text=="undefined" && scope.Find("undefined") is null)
            return CompilerTypes.Undefined;
        var symbol=Resolve(token,scope,shorthand);
        if(symbol!=null&&!symbol.Initialized)throw new CompilationFailure(Diagnostic.Error(file,token,"TSCS1001","Reads before initialization are not supported yet."));
        return symbol?.Type;
    }
    private Symbol? Resolve(Token name,Scope scope,bool shorthand=false)
    {
        var symbol=scope.Find(name.Text);
        // Lexical declarations take precedence over the intrinsic, including TDZ checks.
        if(symbol==null&&name.Text=="globalThis")
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001","The built-in globalThis type is not supported yet."));
        if(symbol==null)
        {
            if(shorthand)
                Error(name,"TS18004",$"No value exists in scope for the shorthand property '{name.Text}'. Either declare one or provide an initializer.");
            else
                Error(name,"TS2304",$"Cannot find name '{name.Text}'.");
        }
        else if(symbol.PendingFunctionReturnInference)
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001",
                "Self references in named function return inference require an explicit return annotation."));
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
    private bool IsAssignableContextually(ValueNode expression,TypeSymbol actual,TypeSymbol expected,int depth=0)
    {
        if(depth>64)return false;
        // A stored union remains assignable to itself; only fresh source
        // literals need contextual refinement beyond ordinary type relations.
        if(CompilerTypes.IsAssignable(actual,expected))return true;
        while(expression is ValueParenthesized group)expression=group.Expression;
        // The two arms may inhabit different alternatives of an expected union.
        if(expression is ValueConditional chosen && conditionalResults.TryGetValue(chosen,out var branches))
            return branches.True is { } t && branches.False is { } f &&
                IsAssignableContextually(chosen.WhenTrue,t,expected,depth+1) &&
                IsAssignableContextually(chosen.WhenFalse,f,expected,depth+1);
        if(expression is ValueBinary { Operator.Kind:TokenKind.AmpAmp or TokenKind.BarBar } logical &&
           logicalResults.TryGetValue(logical,out var logicalBranches))
            return (logicalBranches.LeftResult is not { } l ||
                    IsAssignableContextually(logical.Left,l,expected,depth+1)) &&
                   (!logicalBranches.RightReachable ||
                    IsAssignableContextually(logical.Right,logicalBranches.Right,expected,depth+1));
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
                if(!data.Members.TryGetValue(name,out var member))
                {
                    if(shape.OptionalMembers.Contains(name))continue;
                    return false;
                }
                if(data.OptionalMembers.Contains(name) && !shape.OptionalMembers.Contains(name))return false;
                var requiredValue=shape.OptionalMembers.Contains(name)
                    ?CompilerTypes.Union([required,CompilerTypes.Undefined]):required;
                var property=fresh.Properties.LastOrDefault(p=>!p.Spread && Parser.PropertyName(p.Name)==name);
                if(property is null)
                {
                    if(!CompilerTypes.IsAssignable(member,requiredValue))return false;
                }
                else if(!IsAssignableContextually(property.Value,member,requiredValue,depth+1))
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
        if(value is ValueConditional chosen && conditionalResults.TryGetValue(chosen,out var branches))
        {
            // Freshness is a property of the source arms, not the joined
            // structural union. Keep per-property excess diagnostics.
            bool armsValid=true;
            if(branches.True is { } t && !CheckContextualLiteral(chosen.WhenTrue,expected,t))armsValid=false;
            if(branches.False is { } f && !CheckContextualLiteral(chosen.WhenFalse,expected,f))armsValid=false;
            return armsValid;
        }
        if(value is ValueBinary { Operator.Kind:TokenKind.AmpAmp or TokenKind.BarBar } logical &&
           logicalResults.TryGetValue(logical,out var logicalBranches))
        {
            // Check fresh objects only on operand paths whose value can
            // escape the short-circuit operator.
            bool operandsValid=true;
            if(logicalBranches.LeftResult is { } left &&
               !CheckContextualLiteral(logical.Left,expected,left))operandsValid=false;
            if(logicalBranches.RightReachable &&
               !CheckContextualLiteral(logical.Right,expected,logicalBranches.Right))operandsValid=false;
            return operandsValid;
        }
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
                    if(property.Spread)continue;
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
            if(property.Spread)continue;
            var name=Parser.PropertyName(property.Name);
            if(!shape.Members.TryGetValue(name,out var required))
            {
                Error(property.Name,"TS2353",$"Object literal may only specify known properties, and '{name}' does not exist in type '{shape.DisplayName}'.");
                valid=false;
            }
            else if(data.Members.TryGetValue(name,out var member))
            {
                if(shape.OptionalMembers.Contains(name))
                    required=CompilerTypes.Union([required,CompilerTypes.Undefined]);
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
        if(source is ArrayTypeSymbol {Readonly:true} &&
           target is ArrayTypeSymbol {Readonly:false})
        {
            Error(token,"TS4104",
                $"The type '{source.DisplayName}' is 'readonly' and cannot be assigned to the mutable type '{target.DisplayName}'.");
            return;
        }
        if(source is ObjectTypeSymbol data && target is ObjectTypeSymbol shape)
        {
            var missing=shape.Members.Keys.Where(name=>!shape.OptionalMembers.Contains(name) &&
                (!data.Members.ContainsKey(name) || data.OptionalMembers.Contains(name))).ToArray();
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

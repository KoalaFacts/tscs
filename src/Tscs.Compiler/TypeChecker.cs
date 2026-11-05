namespace Tscs;

// Primitive branches of checkVariableLikeDeclaration/checkIdentifier/checkBinaryLikeExpression.
internal sealed class TypeChecker(string file,bool strictModule=false)
{
    private readonly List<Diagnostic> diagnostics=[];
    private readonly Dictionary<ValueConditional,(TypeSymbol? True,TypeSymbol? False)> conditionalResults = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ValueBinary,LogicalValueOutcome> logicalResults = new(ReferenceEqualityComparer.Instance);
    // Array inference collects exact checked types before primitive widening.
    // Contextual tuple checks must use each slot, not the joined array element.
    private readonly Dictionary<ValueArray,TypeSymbol?[]> checkedArrayElements = new(ReferenceEqualityComparer.Instance);
    // Preserve each spread receiver's checked tuple positions. The array's
    // aggregate element union is insufficient for contextual tuple assignment.
    private readonly Dictionary<ValueArraySpread,ArrayTypeSymbol> checkedArraySpreadOperands = new(ReferenceEqualityComparer.Instance);
    // Preserve each spread's checked source identity. The final merged object
    // cannot reconstruct overwritten or optional source members faithfully.
    private readonly Dictionary<ObjectProperty,ObjectTypeSymbol> checkedObjectSpreadOperands = new(ReferenceEqualityComparer.Instance);
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
                        foreach(var pending in function.Parameters)
                            parameters.PendingParameters.Add(pending.Name.Text);
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
                            parameters.PendingParameters.Remove(parameter.Name.Text);
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
                        // Parentheses are semantically transparent for const literals.
                        // A type-level typeof query must agree with the checked read.
                        ValueNode? constantInitializer=v.Initializer;
                        while(constantInitializer is ValueParenthesized constGroup)
                            constantInitializer=constGroup.Expression;
                        var constantLiteral=v.Constant && v.Annotation==null &&
                            (constantInitializer is ValueAtom or ValueUnary)
                            ?Binder.ResolveFiniteInitializerType(constantInitializer,scope,file,true)
                            :null;
                        // A mutable binding widens an inferred literal read, but an
                        // explicit annotation or assertion still keeps its constraint.
                        var inferredSource=source is { } inferred && !v.Constant &&
                            v.Annotation==null && v.Initializer is { } mutableValueInitializer
                            ?WidenMutableLiteral(inferred,mutableValueInitializer,scope):source;
                        symbol.Type=annotatedType??constantLiteral??inferredSource;
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
    
    // Switch labels are side-effect-free, exact primitive values: literals,
    // intrinsic undefined, or initialized immutable bindings of an exact type.
    // A narrowed mutable binding cannot become a constant runtime case label.
    private TypeSymbol? TryReadSwitchCaseLabel(ValueNode expression,Scope scope)
    {
        if(TryReadLiteral(expression) is { } literal)return literal;
        while(expression is ValueParenthesized group)expression=group.Expression;
        if(expression is ValueAtom {Token.Kind:TokenKind.Null})
            return CompilerTypes.Null;
        if(expression is not ValueAtom {Token.Kind:TokenKind.Identifier} atom)
            return null;
        var binding=scope.Find(atom.Token.Text);
        if(binding is null && atom.Token.Text is not ("undefined" or "globalThis"))
            throw new CompilationFailure(Diagnostic.Error(file,atom.Token,"TS2304",
                $"Cannot find name '{atom.Token.Text}'."));
        // TypeOf performs TDZ and cross-module checks. In particular, a
        // declared but uninitialized const is never silently accepted.
        _=TypeOf(expression,scope);
        if(binding is null && atom.Token.Text=="undefined")
            return CompilerTypes.Undefined;
        if(binding is not {IsConstant:true,Initialized:true})
            return null;
        var declared=binding.DeclaredType;
        return declared is LiteralTypeSymbol ||
            declared?.Equals(CompilerTypes.Null)==true ||
            declared?.Equals(CompilerTypes.Undefined)==true
            ?declared:null;
    }

    // Bounded pure switch labels can use checked conditional, nullish and
    // logical expressions. Inspect every possible operand, including lazy arms:
    // a runtime-unreachable branch must not hide a call, getter or mutation.
    // Effects require a separate evaluation/flow model before admission.
    // Iteration avoids stack overflow on long binary expression chains.
    private bool IsFinitePureSwitchCase(ValueNode value,Scope scope)
    {
        var pending=new Stack<ValueNode>();
        pending.Push(value);
        int visited=0;
        while(pending.Count>0)
        {
            if(++visited>64)return false;
            switch(pending.Pop())
            {
                case ValueAtom {Token.Kind:TokenKind.Identifier or TokenKind.Number or
                    TokenKind.String or TokenKind.True or TokenKind.False or TokenKind.Null} atom:
                    // A primitive final result is not enough: a string +
                    // object may call user toString/valueOf, and an any
                    // operand may hide arbitrary effects. Check every leaf.
                    var priorDiagnostics=diagnostics.Count;
                    var operand=TypeOf(atom,scope);
                    if(operand is null)
                    {
                        // A missing identifier records a real TS2304, but
                        // cannot excuse an effectful or nonprimitive sibling.
                        // Visit the entire bounded expression before recovery.
                        if(diagnostics.Count==priorDiagnostics)return false;
                        break;
                    }
                    if(!IsKnownPrimitiveSwitchCaseType(operand))return false;
                    break;
                case ValueParenthesized group:
                    pending.Push(group.Expression);
                    break;
                case ValueUnary {Operator.Kind:TokenKind.Minus or TokenKind.Plus or
                    TokenKind.Bang} unary:
                    pending.Push(unary.Operand);
                    break;
                case ValueConditional conditional:
                    pending.Push(conditional.WhenFalse);
                    pending.Push(conditional.WhenTrue);
                    pending.Push(conditional.Condition);
                    break;
                case ValueBinary {Operator.Kind:TokenKind.Plus or TokenKind.Minus or
                    TokenKind.Star or TokenKind.Slash or TokenKind.Percent or
                    TokenKind.AmpAmp or TokenKind.BarBar or TokenKind.QuestionQuestion or
                    TokenKind.StrictEqual or TokenKind.StrictNotEqual or
                    TokenKind.EqualEqual or TokenKind.BangEqual or
                    TokenKind.LessThan or TokenKind.LessEqual or
                    TokenKind.GreaterThan or TokenKind.GreaterEqual} binary:
                    pending.Push(binary.Right);
                    pending.Push(binary.Left);
                    break;
                default:return false;
            }
        }
        return true;
    }

    // A finite runtime case label may call a directly bound typed function or
    // a checked function-valued member of an ordinary object identifier.
    // This retains native member-call receiver identity and evaluates the
    // label only when JavaScript switch matching reaches that case. Neither
    // result is treated as an exact discriminant for narrowing.
    // Optional/dynamic/recursive callee chains, spread arguments and untyped
    // receivers require an explicit effect model before being admitted.
    private bool IsFiniteCheckedSwitchCaseCall(ValueNode value,Scope scope)
    {
        while(value is ValueParenthesized group)value=group.Expression;
        if(value is not ValueCall
           {
               Optional:false,ChainContinuation:false,SpreadIndex:-1
           } call || call.Arguments.Count>16)
            return false;
        var callee=call.Callee;
        while(callee is ValueParenthesized calleeGroup)callee=calleeGroup.Expression;
        switch(callee)
        {
            case ValueAtom {Token.Kind:TokenKind.Identifier} direct:
                if(TypeOf(direct,scope) is not FunctionTypeSymbol)return false;
                break;
            case ValuePropertyAccess
            {
                Optional:false,ChainContinuation:false,
                Receiver:ValueAtom {Token.Kind:TokenKind.Identifier} receiver
            } member:
                if(TypeOf(receiver,scope) is not ObjectTypeSymbol ||
                   TypeOf(member,scope) is not FunctionTypeSymbol)return false;
                break;
            case ValueElementAccess
            {
                Optional:false,ChainContinuation:false,
                Receiver:ValueAtom {Token.Kind:TokenKind.Identifier} indexReceiver,
                Index:ValueAtom {Token.Kind:TokenKind.String}
            } indexedMember:
                if(TypeOf(indexReceiver,scope) is not ObjectTypeSymbol ||
                   TypeOf(indexedMember,scope) is not FunctionTypeSymbol)return false;
                break;
            default:return false;
        }
        return call.Arguments.All(argument=>IsFinitePureSwitchCase(argument,scope));
    }

    private static bool IsKnownPrimitiveSwitchCaseType(TypeSymbol type) => type switch
    {
        PrimitiveTypeSymbol {Kind:PrimitiveKind.Number or PrimitiveKind.String or
            PrimitiveKind.Boolean or PrimitiveKind.Null or PrimitiveKind.Undefined} => true,
        LiteralTypeSymbol {Kind:PrimitiveKind.Number or PrimitiveKind.String or
            PrimitiveKind.Boolean} => true,
        UnionTypeSymbol union when union.Alternatives.Count<=64 =>
            union.Alternatives.All(IsKnownPrimitiveSwitchCaseType),
        _=>false
    };

    private static bool ComputedCaseTypesOverlap(TypeSymbol selector,TypeSymbol label)
    {
        // Partially intersecting unions can match even when neither whole
        // union is assignable to the other (number|boolean vs number|string).
        IReadOnlyList<TypeSymbol> selectors=selector is UnionTypeSymbol s?s.Alternatives:[selector];
        IReadOnlyList<TypeSymbol> labels=label is UnionTypeSymbol l?l.Alternatives:[label];
        return selectors.Any(left=>labels.Any(right=>
            CompilerTypes.IsAssignable(left,right) || CompilerTypes.IsAssignable(right,left)));
    }

    // Case-local types view the existing symbol. A normally completing
    // clause can fall into the next one, so its effective incoming union
    // must be joined with the next case's direct-entry variants.
    private void CheckSwitch(SwitchStatement switching,Scope scope,TypeSymbol? expectedReturn,bool allowContinue)
    {
        var selectorType=TypeOf(switching.Selector,scope);
        var labels=new Dictionary<SwitchClause,TypeSymbol>();
        var selected=new HashSet<TypeSymbol>();
        bool hasComputedLabels=false;
        foreach(var clause in switching.Clauses)
        {
            if(clause.Label is not { } label)continue;
            var diagnosticsBeforeLabel=diagnostics.Count;
            var type=TryReadSwitchCaseLabel(label,scope);
            bool computed=type==null;
            if(computed)
            {
                if(!IsFinitePureSwitchCase(label,scope) &&
                   !IsFiniteCheckedSwitchCaseCall(label,scope))
                {
                    // An unresolved name may carry a suppressible TS2304,
                    // but an unsupported call, object coercion or any-typed
                    // operand must remain a hard error even beside that name.
                    throw new CompilationFailure(Diagnostic.Error(file,clause.Keyword,"TSCS1001",
                        "Only finite primitive expressions and checked direct calls are supported as switch labels."));
                }
                // A predeclared, not-yet-initialized binding cannot be used
                // as a runtime case value (even if its annotation is known).
                var unwrapped=label;
                while(unwrapped is ValueParenthesized group)unwrapped=group.Expression;
                if(unwrapped is ValueAtom {Token.Kind:TokenKind.Identifier} atom &&
                   scope.Find(atom.Token.Text) is {Initialized:false})
                    throw new CompilationFailure(Diagnostic.Error(file,clause.Keyword,"TSCS1001",
                        "A switch case cannot read an uninitialized binding."));
                type=TypeOf(label,scope);
                if(type==null)
                {
                    // The value checker has already recorded the real semantic
                    // diagnostic (e.g. TS2304). Do not replace it with an
                    // unsupported-type error thrown from the switch checker.
                    if(diagnostics.Count>diagnosticsBeforeLabel)
                    {
                        // Retain the actual label error and still visit all
                        // later labels and clause bodies. A next-line ts-ignore
                        // can suppress this diagnostic, but never unrelated
                        // type errors inside the switch.
                        hasComputedLabels=true;
                        continue;
                    }
                    throw new CompilationFailure(Diagnostic.Error(file,clause.Keyword,"TSCS1001",
                        "Computed switch case labels require a resolved primitive type."));
                }
                if(!IsKnownPrimitiveSwitchCaseType(type))
                    throw new CompilationFailure(Diagnostic.Error(file,clause.Keyword,"TSCS1001",
                        "Computed switch case labels require checked primitive types."));
                // Dynamic case values are not constant: duplicate identity
                // and discriminated-union narrowing cannot be assumed.
                hasComputedLabels=true;
            }
            else
            {
                if(!selected.Add(type!))
                    throw new CompilationFailure(Diagnostic.Error(file,clause.Keyword,"TSCS1001",
                        "Duplicate switch case labels are not supported yet."));
                labels.Add(clause,type!);
            }
            if(selectorType!=null && !type!.Equals(CompilerTypes.Null) &&
               !type.Equals(CompilerTypes.Undefined) &&
               !(computed
                   ?ComputedCaseTypesOverlap(selectorType,type)
                   :CompilerTypes.IsAssignable(type,selectorType)))
                Error(clause.Keyword,"TS2678",
                    $"Type '{type.DisplayName}' is not comparable to type '{selectorType.DisplayName}'.");
        }

        Symbol? subject=null;
        UnionTypeSymbol? variants=null;
        string? memberName=null;
        // Share the same parenthesis-aware discriminant recognizer as if/else.
        // The selector's grouping must not erase the union branch information.
        if(!hasComputedLabels &&
           TryReadDiscriminant(switching.Selector,out var variable,out var property))
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
    private static bool TryStaticNamedIndex(ValueNode index,TypeSymbol? receiver,out string name,bool allowFreshMissing=false)
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
               (shape.Members.ContainsKey(Parser.PropertyName(atom.Token)) || allowFreshMissing))
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
    private static bool IsStaticTupleNumericIndex(ValueNode index)
    {
        while(index is ValueParenthesized grouped)index=grouped.Expression;
        return index is ValueAtom {Token.Kind:TokenKind.Number} or
            ValueUnary {Operator.Kind:TokenKind.Minus,Operand:ValueAtom {Token.Kind:TokenKind.Number}};
    }

    private static bool TryFiniteTupleIndex(ValueNode index,out long position)
    {
        while(index is ValueParenthesized grouped)index=grouped.Expression;
        if(index is ValueAtom {Token.Kind:TokenKind.Number} numeric &&
           double.TryParse(numeric.Token.Value??numeric.Token.Text,
               System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,
               out var value) && double.IsFinite(value) &&
           value>=0 && value<=9007199254740991d && Math.Truncate(value)==value)
        {
            position=(long)value;
            return true;
        }
        position=0;
        return false;
    }

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
                    x.TupleOptionalStart==y.TupleOptionalStart &&
                    (x.TupleElements is null)==(y.TupleElements is null) &&
                    (x.TupleElements is null || x.TupleElements.Count==y.TupleElements!.Count &&
                       x.TupleElements.Select((t,i)=>Check(t,y.TupleElements[i],depth+1)).All(v=>v)) &&
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

    // A checked array union may exceed the bounded 64 alternative model;
    // convert its overflow into a source diagnostic rather than a CLR exception.
    private ArrayTypeSymbol? CheckedNativeArray(TypeSymbol? source,Token at)
    {
        try {return CompilerTypes.TryGetCheckedNativeArray(source);}
        catch(InvalidOperationException failure)
        {
            throw new CompilationFailure(Diagnostic.Error(file,at,"TSCS1001",failure.Message));
        }
    }

    private void EnsureWritableIndex(ValueElementAccess target,TypeSymbol? receiver)
    {
        if(receiver is ArrayTypeSymbol {Readonly:true} readonlyArray)
        {
            if(readonlyArray.TupleElements is not null && TryFiniteTupleIndex(target.Index,out var position))
                throw new CompilationFailure(Diagnostic.Error(file,target.Open,"TS2540",
                    $"Cannot assign to '{position}' because it is a read-only property."));
            throw new CompilationFailure(Diagnostic.Error(file,target.Open,"TS2542",
                $"Index signature in type '{readonlyArray.DisplayName}' only permits reading."));
        }
        // Checked union reads must not make the original readonly arrays
        // writable. A fresh destructured rest/spread copy remains mutable.
        if(receiver is UnionTypeSymbol arrayUnion &&
           arrayUnion.Alternatives.Count>0 &&
           arrayUnion.Alternatives.All(arm=>arm is ArrayTypeSymbol) &&
           arrayUnion.Alternatives.Cast<ArrayTypeSymbol>().Any(arm=>arm.Readonly))
            throw new CompilationFailure(Diagnostic.Error(file,target.Open,"TS2542",
                $"Index signature in type '{receiver.DisplayName}' only permits reading."));
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

    // Compile-time literal indices, including const variables and finite
    // literal unions, must never fall back to a tuple's joined element type.
    // Only an unknown runtime number may use the joined type.
    private TypeSymbol ResolveFiniteTupleRead(
        ArrayTypeSymbol receiver,ValueNode syntax,TypeSymbol selector,Token location)
    {
        var slots=receiver.TupleElements!;
        TypeSymbol At(string value)
        {
            if(!long.TryParse(value,System.Globalization.NumberStyles.Integer,
                   System.Globalization.CultureInfo.InvariantCulture,out var position) ||
               position<0 || position>9007199254740991L)
                throw new CompilationFailure(Diagnostic.Error(file,location,"TSCS1001",
                    "Tuple index must be a non-negative safe integer."));
            if(position>=slots.Count)
                throw new CompilationFailure(Diagnostic.Error(file,location,"TS2493",
                    $"Tuple type '{receiver.DisplayName}' of length '{slots.Count}' has no element at index '{position}'."));
            return CompilerTypes.TupleSlot(receiver,(int)position);
        }
        if(TryFiniteTupleIndex(syntax,out var direct))
            return At(direct.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if(IsStaticTupleNumericIndex(syntax))
            throw new CompilationFailure(Diagnostic.Error(file,location,"TSCS1001",
                "Tuple literal indices must be non-negative safe integers."));
        if(selector is LiteralTypeSymbol { Kind:PrimitiveKind.Number } literal)
            return At(literal.Value);
        if(selector is UnionTypeSymbol union &&
           union.Alternatives.All(t=>t is LiteralTypeSymbol { Kind:PrimitiveKind.Number }))
        {
            try {return CompilerTypes.Union(union.Alternatives.Cast<LiteralTypeSymbol>().Select(t=>At(t.Value)));}
            catch(InvalidOperationException failure)
            {
                throw new CompilationFailure(Diagnostic.Error(file,location,"TSCS1001",failure.Message));
            }
        }
        return receiver.ElementType;
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
                    current=array.TupleElements is not null && indexType is not null
                        ?ResolveFiniteTupleRead(array,element.Index,indexType,element.Open)
                        :array.ElementType;
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
        // A do body executes before its condition. Direct, sequential
        // assignments cannot be bypassed by break/continue/branch control
        // flow. Only for that proven subset may the first condition (and the
        // following statement) read bindings initialized by the body.
        // Do not treat a conditional, nested loop, try, call statement or
        // deferred closure as a proof of definite initialization.
        bool guaranteedBody=loop.Body.Statements.All(statement=>
            statement is EmptyStatement ||
            statement is Assignment assignment &&
                !ContainsNestedDeferredFunction(assignment.Value));
        InvalidateLoopBackedgeFlowTypes(scope,loop.Body);
        var before=CaptureInitialized(scope);
        var writesBefore=CaptureFlowWriteVersions(scope);
        List<Symbol>? definitelyAssigned=null;
        try
        {
            if(!guaranteedBody)
            {
                // A continue or conditional branch can bypass the first write.
                var entryConditionType=TypeOf(loop.Condition,scope);
                CheckRuntimeCondition(loop.Condition,entryConditionType,loop.Keyword);
                CheckBlock(loop.Body,scope,false,expectedReturn,true,true);
            }
            else
            {
                int beforeBodyDiagnostics=diagnostics.Count;
                var directTargets=new HashSet<Symbol>();
                foreach(var statement in loop.Body.Statements)
                {
                    if(statement is not Assignment assignment)continue;
                    // Evaluate one statement at a time: checking a deferred
                    // arrow body must never initialize a captured binding for
                    // the next statement's RHS or the loop condition.
                    var priorInitialized=CaptureInitialized(scope);
                    var priorTypes=CaptureFlowTypes(scope);
                    var priorVersions=CaptureFlowWriteVersions(scope);
                    CheckBlock(new Block([assignment]),scope,true,expectedReturn,true,true);
                    var target=scope.Find(assignment.Name.Text);
                    // Only a direct deferred function body contributes
                    // speculative captured writes. A normal executed RHS may
                    // contain n++ or other real mutations; preserving its
                    // write version is essential to invalidate stale flow
                    // narrowings after the loop.
                    if(IsDeferredFunctionValue(assignment.Value))
                        foreach(var (symbol,initialized) in priorInitialized)
                        {
                            if(ReferenceEquals(symbol,target))continue;
                            symbol.Initialized=initialized;
                            symbol.Type=priorTypes[symbol];
                            symbol.FlowWriteVersion=priorVersions[symbol];
                        }
                    // Preserve ordinary semantic diagnostics so the scanner
                    // can apply @ts-ignore. Throwing CompilationFailure here
                    // would bypass that suppression in Compilation.CheckSources.
                    // Even an ignored type mismatch is a runtime assignment:
                    // its target is available to later statements. Keep the
                    // semantic diagnostic for the scanner to suppress or report.
                    if(target is { Imported:false,IsConstant:false })
                    {
                        directTargets.Add(target);
                        target.Initialized=true;
                    }
                }
                definitelyAssigned=[..directTargets];
                // Never skip later body assignments or the condition merely
                // because an earlier error may be @ts-ignore'd. A thrown
                // condition error must not erase already recorded body errors.
                try
                {
                    var conditionType=TypeOf(loop.Condition,scope);
                    CheckRuntimeCondition(loop.Condition,conditionType,loop.Keyword);
                }
                catch(CompilationFailure failure) when (diagnostics.Count>beforeBodyDiagnostics)
                {
                    diagnostics.Add(failure.Diagnostic);
                }
            }
        }
        finally
        {
            RestoreInitialized(before);
            InvalidateLoopWrittenFlowTypes(writesBefore);
            // The loop runs at least once. Preserve only proven direct
            // binding writes, not speculative writes inside an initializer,
            // conditional, function body or the condition itself.
            if(definitelyAssigned is not null)
                foreach(var symbol in definitelyAssigned)symbol.Initialized=true;
        }
    }

    // The initializer always runs, but body and increment effects do not
    // establish definite initialization after a possibly empty loop.
    private void CheckClassicFor(ClassicForStatement loop,Scope scope,TypeSymbol? expectedReturn)
    {
        Block? localDeclarations=loop.Initializer switch
        {
            Variable declaration=>new Block([declaration]),
            ForVariableDeclarations group=>new Block([..group.Declarations]),
            _=>null
        };
        Scope header=localDeclarations is { } declarations
            ?Binder.Bind(file,declarations,scope):new Scope(scope);
        if(localDeclarations is { } prebound)
        {
            var deferredCaptures=new HashSet<Symbol>();
            // A header's bindings are lexically visible to all initializers,
            // but are assigned in declaration order. Binder pre-initializes
            // finite types for checking; that does not prove a value is ready.
            foreach(var local in prebound.Statements.Cast<Variable>())
                header.Symbols[local.Name.Text].Initialized=false;
            foreach(var local in prebound.Statements.Cast<Variable>())
            {
                // A direct arrow/function initializer creates a callable but
                // does not execute its body or parameter defaults. Checking
                // that deferred body must not initialize a later declarator,
                // narrow a captured symbol or record a runtime write.
                // Keep the current binding readable for valid recursion.
                var deferred=IsDeferredFunctionValue(local.Initializer);
                // A later initializer may invoke an earlier closure directly,
                // through an alias, or through a callback argument. Until its
                // captured header variables have initialized, no executable
                // calls can be proven safe within this bounded header subset.
                if(!deferred && deferredCaptures.Any(symbol=>!symbol.Initialized) &&
                   ContainsExecutedCall(local.Initializer))
                    throw new CompilationFailure(Diagnostic.Error(file,local.Name,"TSCS1001",
                        "Calls before captured classic-for bindings initialize are not supported."));
                var initializedBefore=deferred?CaptureInitialized(header):null;
                var typesBefore=deferred?CaptureFlowTypes(header):null;
                var deferredWritesBefore=deferred?CaptureFlowWriteVersions(header):null;
                var binding=header.Symbols[local.Name.Text];
                var previousDeferred=deferredClassicForBindings;
                var previousUnready=unreadyDeferredHeaderBindings;
                var previousCaptures=recordingDeferredHeaderCaptures;
                if(deferred)
                {
                    // An outer uncalled closure may itself contain nested
                    // classic-for declarations. Preserve its unready captures
                    // while checking the nested callable rather than hiding
                    // them behind the inner header's tracking set.
                    var unready=new HashSet<Symbol>(
                        header.Symbols.Values.Where(symbol=>!symbol.Initialized));
                    if(previousUnready is not null)unready.UnionWith(previousUnready);
                    unreadyDeferredHeaderBindings=unready;
                    recordingDeferredHeaderCaptures=new HashSet<Symbol>();
                    // Future header bindings may be captured by this uncalled
                    // callable, including writes. Permit only these symbols
                    // in the deferred checker; do not exempt executed calls.
                    var allowed=previousDeferred is null
                        ?new HashSet<Symbol>():new HashSet<Symbol>(previousDeferred);
                    allowed.UnionWith(header.Symbols.Values);
                    deferredClassicForBindings=allowed;
                    foreach(var symbol in header.Symbols.Values)
                        symbol.Initialized=true;
                }
                try
                {
                    CheckBlock(new Block([local]),header,true,expectedReturn,preboundLocal:true);
                }
                finally
                {
                    deferredClassicForBindings=previousDeferred;
                    if(deferred)
                    {
                        deferredCaptures.UnionWith(recordingDeferredHeaderCaptures!);
                        // Captures discovered in a nested deferred initializer
                        // are dependencies of any enclosing deferred body too.
                        // Otherwise an outer header could call that body before
                        // its later let binding has initialized.
                        previousCaptures?.UnionWith(recordingDeferredHeaderCaptures!);
                        // The callable was created but never invoked here.
                        // Only the current binding's declared/inferred type
                        // and initialization become real after this step.
                        var resolvedType=binding.Type;
                        var declaredType=binding.DeclaredType;
                        RestoreInitialized(initializedBefore!);
                        RestoreFlowTypes(typesBefore!);
                        foreach(var (symbol,version) in deferredWritesBefore!)
                            symbol.FlowWriteVersion=version;
                        binding.Type=resolvedType;
                        binding.DeclaredType=declaredType;
                        binding.Initialized=local.Initializer is not null;
                    }
                    unreadyDeferredHeaderBindings=previousUnready;
                    recordingDeferredHeaderCaptures=previousCaptures;
                }
            }
        }
        else if(loop.Initializer is { } initializer)
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

    private static bool IsDeferredFunctionValue(ValueNode? value)
    {
        // Parentheses and erased type operators do not invoke the function.
        // A ValueCall / comma expression could execute a deferred body now,
        // and must not gain an initialization exemption.
        while(true)
        {
            switch(value)
            {
                case ValueArrow:return true;
                case ValueParenthesized grouped:value=grouped.Expression;break;
                case ValueTypeOperator asserted:value=asserted.Expression;break;
                case ValueNonNull nonNull:value=nonNull.Expression;break;
                default:return false;
            }
        }
    }

    // A nested arrow is checked statically but its captured assignments
    // have not executed. Exclude such compound RHS expressions from the
    // definite-write proof; a direct deferred function value is safe because
    // the statement-level checker restores its captured effects immediately.
    private static bool ContainsNestedDeferredFunction(ValueNode value)
    {
        if(IsDeferredFunctionValue(value))return false;
        var pending=new Stack<ValueNode>();
        pending.Push(value);
        while(pending.Count>0)
        {
            switch(pending.Pop())
            {
                case ValueArrow:return true;
                case ValueCall call:
                    pending.Push(call.Callee);
                    foreach(var argument in call.Arguments)pending.Push(argument);
                    break;
                case ValueBinary binary:
                    pending.Push(binary.Left);pending.Push(binary.Right);break;
                case ValueConditional conditional:
                    pending.Push(conditional.Condition);
                    pending.Push(conditional.WhenTrue);
                    pending.Push(conditional.WhenFalse);
                    break;
                case ValueUnary unary:pending.Push(unary.Operand);break;
                case ValueParenthesized grouped:pending.Push(grouped.Expression);break;
                case ValueTypeOperator typed:pending.Push(typed.Expression);break;
                case ValueNonNull nonNull:pending.Push(nonNull.Expression);break;
                case ValuePrefixUpdate prefix:pending.Push(prefix.Target);break;
                case ValuePostfixUpdate postfix:pending.Push(postfix.Target);break;
                case ValueTemplate template:
                    foreach(var expression in template.Expressions)pending.Push(expression);
                    break;
                case ValueElementAccess element:
                    pending.Push(element.Receiver);pending.Push(element.Index);break;
                case ValuePropertyAccess property:pending.Push(property.Receiver);break;
                case ValueArraySpread spread:pending.Push(spread.Operand);break;
                case ValueArray array:
                    foreach(var element in array.Elements)pending.Push(element);
                    break;
                case ValueObject obj:
                    foreach(var property in obj.Properties)pending.Push(property.Value);
                    break;
            }
        }
        return false;
    }

    // A callable body is not run by its declaration. For intervening
    // initializers, conservatively reject executed calls while a previous
    // callable still captures an uninitialized header binding. Inspect the
    // runtime expression tree iteratively; a nested arrow is deferred unless
    // invoked by a ValueCall outside it.
    private static bool ContainsExecutedCall(ValueNode? expression)
    {
        if(expression is null)return false;
        var pending=new Stack<ValueNode>();
        pending.Push(expression);
        while(pending.Count>0)
        {
            switch(pending.Pop())
            {
                case ValueCall:return true;
                case ValueArrow:break;
                case ValueBinary binary:
                    pending.Push(binary.Right);pending.Push(binary.Left);break;
                case ValueConditional conditional:
                    pending.Push(conditional.Condition);
                    pending.Push(conditional.WhenTrue);
                    pending.Push(conditional.WhenFalse);
                    break;
                case ValueUnary unary:pending.Push(unary.Operand);break;
                case ValueParenthesized grouped:pending.Push(grouped.Expression);break;
                case ValueTypeOperator typed:pending.Push(typed.Expression);break;
                case ValueNonNull nonNull:pending.Push(nonNull.Expression);break;
                case ValuePrefixUpdate prefix:pending.Push(prefix.Target);break;
                case ValuePostfixUpdate postfix:pending.Push(postfix.Target);break;
                case ValueTemplate template:
                    foreach(var value in template.Expressions)pending.Push(value);
                    break;
                case ValueElementAccess indexed:
                    pending.Push(indexed.Receiver);pending.Push(indexed.Index);break;
                case ValuePropertyAccess property:pending.Push(property.Receiver);break;
                case ValueArraySpread spread:pending.Push(spread.Operand);break;
                case ValueArray array:
                    foreach(var value in array.Elements)pending.Push(value);
                    break;
                case ValueObject obj:
                    foreach(var property in obj.Properties)pending.Push(property.Value);
                    break;
            }
        }
        return false;
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

    // A bounded runtime intrinsic, not a fabricated global Array value.
    // A local/imported Array binding always wins; optional or transplanted
    // references do not get trusted built-in behavior.
    private static bool HasPendingParameter(Scope scope,string name)
    {
        for(Scope? current=scope;current!=null;current=current.Parent)
        {
            if(current.Symbols.ContainsKey(name))return false;
            if(current.PendingParameters.Contains(name))return true;
        }
        return false;
    }

    private static bool IsIntrinsicArrayIsArray(ValueCall call,Scope scope) =>
        !call.Optional && !call.ChainContinuation &&
        call.Callee is ValuePropertyAccess
        {
            Optional:false,ChainContinuation:false,
            Name:{Text:"isArray"},
            Receiver:ValueAtom {Token:{Kind:TokenKind.Identifier,Text:"Array"}}
        } && scope.Find("Array") is null && !HasPendingParameter(scope,"Array");

    // Array.from has a separate, checked finite iterable contract. Recognize
    // only the actual unshadowed runtime intrinsic; a local or parameter
    // named Array must retain its ordinary callable property semantics.
    private static bool IsIntrinsicArrayFrom(ValueCall call,Scope scope) =>
        !call.Optional && !call.ChainContinuation &&
        call.Callee is ValuePropertyAccess
        {
            Optional:false,ChainContinuation:false,
            Name:{Text:"from"},
            Receiver:ValueAtom {Token:{Kind:TokenKind.Identifier,Text:"Array"}}
        } && scope.Find("Array") is null && !HasPendingParameter(scope,"Array");

    // Array.of creates a fresh writable array from checked runtime values.
    // Keep shadowed Array bindings on their own declared callable signature.
    private static bool IsIntrinsicArrayOf(ValueCall call,Scope scope) =>
        !call.Optional && !call.ChainContinuation &&
        call.Callee is ValuePropertyAccess
        {
            Optional:false,ChainContinuation:false,
            Name:{Text:"of"},
            Receiver:ValueAtom {Token:{Kind:TokenKind.Identifier,Text:"Array"}}
        } && scope.Find("Array") is null && !HasPendingParameter(scope,"Array");


    // Object.keys/values/entries are real runtime built-ins, never a
    // synthesized ambient global. A lexically shadowed Object value retains
    // its own checked property/call signature.
    private static bool TryIntrinsicObjectEnumeration(ValueCall call,Scope scope,out string method)
    {
        method="";
        if(call.Optional || call.ChainContinuation ||
           call.Callee is not ValuePropertyAccess property ||
           property.Optional || property.ChainContinuation ||
           property.Receiver is not ValueAtom
           {
               Token:{Kind:TokenKind.Identifier,Text:"Object"}
           } ||
           property.Name.Text is not ("keys" or "values" or "entries") ||
           scope.Find("Object") is not null || HasPendingParameter(scope,"Object"))
            return false;
        method=property.Name.Text;
        return true;
    }

    // Object.is compares identity with SameValue semantics, while Object.hasOwn
    // tests own property keys (not inherited keys). Both remain native calls.
    // A lexically shadowed Object is always checked as an ordinary value.
    private static bool TryIntrinsicObjectIdentity(ValueCall call,Scope scope,out string method)
    {
        method="";
        if(call.Optional || call.ChainContinuation ||
           call.Callee is not ValuePropertyAccess property ||
           property.Optional || property.ChainContinuation ||
           property.Receiver is not ValueAtom
           {
               Token:{Kind:TokenKind.Identifier,Text:"Object"}
           } || property.Name.Text is not ("is" or "hasOwn") ||
           scope.Find("Object") is not null || HasPendingParameter(scope,"Object"))
            return false;
        method=property.Name.Text;
        return true;
    }

    // Native Object integrity calls require an unshadowed direct Object receiver.
    // The runtime owns the integrity operation; the checker models shallow readonly.
    private static bool TryIntrinsicObjectIntegrity(ValueCall call,Scope scope,out string method)
    {
        method="";
        if(call.Optional || call.ChainContinuation ||
           call.Callee is not ValuePropertyAccess property ||
           property.Optional || property.ChainContinuation ||
           property.Receiver is not ValueAtom
           {
               Token:{Kind:TokenKind.Identifier,Text:"Object"}
           } ||
           property.Name.Text is not ("freeze" or "seal" or "preventExtensions" or
               "isFrozen" or "isSealed" or "isExtensible") ||
           scope.Find("Object") is not null || HasPendingParameter(scope,"Object"))
            return false;
        method=property.Name.Text;
        return true;
    }

    // Native Object.assign mutates the first object at runtime. For finite,
    // required and non-readonly shapes, merge disjoint (or identical) member
    // types without inventing a general intersection/any escape hatch.
    private static bool IsIntrinsicObjectAssign(ValueCall call,Scope scope) =>
        !call.Optional && !call.ChainContinuation &&
        call.Callee is ValuePropertyAccess
        {
            Optional:false,ChainContinuation:false,
            Name:{Text:"assign"},
            Receiver:ValueAtom {Token:{Kind:TokenKind.Identifier,Text:"Object"}}
        } && scope.Find("Object") is null && !HasPendingParameter(scope,"Object");

    // TypeScript diagnoses an unused pure expression on the left of a comma.
    // This bounded subset accepts explicit call/update/delete side effects;
    // unknown getters and arbitrary expressions are not assumed effectful.
    private static bool HasCommaLeftSideEffect(ValueNode expression)
    {
        while(expression is ValueParenthesized group)expression=group.Expression;
        return expression switch
        {
            ValueCall or ValuePrefixUpdate or ValuePostfixUpdate=>true,
            ValueUnary {Operator.Kind:TokenKind.Delete}=>true,
            ValueConditional conditional=>HasCommaLeftSideEffect(conditional.WhenTrue) &&
                                           HasCommaLeftSideEffect(conditional.WhenFalse),
            ValueBinary {Operator.Kind:TokenKind.AmpAmp or TokenKind.BarBar or TokenKind.QuestionQuestion} logical=>
                HasCommaLeftSideEffect(logical.Right),
            ValueBinary {Operator.Kind:TokenKind.Comma} chain=>
                HasCommaLeftSideEffect(chain.Right),
            _=>false
        };
    }

    // Runtime Array.isArray is precise for checked arrays and definite
    // non-array primitives/functions. A structural object can actually be
    // backed by an array, so do not eliminate those arms by guessing.
    // unknown/any/void also forbid a sound finite partition.
    private static DiscriminantBranches? TryNarrowArrayIsArray(ValueNode condition,Scope scope)
    {
        if(condition is not ValueCall call || !IsIntrinsicArrayIsArray(call,scope) ||
           call.Arguments.Count!=1 || call.SpreadIndex>=0)
            return null;
        ValueNode input=call.Arguments[0];
        while(input is ValueParenthesized grouped)input=grouped.Expression;
        if(input is not ValueAtom {Token:{Kind:TokenKind.Identifier}} identifier)
            return null;
        var symbol=scope.Find(identifier.Token.Text);
        if(symbol?.Type is not UnionTypeSymbol union)return null;
        var arrays=new List<TypeSymbol>();
        var other=new List<TypeSymbol>();
        foreach(var candidate in union.Alternatives)
        {
            if(candidate is ArrayTypeSymbol)
                arrays.Add(candidate);
            else if(candidate is LiteralTypeSymbol ||
                    candidate is FunctionTypeSymbol ||
                    candidate is PrimitiveTypeSymbol
                    {
                        Kind:PrimitiveKind.Number or PrimitiveKind.String or
                             PrimitiveKind.Boolean or PrimitiveKind.Null or
                             PrimitiveKind.Undefined
                    })
                other.Add(candidate);
            else return null;
        }
        if(arrays.Count==0 || other.Count==0)return null;
        return new DiscriminantBranches(symbol,
            CompilerTypes.Union(arrays),CompilerTypes.Union(other));
    }

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
        if(TryNarrowArrayIsArray(condition,scope) is { } arrayGuard)
            return arrayGuard;
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
                // Native arrays and finite tuples are runtime objects; unlike
                // broad structural object types they cannot be primitive values.
                ArrayTypeSymbol => "object",
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
                case ForVariableDeclarations headerDeclarationBatch:
                    foreach(var declaration in headerDeclarationBatch.Declarations)
                        if(declaration.Initializer is { } value)
                            RecordValueWrites(value,shadowed);
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
                    if(nestedClassic.Initializer is ForVariableDeclarations nestedHeaderDeclarations)
                        foreach(var declaration in nestedHeaderDeclarations.Declarations)
                            headerLocals.Add(declaration.Name.Text);
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

    // Only directly deferred classic-for callable initializers may capture
    // another binding from the same lexical header before its text declaration.
    // Immediate initializer reads still obey TDZ/source-order checks.
    private HashSet<Symbol>? deferredClassicForBindings;
    // References to not-yet-initialized header bindings encountered while
    // checking an uncalled closure are deferred runtime dependencies.
    private HashSet<Symbol>? unreadyDeferredHeaderBindings;
    private HashSet<Symbol>? recordingDeferredHeaderCaptures;
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
        // Native flat signatures depend on the call's actual JavaScript this
        // receiver. A function-valued object property can carry a copied
        // intrinsic signature without being an array at runtime.
        var boundNativeFlatMembers=new HashSet<ValueNode>(ReferenceEqualityComparer.Instance);
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
                    foreach(var parameterName in arrow.Parameters)
                        local.PendingParameters.Add(parameterName.Name.Text);
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
                        local.PendingParameters.Remove(parameter.Name.Text);
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
                if(typeOperator.Keyword.Text=="as" &&
                   typeOperator.Target is NamedTypeSyntax {Name.Kind:TokenKind.Const})
                {
                    // Already checked nested identifier leaves keep the declared
                    // or inferred type; fresh literal shells alone are readonly.
                    if(actual is null) {types.Push(null);continue;}
                    var narrowed=CompilerTypes.FromFiniteConstAssertion(typeOperator.Expression,
                        identifierType:token=>TypeOfAtom(token,scope),checkedType:actual,
                        checkedArraySlot:(tuple,index)=>
                            checkedArrayElements.TryGetValue(tuple,out var slots) && index<slots.Length
                                ?slots[index]:null,
                        checkedObjectSpreadSource:entry=>
                            checkedObjectSpreadOperands.TryGetValue(entry,out var source)?source:null,
                        checkedTupleSpreadSource:entry=>
                            checkedArraySpreadOperands.TryGetValue(entry,out var tupleSource)?tupleSource:null);
                    if(narrowed is null)
                    {
                        ValueNode operand=typeOperator.Expression;
                        while(operand is ValueParenthesized constGrouped)operand=constGrouped.Expression;
                        bool unsupportedLiteral=operand is ValueObject or ValueArray;
                        throw new CompilationFailure(Diagnostic.Error(file,typeOperator.Keyword,
                            unsupportedLiteral?"TSCS1001":"TS1355",
                            unsupportedLiteral
                                ?"This const assertion requires a finite literal with statically checked spread operands."
                                :"A const assertion must be applied to a literal expression."));
                    }
                    types.Push(actual==null?null:narrowed);
                    continue;
                }
                var expected=TypeResolver.Resolve(typeOperator.Target,scope,file);
                if(actual==null) { types.Push(null);continue; }
                if(typeOperator.Keyword.Text=="as")
                {
                    // One-direction assignability is enough for an assertion;
                    // unrelated primitive/structural forms must not silently
                    // become casts that TypeScript itself would reject.
                    if(!CompilerTypes.IsAssignable(actual,expected) &&
                       !CompilerTypes.IsAssignable(expected,actual) &&
                       !SharesPrimitiveLiteralFamily(actual,expected))
                        Error(typeOperator.Keyword,"TS2352",
                            $"Conversion of type '{actual.DisplayName}' to type '{expected.DisplayName}' may be a mistake because neither type sufficiently overlaps with the other.");
                    types.Push(expected);
                }
                else
                {
                    TypeSymbol contextual=expected;
                    if(expected is UnionTypeSymbol objectUnion &&
                       objectUnion.Alternatives.OfType<ObjectTypeSymbol>().Any(shape=>
                           shape.Members.Values.Any(member=>member is LiteralTypeSymbol ||
                               member is UnionTypeSymbol nested && nested.Alternatives.Any(
                                   variant=>variant is LiteralTypeSymbol))))
                        contextual=SelectFreshDiscriminatedArm(typeOperator.Expression,objectUnion)
                            ?? throw new CompilationFailure(Diagnostic.Error(file,typeOperator.Keyword,"TSCS1001",
                                "Satisfies requires a fresh object with a unique string-literal discriminant arm."));
                    if(CheckContextualLiteral(typeOperator.Expression,contextual,actual) &&
                       !IsAssignableContextually(typeOperator.Expression,actual,contextual))
                        Error(typeOperator.Keyword,"TS1360",
                            $"Type '{actual.DisplayName}' does not satisfy expected type '{expected.DisplayName}'.");
                    types.Push(CompilerTypes.PreserveSatisfiedLiteralTypes(typeOperator.Expression,actual,contextual));
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
                if(CheckedNativeArray(operandType,spread.Keyword) is { } expanded)
                {
                    // A trailing array rest uses the checked source's original
                    // tuple optional boundary when constructing its tail type.
                    // The runtime spread still copies through native iteration.
                    checkedArraySpreadOperands[spread]=expanded;
                    types.Push(expanded.ElementType);
                }
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
                var checkedElements=new TypeSymbol?[arrayLiteral.Elements.Count];
                for(int i=arrayLiteral.Elements.Count-1;i>=0;i--)
                {
                    var next=types.Pop();
                    checkedElements[i]=next;
                    if(next!=null)
                        next=WidenMutableLiteral(next,arrayLiteral.Elements[i],scope);
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
                checkedArrayElements[arrayLiteral]=checkedElements;
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
                   CheckedNativeArray(receiverType,indexed.Open) is null)
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
                if(presentReceiver!=null && TryStaticNamedIndex(indexed.Index,presentReceiver,out var key,
                        indexed.BindingFreshOmittedDefault) &&
                   (presentReceiver is ObjectTypeSymbol or UnionTypeSymbol ||
                    presentReceiver.Equals(CompilerTypes.Any)) &&
                   !(CompilerTypes.IsNumber(indexType) &&
                     presentReceiver is UnionTypeSymbol &&
                     CheckedNativeArray(presentReceiver,indexed.Open) is not null))
                {
                    if(CompilerTypes.TryGetProperty(presentReceiver,key) is { } member)
                        PushOptionalResult(indexed,member,isChain,mayBeUndefined);
                    else if(indexed.BindingFreshOmittedDefault &&
                            presentReceiver is ObjectTypeSymbol)
                        // Missing own key in a directly fresh object with a
                        // binding default is undefined, not an invented member.
                        PushOptionalResult(indexed,CompilerTypes.Undefined,isChain,mayBeUndefined);
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
                if(CheckedNativeArray(presentReceiver,indexed.Open) is { } array)
                {
                    PushOptionalResult(indexed,array.TupleElements is not null && indexType is not null
                        ?ResolveFiniteTupleRead(array,indexed.Index,indexType,indexed.Open)
                        :array.ElementType,isChain,mayBeUndefined);
                }
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
                var computedNumericMembers=new HashSet<string>(StringComparer.Ordinal);
                var optionalMembers=new HashSet<string>(StringComparer.Ordinal);
                var mixedMembers=new HashSet<string>(StringComparer.Ordinal);
                for(int i=0;i<inferred.Length;i++)
                {
                    var entry=literal.Properties[i];
                    var memberType=inferred[i];
                    if(memberType==null)continue;
                    if(entry.Spread)
                    {
                        var source=CompilerTypes.TryFiniteObjectSpreadSource(memberType);
                        if(source is null)
                            throw new CompilationFailure(Diagnostic.Error(file,entry.Name,"TSCS1001",
                                "Object spread requires a statically checked object operand."));
                        checkedObjectSpreadOperands[entry]=source;
                        foreach(var member in source.Members)
                        {
                            if(members.ContainsKey(member.Key) &&
                               source.MixedRequiredOptionalMembers.Contains(member.Key) &&
                               source.NumericMembers.Contains(member.Key))
                                throw new CompilationFailure(Diagnostic.Error(file,entry.Name,"TSCS1001",
                                    "Mixed optional numeric keys cannot be safely spread over a prior key."));
                            if(source.MixedRequiredOptionalMembers.Contains(member.Key))
                                mixedMembers.Add(member.Key);
                            else if(!source.OptionalMembers.Contains(member.Key))
                                mixedMembers.Remove(member.Key);
                            try
                            {
                                if(!CompilerTypes.MergeFiniteSpreadMember(members,numericMembers,
                                    optionalMembers,member.Key,member.Value,
                                    source.NumericMembers.Contains(member.Key),
                                    source.OptionalMembers.Contains(member.Key),
                                    computedNumericMembers,source.ComputedNumericMembers.Contains(member.Key)))
                                    throw new CompilationFailure(Diagnostic.Error(file,entry.Name,"TSCS1001",
                                        "Conditional numeric/string key identity in optional object spreads is not supported."));
                            }
                            catch(InvalidOperationException failure)
                            {
                                throw new CompilationFailure(Diagnostic.Error(file,entry.Name,"TSCS1001",
                                    failure.Message));
                            }
                        }
                    }
                    else
                    {
                        var name=Parser.PropertyName(entry.Name);
                        // Fresh inferred object members are mutable. A primitive
                        // literal read widens unless an explicit assertion supplied it.
                        members[name]=WidenMutableLiteral(memberType,entry.Value,scope);
                        optionalMembers.Remove(name);
                        mixedMembers.Remove(name);
                        if(entry.Name.Kind==TokenKind.Number)numericMembers.Add(name);
                        else numericMembers.Remove(name);
                        if(entry.Computed && entry.Name.Kind==TokenKind.Number)computedNumericMembers.Add(name);
                        else computedNumericMembers.Remove(name);
                    }
                }
                // Do not turn an unresolved property into an implicit any.
                // Mixed source provenance is only meaningful when this
                // resulting property can actually be absent.
                mixedMembers.IntersectWith(optionalMembers);
                types.Push(known?new ObjectTypeSymbol(members,numericMembers:numericMembers,
                    optionalMembers:optionalMembers,computedNumericMembers:computedNumericMembers,
                    mixedRequiredOptionalMembers:mixedMembers):null);
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
                if(presentReceiver is ArrayTypeSymbol &&
                   presentMember is FunctionTypeSymbol { NativeArrayFlatReceiver: not null })
                    boundNativeFlatMembers.Add(property);
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
                // Recognize only a direct, unshadowed Array.isArray call.
                // The intrinsic takes any single value and returns boolean;
                // ordinary property/call checking handles shadowed names.
                bool intrinsicArrayGuard=IsIntrinsicArrayIsArray(call,scope);
                bool intrinsicArrayFrom=IsIntrinsicArrayFrom(call,scope);
                bool intrinsicArrayOf=IsIntrinsicArrayOf(call,scope);
                bool intrinsicObjectEnumeration=TryIntrinsicObjectEnumeration(call,scope,out var objectMethod);
                bool intrinsicObjectIntegrity=TryIntrinsicObjectIntegrity(call,scope,out var integrityMethod);
                bool intrinsicObjectIdentity=TryIntrinsicObjectIdentity(call,scope,out var identityMethod);
                bool intrinsicObjectAssign=IsIntrinsicObjectAssign(call,scope);
                if(!combine)
                {
                    pending.Push((call,true));
                    for(int i=call.Arguments.Count-1;i>=0;i--)
                        pending.Push((call.Arguments[i],false));
                    if(!intrinsicArrayGuard && !intrinsicArrayFrom && !intrinsicArrayOf &&
                       !intrinsicObjectEnumeration && !intrinsicObjectIntegrity &&
                       !intrinsicObjectAssign && !intrinsicObjectIdentity)
                        pending.Push((call.Callee,false));
                    continue;
                }
                var arguments=new TypeSymbol?[call.Arguments.Count];
                for(int i=arguments.Length-1;i>=0;i--)arguments[i]=types.Pop();

                if(intrinsicObjectIdentity)
                {
                    if(call.SpreadIndex>=0)
                    {
                        Error(call.Open,"TSCS1001",
                            "Object."+identityMethod+" spread arguments are not yet supported.");
                        types.Push(null);
                        continue;
                    }
                    if(arguments.Length!=2)
                    {
                        Error(call.Open,"TS2554",
                            "Object."+identityMethod+" expects exactly two arguments.");
                        types.Push(null);
                        continue;
                    }
                    if(arguments[0] is null || arguments[1] is null)
                    {
                        types.Push(null);
                        continue;
                    }
                    if(identityMethod=="hasOwn" &&
                       (!CompilerTypes.CanUseInRight(arguments[0]!) ||
                        !CompilerTypes.CanUseInLeft(arguments[1]!)))
                    {
                        Error(call.Open,"TSCS1001",
                            "Object.hasOwn requires a checked object and a string or number property key.");
                        types.Push(null);
                        continue;
                    }
                    // Object.is accepts all checked values including nullish
                    // values and different types. Neither intrinsic narrows
                    // a symbol's flow type or adds a phantom object property.
                    types.Push(CompilerTypes.Boolean);
                    continue;
                }
                if(intrinsicObjectIntegrity)
                {
                    if(call.SpreadIndex>=0)
                    {
                        Error(call.Open,"TSCS1001",
                            "Object integrity spread arguments are not yet supported.");
                        types.Push(null);
                        continue;
                    }
                    if(arguments.Length!=1)
                    {
                        Error(call.Open,"TS2554",
                            "Object."+integrityMethod+" expects exactly one argument.");
                        types.Push(null);
                        continue;
                    }
                    TypeSymbol? target=arguments[0];
                    if(target is null)
                    {
                        types.Push(null);
                        continue;
                    }
                    if(target is not (ObjectTypeSymbol or ArrayTypeSymbol))
                    {
                        Error(call.Open,"TSCS1001",
                            "Object integrity requires a checked finite object or array.");
                        types.Push(null);
                        continue;
                    }
                    if(integrityMethod is "isFrozen" or "isSealed" or "isExtensible")
                    {
                        types.Push(CompilerTypes.Boolean);
                        continue;
                    }
                    if(integrityMethod=="freeze")
                    {
                        // Only the outer shell is readonly; member values and
                        // nested references retain their original type identity.
                        target=target switch
                        {
                            ArrayTypeSymbol array=>array with { Readonly=true },
                            ObjectTypeSymbol shape=>new ObjectTypeSymbol(
                                shape.Members,shape.Members.Keys,shape.NumericMembers,
                                shape.OptionalMembers,shape.ComputedNumericMembers,
                                shape.MixedRequiredOptionalMembers),
                            _=>target
                        };
                    }
                    // seal and preventExtensions return T, not Readonly<T>.
                    types.Push(target);
                    continue;
                }
                if(intrinsicObjectAssign)
                {
                    // Object.assign returns its original target at runtime.
                    // Keep aliases and mutation behavior in emitted JavaScript;
                    // a finite type here describes the additional known keys.
                    if(call.SpreadIndex>=0)
                    {
                        Error(call.Open,"TSCS1001",
                            "Object.assign spread arguments are not yet supported.");
                        types.Push(null);
                        continue;
                    }
                    if(arguments.Length is < 1 or > 8)
                    {
                        Error(call.Open,"TS2554",
                            "Object.assign supports one target and up to seven explicit sources.");
                        types.Push(null);
                        continue;
                    }
                    var mergedMembers=new Dictionary<string,TypeSymbol>(StringComparer.Ordinal);
                    bool mergeIsChecked=true;
                    foreach(var item in arguments)
                    {
                        if(item is null)
                        {
                            mergeIsChecked=false;
                            break;
                        }
                        if(item is not ObjectTypeSymbol shape ||
                           shape.OptionalMembers.Count>0 ||
                           shape.ReadonlyMembers.Count>0 ||
                           shape.NumericMembers.Count>0)
                        {
                            Error(call.Open,"TSCS1001",
                                "Object.assign requires finite objects with required, writable, nonnumeric members.");
                            mergeIsChecked=false;
                            break;
                        }
                        foreach(var member in shape.Members)
                        {
                            if(mergedMembers.TryGetValue(member.Key,out var previous) &&
                               !CompilerTypes.SameStructuralInput(previous,member.Value))
                            {
                                Error(call.Open,"TSCS1001",
                                    "Object.assign overlapping members require identical checked types.");
                                mergeIsChecked=false;
                                break;
                            }
                            mergedMembers[member.Key]=member.Value;
                        }
                        if(!mergeIsChecked)break;
                    }
                    types.Push(mergeIsChecked?new ObjectTypeSymbol(mergedMembers):null);
                    continue;
                }
                if(intrinsicObjectEnumeration)
                {
                    // Only finite structural objects and native arrays have a
                    // sound checked enumerable-member contract here. Every JS
                    // result remains the original native call, with no polyfill.
                    if(call.SpreadIndex>=0)
                    {
                        Error(call.Open,"TSCS1001",
                            "Object enumeration spread arguments are not yet supported.");
                        types.Push(null);
                        continue;
                    }
                    if(arguments.Length!=1)
                    {
                        Error(call.Open,"TS2554",
                            "Object."+objectMethod+" expects exactly one argument.");
                        types.Push(null);
                        continue;
                    }
                    var source=arguments[0];
                    if(source is null)
                    {
                        types.Push(null);
                        continue;
                    }
                    if(source is not (ObjectTypeSymbol or ArrayTypeSymbol))
                    {
                        Error(call.Open,"TSCS1001",
                            "Object enumeration requires a checked finite object or array.");
                        types.Push(null);
                        continue;
                    }
                    if(objectMethod=="keys")
                    {
                        types.Push(new ArrayTypeSymbol(CompilerTypes.String));
                        continue;
                    }
                    // The ES2017 generic overload has a usable element type
                    // for arrays or homogeneous, required object members.
                    // Heterogeneous/optional object fallbacks in the standard
                    // library involve any; do not fabricate an any[] success.
                    TypeSymbol? memberType=source switch
                    {
                        ArrayTypeSymbol array=>array.ElementType,
                        ObjectTypeSymbol shape when
                            shape.Members.Count>0 && shape.OptionalMembers.Count==0 &&
                            shape.Members.Values.All(member=>CompilerTypes.SameStructuralInput(
                                member,shape.Members.Values.First()))=>
                            shape.Members.Values.First(),
                        _=>null
                    };
                    if(memberType is null)
                    {
                        Error(call.Open,"TSCS1001",
                            "Object.values/entries require a checked array or homogeneous required object members.");
                        types.Push(null);
                        continue;
                    }
                    if(objectMethod=="entries")
                    {
                        TypeSymbol entryElement;
                        try {entryElement=CompilerTypes.Union([CompilerTypes.String,memberType]);}
                        catch(InvalidOperationException failure)
                        {
                            throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                                "TSCS1001",failure.Message));
                        }
                        memberType=new ArrayTypeSymbol(entryElement,
                            TupleElements:[CompilerTypes.String,memberType]);
                    }
                    types.Push(new ArrayTypeSymbol(memberType));
                    continue;
                }
                if(intrinsicArrayGuard)
                {
                    if(call.SpreadIndex>=0)
                        throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                            "TSCS1001","Array.isArray spread arguments are not yet supported."));
                    if(arguments.Length!=1)
                        Error(call.Open,"TS2554","Array.isArray expects exactly one argument.");
                    types.Push(CompilerTypes.Boolean);
                    continue;
                }
                if(intrinsicArrayOf)
                {
                    // Bounded native Array.of<T>(...items:T[]): the first checked
                    // argument (or the element of a final array spread) fixes T.
                    // Subsequent values must fit T; do not silently infer a
                    // heterogeneous union, as the pinned overload does not.
                    // Without contextual generic inference an empty call
                    // produces unknown[]; a later typed context cannot infer T.
                    if(call.SpreadIndex>=0 && call.SpreadIndex!=arguments.Length-1)
                    {
                        Error(call.Open,"TSCS1001",
                            "Array.of supports only a final checked array spread.");
                        types.Push(null);
                        continue;
                    }
                    TypeSymbol? element=null;
                    bool valid=true;
                    static bool NullishOnly(TypeSymbol type)=>
                        type.Equals(CompilerTypes.Null) ||
                        type.Equals(CompilerTypes.Undefined) ||
                        type is UnionTypeSymbol union && union.Alternatives.All(NullishOnly);
                    // Nullish alternatives can join an otherwise compatible
                    // inferred element. A number|string source must not silently
                    // widen a number input into an unrelated union.
                    static bool CompatibleNullishJoin(TypeSymbol first,TypeSymbol later)=>
                        NullishOnly(first) || NullishOnly(later) ||
                        (CompilerTypes.ContainsNullish(later) &&
                         CompilerTypes.IsAssignable(CompilerTypes.NonNullAssertionResult(later),first)) ||
                        (CompilerTypes.ContainsNullish(first) &&
                         CompilerTypes.IsAssignable(CompilerTypes.NonNullAssertionResult(first),later));
                    // A literal object in first position has an inferred,
                    // fresh structural shape. It does not impose the same
                    // excess-property constraint as a previously typed value.
                    static bool FreshObject(ValueNode value)
                    {
                        while(value is ValueParenthesized grouped)value=grouped.Expression;
                        return value is ValueObject;
                    }
                    bool firstArgumentFresh=call.Arguments.Count>0 &&
                        FreshObject(call.Arguments[0]);
                    static bool SamePrimitiveFamily(TypeSymbol a,TypeSymbol b)
                    {
                        static PrimitiveKind? Kind(TypeSymbol type)=>type switch
                        {
                            LiteralTypeSymbol literal=>literal.Kind,
                            PrimitiveTypeSymbol primitive=>primitive.Kind,
                            _=>null
                        };
                        var left=Kind(a);
                        return (left is PrimitiveKind.Number or PrimitiveKind.String or PrimitiveKind.Boolean) &&
                            left==Kind(b);
                    }
                    for(int i=0;i<arguments.Length;i++)
                    {
                        var actual=arguments[i];
                        if(actual is null)
                        {
                            valid=false;
                            continue;
                        }
                        TypeSymbol item;
                        if(i==call.SpreadIndex)
                        {
                            if(actual is not ArrayTypeSymbol ofSpread)
                            {
                                Error(call.Open,"TSCS1001",
                                    "Array.of spread requires a statically checked array.");
                                valid=false;
                                continue;
                            }
                            // Array.of<T>(...items:T[]) does not accept
                            // incompatible tuple members as if their joined
                            // iteration element were a pre-declared union T.
                            // Comparable and same-primitive-family members
                            // preserve ordinary homogeneous/literal spreads.
                            // An earlier element type may already accept every
                            // tuple member (e.g. number|string). Without one,
                            // find a non-nullish anchor: a leading null must not
                            // hide incompatible later number/string members.
                            if(ofSpread.TupleElements is {Count:>1} tuple)
                            {
                                // Strip nullish alternatives for compatibility
                                // comparisons, but keep them in the inferred
                                // output type. An earlier typed union may accept
                                // every non-nullish member, while a preceding
                                // null/undefined alone cannot constrain them.
                                var comparable=tuple.Where(member=>!NullishOnly(member))
                                    .Select(CompilerTypes.NonNullAssertionResult).ToArray();
                                var constrained=element is null?null:CompilerTypes.NonNullAssertionResult(element);
                                bool covered=constrained is not null &&
                                    comparable.All(member=>CompilerTypes.IsAssignable(member,constrained));
                                if(!covered && comparable.Length>1 &&
                                   comparable.Skip(1).Any(member=>
                                       !CompilerTypes.IsAssignable(member,comparable[0]) &&
                                       !CompilerTypes.IsAssignable(comparable[0],member) &&
                                       !SamePrimitiveFamily(member,comparable[0])))
                                {
                                    Error(call.Open,"TS2345",
                                        "Array.of tuple spread members require one compatible inferred element type.");
                                    valid=false;
                                    continue;
                                }
                            }
                            item=ofSpread.ElementType;
                        }
                        else item=WidenMutableLiteral(actual,call.Arguments[i],scope);
                        if(element is null)element=item;
                        else
                        {
                            // Named/typed first inputs constrain later fresh
                            // object literals. Validate their own extra fields
                            // before structural width assignability can hide
                            // TS2353. A fresh first object instead uses bounded
                            // structural inference without an excess check.
                            if(!firstArgumentFresh && i!=call.SpreadIndex &&
                               !CheckContextualLiteral(call.Arguments[i],element,item))
                                valid=false;
                            if(i==call.SpreadIndex
                                ? CompilerTypes.IsAssignable(item,element)
                                : IsAssignableContextually(call.Arguments[i],item,element))
                                continue;
                            // TypeScript can infer an optional element when
                            // null/undefined is mixed with an otherwise common T.
                            // Do not generalize this to incompatible number/string
                            // argument families.
                            if(CompatibleNullishJoin(element,item))
                            {
                                try {element=CompilerTypes.Union([element,item]);}
                                catch(InvalidOperationException failure)
                                {
                                    Error(call.Open,"TSCS1001",failure.Message);
                                    valid=false;
                                }
                            }
                            else
                            {
                                Error(call.Open,"TS2345",
                                    $"Argument of type '{item.DisplayName}' is not assignable to parameter of type '{element.DisplayName}'.");
                                valid=false;
                            }
                        }
                    }
                    types.Push(valid?new ArrayTypeSymbol(element??CompilerTypes.Unknown):null);
                    continue;
                }
                if(intrinsicArrayFrom)
                {
                    // Array.from iterates a checked finite source. Explicitly
                    // typed map callbacks receive the element and numeric index;
                    // their result fixes the writable output array's element.
                    // Array-like sources, contextual mapper inference, and spread
                    // arguments remain outside this bounded implementation.
                    if(call.SpreadIndex>=0)
                    {
                        Error(call.Open,"TSCS1001",
                            "Array.from spread arguments are not yet supported.");
                        types.Push(null);
                        continue;
                    }
                    if(arguments.Length is <1 or >3)
                    {
                        Error(call.Open,"TS2554",
                            arguments.Length==0
                                ?"Array.from requires an iterable argument."
                                :"Array.from accepts at most a source, mapping function and thisArg.");
                        types.Push(null);
                        continue;
                    }
                    TypeSymbol? element;
                    bool homogeneous=true;
                    try
                    {
                        element=CompilerTypes.TryGetIterationElementType(arguments[0]);
                        // The pinned iterable overloads do not infer a common
                        // element from heterogeneous unions (number[] | string).
                        // The bounded semantic model can also exceed its union
                        // size limit during joining: diagnose, never crash.
                        if(element is not null && arguments[0] is UnionTypeSymbol sources)
                            homogeneous=sources.Alternatives.All(source=>
                            {
                                var arm=CompilerTypes.TryGetIterationElementType(source);
                                return arm is not null &&
                                    CompilerTypes.SameStructuralInput(arm,element!);
                            });
                    }
                    catch(InvalidOperationException failure)
                    {
                        throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                            "TSCS1001",failure.Message));
                    }
                    if(element is null)
                    {
                        Error(call.Open,"TSCS1001",
                            "Array.from requires a checked array, tuple, string, or finite union of these.");
                        types.Push(null);
                        continue;
                    }
                    if(!homogeneous)
                    {
                        Error(call.Open,"TSCS1001",
                            "Array.from on heterogeneous iterable unions is not yet supported.");
                        types.Push(null);
                        continue;
                    }
                    TypeSymbol outputElement=element;
                    if(arguments.Length>=2)
                    {
                        // The mapper is a normal checked function value, not an
                        // invented any-returning intrinsic. Explicit zero-, one-
                        // and two-parameter signatures are supported. The source
                        // value must fit the declared parameter (contravariance);
                        // the index is always a number. The native JS call keeps
                        // any supplied thisArg, including primitive contexts.
                        if(arguments[1] is not FunctionTypeSymbol mapper ||
                           mapper.Parameters.Count>2 || mapper.RestElementType is not null ||
                           // These method values have receiver/depth-dependent
                           // contracts. Their placeholder return signatures
                           // are not safe to reuse as Array.from mappers.
                           mapper.NativeArrayFlatReceiver is not null ||
                           mapper.NativeArrayMapElementType is not null ||
                           mapper.NativeArrayReduceReceiver is not null ||
                           mapper.NativeArraySpliceOverloadElementType is not null ||
                           mapper.NativeMutableArraySplice ||
                           mapper.NativeArrayFlatMap ||
                           mapper.NativeArrayObjectThisArg)
                        {
                            Error(call.Open,"TS2345",
                                "Array.from mapping requires a checked callback with at most two explicit parameters.");
                            types.Push(null);
                            continue;
                        }
                        TypeSymbol[] passed=[element,CompilerTypes.Number];
                        bool valid=true;
                        for(int i=0;i<mapper.Parameters.Count;i++)
                            if(!CompilerTypes.IsAssignable(passed[i],mapper.Parameters[i]))
                            {
                                Error(call.Open,"TS2345",
                                    $"Array.from callback parameter {i+1} cannot accept '{passed[i].DisplayName}'.");
                                valid=false;
                            }
                        if(!valid)
                        {
                            types.Push(null);
                            continue;
                        }
                        outputElement=mapper.ReturnType;
                    }
                    types.Push(new ArrayTypeSymbol(outputElement));
                    continue;
                }
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
                // Bounded native thisArg permits a finite object/array or
                // explicit undefined. The frozen TS7 custom minimal array
                // ambient exposes one-argument overloads: primitive/function
                // contexts are not accepted in this implemented subset.
                // This is semantic type checking, not a source-name exception.
                if(signature.NativeArrayObjectThisArg &&
                   call.SpreadIndex<0 && arguments.Length==2)
                {
                    static bool SupportedThisArg(TypeSymbol? value)=>value switch
                    {
                        ObjectTypeSymbol or ArrayTypeSymbol=>true,
                        PrimitiveTypeSymbol {Kind:PrimitiveKind.Undefined}=>true,
                        UnionTypeSymbol u=>u.Alternatives.All(SupportedThisArg),
                        _=>false
                    };
                    if(!SupportedThisArg(arguments[1]))
                        Error(call.Open,"TS2554",
                            "A primitive or callable thisArg is unsupported by the checked native array callback subset.");
                }
                // With an explicit seed the accumulator U may differ from the
                // source element. A typed callback's first argument fixes U,
                // but a zero-argument/invalid callback falls back to the seed,
                // leaving the ordinary checker to reject the bad callback.
                // Preserve the exact original array readonly identity.
                if(signature.NativeArrayReduceReceiver is { } receiver &&
                   call.SpreadIndex<0 && arguments.Length==2)
                {
                    TypeSymbol? accumulator=arguments[0] is FunctionTypeSymbol fold &&
                        fold.Parameters.Count is >=1 and <=4
                        ?fold.Parameters[0]:arguments[1];
                    if(accumulator is not null)
                        signature=new FunctionTypeSymbol(
                            [CompilerTypes.NativeArrayReduceCallbackSignatures(
                                accumulator,receiver),accumulator],
                            accumulator,2,NativeArrayReduceReceiver:receiver);
                }
                // The short splice/toSpliced(start, deleteCount?) overload permits an
                // omitted/undefined delete count. Inserting items selects the
                // long overload, where deleteCount must be number. Preserve
                // the ordinary argument checker/diagnostics for both paths.
                if(signature.NativeArraySpliceOverloadElementType is { } splicedElement &&
                   arguments.Length>=3)
                    signature=signature with {Parameters=[CompilerTypes.Number,
                        CompilerTypes.Number,splicedElement]};
                // Native flat's output varies with depth; no implicit any[].
                // Only bounded nonnegative literal depths have checked types.
                if(signature.NativeArrayFlatReceiver is { } flatReceiver)
                {
                    // Only a property checked *on a real array* is a native
                    // flat call. Merely seeing .flat syntax on a structural
                    // object carrying a transplanted intrinsic is unsound.
                    // Parentheses preserve a JS reference and its this binding.
                    ValueNode boundCallee=call.Callee;
                    while(true)
                    {
                        if(boundCallee is ValueParenthesized wrapped)
                            boundCallee=wrapped.Expression;
                        else if(boundCallee is ValueNonNull asserted)
                            boundCallee=asserted.Expression;
                        else if(boundCallee is ValueTypeOperator annotated)
                            boundCallee=annotated.Expression;
                        else break;
                    }
                    if(!boundNativeFlatMembers.Contains(boundCallee))
                        throw new CompilationFailure(Diagnostic.Error(file,call.Open,"TSCS1001",
                            "Array.flat must be called on its checked native array receiver."));
                    if(call.SpreadIndex>=0)
                        throw new CompilationFailure(Diagnostic.Error(file,call.Open,"TSCS1001",
                            "Array.flat does not support spread depth arguments."));
                    int depth=1;
                    if(call.Arguments.Count==1)
                    {
                        // Numeric literals are compared by decoded ECMAScript
                        // value, not token spelling: 0x2, 2.0 and 2e0 are 2.
                        if(!TryFiniteTupleIndex(call.Arguments[0],out var literalDepth) ||
                           literalDepth>3)
                            throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                                "TSCS1001","Array.flat depth must be a literal integer from 0 to 3."));
                        depth=(int)literalDepth;
                    }
                    if(call.Arguments.Count<=1)
                    {
                        // The fixed TS7 FlatArray type is distributive over
                        // mixed scalar/array unions at each depth. For example,
                        // (number[] | string[][])[] gives (number | string[])[]
                        // at depth 1 and (number | string)[] at depth 2.
                        // Preserve the ordinary readonly element shape when
                        // the requested depth does not reach that array.
                        static TypeSymbol FlattenOneLevel(TypeSymbol element)
                        {
                            if(element is ArrayTypeSymbol nested)return nested.ElementType;
                            if(element is UnionTypeSymbol union)
                                return CompilerTypes.Union(
                                    union.Alternatives.Select(FlattenOneLevel));
                            return element;
                        }
                        TypeSymbol elementType=flatReceiver.ElementType;
                        try
                        {
                            for(int i=0;i<depth;i++)
                                elementType=FlattenOneLevel(elementType);
                        }
                        catch(InvalidOperationException failure)
                        {
                            throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                                "TSCS1001",failure.Message));
                        }
                        signature=signature with {ReturnType=new ArrayTypeSymbol(elementType)};
                    }
                }
                // Even if the shared callable signature fits the bounded
                // return union, an optional call adds undefined. Guard that
                // final join too: the public compilation API must diagnose a
                // 65th variant, never leak InvalidOperationException.
                TypeSymbol CheckedOptionalCallReturn(TypeSymbol present)
                {
                    if(!optionalMissing)return present;
                    try {return CompilerTypes.Union([present,CompilerTypes.Undefined]);}
                    catch(InvalidOperationException failure)
                    {
                        throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                            "TSCS1001",failure.Message));
                    }
                }
                var resultType=CheckedOptionalCallReturn(signature.ReturnType);
                // Native Array<T>.map/flatMap are bounded intrinsics: callback
                // inputs use normal function checking; only the explicit
                // callback result determines the output element type.
                TypeSymbol presentResult=signature.ReturnType;
                if(signature.NativeArrayMapElementType is not null &&
                   call.SpreadIndex<0 && arguments.Length is >=1 and <=2)
                {
                    // A union of explicitly typed callback signatures can be
                    // passed to map/flatMap. Never let a callable union silently
                    // fall back to the intrinsic any[] placeholder. The same
                    // checked input contract must hold in every callable arm.
                    // TypeScript flatMap infers a single U from T | ReadonlyArray<T>.
                    // Scalar-only unions may themselves be U; if arrays occur
                    // in a return union, each member must contribute the same
                    // checked element U. Never widen incompatible scalar/array
                    // alternatives into an invented output union.
                    TypeSymbol CheckedMappedElement(TypeSymbol value)
                    {
                        if(!signature.NativeArrayFlatMap)return value;
                        if(value is ArrayTypeSymbol direct)return direct.ElementType;
                        if(value is not UnionTypeSymbol unionReturn)return value;
                        if(!unionReturn.Alternatives.Any(item=>item is ArrayTypeSymbol))
                            return value;
                        // An array member fixes U, including when U itself
                        // is a union. Each other array arm must have the same U;
                        // each scalar arm must be one of U's alternatives.
                        // This accepts U | U[] without accepting number | string[].
                        TypeSymbol? arrayElement=null;
                        foreach(var item in unionReturn.Alternatives)
                        {
                            if(item is not ArrayTypeSymbol array)continue;
                            if(arrayElement is not null &&
                               !CompilerTypes.SameStructuralInput(arrayElement,array.ElementType))
                                throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                                    "TSCS1001",
                                    "Array.flatMap callback return union cannot infer one common element type."));
                            arrayElement=array.ElementType;
                        }
                        if(arrayElement is null)return value;
                        foreach(var item in unionReturn.Alternatives)
                        {
                            if(item is ArrayTypeSymbol)continue;
                            if(CompilerTypes.SameStructuralInput(arrayElement,item) ||
                               (arrayElement is UnionTypeSymbol elementUnion &&
                                elementUnion.Alternatives.Any(option=>
                                    CompilerTypes.SameStructuralInput(option,item))))
                                continue;
                            throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                                "TSCS1001",
                                "Array.flatMap callback return union cannot infer one common element type."));
                        }
                        return arrayElement;
                    }
                    TypeSymbol? mapperReturn=null;
                    TypeSymbol? validatedUnionElement=null;
                    if(arguments[0] is FunctionTypeSymbol mapper)
                        mapperReturn=mapper.ReturnType;
                    else if(arguments[0] is UnionTypeSymbol callbackUnion &&
                            callbackUnion.Alternatives.All(item=>item is FunctionTypeSymbol))
                    {
                        try
                        {
                            mapperReturn=CompilerTypes.TryCommonCallableSignature(callbackUnion,
                                structuralInputs:true)?.ReturnType;
                        }
                        catch(InvalidOperationException failure)
                        {
                            throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                                "TSCS1001",failure.Message));
                        }
                        if(mapperReturn is null)
                            throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                                "TSCS1001","Array callback union requires identical checked input signatures."));
                        // TS generic inference selects one callback output U,
                        // not an arbitrary union assembled from function arms.
                        // In flatMap an arm returning U[] contributes U;
                        // an arm returning U contributes U itself. If those
                        // independently inferred results disagree, fail closed
                        // rather than accepting a program the reference rejects.
                        var callbackReturns=callbackUnion.Alternatives.Cast<FunctionTypeSymbol>()
                            .Select(item=>item.ReturnType).ToArray();
                        var firstMapped=CheckedMappedElement(callbackReturns[0]);
                        if(callbackReturns.Skip(1).Any(type=>
                            !CompilerTypes.SameStructuralInput(firstMapped,CheckedMappedElement(type))))
                            throw new CompilationFailure(Diagnostic.Error(file,call.Open,
                                "TSCS1001","Array callback union cannot infer one common result element type."));
                        // Reuse the validated U. Re-flattening the joined
                        // return types would split scalar-union U into its
                        // primitive members and falsely reject U | U[].
                        validatedUnionElement=firstMapped;
                    }
                    if(mapperReturn is not null)
                    {
                        // Also validate a single callback whose explicitly
                        // declared return is itself a scalar-or-array union.
                        // The same one-level normalizer validates union arms.
                        var mappedElement=validatedUnionElement??CheckedMappedElement(mapperReturn);
                        var mapped=new ArrayTypeSymbol(mappedElement);
                        presentResult=mapped;
                        resultType=CheckedOptionalCallReturn(mapped);
                    }
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
                    Error(call.Open,signature.NativeArraySpliceOverloadElementType is not null &&
                        supplied==0?"TS2555":"TS2554",
                        $"Expected {expected} arguments, but got {supplied}.");
                    // TS7 reports only the native reducing overload's arity
                    // error when the number of arguments is wrong. Keep the
                    // failure but avoid unrelated parameter mismatch noise.
                    if(signature.NativeArrayReduceReceiver is not null)
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
                        // The pinned Go TS7 overload resolver reports TS2769
                        // for a bad first/second argument in the two candidate
                        // mutable splice overloads. With insertion (>=3
                        // arguments), only the long overload participates and
                        // the regular TS2345 parameter error is correct.
                        Error(call.Open,signature.NativeMutableArraySplice &&
                            arguments.Length<=2 && i<2?"TS2769":"TS2345",
                            $"Argument of type '{actual.DisplayName}' is not assignable to parameter of type '{type.DisplayName}'.");
                }
                if(call.BindingArrayTail)
                {
                    // Only parser-generated destructuring lowers through this
                    // marker. The checked iterator copy and slice retain native
                    // sparse-array semantics, while a fixed tuple source also
                    // carries exact tail positions at the type level.
                    if(call.Callee is not ValuePropertyAccess { Receiver:ValueArray copied } ||
                       copied.Elements.Count!=1 ||
                       copied.Elements[0] is not ValueArraySpread {BindingArrayRest:true} restCopy ||
                       !checkedArraySpreadOperands.TryGetValue(restCopy,out var source))
                        throw new CompilationFailure(Diagnostic.Error(file,call.Open,"TSCS1001",
                            "An array rest binding requires a checked array source."));
                    if(source.TupleElements is { } positions)
                    {
                        if(call.Arguments.Count!=1 ||
                           call.Arguments[0] is not ValueAtom {Token.Kind:TokenKind.Number} startToken ||
                           !int.TryParse(startToken.Token.Text,
                               System.Globalization.NumberStyles.None,
                               System.Globalization.CultureInfo.InvariantCulture,out var start))
                            throw new InvalidOperationException("Invalid generated finite tuple-rest offset.");
                        var tailSlots=positions.Skip(start).ToArray();
                        // An optional tuple suffix remains optional after
                        // removing leading positions. If the rest starts inside
                        // that suffix, every surviving position is optional.
                        var tailOptionalStart=source.TupleOptionalStart<0 || tailSlots.Length==0
                            ?-1:Math.Max(0,source.TupleOptionalStart-start);
                        resultType=CompilerTypes.FiniteTuple(tailSlots,false,tailOptionalStart);
                    }
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
            else if(binary.Operator.Kind==TokenKind.Comma)
            {
                // Evaluate both operands left-to-right in emitted JS; only the
                // final operand supplies the expression's static value type.
                if(!HasCommaLeftSideEffect(binary.Left))
                    Error(binary.Operator,"TS2695",
                        "Left side of comma operator is unused and has no side effects.");
                types.Push(right);
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
    // TypeScript permits a literal assertion between different singleton
    // values of one primitive kind, including finite literal unions.
    // Truly unrelated primitive families still fail with TS2352.
    private static bool SharesPrimitiveLiteralFamily(TypeSymbol left,TypeSymbol right)
    {
        var leftMembers=left is UnionTypeSymbol lu?lu.Alternatives:new[] {left};
        var rightMembers=right is UnionTypeSymbol ru?ru.Alternatives:new[] {right};
        return leftMembers.Any(l=>l is LiteralTypeSymbol a &&
            rightMembers.Any(r=>r is LiteralTypeSymbol b && a.Kind==b.Kind));
    }

    // Widen only *fresh* inferred primitive consts. An explicitly constrained
    // literal type is non-widening across copies, including object/array members.
    // A finite chain of plain const aliases remains fresh and widens at mutable
    // initialization. Unknown or complex origins conservatively preserve type.
    private static bool IsWideningConstOrigin(ValueNode expression,Scope scope,int depth=0)
    {
        if(depth>=64)return false;
        while(expression is ValueParenthesized group)expression=group.Expression;
        if(expression is not ValueAtom { Token.Kind:TokenKind.Identifier } name)return false;
        var symbol=scope.Find(name.Token.Text);
        if(symbol is not { IsConstant:true,Declaration:Variable declaration } ||
           declaration.Annotation!=null)return false;
        var initial=declaration.Initializer;
        while(initial is ValueParenthesized grouped)initial=grouped.Expression;
        if(initial is ValueAtom atom && atom.Token.Kind is
            (TokenKind.Number or TokenKind.String or TokenKind.True or TokenKind.False))
            return true;
        if(initial is ValueUnary { Operator.Kind:TokenKind.Minus,Operand:ValueAtom number } &&
           number.Token.Kind==TokenKind.Number)return true;
        if(initial is ValueAtom { Token.Kind:TokenKind.Identifier })
            return IsWideningConstOrigin(initial,symbol.DeclaringScope??scope,depth+1);
        return false;
    }

    private static TypeSymbol WidenMutableLiteral(TypeSymbol inferred,ValueNode value,Scope scope)
    {
        if(inferred is not LiteralTypeSymbol literal)return inferred;
        while(value is ValueParenthesized grouped)value=grouped.Expression;
        if(!IsWideningConstOrigin(value,scope))return inferred;
        return literal.Kind switch
        {
            PrimitiveKind.Number=>CompilerTypes.Number,
            PrimitiveKind.String=>CompilerTypes.String,
            PrimitiveKind.Boolean=>CompilerTypes.Boolean,
            _=>inferred
        };
    }

    private TypeSymbol? TypeOfAtom(Token token,Scope scope,bool shorthand=false)
    {
        var literal=CompilerTypes.FromLiteral(token);
        if(literal!=null)return literal;
        if(token.Kind==TokenKind.Identifier && HasPendingParameter(scope,token.Text))
            throw new CompilationFailure(Diagnostic.Error(file,token,"TSCS1001",
                "Parameter default cannot read a parameter before initialization."));
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
        else if(symbol.File==file && symbol.Declaration is not FunctionDeclaration &&
                name.Start<=symbol.DeclarationStart &&
                (deferredClassicForBindings is null || !deferredClassicForBindings.Contains(symbol)))
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001","References before declaration are not supported yet."));
        if(symbol is not null && unreadyDeferredHeaderBindings?.Contains(symbol)==true)
            recordingDeferredHeaderCaptures?.Add(symbol);
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

    // A fresh object satisfies one disjoint tagged arm without losing its exact
    // discriminant. Stored values and ambiguous/overlapping tags stay outside
    // this bounded contextual-inference path; neither may be guessed safe.
    private static ObjectTypeSymbol? SelectFreshDiscriminatedArm(ValueNode value,UnionTypeSymbol union)
    {
        while(value is ValueParenthesized group)value=group.Expression;
        if(value is not ValueObject fresh || fresh.Properties.Any(p=>p.Spread) ||
           union.Alternatives.Count<2 || union.Alternatives.Any(t=>t is not ObjectTypeSymbol))
            return null;
        var shapes=union.Alternatives.Cast<ObjectTypeSymbol>().ToArray();
        foreach(var property in fresh.Properties)
        {
            if(property.Value is not ValueAtom { Token.Kind:TokenKind.String } literal)
                continue;
            var name=Parser.PropertyName(property.Name);
            if(shapes.Any(shape=>shape.OptionalMembers.Contains(name) ||
                           !shape.Members.ContainsKey(name)))
                continue;
            var tag=CompilerTypes.FromLiteralExact(literal.Token).Value;
            var seen=new HashSet<string>(StringComparer.Ordinal);
            int chosen=-1;
            bool ambiguous=false;
            for(int i=0;i<shapes.Length;i++)
            {
                var member=shapes[i].Members[name];
                IReadOnlyList<TypeSymbol> alternatives=member is UnionTypeSymbol tags
                    ?tags.Alternatives:new[] { member };
                if(alternatives.Count==0 ||
                   alternatives.Any(t=>t is not LiteralTypeSymbol { Kind:PrimitiveKind.String }))
                {
                    ambiguous=true;
                    break;
                }
                foreach(var candidate in alternatives)
                {
                    var label=((LiteralTypeSymbol)candidate).Value;
                    if(!seen.Add(label)) { ambiguous=true; break; }
                    if(label==tag)chosen=i;
                }
                if(ambiguous)break;
            }
            if(!ambiguous && chosen>=0)return shapes[chosen];
        }
        return null;
    }

    // TypeOf has already evaluated these children, including identifiers,
    // indexed reads and nested tuple values. Rechecking them could repeat
    // side effects; synthesizing their types from a joined union loses slots.
    // A finite tuple spread copies positional values without changing their
    // types or mutability. A trailing finite optional tuple spread retains
    // its possible length range; unknown-length arrays remain unsupported.
    // All operand expressions were already checked once by TypeOf.
    private bool TryExpandFiniteTupleValues(
        ValueArray array,out List<(ValueNode? Literal,TypeSymbol Type)> expanded,
        out int minimumCount)
    {
        expanded=[];
        minimumCount=0;
        if(!checkedArrayElements.TryGetValue(array,out var checkedTypes))return false;
        for(int i=0;i<array.Elements.Count;i++)
        {
            if(array.Elements[i] is ValueArraySpread spread)
            {
                if(!checkedArraySpreadOperands.TryGetValue(spread,out var operand) ||
                   operand.TupleElements is not { } slots)return false;
                // A finite optional tail contributes a range of possible
                // lengths. It is safe only as the final source element:
                // later items would no longer have fixed target positions.
                int optional=operand.TupleOptionalStart;
                if(optional>=0 && i!=array.Elements.Count-1)return false;
                for(int slot=0;slot<slots.Count;slot++)
                {
                    expanded.Add((null,CompilerTypes.TupleSlot(operand,slot)));
                    if(optional<0 || slot<optional)minimumCount++;
                }
            }
            else
            {
                if(checkedTypes[i] is not { } type)return false;
                expanded.Add((array.Elements[i],type));
                minimumCount++;
            }
            if(expanded.Count>32)return false;
        }
        return true;
    }

    private TypeSymbol? CheckedTupleElement(ValueArray array,int index)
    {
        if(checkedArrayElements.TryGetValue(array,out var checkedTypes) &&
           index<checkedTypes.Length && checkedTypes[index] is { } checkedType)
            return checkedType;
        return CompilerTypes.FromInitializer(array.Elements[index]);
    }

    // An earlier explicit property cannot contextually narrow a final merged
    // member after a later spread may overwrite it at runtime.
    private bool LaterSpreadOverridesProperty(ValueObject literal,ObjectProperty property,string name)
    {
        bool after=false;
        foreach(var entry in literal.Properties)
        {
            if(ReferenceEquals(entry,property)){after=true;continue;}
            if(after && entry.Spread &&
               checkedObjectSpreadOperands.TryGetValue(entry,out var source) &&
               source.Members.ContainsKey(name))return true;
        }
        return false;
    }

    private bool IsAssignableContextually(ValueNode expression,TypeSymbol actual,TypeSymbol expected,int depth=0)
    {
        if(depth>64)return false;
        // A stored union remains assignable to itself; only fresh source
        // literals need contextual refinement beyond ordinary type relations.
        if(CompilerTypes.IsAssignable(actual,expected))return true;
        while(expression is ValueParenthesized group)expression=group.Expression;
        // The sequence's value is its final operand. Preserve contextual
        // singleton refinement and tuple/object checking on that source AST,
        // not just the widened type of the whole sequence.
        if(expression is ValueBinary {Operator.Kind:TokenKind.Comma} sequence)
            return IsAssignableContextually(sequence.Right,actual,expected,depth+1);
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
                else if(LaterSpreadOverridesProperty(fresh,property,name))
                {
                    // Use the final joined type, not the earlier literal's
                    // exact value: an optional override could be present.
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
            // Contextual tuple annotations require all positions; a raw
            // never[] / T[] cannot fill a tuple just by sharing element type.
            if(targetArray.TupleElements is { } tuple)
            {
                int minimum=targetArray.TupleOptionalStart<0?tuple.Count:targetArray.TupleOptionalStart;
                if(!TryExpandFiniteTupleValues(elements,out var positions,out int sourceMinimum) ||
                   sourceMinimum<minimum || positions.Count>tuple.Count)return false;
                for(int i=0;i<positions.Count;i++)
                {
                    var (element,checkedType)=positions[i];
                    var required=CompilerTypes.TupleSlot(targetArray,i);
                    if(element is null
                        ?!CompilerTypes.IsAssignable(checkedType,required)
                        :!IsAssignableContextually(element,checkedType,required,depth+1))
                        return false;
                }
                return true;
            }
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
        // Unlike an ordinary type assertion, 'as const' does not erase
        // freshness. Preserve excess-property checks when a fresh const
        // object is used under an annotation, return, call or 'satisfies'.
        // Keep ordinary 'as SomeType' opaque: it deliberately changes the
        // contextual expression being checked.
        while(true)
        {
            if(value is ValueParenthesized literalGroup)
            {
                value=literalGroup.Expression;
                continue;
            }
            if(value is ValueTypeOperator
                {Keyword.Text:"as",Target:NamedTypeSyntax {Name.Kind:TokenKind.Const}} constant)
            {
                value=constant.Expression;
                continue;
            }
            break;
        }
        if(value is ValueBinary {Operator.Kind:TokenKind.Comma} sequence)
            // The left operand runs, but only the final operand's fresh
            // literal can supply the resulting value at this use site.
            return CheckContextualLiteral(sequence.Right,expected,actual);
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
            if(targetArray.TupleElements is { } slots)
            {
                int minimum=targetArray.TupleOptionalStart<0?slots.Count:targetArray.TupleOptionalStart;
                if(!TryExpandFiniteTupleValues(literalArray,out var positions,out int sourceMinimum) ||
                   sourceMinimum<minimum || positions.Count>slots.Count)
                {
                    Error(literalArray.Open,"TS2322",
                        $"Array literal does not match the length of tuple type '{targetArray.DisplayName}'.");
                    return false;
                }
                bool tupleValid=true;
                for(int i=0;i<positions.Count;i++)
                {
                    var (element,inferred)=positions[i];
                    var slotType=CompilerTypes.TupleSlot(targetArray,i);
                    // Spread operands are already checked values, not fresh
                    // literal slots. Direct nested literals still undergo
                    // their ordinary excess-property checks.
                    if(element is not null && !CheckContextualLiteral(element,slots[i],inferred))
                        tupleValid=false;
                    else if(element is null
                        ?!CompilerTypes.IsAssignable(inferred,slotType)
                        :!IsAssignableContextually(element,inferred,slotType))
                    {
                        Mismatch(literalArray.Open,inferred,slots[i]);
                        tupleValid=false;
                    }
                }
                return tupleValid;
            }
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
                // This earlier value may be overwritten. Final assignability
                // is checked using the merged type by IsAssignableContextually.
                if(LaterSpreadOverridesProperty(literal,property,name))continue;
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
            // An optional member exists, but is not assignable to a required
            // member. That is TS2322, not the genuinely missing-key TS2741.
            var missing=shape.Members.Keys.Where(name=>!shape.OptionalMembers.Contains(name) &&
                !data.Members.ContainsKey(name)).ToArray();
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

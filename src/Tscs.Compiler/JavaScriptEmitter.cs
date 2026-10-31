using System.Text;

namespace Tscs;

// Lowers the already checked, bounded TypeScript script AST to ES2015 JavaScript.
// Source parsing/binding/checking remain owned by Compilation/CompilerProgram;
// this writer never reparses tokens or infers missing semantics.
internal static class JavaScriptEmitter
{
    public static string Emit(Block root,bool emitModules=false)
    {
        var writer=new Writer(emitModules);
        foreach(var statement in root.Statements)writer.WriteStatement(statement,0);
        return writer.Text;
    }

    private sealed class Writer(bool emitModules)
    {
        private readonly StringBuilder output=new();
        public string Text=>output.ToString();

        private void Indent(int depth)=>output.Append(' ',depth*2);

        public void WriteStatement(Statement statement,int depth)
        {
            switch(statement)
            {
                case TypeAliasDeclaration or InterfaceDeclaration:
                    // Type-only declarations have no JavaScript runtime identity.
                    break;
                case EmptyStatement:
                    Indent(depth);
                    output.Append(";\n");
                    break;
                case Variable variable:
                    Indent(depth);
                    if(variable.Exported)output.Append("export ");
                    output.Append(variable.Constant?"const ":"let ").Append(variable.Name.Text);
                    if(variable.Initializer is { } initializer)
                    {
                        output.Append(" = ");
                        WriteExpression(initializer);
                    }
                    output.Append(";\n");
                    break;
                case Assignment assignment:
                    Indent(depth);
                    output.Append(assignment.Name.Text).Append(" = ");
                    WriteExpression(assignment.Value);
                    output.Append(";\n");
                    break;
                case ElementAssignment element:
                    Indent(depth);
                    WriteExpression(element.Target);
                    output.Append(" = ");
                    WriteExpression(element.Value);
                    output.Append(";\n");
                    break;
                case PropertyAssignment property:
                    Indent(depth);
                    WriteExpression(property.Target);
                    output.Append(" = ");
                    WriteExpression(property.Value);
                    output.Append(";\n");
                    break;
                case CompoundAssignment compound:
                    Indent(depth);
                    if(compound.Operator.Kind==TokenKind.PowerEqual)
                        WritePowerCompound(compound);
                    else
                    {
                        WriteExpression(compound.Target);
                        output.Append(' ').Append(compound.Operator.Text).Append(' ');
                        WriteExpression(compound.Value);
                    }
                    output.Append(";\n");
                    break;
                case PostfixUpdate update:
                    Indent(depth);
                    output.Append(update.Name.Text).Append(update.Operator.Text).Append(";\n");
                    break;
                case Expression expression:
                    Indent(depth);
                    WriteExpression(expression.Value);
                    output.Append(";\n");
                    break;
                case Block block:
                    Indent(depth);
                    WriteBlock(block,depth);
                    output.Append('\n');
                    break;
                case BreakStatement:
                    Indent(depth);
                    output.Append("break;\n");
                    break;
                case ContinueStatement:
                    Indent(depth);
                    output.Append("continue;\n");
                    break;
                case ReturnStatement returned:
                    Indent(depth);
                    output.Append("return");
                    if(returned.Value is { } returnValue)
                    {
                        output.Append(' ');
                        WriteExpression(returnValue);
                    }
                    output.Append(";\n");
                    break;
                case ThrowStatement thrown:
                    Indent(depth);
                    output.Append("throw ");
                    WriteExpression(thrown.Value);
                    output.Append(";\n");
                    break;
                case TryStatement guarded:
                    Indent(depth);
                    output.Append("try ");
                    WriteBlock(guarded.Body,depth);
                    if(guarded.CatchBody is { } caught)
                    {
                        output.Append(" catch");
                        if(guarded.CatchBinding is { } binding)
                            output.Append(" (").Append(binding.Text).Append(')');
                        output.Append(' ');
                        WriteBlock(caught,depth);
                    }
                    if(guarded.FinallyBody is { } cleanup)
                    {
                        output.Append(" finally ");
                        WriteBlock(cleanup,depth);
                    }
                    output.Append('\n');
                    break;
                case FunctionDeclaration function:
                    Indent(depth);
                    if(function.Exported)output.Append("export ");
                    output.Append("function ").Append(function.Name.Text).Append('(');
                    for(int i=0;i<function.Parameters.Count;i++)
                    {
                        if(i>0)output.Append(", ");
                        var parameter=function.Parameters[i];
                        if(parameter.Rest)output.Append("...");
                        output.Append(parameter.Name.Text);
                        if(parameter.DefaultValue is { } defaultExpression)
                        {
                            output.Append(" = ");
                            WriteExpression(defaultExpression);
                        }
                    }
                    output.Append(") ");
                    WriteBlock(function.Body,depth);
                    output.Append('\n');
                    break;
                case DoWhileStatement doLoop:
                    Indent(depth);
                    output.Append("do ");
                    WriteBlock(doLoop.Body,depth);
                    output.Append(" while (");
                    WriteExpression(doLoop.Condition);
                    output.Append(");\n");
                    break;
                case WhileStatement whileLoop:
                    Indent(depth);
                    output.Append("while (");
                    WriteExpression(whileLoop.Condition);
                    output.Append(") ");
                    WriteBlock(whileLoop.Body,depth);
                    output.Append('\n');
                    break;
                case ClassicForStatement classic:
                    Indent(depth);
                    output.Append("for (");
                    WriteForHeader(classic.Initializer);
                    output.Append("; ");
                    if(classic.Condition is { } forCondition)WriteExpression(forCondition);
                    output.Append("; ");
                    WriteForHeader(classic.Update);
                    output.Append(") ");
                    WriteBlock(classic.Body,depth);
                    output.Append('\n');
                    break;
                case ForInStatement enumerator:
                    Indent(depth);
                    output.Append("for (").Append(enumerator.Constant?"const ":"let ")
                        .Append(enumerator.Name.Text).Append(" in ");
                    WriteExpression(enumerator.Source);
                    output.Append(") ");
                    WriteBlock(enumerator.Body,depth);
                    output.Append('\n');
                    break;
                case ForOfStatement loop:
                    Indent(depth);
                    output.Append("for (").Append(loop.Constant?"const ":"let ")
                        .Append(loop.Name.Text).Append(" of ");
                    WriteExpression(loop.Source);
                    output.Append(") ");
                    WriteBlock(loop.Body,depth);
                    output.Append('\n');
                    break;
                case ConditionalStatement conditional:
                    Indent(depth);
                    WriteConditional(conditional,depth);
                    output.Append('\n');
                    break;
                case SwitchStatement switching:
                    Indent(depth);
                    output.Append("switch (");
                    WriteExpression(switching.Selector);
                    output.Append(") {\n");
                    foreach(var clause in switching.Clauses)
                    {
                        Indent(depth+1);
                        if(clause.Label is { } label)
                        {
                            output.Append("case ");
                            WriteExpression(label);
                        }
                        else output.Append("default");
                        output.Append(": ");
                        WriteBlock(clause.Body,depth+1);
                        output.Append('\n');
                    }
                    Indent(depth);
                    output.Append("}\n");
                    break;
                case ImportDeclaration import:
                    if(import.TypeOnly)break;
                    if(!emitModules)throw new NotSupportedException("ES module JavaScript output requires an explicit ES2015 module option.");
                    Indent(depth);
                    output.Append("import { ");
                    for(int i=0;i<import.Specifiers.Count;i++)
                    {
                        if(i>0)output.Append(", ");
                        var specifier=import.Specifiers[i];
                        output.Append(specifier.Imported.Text);
                        if(specifier.Imported.Text!=specifier.Local.Text)
                            output.Append(" as ").Append(specifier.Local.Text);
                    }
                    output.Append(" } from ").Append(import.ModulePath.Text).Append(";\n");
                    break;
                case ExportDeclaration export:
                    if(!emitModules)throw new NotSupportedException("ES module JavaScript output requires an explicit ES2015 module option.");
                    Indent(depth);
                    output.Append("export { ");
                    for(int i=0;i<export.Specifiers.Count;i++)
                    {
                        if(i>0)output.Append(", ");
                        var specifier=export.Specifiers[i];
                        output.Append(specifier.Local.Text);
                        if(specifier.Local.Text!=specifier.Exported.Text)
                            output.Append(" as ").Append(specifier.Exported.Text);
                    }
                    output.Append(" }");
                    if(export.ModulePath is { } from)
                        output.Append(" from ").Append(from.Text);
                    output.Append(";\n");
                    break;
                default:
                    throw new NotSupportedException("The AST-backed ES2015 writer only supports checked global-script statements.");
            }
        }

        private int powerTemporaryIndex;

        private string FreshPowerTemporary(HashSet<string> used)
        {
            // RHS lexical references must not be shadowed by an arrow parameter.
            // Name allocation is deterministic within a generated module.
            string name;
            do { name="__tscs_power_"+powerTemporaryIndex++; }
            while(!used.Add(name));
            return name;
        }

        private static HashSet<string> ReferencedValueNames(ValueNode root)
        {
            // Reserve every value name reachable inside callbacks, not only the
            // top-level expression. An arrow's free names can be captured by the
            // generated guard parameter even when they appear in a block body.
            var names=new HashSet<string>(StringComparer.Ordinal);
            var pending=new Stack<object>();
            pending.Push(root);
            while(pending.Count>0)
            {
                switch(pending.Pop())
                {
                    case ValueAtom atom:
                        if(atom.Token.Kind==TokenKind.Identifier)names.Add(atom.Token.Text);
                        break;
                    case ValueCall call:
                        pending.Push(call.Callee);
                        foreach(var arg in call.Arguments)pending.Push(arg);
                        break;
                    case ValueBinary binary:
                        pending.Push(binary.Left);pending.Push(binary.Right);break;
                    case ValueConditional conditional:
                        pending.Push(conditional.Condition);
                        pending.Push(conditional.WhenTrue);
                        pending.Push(conditional.WhenFalse);
                        break;
                    case ValueUnary unary: pending.Push(unary.Operand);break;
                    case ValuePrefixUpdate prefix: pending.Push(prefix.Target);break;
                    case ValuePostfixUpdate postfix: pending.Push(postfix.Target);break;
                    case ValueParenthesized grouped:pending.Push(grouped.Expression);break;
                    case ValueTypeOperator typed:pending.Push(typed.Expression);break;
                    case ValueNonNull asserted:pending.Push(asserted.Expression);break;
                    case ValueElementAccess indexed:
                        pending.Push(indexed.Receiver);pending.Push(indexed.Index);break;
                    case ValuePropertyAccess property:pending.Push(property.Receiver);break;
                    case ValueArraySpread spread:
                        pending.Push(spread.Operand);
                        break;
                    case ValueArray array:
                        foreach(var element in array.Elements)pending.Push(element);
                        break;
                    case ValueObject obj:
                        foreach(var property in obj.Properties)pending.Push(property.Value);
                        break;
                    case ValueArrow arrow:
                        if(arrow.FunctionName is { } functionName)
                            names.Add(functionName.Text);
                        foreach(var parameter in arrow.Parameters)pending.Push(parameter);
                        if(arrow.Body is { } body)pending.Push(body);
                        if(arrow.BlockBody is { } blockBody)pending.Push(blockBody);
                        break;
                    case ParameterDeclaration parameter:
                        names.Add(parameter.Name.Text);
                        if(parameter.DefaultValue is { } defaultValue)pending.Push(defaultValue);
                        break;
                    case Block block:
                        foreach(var statement in block.Statements)pending.Push(statement);
                        break;
                    case FunctionDeclaration function:
                        names.Add(function.Name.Text);
                        foreach(var parameter in function.Parameters)pending.Push(parameter);
                        pending.Push(function.Body);
                        break;
                    case Variable variable:
                        names.Add(variable.Name.Text);
                        if(variable.Initializer is { } initializer)pending.Push(initializer);
                        break;
                    case ReturnStatement returned:
                        if(returned.Value is { } returnValue)pending.Push(returnValue);
                        break;
                    case ThrowStatement thrown:pending.Push(thrown.Value);break;
                    case Expression expression:pending.Push(expression.Value);break;
                    case Assignment assignment:
                        names.Add(assignment.Name.Text);pending.Push(assignment.Value);break;
                    case PropertyAssignment propertyAssignment:
                        pending.Push(propertyAssignment.Target);
                        pending.Push(propertyAssignment.Value);
                        break;
                    case ElementAssignment elementAssignment:
                        pending.Push(elementAssignment.Target);
                        pending.Push(elementAssignment.Value);
                        break;
                    case CompoundAssignment compoundAssignment:
                        pending.Push(compoundAssignment.Target);
                        pending.Push(compoundAssignment.Value);
                        break;
                    case PostfixUpdate update:names.Add(update.Name.Text);break;
                    case ConditionalStatement branch:
                        pending.Push(branch.Condition);
                        pending.Push(branch.Consequent);
                        if(branch.Alternate is { } alternate)pending.Push(alternate);
                        break;
                    case WhileStatement loop:
                        pending.Push(loop.Condition);pending.Push(loop.Body);break;
                    case DoWhileStatement doWhile:
                        pending.Push(doWhile.Condition);pending.Push(doWhile.Body);break;
                    case ClassicForStatement classic:
                        if(classic.Initializer is { } init)pending.Push(init);
                        if(classic.Condition is { } condition)pending.Push(condition);
                        if(classic.Update is { } next)pending.Push(next);
                        pending.Push(classic.Body);
                        break;
                    case ForOfStatement elements:
                        names.Add(elements.Name.Text);
                        pending.Push(elements.Source);pending.Push(elements.Body);
                        break;
                    case ForInStatement keys:
                        names.Add(keys.Name.Text);
                        pending.Push(keys.Source);pending.Push(keys.Body);
                        break;
                    case SwitchStatement switching:
                        pending.Push(switching.Selector);
                        foreach(var clause in switching.Clauses)pending.Push(clause);
                        break;
                    case SwitchClause clause:
                        if(clause.Label is { } label)pending.Push(label);
                        pending.Push(clause.Body);
                        break;
                    case TryStatement handler:
                        pending.Push(handler.Body);
                        if(handler.CatchBinding is { } binding)names.Add(binding.Text);
                        if(handler.CatchBody is { } catchBody)pending.Push(catchBody);
                        if(handler.FinallyBody is { } finallyBody)pending.Push(finallyBody);
                        break;
                }
            }
            return names;
        }

        private void WritePowerCompound(CompoundAssignment compound)
        {
            // ES2015 has no ** or **=. Use Math.pow, preserving one evaluation
            // of any receiver/index and reading the old lvalue before the RHS.
            // The generated arrow is an expression, so it also works in for headers.
            switch(compound.Target)
            {
                case ValueAtom identifier:
                    output.Append(identifier.Token.Text).Append(" = Math.pow(").Append(identifier.Token.Text).Append(", ");
                    WriteExpression(compound.Value);
                    output.Append(')');
                    break;
                case ValuePropertyAccess property:
                    {
                        var receiver=FreshPowerTemporary(ReferencedValueNames(compound.Value));
                        output.Append("((").Append(receiver).Append(") => (")
                            .Append(receiver).Append('.').Append(property.Name.Text)
                            .Append(" = Math.pow(").Append(receiver).Append('.').Append(property.Name.Text).Append(", ");
                        WriteExpression(compound.Value);
                        output.Append(")))(");
                        WriteExpression(property.Receiver);
                        output.Append(')');
                        break;
                    }
                case ValueElementAccess element:
                    {
                        var names=ReferencedValueNames(compound.Value);
                        var receiver=FreshPowerTemporary(names);
                        var index=FreshPowerTemporary(names);
                        output.Append("((").Append(receiver).Append(", ").Append(index).Append(") => (")
                            .Append(receiver).Append('[').Append(index).Append("] = Math.pow(")
                            .Append(receiver).Append('[').Append(index).Append("], ");
                        WriteExpression(compound.Value);
                        output.Append(")))(");
                        WriteExpression(element.Receiver);
                        output.Append(", ");
                        WriteExpression(element.Index);
                        output.Append(')');
                        break;
                    }
                default:
                    throw new NotSupportedException("Unsupported exponentiation compound target.");
            }
        }

        private void WriteForHeader(Statement? statement)
        {
            switch(statement)
            {
                case null: break;
                case Variable variable:
                    output.Append("let ").Append(variable.Name.Text);
                    if(variable.Initializer is { } initial)
                    {
                        output.Append(" = ");
                        WriteExpression(initial);
                    }
                    break;
                case Assignment assignment:
                    output.Append(assignment.Name.Text).Append(" = ");
                    WriteExpression(assignment.Value);
                    break;
                case PostfixUpdate increment:
                    output.Append(increment.Name.Text).Append(increment.Operator.Text);
                    break;
                case Expression { Value:ValuePrefixUpdate or ValuePostfixUpdate } expression:
                    WriteExpression(expression.Value);
                    break;
                case CompoundAssignment compound:
                    if(compound.Operator.Kind==TokenKind.PowerEqual)
                        WritePowerCompound(compound);
                    else
                    {
                        WriteExpression(compound.Target);
                        output.Append(' ').Append(compound.Operator.Text).Append(' ');
                        WriteExpression(compound.Value);
                    }
                    break;
                case PropertyAssignment property:
                    WriteExpression(property.Target);
                    output.Append(" = ");
                    WriteExpression(property.Value);
                    break;
                case ElementAssignment element:
                    WriteExpression(element.Target);
                    output.Append(" = ");
                    WriteExpression(element.Value);
                    break;
                default:
                    throw new NotSupportedException("Only checked classic-for header mutations can be emitted.");
            }
        }

        private void WriteBlock(Block block,int depth)
        {
            output.Append("{\n");
            foreach(var statement in block.Statements)WriteStatement(statement,depth+1);
            Indent(depth);
            output.Append('}');
        }

        private void WriteConditional(ConditionalStatement conditional,int depth)
        {
            output.Append("if (");
            WriteExpression(conditional.Condition);
            output.Append(") ");
            WriteBlock(conditional.Consequent,depth);
            switch(conditional.Alternate)
            {
                case null: break;
                case Block otherwise:
                    output.Append(" else ");
                    WriteBlock(otherwise,depth);
                    break;
                case ConditionalStatement chain:
                    output.Append(" else ");
                    WriteConditional(chain,depth);
                    break;
                default:
                    throw new NotSupportedException("Only checked braced or else-if branches can be emitted.");
            }
        }

        private readonly record struct Piece(ValueNode? Node,string? Literal,int ParentPrecedence,bool IsRight,Block? ArrowBlock=null);

        private static int Precedence(ValueNode node) => node switch
        {
            ValueConditional or ValueArrow => 0,
            ValueBinary binary => binary.Operator.Kind switch
            {
                TokenKind.BarBar or TokenKind.QuestionQuestion => 1,
                TokenKind.AmpAmp => 2,
                TokenKind.Bar => 3,
                TokenKind.Caret => 4,
                TokenKind.Amp => 5,
                TokenKind.EqualEqual or TokenKind.BangEqual or TokenKind.StrictEqual or TokenKind.StrictNotEqual => 6,
                TokenKind.LessThan or TokenKind.LessEqual or TokenKind.GreaterThan or TokenKind.GreaterEqual or TokenKind.In => 7,
                TokenKind.LeftShift or TokenKind.RightShift or TokenKind.UnsignedRightShift => 8,
                TokenKind.Plus or TokenKind.Minus => 9,
                TokenKind.Star or TokenKind.Slash or TokenKind.Percent => 10,
                // Lowered to a Math.pow call, which binds as a primary/call.
                TokenKind.Power => 13,
                _ => throw new NotSupportedException("Unsupported binary operator in emitted AST.")
            },
            ValueUnary or ValuePrefixUpdate => 11,
            ValuePostfixUpdate => 12,
            // Erasure is wrapped with grouping to keep the JS precedence.
            ValueTypeOperator or ValueNonNull => 14,
            ValueCall or ValuePropertyAccess or ValueElementAccess => 13,
            ValueAtom or ValueParenthesized or ValueObject or ValueArray => 14,
            _ => throw new NotSupportedException("Unsupported expression in emitted AST.")
        };

        private void WriteExpression(ValueNode value)
        {
            // A nonrecursive traversal keeps the scanner/parser/checker's
            // supported long left-deep expressions safe to emit as well.
            var pending=new Stack<Piece>();
            void Text(string literal)=>pending.Push(new(null,literal,0,false));
            void Node(ValueNode node,int parentPrecedence=0,bool right=false)=>
                pending.Push(new(node,null,parentPrecedence,right));

            // A concise method must stay a native ES2015 method (not
            // "key: function"), since methods have no constructor prototype.
            // The body has already been checked as a lexical function scope.
            void ObjectEntry(ObjectProperty entry)
            {
                var key=entry.Computed?"["+entry.Name.Text+"]":entry.Name.Text;
                if(!entry.Method)
                {
                    Node(entry.Value);
                    Text(key+": ");
                    return;
                }
                if(entry.Value is not ValueArrow method ||
                   !method.FunctionExpression || method.BlockBody is not { } body)
                    throw new NotSupportedException("Unvalidated object method syntax.");
                pending.Push(new(null,null,0,false,body));
                Text(" ");
                Text(")");
                for(int i=method.Parameters.Count-1;i>=0;i--)
                {
                    var parameter=method.Parameters[i];
                    if(parameter.DefaultValue is { } initializer)
                    {
                        Node(initializer);
                        Text(" = ");
                    }
                    Text(parameter.Name.Text);
                    if(parameter.Rest)Text("...");
                    if(i>0)Text(", ");
                }
                Text(key+"(");
            }

            // Rebuild the complete unparenthesized continuation on a temporary
            // receiver, rather than emitting a guard for only the first ?. node.
            // Ordinary property/index/call suffixes then execute only inside the
            // present path, preserving both laziness and native member-call this.
            void GuardedContinuation(ValueNode chain,bool deleting=false)
            {
                var used=ReferencedValueNames(chain);
                var temporary=FreshPowerTemporary(used);
                ValueNode seed=new ValueAtom(new Token(TokenKind.Identifier,temporary,0,temporary.Length));
                var suffix=new Stack<ValueNode>();
                ValueNode? originalReceiver=null;
                ValueNode present=seed;
                ValueNode current=chain;
                bool found=false;
                while(!found)
                {
                    switch(current)
                    {
                        case ValuePropertyAccess property when property.Optional:
                            originalReceiver=property.Receiver;
                            present=property with {Receiver=seed,Optional=false,ChainContinuation=false};
                            found=true;
                            break;
                        case ValueElementAccess index when index.Optional:
                            originalReceiver=index.Receiver;
                            present=index with {Receiver=seed,Optional=false,ChainContinuation=false};
                            found=true;
                            break;
                        case ValuePropertyAccess property when property.ChainContinuation:
                            suffix.Push(property);
                            current=property.Receiver;
                            break;
                        case ValueElementAccess index when index.ChainContinuation:
                            suffix.Push(index);
                            current=index.Receiver;
                            break;
                        case ValueCall call when call.ChainContinuation:
                            suffix.Push(call);
                            current=call.Callee;
                            break;
                        default:
                            throw new NotSupportedException("Unvalidated optional-chain continuation.");
                    }
                }
                while(suffix.Count>0)
                {
                    present=suffix.Pop() switch
                    {
                        ValuePropertyAccess property=>property with
                            {Receiver=present,ChainContinuation=false},
                        ValueElementAccess index=>index with
                            {Receiver=present,ChainContinuation=false},
                        ValueCall call=>call with
                            {Callee=present,ChainContinuation=false},
                        _=>throw new NotSupportedException("Unvalidated optional-chain suffix.")
                    };
                }
                Text(")");
                Node(originalReceiver!);
                Text("))(");
                Node(present);
                Text("(("+temporary+") => ("+temporary+" === null || "+temporary+
                    " === undefined ? "+(deleting?"true : delete ":"undefined : "));
            }

            Node(value);
            while(pending.Count>0)
            {
                var item=pending.Pop();
                if(item.Literal is { } literal)
                {
                    output.Append(literal);
                    continue;
                }
                if(item.ArrowBlock is { } block)
                {
                    // Emit statements only when their turn arrives in the expression stack.
                    WriteBlock(block,0);
                    continue;
                }
                var node=item.Node!;
                int precedence=Precedence(node);
                bool wrap=precedence<item.ParentPrecedence ||
                    (item.IsRight && precedence==item.ParentPrecedence);
                if(wrap)Text(")");
                switch(node)
                {
                    case ValueAtom atom:
                        Text(atom.Token.Text);
                        break;
                    case ValueArrow arrow:
                        // Type annotations erase; ES2015 arrow captures remain lexical.
                        if(arrow.BlockBody is { } body)
                            pending.Push(new(null,null,0,false,body));
                        else Node(arrow.Body!);
                        Text(arrow.FunctionExpression?" ":" => ");
                        Text(")");
                        for(int i=arrow.Parameters.Count-1;i>=0;i--)
                        {
                            var parameter=arrow.Parameters[i];
                            if(parameter.DefaultValue is { } initializer)
                            {
                                Node(initializer);
                                Text(" = ");
                            }
                            Text(parameter.Name.Text);
                            if(parameter.Rest)Text("...");
                            if(i>0)Text(", ");
                        }
                        Text(arrow.FunctionExpression
                            ? (arrow.FunctionName is { } self ? "function "+self.Text+"(" : "function (")
                            : "(");
                        break;
                    case ValueConditional chosen:
                        // Parenthesize the complete conditional: nested values
                        // retain right-associativity and JS evaluates one arm.
                        Text(")");
                        Node(chosen.WhenFalse);
                        Text(" : ");
                        Node(chosen.WhenTrue);
                        Text(" ? ");
                        Node(chosen.Condition);
                        Text("(");
                        break;
                    case ValueBinary binary:
                        if(binary.Operator.Kind==TokenKind.Power)
                        {
                            // Right-associative AST nesting is retained by nested
                            // Math.pow calls, valid in the ES2015 target.
                            Text(")");
                            Node(binary.Right);
                            Text(", ");
                            Node(binary.Left);
                            Text("Math.pow(");
                        }
                        else if(binary.Operator.Kind==TokenKind.QuestionQuestion)
                        {
                            // ES2015 has no ?? operator; evaluate the left
                            // exactly once and preserve lazy RHS evaluation.
                            var temp=FreshPowerTemporary(ReferencedValueNames(binary.Right));
                            Text(")");
                            Node(binary.Left);
                            Text(" : "+temp+"))(");
                            Node(binary.Right);
                            Text("(("+temp+") => ("+temp+" === null || "+temp+" === undefined ? ");
                        }
                        else
                        {
                            Node(binary.Right,precedence,true);
                            Text(" "+binary.Operator.Text+" ");
                            Node(binary.Left,precedence);
                        }
                        break;
                    case ValueUnary unary:
                    {
                        // Optional deletion must remove the *property*, not
                        // delete a guarded read result. Preserve nullish true
                        // and evaluate a receiver exactly once in ES2015.
                        ValueNode deleteTarget=unary.Operand;
                        while(deleteTarget is ValueParenthesized groupDelete)
                            deleteTarget=groupDelete.Expression;
                        if(unary.Operator.Kind==TokenKind.Delete &&
                           (deleteTarget is ValuePropertyAccess { ChainContinuation:true } ||
                            deleteTarget is ValueElementAccess { ChainContinuation:true }))
                        {
                            // Delete the final member in the present branch, not
                            // a temporary read result; short circuit is true.
                            GuardedContinuation(deleteTarget,deleting:true);
                        }
                        else if(unary.Operator.Kind==TokenKind.Delete &&
                           deleteTarget is ValuePropertyAccess { Optional:true,ChainContinuation:false } propertyDelete)
                        {
                            var temp=FreshPowerTemporary(ReferencedValueNames(unary.Operand));
                            Text(")");
                            Node(propertyDelete.Receiver);
                            Text("(("+temp+") => ("+temp+" == null ? true : delete "+temp+"."+propertyDelete.Name.Text+"))(");
                        }
                        else if(unary.Operator.Kind==TokenKind.Delete &&
                                deleteTarget is ValueElementAccess { Optional:true,ChainContinuation:false } indexDelete)
                        {
                            var temp=FreshPowerTemporary(ReferencedValueNames(unary.Operand));
                            Text(")");
                            Node(indexDelete.Receiver);
                            Text("]))(");
                            Node(indexDelete.Index);
                            Text("(("+temp+") => ("+temp+" == null ? true : delete "+temp+"[");
                        }
                        else
                        {
                            Node(unary.Operand,precedence);
                            Text(unary.Operator.Text+(unary.Operator.Kind is TokenKind.Plus or TokenKind.Minus or TokenKind.Typeof or TokenKind.Void or TokenKind.Delete?" ":""));
                        }
                        break;
                    }
                    case ValuePrefixUpdate prefix:
                        Node(prefix.Target,precedence);
                        Text(prefix.Operator.Text);
                        break;
                    case ValuePostfixUpdate postfix:
                        Text(postfix.Operator.Text);
                        Node(postfix.Target,precedence);
                        break;
                    case ValueTypeOperator typed:
                        Text(")");
                        Node(typed.Expression);
                        Text("(");
                        break;
                    case ValueNonNull asserted:
                        // TypeScript's ! is never a runtime null check. Parentheses
                        // preserve receiver binding and expression precedence.
                        Text(")");
                        Node(asserted.Expression);
                        Text("(");
                        break;
                    case ValueParenthesized grouped:
                        Text(")");
                        Node(grouped.Expression);
                        Text("(");
                        break;
                    case ValueElementAccess continuedIndex when continuedIndex.ChainContinuation:
                        GuardedContinuation(continuedIndex);
                        break;
                    case ValueElementAccess indexed:
                        if(indexed.Optional)
                        {
                            // A lowered ES2015 arrow evaluates the receiver once
                            // and only evaluates the index on the present path.
                            var used=ReferencedValueNames(indexed.Index);
                            used.UnionWith(ReferencedValueNames(indexed.Receiver));
                            var temp=FreshPowerTemporary(used);
                            Text(")");
                            Node(indexed.Receiver);
                            Text("]))(");
                            Node(indexed.Index);
                            Text("(("+temp+") => ("+temp+" === null || "+temp+
                                " === undefined ? undefined : "+temp+"[");
                        }
                        else
                        {
                            Text("]");
                            Node(indexed.Index);
                            Text("[");
                            Node(indexed.Receiver,precedence);
                        }
                        break;
                    case ValueArray array:
                        Text("]");
                        for(int i=array.Elements.Count-1;i>=0;i--)
                        {
                            if(array.Elements[i] is ValueArraySpread spread)
                            {
                                Text(")");
                                Node(spread.Operand);
                                Text("...(");
                            }
                            else Node(array.Elements[i]);
                            if(i>0)Text(", ");
                        }
                        Text("[");
                        break;
                    case ValuePropertyAccess continuedProperty when continuedProperty.ChainContinuation:
                        GuardedContinuation(continuedProperty);
                        break;
                    case ValuePropertyAccess property:
                        if(property.Optional)
                        {
                            var temp=FreshPowerTemporary(ReferencedValueNames(property.Receiver));
                            Text(")");
                            Node(property.Receiver);
                            Text("(("+temp+") => ("+temp+" === null || "+temp+
                                " === undefined ? undefined : "+temp+"."+property.Name.Text+"))(");
                        }
                        else
                        {
                            Text("."+property.Name.Text);
                            Node(property.Receiver,precedence);
                        }
                        break;
                    case ValueObject literalObject:
                        // Object spread is ES2018 syntax. The target is ES2015,
                        // so copy one ordered source at a time using ES2015
                        // Object.assign. Nesting calls (rather than passing all
                        // arguments to one call) preserves observable left-to-
                        // right evaluation/copy order for later expressions.
                        if(literalObject.Properties.Any(p=>p.Spread))
                        {
                            Text(")");
                            for(int i=literalObject.Properties.Count-1;i>=0;i--)
                            {
                                var entry=literalObject.Properties[i];
                                Text(")");
                                if(entry.Spread)Node(entry.Value);
                                else
                                {
                                    Text(" }");
                                    ObjectEntry(entry);
                                    Text("{ ");
                                }
                                Text(", ");
                            }
                            Text("{}");
                            for(int i=0;i<literalObject.Properties.Count;i++)
                                Text("Object.assign(");
                            Text("(");
                        }
                        else
                        {
                            // Keep existing non-spread ES2015 output unchanged.
                            Text(" })");
                            for(int i=literalObject.Properties.Count-1;i>=0;i--)
                            {
                                var entry=literalObject.Properties[i];
                                ObjectEntry(entry);
                                if(i>0)Text(", ");
                            }
                            Text("({ ");
                        }
                        break;
                    case ValueCall continuedCall when continuedCall.ChainContinuation:
                        GuardedContinuation(continuedCall);
                        break;
                    case ValueCall call:
                        if(call.Optional)
                        {
                            // Evaluate the callable once and defer all arguments
                            // until the presence check. Member calls additionally
                            // retain their original receiver as the JS this value.
                            var used=ReferencedValueNames(call.Callee);
                            foreach(var argument in call.Arguments)
                                used.UnionWith(ReferencedValueNames(argument));
                            var functionTemp=FreshPowerTemporary(used);
                            ValueNode callee=call.Callee;
                            while(true)
                            {
                                if(callee is ValueParenthesized group)callee=group.Expression;
                                else if(callee is ValueNonNull asserted)callee=asserted.Expression;
                                else if(callee is ValueTypeOperator typed)callee=typed.Expression;
                                else break;
                            }
                            ValueNode? receiver=callee switch
                            {
                                ValuePropertyAccess property=>property.Receiver,
                                ValueElementAccess indexed=>indexed.Receiver,
                                _=>null
                            };
                            if(receiver is not null)
                            {
                                var receiverTemp=FreshPowerTemporary(used);
                                Text(")");
                                Node(receiver);
                                Text("))(");
                                if(callee is ValuePropertyAccess member)Text("."+member.Name.Text);
                                else if(callee is ValueElementAccess indexed)
                                {
                                    Text("]");
                                    Node(indexed.Index);
                                    Text("[");
                                }
                                Text(receiverTemp);
                                Text("])))(");
                                for(int i=call.Arguments.Count-1;i>=0;i--)
                                {
                                    if(i==call.SpreadIndex)
                                    {
                                        Text(")");
                                        Node(call.Arguments[i]);
                                        Text("...(");
                                    }
                                    else Node(call.Arguments[i]);
                                    if(i>0)Text(", ");
                                }
                                Text("(("+receiverTemp+") => (("+functionTemp+") => ("+
                                    functionTemp+" === null || "+functionTemp+
                                    " === undefined ? undefined : Reflect.apply("+functionTemp+
                                    ", "+receiverTemp+", [");
                            }
                            else
                            {
                                Text(")");
                                Node(call.Callee);
                                Text(")))(");
                                for(int i=call.Arguments.Count-1;i>=0;i--)
                                {
                                    if(i==call.SpreadIndex)
                                    {
                                        Text(")");
                                        Node(call.Arguments[i]);
                                        Text("...(");
                                    }
                                    else Node(call.Arguments[i]);
                                    if(i>0)Text(", ");
                                }
                                Text("(("+functionTemp+") => ("+functionTemp+
                                    " === null || "+functionTemp+
                                    " === undefined ? undefined : "+functionTemp+"(");
                            }
                            break;
                        }
                        Text(")");
                        for(int i=call.Arguments.Count-1;i>=0;i--)
                        {
                            if(i==call.SpreadIndex)
                            {
                                // Always group the spread operand to preserve
                                // conditional/binary expression precedence.
                                Text(")");
                                Node(call.Arguments[i]);
                                Text("...(");
                            }
                            else Node(call.Arguments[i]);
                            if(i>0)Text(", ");
                        }
                        Text("(");
                        Node(call.Callee,precedence);
                        break;
                    default:
                        throw new NotSupportedException("Unsupported expression in emitted AST.");
                }
                if(wrap)Text("(");
            }
        }
    }
}

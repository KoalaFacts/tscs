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
            var names=new HashSet<string>(StringComparer.Ordinal);
            var pending=new Stack<ValueNode>();
            pending.Push(root);
            while(pending.Count>0)
            {
                switch(pending.Pop())
                {
                    case ValueAtom atom:
                        if(atom.Token.Kind==TokenKind.Identifier)names.Add(atom.Token.Text);
                        break;
                    case ValueCall call:
                        names.Add(call.Callee.Text);
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
                    case ValueElementAccess indexed:
                        pending.Push(indexed.Receiver);pending.Push(indexed.Index);break;
                    case ValuePropertyAccess property:pending.Push(property.Receiver);break;
                    case ValueArray array:
                        foreach(var element in array.Elements)pending.Push(element);
                        break;
                    case ValueObject obj:
                        foreach(var property in obj.Properties)pending.Push(property.Value);
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

        private readonly record struct Piece(ValueNode? Node,string? Literal,int ParentPrecedence,bool IsRight);

        private static int Precedence(ValueNode node) => node switch
        {
            ValueConditional => 0,
            ValueBinary binary => binary.Operator.Kind switch
            {
                TokenKind.BarBar => 1,
                TokenKind.AmpAmp => 2,
                TokenKind.Bar => 3,
                TokenKind.Caret => 4,
                TokenKind.Amp => 5,
                TokenKind.EqualEqual or TokenKind.BangEqual or TokenKind.StrictEqual or TokenKind.StrictNotEqual => 6,
                TokenKind.LessThan or TokenKind.LessEqual or TokenKind.GreaterThan or TokenKind.GreaterEqual => 7,
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
            ValueTypeOperator => 14,
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

            Node(value);
            while(pending.Count>0)
            {
                var item=pending.Pop();
                if(item.Literal is { } literal)
                {
                    output.Append(literal);
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
                        else
                        {
                            Node(binary.Right,precedence,true);
                            Text(" "+binary.Operator.Text+" ");
                            Node(binary.Left,precedence);
                        }
                        break;
                    case ValueUnary unary:
                        Node(unary.Operand,precedence);
                        Text(unary.Operator.Text+(unary.Operator.Kind is TokenKind.Plus or TokenKind.Minus or TokenKind.Typeof?" ":""));
                        break;
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
                    case ValueParenthesized grouped:
                        Text(")");
                        Node(grouped.Expression);
                        Text("(");
                        break;
                    case ValueElementAccess indexed:
                        Text("]");
                        Node(indexed.Index);
                        Text("[");
                        Node(indexed.Receiver,precedence);
                        break;
                    case ValueArray array:
                        Text("]");
                        for(int i=array.Elements.Count-1;i>=0;i--)
                        {
                            Node(array.Elements[i]);
                            if(i>0)Text(", ");
                        }
                        Text("[");
                        break;
                    case ValuePropertyAccess property:
                        Text("."+property.Name.Text);
                        Node(property.Receiver,precedence);
                        break;
                    case ValueObject literalObject:
                        // Parentheses keep an object literal an expression even
                        // at statement start or as an immediate member receiver.
                        Text(" })");
                        for(int i=literalObject.Properties.Count-1;i>=0;i--)
                        {
                            var property=literalObject.Properties[i];
                            Node(property.Value);
                            Text(property.Name.Text+": ");
                            if(i>0)Text(", ");
                        }
                        Text("({ ");
                        break;
                    case ValueCall call:
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
                        Text(call.Callee.Text);
                        break;
                    default:
                        throw new NotSupportedException("Unsupported expression in emitted AST.");
                }
                if(wrap)Text("(");
            }
        }
    }
}

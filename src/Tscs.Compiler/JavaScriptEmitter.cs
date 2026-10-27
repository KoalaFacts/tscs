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
                case PropertyAssignment property:
                    Indent(depth);
                    WriteExpression(property.Target);
                    output.Append(" = ");
                    WriteExpression(property.Value);
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
                case ReturnStatement returned:
                    Indent(depth);
                    output.Append("return ");
                    WriteExpression(returned.Value);
                    output.Append(";\n");
                    break;
                case FunctionDeclaration function:
                    Indent(depth);
                    if(function.Exported)output.Append("export ");
                    output.Append("function ").Append(function.Name.Text).Append('(');
                    for(int i=0;i<function.Parameters.Count;i++)
                    {
                        if(i>0)output.Append(", ");
                        output.Append(function.Parameters[i].Name.Text);
                    }
                    output.Append(") ");
                    WriteBlock(function.Body,depth);
                    output.Append('\n');
                    break;
                case ConditionalStatement conditional:
                    Indent(depth);
                    WriteConditional(conditional,depth);
                    output.Append('\n');
                    break;
                case ImportDeclaration import:
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
            ValueBinary binary => binary.Operator.Kind switch
            {
                TokenKind.BarBar => 1,
                TokenKind.AmpAmp => 2,
                TokenKind.EqualEqual or TokenKind.BangEqual or TokenKind.StrictEqual or TokenKind.StrictNotEqual => 3,
                TokenKind.LessThan or TokenKind.LessEqual or TokenKind.GreaterThan or TokenKind.GreaterEqual => 4,
                TokenKind.Plus or TokenKind.Minus => 5,
                TokenKind.Star => 6,
                _ => throw new NotSupportedException("Unsupported binary operator in emitted AST.")
            },
            ValueUnary => 7,
            ValueCall or ValuePropertyAccess => 8,
            ValueAtom or ValueParenthesized or ValueObject => 9,
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
                    case ValueBinary binary:
                        Node(binary.Right,precedence,true);
                        Text(" "+binary.Operator.Text+" ");
                        Node(binary.Left,precedence);
                        break;
                    case ValueUnary unary:
                        Node(unary.Operand,precedence);
                        Text(unary.Operator.Text+(unary.Operator.Kind is TokenKind.Plus or TokenKind.Minus?" ":""));
                        break;
                    case ValueParenthesized grouped:
                        Text(")");
                        Node(grouped.Expression);
                        Text("(");
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
                            Node(call.Arguments[i]);
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

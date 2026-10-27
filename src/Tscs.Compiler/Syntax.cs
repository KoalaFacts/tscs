namespace Tscs;

internal enum TokenKind { End, Identifier, Number, String, Let, Const, True, False, Open, Close, Colon, Semi, Equal, Comma, Unsupported, Null, OpenBracket, CloseBracket, Minus, MinusMinus, Plus, PlusPlus, Star, Import, Export, Function, Return, If, Else, OpenParen, CloseParen, Bang, AmpAmp, BarBar, EqualEqual, BangEqual, StrictEqual, StrictNotEqual, LessThan, LessEqual, GreaterThan, GreaterEqual }
[Flags]
internal enum StringTokenFlags { None=0, Unterminated=1<<2, ExtendedUnicodeEscape=1<<3, UnicodeEscape=1<<10, ContainsInvalidEscape=1<<11, HexEscape=1<<12, SingleQuote=1<<16 }
internal readonly record struct Token(TokenKind Kind, string Text, int Start, int Length, bool LineBreak = false,string? Value = null, StringTokenFlags Flags = StringTokenFlags.None);
internal abstract record Statement;
internal sealed record Block(List<Statement> Statements) : Statement;
internal abstract record ValueNode;
internal sealed record ValueAtom(Token Token) : ValueNode;
internal sealed record ValueBinary(ValueNode Left, Token Operator, ValueNode Right) : ValueNode;
internal sealed record ValueUnary(Token Operator, ValueNode Operand) : ValueNode;
internal sealed record ValueParenthesized(Token Open, ValueNode Expression, Token Close) : ValueNode;
internal sealed record ValueCall(Token Callee, IReadOnlyList<ValueNode> Arguments) : ValueNode;
internal sealed record Variable(Token Name, bool Constant, string? Annotation, ValueNode? Initializer, bool Exported=false) : Statement;
internal sealed record ParameterDeclaration(Token Name,string Annotation) : Statement;
internal sealed record FunctionDeclaration(Token Name,IReadOnlyList<ParameterDeclaration> Parameters,string ReturnAnnotation,Block Body,bool Exported=false) : Statement;
internal sealed record ReturnStatement(Token Keyword,ValueNode Value) : Statement;
internal sealed record ConditionalStatement(Token Keyword,ValueNode Condition,Block Consequent,Statement? Alternate) : Statement;
internal sealed record ImportSpecifier(Token Imported,Token Local);
internal sealed record ImportDeclaration(Token ModulePath,IReadOnlyList<ImportSpecifier> Specifiers) : Statement;
internal sealed record ExportSpecifier(Token Local,Token Exported);
internal sealed record ExportDeclaration(IReadOnlyList<ExportSpecifier> Specifiers,Token? ModulePath=null) : Statement;
internal sealed record Assignment(Token Name, ValueNode Value) : Statement;
internal sealed record PostfixUpdate(Token Name, Token Operator) : Statement;
internal sealed record Expression(ValueNode Value) : Statement;
internal sealed class CompilationFailure(Diagnostic diagnostic) : Exception(diagnostic.Message)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}

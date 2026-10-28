namespace Tscs;

internal enum TokenKind { End, Identifier, Number, String, Let, Const, True, False, Open, Close, Colon, Semi, Equal, Comma, Unsupported, Null, OpenBracket, CloseBracket, Minus, MinusMinus, Plus, PlusPlus, PlusEqual, MinusEqual, StarEqual, SlashEqual, PercentEqual, Star, Slash, Percent, Import, Export, Function, Return, If, Else, For, While, Do, Switch, Case, Default, Break, Continue, Type, Interface, OpenParen, CloseParen, Bang, Typeof, AmpAmp, BarBar, Bar, Amp, Caret, Tilde, LeftShift, RightShift, UnsignedRightShift, EqualEqual, BangEqual, StrictEqual, StrictNotEqual, Question, LessThan, LessEqual, GreaterThan, GreaterEqual, Dot }
[Flags]
internal enum StringTokenFlags { None=0, Unterminated=1<<2, ExtendedUnicodeEscape=1<<3, UnicodeEscape=1<<10, ContainsInvalidEscape=1<<11, HexEscape=1<<12, SingleQuote=1<<16 }
internal readonly record struct Token(TokenKind Kind, string Text, int Start, int Length, bool LineBreak = false,string? Value = null, StringTokenFlags Flags = StringTokenFlags.None);
internal abstract record Statement;
internal sealed record EmptyStatement() : Statement;
internal sealed record Block(List<Statement> Statements) : Statement;
// Type syntax preserves source tokens; semantic types are built by the binder.
internal abstract record TypeSyntax;
internal sealed record PrimitiveTypeSyntax(Token Name) : TypeSyntax;
// Literal annotations retain decoded source values independently from widened expressions.
internal sealed record LiteralTypeSyntax(Token Literal,bool Negative=false) : TypeSyntax;
internal sealed record ObjectTypeProperty(Token Name,TypeSyntax Type);
internal sealed record ObjectTypeSyntax(Token Open,IReadOnlyList<ObjectTypeProperty> Properties,Token Close) : TypeSyntax;
internal sealed record ArrayTypeSyntax(TypeSyntax Element,Token Open,Token Close) : TypeSyntax;
internal sealed record NamedTypeSyntax(Token Name) : TypeSyntax;
internal sealed record UnionTypeSyntax(IReadOnlyList<TypeSyntax> Members) : TypeSyntax;
internal sealed record TypeAliasDeclaration(Token Name,TypeSyntax Target,bool Exported=false) : Statement;
internal sealed record InterfaceDeclaration(Token Name,ObjectTypeSyntax Body,bool Exported=false) : Statement;
internal abstract record ValueNode;
internal sealed record ValueAtom(Token Token) : ValueNode;
internal sealed record ValueBinary(ValueNode Left, Token Operator, ValueNode Right) : ValueNode;
internal sealed record ValueConditional(ValueNode Condition,Token Question,ValueNode WhenTrue,Token Colon,ValueNode WhenFalse) : ValueNode;
// Contextual operators are erased in JS: 'as' asserts a new view,
// 'satisfies' checks a constraint while retaining source inference.
internal sealed record ValueTypeOperator(ValueNode Expression,Token Keyword,TypeSyntax Target) : ValueNode;
internal sealed record ValueUnary(Token Operator, ValueNode Operand) : ValueNode;
internal sealed record ValueParenthesized(Token Open, ValueNode Expression, Token Close) : ValueNode;
internal sealed record ValueCall(Token Callee, IReadOnlyList<ValueNode> Arguments) : ValueNode;
internal sealed record ObjectProperty(Token Name,ValueNode Value);
internal sealed record ValueObject(Token Open,IReadOnlyList<ObjectProperty> Properties,Token Close) : ValueNode;
internal sealed record ValueArray(Token Open,IReadOnlyList<ValueNode> Elements,Token Close) : ValueNode;
internal sealed record ValueElementAccess(ValueNode Receiver,Token Open,ValueNode Index,Token Close) : ValueNode;
internal sealed record ValuePropertyAccess(ValueNode Receiver,Token Name) : ValueNode;
internal sealed record PropertyAssignment(ValuePropertyAccess Target,ValueNode Value) : Statement;
internal sealed record ElementAssignment(ValueElementAccess Target,ValueNode Value) : Statement;
internal sealed record Variable(Token Name, bool Constant, TypeSyntax? Annotation, ValueNode? Initializer, bool Exported=false) : Statement;
internal sealed record ParameterDeclaration(Token Name,TypeSyntax Annotation) : Statement;
internal sealed record FunctionDeclaration(Token Name,IReadOnlyList<ParameterDeclaration> Parameters,TypeSyntax ReturnAnnotation,Block Body,bool Exported=false) : Statement;
internal sealed record ReturnStatement(Token Keyword,ValueNode Value) : Statement;
internal sealed record ConditionalStatement(Token Keyword,ValueNode Condition,Block Consequent,Statement? Alternate) : Statement;
// Braced switch arms keep lexical scopes while flow analysis models return, break and fallthrough.
internal sealed record SwitchClause(Token Keyword,ValueNode? Label,Block Body);
internal sealed record SwitchStatement(Token Keyword,ValueNode Selector,IReadOnlyList<SwitchClause> Clauses) : Statement;
internal sealed record BreakStatement(Token Keyword) : Statement;
internal sealed record ContinueStatement(Token Keyword) : Statement;
internal sealed record ForOfStatement(Token Keyword,Token Name,bool Constant,ValueNode Source,Block Body) : Statement;
internal sealed record WhileStatement(Token Keyword,ValueNode Condition,Block Body) : Statement;
internal sealed record DoWhileStatement(Token Keyword,Block Body,ValueNode Condition) : Statement;
// A classic for declaration is scoped to the header, body and increment.
internal sealed record ClassicForStatement(Token Keyword,Statement? Initializer,ValueNode? Condition,Statement? Update,Block Body) : Statement;
internal sealed record ImportSpecifier(Token Imported,Token Local);
internal sealed record ImportDeclaration(Token ModulePath,IReadOnlyList<ImportSpecifier> Specifiers,bool TypeOnly=false) : Statement;
internal sealed record ExportSpecifier(Token Local,Token Exported);
internal sealed record ExportDeclaration(IReadOnlyList<ExportSpecifier> Specifiers,Token? ModulePath=null) : Statement;
internal sealed record Assignment(Token Name, ValueNode Value) : Statement;
internal sealed record PostfixUpdate(Token Name, Token Operator) : Statement;
// Keep the original compound operator to preserve single evaluation of a property/index receiver.
internal sealed record CompoundAssignment(ValueNode Target,Token Operator,ValueNode Value) : Statement;
internal sealed record Expression(ValueNode Value) : Statement;
internal sealed class CompilationFailure(Diagnostic diagnostic) : Exception(diagnostic.Message)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}

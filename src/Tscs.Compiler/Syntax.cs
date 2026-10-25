namespace Tscs;

internal enum TokenKind { End, Identifier, Number, String, Let, Const, True, False, Open, Close, Colon, Semi, Equal, Comma, Unsupported, Null, OpenBracket, CloseBracket, Minus }
[Flags]
internal enum StringTokenFlags { None=0, Unterminated=1<<2, ExtendedUnicodeEscape=1<<3, UnicodeEscape=1<<10, ContainsInvalidEscape=1<<11, HexEscape=1<<12, SingleQuote=1<<16 }
internal readonly record struct Token(TokenKind Kind, string Text, int Start, int Length, bool LineBreak = false,string? Value = null, StringTokenFlags Flags = StringTokenFlags.None);
internal abstract record Statement;
internal sealed record Block(List<Statement> Statements) : Statement;
internal sealed record Variable(Token Name, bool Constant, string? Annotation, Token? Initializer) : Statement;
internal sealed record Assignment(Token Name, Token Value) : Statement;
internal sealed record Expression(Token Value) : Statement;
internal sealed class CompilationFailure(Diagnostic diagnostic) : Exception(diagnostic.Message)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}

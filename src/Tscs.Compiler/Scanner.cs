using System.Globalization;
using System.Text;

namespace Tscs;

// Bounded port of upstream scanner.Scan: trivia, identifier, primitive literal and punctuation paths.
internal sealed class Scanner(string file, string text)
{
    private int position;
    public List<Token> Scan()
    {
        var tokens = new List<Token>();
        while (true)
        {
            bool lineBreak = false;
            while (position < text.Length)
            {
                var c = text[position];
                if (c is ' ' or '\t' or '\v' or '\f' or '\uFEFF' || IsLineBreak(c) || char.GetUnicodeCategory(c)==UnicodeCategory.SpaceSeparator) { lineBreak |= IsLineBreak(c); position++; continue; }
                if (c == '/' && position + 1 < text.Length && text[position + 1] is '/' or '*')
                {
                    int start = position;
                    bool block = text[position + 1] == '*'; position += 2;
                    while (position < text.Length && (block ? !(text[position] == '*' && position + 1 < text.Length && text[position + 1] == '/') : !IsLineBreak(text[position])))
                    { lineBreak |= IsLineBreak(text[position]); position++; }
                    if (block)
                    {
                        if (position == text.Length) throw Error(new(TokenKind.End,"",position,0),"TS1010","'*/' expected.");
                        position += 2;
                    }
                    string comment = text[start..position];
                    if (comment.Contains("@ts-", StringComparison.Ordinal) || comment.StartsWith("///", StringComparison.Ordinal))
                        throw Error(new(TokenKind.Unsupported,comment,start,position-start),"TSCS1001","Comment directives are not supported yet.");
                    continue;
                }
                break;
            }
            if (position == text.Length) { tokens.Add(new(TokenKind.End,"",position,0,lineBreak)); return tokens; }
            int begin = position++; char first = text[begin]; TokenKind kind;string? decodedValue=null;
            if (IsIdentifierStart(first))
            {
                while (position < text.Length && IsIdentifierPart(text[position])) position++;
                string value = text[begin..position];
                kind = value switch { "let"=>TokenKind.Let,"const"=>TokenKind.Const,"true"=>TokenKind.True,"false"=>TokenKind.False,
                    "case" or "default" or "extends" or "super" or "catch" or "finally" or "in" or "instanceof" or "implements" or "package" or "private" or "protected" or "public" or "static" or "function" or "class" or "var" or "interface" or "type" or "import" or "export" or "declare" or "if" or "else" or "return" or "while" or "for" or "switch" or "new" or "null" or "undefined" or "this" or "throw" or "try" or "enum" or "async" or "await" or "yield" or "break" or "continue" or "do" or "delete" or "typeof" or "void" or "with" or "debugger" or "namespace" or "using" => TokenKind.Unsupported, _ => TokenKind.Identifier };
            }
            else if (first is >= '0' and <= '9')
            {
                while (position < text.Length && text[position] is >= '0' and <= '9') position++;
                kind = TokenKind.Number;
                if ((first == '0' && position > begin + 1) || (position < text.Length && (IsIdentifierPart(text[position]) || text[position] == '.'))) kind = TokenKind.Unsupported;
            }
            else if (first is '\'' or '"')
            {
                kind = TokenKind.String;
                var decoded=new StringBuilder();
                while (position < text.Length && text[position] != first && text[position] is not ('\r' or '\n'))
                {
                    if(text[position]!='\\'){decoded.Append(text[position++]);continue;}
                    int escapeStart=position++;
                    if(position==text.Length)throw Error(new(TokenKind.Unsupported,"\\",escapeStart,1),"TSCS1001","This string escape is not supported yet.");
                    char escaped=text[position++];
                    switch(escaped)
                    {
                        case 'b':decoded.Append('\b');break;
                        case 't':decoded.Append('\t');break;
                        case 'n':decoded.Append('\n');break;
                        case 'v':decoded.Append('\v');break;
                        case 'f':decoded.Append('\f');break;
                        case 'r':decoded.Append('\r');break;
                        case '\'':case '"':case '\\':case '/':decoded.Append(escaped);break;
                        case '\r':if(position<text.Length&&text[position]=='\n')position++;break;
                        case '\n':case '\u2028':case '\u2029':break;
                        default:throw Error(new(TokenKind.Unsupported,"\\",escapeStart,1),"TSCS1001","This string escape is not supported yet.");
                    }
                }
                if (position == text.Length || text[position] != first)
                    throw Error(new(TokenKind.String,text[begin..position],position,0),"TS1002","Unterminated string literal.");
                position++;
                decodedValue=decoded.ToString();
            }
            else kind = first switch { '{'=>TokenKind.Open,'}'=>TokenKind.Close,':'=>TokenKind.Colon,';'=>TokenKind.Semi,'='=>TokenKind.Equal,','=>TokenKind.Comma,_=>TokenKind.Unsupported };
            var token = new Token(kind,text[begin..position],begin,position-begin,lineBreak,decodedValue);
            if (kind == TokenKind.Unsupported) throw Error(token,"TSCS1001",$"Unsupported syntax '{token.Text}'.");
            tokens.Add(token);
        }
    }
    private CompilationFailure Error(Token token,string code,string message)=>new(Diagnostic.Error(file,token,code,message));
    internal static bool IsLineBreak(char c)=>c is '\r' or '\n' or '\u2028' or '\u2029';
    private static bool IsIdentifierStart(char c)=>c is '_' or '$' || char.IsLetter(c);
    private static bool IsIdentifierPart(char c)=>IsIdentifierStart(c)||char.IsDigit(c)||char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.ConnectorPunctuation || c is '\u200C' or '\u200D';
}

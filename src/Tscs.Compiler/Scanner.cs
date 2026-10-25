using System.Globalization;
using System.Text;

namespace Tscs;

// Bounded port of upstream scanner.Scan: trivia, identifier, primitive literal and punctuation paths.
internal sealed class Scanner(string file, string text, bool jsonMode=false)
{
    private int position;
    public List<Diagnostic> Diagnostics { get; } = [];
    // Cumulative lexical diagnostics available after each token was scanned.
    public List<int> DiagnosticCounts { get; } = [];
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
                    if (!jsonMode && (comment.Contains("@ts-", StringComparison.Ordinal) || comment.StartsWith("///", StringComparison.Ordinal)))
                        throw Error(new(TokenKind.Unsupported,comment,start,position-start),"TSCS1001","Comment directives are not supported yet.");
                    continue;
                }
                break;
            }
            if (position == text.Length) { tokens.Add(new(TokenKind.End,"",position,0,lineBreak)); DiagnosticCounts.Add(Diagnostics.Count); return tokens; }
            int begin = position++; char first = text[begin]; TokenKind kind;string? decodedValue=null;StringTokenFlags stringFlags=StringTokenFlags.None;
            if (IsIdentifierStart(first))
            {
                while (position < text.Length && IsIdentifierPart(text[position])) position++;
                string value = text[begin..position];
                kind = value switch { "let"=>TokenKind.Let,"const"=>TokenKind.Const,"true"=>TokenKind.True,"false"=>TokenKind.False,
                    "case" or "default" or "extends" or "super" or "catch" or "finally" or "in" or "instanceof" or "implements" or "package" or "private" or "protected" or "public" or "static" or "function" or "class" or "var" or "interface" or "type" or "import" or "export" or "declare" or "if" or "else" or "return" or "while" or "for" or "switch" or "new" or "null" or "undefined" or "this" or "throw" or "try" or "enum" or "async" or "await" or "yield" or "break" or "continue" or "do" or "delete" or "typeof" or "void" or "with" or "debugger" or "namespace" or "using" => TokenKind.Unsupported, _ => TokenKind.Identifier };
                if(jsonMode)
                {
                    // The bootstrap identifier classifier uses host Unicode tables.
                    // Do not expose that approximation as JSON name acceptance.
                    if(value.Any(c=>c>127))throw Error(new(TokenKind.Unsupported,value,begin,position-begin),"TSCS1001","Non-ASCII unquoted JSON names are not supported yet.");
                    kind=value switch {"true"=>TokenKind.True,"false"=>TokenKind.False,"null"=>TokenKind.Null,_=>TokenKind.Identifier};
                }
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
                decodedValue = ScanString(first, out stringFlags);
            }
            else kind = first switch { '[' when jsonMode=>TokenKind.OpenBracket,']' when jsonMode=>TokenKind.CloseBracket,'-' when jsonMode=>TokenKind.Minus, '{'=>TokenKind.Open,'}'=>TokenKind.Close,':'=>TokenKind.Colon,';'=>TokenKind.Semi,'='=>TokenKind.Equal,','=>TokenKind.Comma,_=>TokenKind.Unsupported };
            var token = new Token(kind,text[begin..position],begin,position-begin,lineBreak,decodedValue,stringFlags);
            if (kind == TokenKind.Unsupported) throw Error(token,"TSCS1001",$"Unsupported syntax '{token.Text}'.");
            tokens.Add(token);DiagnosticCounts.Add(Diagnostics.Count);
        }
    }
    // The string scanner recovers locally: malformed escape spelling remains in
    // the value, and the caller receives both tokens and every lexical error.
    private string ScanString(char quote, out StringTokenFlags flags)
    {
        flags=quote=='\''?StringTokenFlags.SingleQuote:StringTokenFlags.None;
        var value=new StringBuilder();
        while(position<text.Length && text[position]!=quote && text[position] is not ('\r' or '\n'))
        {
            if(text[position]=='\\')value.Append(ScanStringEscape(ref flags));
            else value.Append(text[position++]);
        }
        if(position==text.Length || text[position]!=quote)
        {
            flags|=StringTokenFlags.Unterminated;
            Report("TS1002","Unterminated string literal.",position);
        }
        else position++;
        return value.ToString();
    }
    private string ScanStringEscape(ref StringTokenFlags flags)
    {
        int start=position++;
        if(position==text.Length){Report("TS1126","Unexpected end of text.",position);return "";}
        char escaped=text[position++];
        switch(escaped)
        {
            case '0' when position==text.Length || text[position] is < '0' or > '9':return "\0";
            case >= '0' and <= '7':
                int limit=escaped<='3'?3:2;
                while(position<text.Length && position-start-1<limit && text[position] is >= '0' and <= '7')position++;
                int octal=Convert.ToInt32(text[(start+1)..position],8);
                flags|=StringTokenFlags.ContainsInvalidEscape;
                Report("TS1487",$"Octal escape sequences are not allowed. Use the syntax '\\x{octal.ToString("x2",CultureInfo.InvariantCulture)}'.",start,position-start);
                return ((char)octal).ToString();
            case '8':case '9':
                flags|=StringTokenFlags.ContainsInvalidEscape;
                Report("TS1488",$"Escape sequence '{text[start..position]}' is not allowed.",start,position-start);
                return escaped.ToString();
            case 'b':return "\b";
            case 't':return "\t";
            case 'n':return "\n";
            case 'v':return "\v";
            case 'f':return "\f";
            case 'r':return "\r";
            case '\'':case '"':case '\\':case '/':return escaped.ToString();
            case '\r':if(position<text.Length && text[position]=='\n')position++;return "";
            case '\n':case '\u2028':case '\u2029':return "";
            case 'x':case 'u':
                bool extended=escaped=='u' && position<text.Length && text[position]=='{';
                if(extended)position++;
                else if(escaped=='u')flags|=StringTokenFlags.UnicodeEscape;
                int digitsStart=position,required=escaped=='x'?2:4,codePoint=0;
                while(position<text.Length && (extended || position-digitsStart<required))
                {
                    int digit=HexDigit(text[position]);if(digit<0)break;
                    position++;
                    // Only validity up to 10FFFF matters. Saturation retains the
                    // full consumed span even for arbitrarily long invalid input.
                    codePoint=Math.Min(0x110000,codePoint*16+digit);
                }
                if(position-digitsStart<(extended?1:required))
                {
                    flags|=StringTokenFlags.ContainsInvalidEscape;
                    Report("TS1125","Hexadecimal digit expected.",position);
                    return text[start..position];
                }
                bool invalid=false;
                if(extended)
                {
                    if(codePoint>0x10FFFF){Report("TS1198","An extended Unicode escape value must be between 0x0 and 0x10FFFF inclusive.",digitsStart,position-digitsStart);invalid=true;}
                    if(position==text.Length){Report("TS1126","Unexpected end of text.",position);invalid=true;}
                    else if(text[position]=='}')position++;
                    else {Report("TS1199","Unterminated Unicode escape sequence.",position);invalid=true;}
                }
                if(invalid){flags|=StringTokenFlags.ContainsInvalidEscape;return text[start..position];}
                if(extended)flags|=StringTokenFlags.ExtendedUnicodeEscape;
                else if(escaped=='x')flags|=StringTokenFlags.HexEscape;
                // UTF16 storage preserves lone surrogates and mixed escaped pairs.
                return codePoint<=0xFFFF?((char)codePoint).ToString():char.ConvertFromUtf32(codePoint);
            default:
                // Identity escapes consume a whole source character. Preserve
                // supplementary UTF16 pairs instead of replacing either half.
                if(char.IsHighSurrogate(escaped) && position<text.Length && char.IsLowSurrogate(text[position]))
                {position++;return text[(position-2)..position];}
                return escaped.ToString();
        }
    }
    private static int HexDigit(char c)=>c is >= '0' and <= '9'?c-'0':c is >= 'a' and <= 'f'?c-'a'+10:c is >= 'A' and <= 'F'?c-'A'+10:-1;
    private void Report(string code,string message,int start,int length=0)=>Diagnostics.Add(Diagnostic.Error(file,new(TokenKind.String,"",start,length),code,message));
    private CompilationFailure Error(Token token,string code,string message)=>new(Diagnostic.Error(file,token,code,message));
    internal static bool IsLineBreak(char c)=>c is '\r' or '\n' or '\u2028' or '\u2029';
    private static bool IsIdentifierStart(char c)=>c is '_' or '$' || char.IsLetter(c);
    private static bool IsIdentifierPart(char c)=>IsIdentifierStart(c)||char.IsDigit(c)||char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.ConnectorPunctuation || c is '\u200C' or '\u200D';
}

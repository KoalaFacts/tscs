using System.Globalization;
using System.Text;

namespace Tscs;

// Bounded port of upstream scanner.Scan: trivia, identifier, primitive literal and punctuation paths.
internal sealed class Scanner(string file, string text, bool jsonMode=false)
{
    private int position;
    public List<Diagnostic> Diagnostics { get; } = [];
    private readonly List<(int Start,int End)> ignoredNextLines = [];
    private readonly HashSet<int> referenceDirectiveStarts=jsonMode
        ? []
        : TripleSlashPathReferences.Collect(text).Select(r=>r.Start).ToHashSet();
    // Only checker diagnostics on the immediately following physical line
    // can be suppressed; parser and scanner diagnostics still fail normally.
    public bool SuppressesSemanticDiagnostic(int offset)
    {
        int low=0,high=ignoredNextLines.Count-1;
        while(low<=high)
        {
            int mid=low+(high-low)/2;
            var range=ignoredNextLines[mid];
            if(offset<range.Start)high=mid-1;
            else if(offset>=range.End)low=mid+1;
            else return true;
        }
        return false;
    }
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
                    {
                        // A valid leading path reference is resolved by CompilerProgram.
                        // Do not treat arbitrary triple-slash comments as implemented.
                        if (!(comment.StartsWith("///",StringComparison.Ordinal) &&
                              referenceDirectiveStarts.Contains(start)) &&
                            !TryRegisterTsIgnore(start,position,block))
                            throw Error(new(TokenKind.Unsupported,comment,start,position-start),"TSCS1001","Comment directives are not supported yet.");
                    }
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
                kind = value switch { "let"=>TokenKind.Let,"const"=>TokenKind.Const,"import"=>TokenKind.Import,"export"=>TokenKind.Export,"function"=>TokenKind.Function,"return"=>TokenKind.Return,"if"=>TokenKind.If,"else"=>TokenKind.Else,"true"=>TokenKind.True,"false"=>TokenKind.False,
                    "case" or "default" or "extends" or "super" or "catch" or "finally" or "in" or "instanceof" or "implements" or "package" or "private" or "protected" or "public" or "static" or "class" or "var" or "interface" or "type" or "declare" or "while" or "for" or "switch" or "new" or "null" or "undefined" or "this" or "throw" or "try" or "enum" or "async" or "await" or "yield" or "break" or "continue" or "do" or "delete" or "typeof" or "void" or "with" or "debugger" or "namespace" or "using" => TokenKind.Unsupported, _ => TokenKind.Identifier };
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
            else kind = first switch { '.' when !jsonMode=>TokenKind.Dot,'[' when jsonMode=>TokenKind.OpenBracket,']' when jsonMode=>TokenKind.CloseBracket,'(' when !jsonMode=>TokenKind.OpenParen,')' when !jsonMode=>TokenKind.CloseParen,'-'=>TokenKind.Minus, '{'=>TokenKind.Open,'}'=>TokenKind.Close,':'=>TokenKind.Colon,';'=>TokenKind.Semi,'='=>TokenKind.Equal,','=>TokenKind.Comma,'+' when !jsonMode=>TokenKind.Plus,'*' when !jsonMode=>TokenKind.Star,_=>TokenKind.Unsupported };
            // Maximal-munch binary/logical operators only in source mode. Keep
            // JSON scanning unchanged and refuse lone '&' or '|' as before.
            if(!jsonMode)
            {
                if(first=='=' && position<text.Length && text[position]=='=')
                {
                    position++;
                    kind=TokenKind.EqualEqual;
                    if(position<text.Length && text[position]=='=') { position++;kind=TokenKind.StrictEqual; }
                }
                else if(first=='!')
                {
                    kind=TokenKind.Bang;
                    if(position<text.Length && text[position]=='=')
                    {
                        position++;
                        kind=TokenKind.BangEqual;
                        if(position<text.Length && text[position]=='=') { position++;kind=TokenKind.StrictNotEqual; }
                    }
                }
                else if(first=='&' && position<text.Length && text[position]=='&')
                { position++;kind=TokenKind.AmpAmp; }
                else if(first=='|' && position<text.Length && text[position]=='|')
                { position++;kind=TokenKind.BarBar; }
                else if(first=='<' || first=='>')
                {
                    kind=first=='<'?TokenKind.LessThan:TokenKind.GreaterThan;
                    if(position<text.Length && text[position]=='=')
                    { position++;kind=first=='<'?TokenKind.LessEqual:TokenKind.GreaterEqual; }
                }
            }
            // Lex -- as an unsupported decrement token, never as two unary/binary
            // minus operators. Whitespace-separated "- -" remains distinct.
            if(!jsonMode && kind==TokenKind.Minus && position<text.Length && text[position]=='-')
            { position++; kind=TokenKind.MinusMinus; }
            if(!jsonMode && kind==TokenKind.MinusMinus)
                throw Error(new(TokenKind.Unsupported,"--",begin,2,lineBreak),"TSCS1001","Decrement '--' is not supported yet.");
            // Keep the lexical distinction between a single '+' (unary or
            // binary, according to the parser) and the unsupported prefix '++'.
            if(!jsonMode && kind==TokenKind.Plus && position<text.Length && text[position]=='+')
            { position++; kind=TokenKind.PlusPlus; }
            if(!jsonMode && kind==TokenKind.PlusPlus &&
               (tokens.Count==0 || tokens[^1].Kind!=TokenKind.Identifier))
                throw Error(new(TokenKind.Unsupported,"++",begin,2,lineBreak),"TSCS1001","Only identifier postfix '++' is supported yet.");
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
    private bool TryRegisterTsIgnore(int start,int end,bool block)
    {
        // A deliberately bounded subset: leading whole-line // @ts-ignore.
        // Reject other @ts- directives and block/triple-slash forms.
        if(block)return false;
        var payload=text[(start+2)..end].TrimStart(' ','\t');
        const string directive="@ts-ignore";
        if(!payload.StartsWith(directive,StringComparison.Ordinal) ||
           (payload.Length>directive.Length && !char.IsWhiteSpace(payload[directive.Length])))
            return false;
        int lineStart=start;
        while(lineStart>0&&!IsLineBreak(text[lineStart-1]))lineStart--;
        for(int i=lineStart;i<start;i++)
            if(text[i] is not (' ' or '\t' or '\uFEFF'))return false;
        if(end>=text.Length||!IsLineBreak(text[end]))return true;
        int following=end+(text[end]=='\r'&&end+1<text.Length&&text[end+1]=='\n'?2:1);
        int nextLineEnd=following;
        while(nextLineEnd<text.Length&&!IsLineBreak(text[nextLineEnd]))nextLineEnd++;
        ignoredNextLines.Add((following,nextLineEnd));
        return true;
    }
    private static int HexDigit(char c)=>c is >= '0' and <= '9'?c-'0':c is >= 'a' and <= 'f'?c-'a'+10:c is >= 'A' and <= 'F'?c-'A'+10:-1;
    private void Report(string code,string message,int start,int length=0)=>Diagnostics.Add(Diagnostic.Error(file,new(TokenKind.String,"",start,length),code,message));
    private CompilationFailure Error(Token token,string code,string message)=>new(Diagnostic.Error(file,token,code,message));
    internal static bool IsLineBreak(char c)=>c is '\r' or '\n' or '\u2028' or '\u2029';
    private static bool IsIdentifierStart(char c)=>c is '_' or '$' || char.IsLetter(c);
    private static bool IsIdentifierPart(char c)=>IsIdentifierStart(c)||char.IsDigit(c)||char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.ConnectorPunctuation || c is '\u200C' or '\u200D';
}

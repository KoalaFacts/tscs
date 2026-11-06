namespace Tscs;

// Ambient declaration-file syntax (A2.1). This is a syntax tree only: nothing
// here is bound or checked yet, and the bounded statement Parser is unchanged.
// The grammar follows the pinned parser.go type and signature productions
// (parseType, parseNonArrayType, parseTypeMemberSemicolon, parseMappedType,
// isStartOfFunctionTypeOrConstructorType, parseTypeOrTypePredicate).
internal abstract record AmbientType;
internal sealed record AmbientTypeReference(IReadOnlyList<Token> Name,IReadOnlyList<AmbientType> Arguments) : AmbientType;
internal sealed record AmbientThisType(Token Keyword) : AmbientType;
internal sealed record AmbientLiteralType(Token Literal,bool Negative=false) : AmbientType;
internal sealed record AmbientArrayType(AmbientType Element) : AmbientType;
internal sealed record AmbientTupleElement(Token? Name,AmbientType Type,bool Optional,bool Rest);
internal sealed record AmbientTupleType(IReadOnlyList<AmbientTupleElement> Elements) : AmbientType;
internal sealed record AmbientUnionType(IReadOnlyList<AmbientType> Members) : AmbientType;
internal sealed record AmbientIntersectionType(IReadOnlyList<AmbientType> Members) : AmbientType;
internal sealed record AmbientFunctionType(AmbientSignature Signature,bool Constructor,bool Abstract) : AmbientType;
// keyof, unique and readonly.
internal sealed record AmbientTypeOperator(Token Operator,AmbientType Target) : AmbientType;
internal sealed record AmbientTypeQuery(IReadOnlyList<Token> Name,IReadOnlyList<AmbientType> Arguments) : AmbientType;
internal sealed record AmbientIndexedAccessType(AmbientType Target,AmbientType Index) : AmbientType;
internal sealed record AmbientConditionalType(AmbientType Check,AmbientType Extends,AmbientType True,AmbientType False) : AmbientType;
internal sealed record AmbientInferType(Token Name,AmbientType? Constraint) : AmbientType;
// Modifiers are "+", "-", the bare modifier, or null when absent.
internal sealed record AmbientMappedType(string? ReadonlyModifier,Token Parameter,AmbientType Constraint,
    AmbientType? NameType,string? OptionalModifier,AmbientType? Type) : AmbientType;
internal sealed record AmbientTypeLiteral(IReadOnlyList<AmbientMember> Members) : AmbientType;
internal sealed record AmbientParenthesizedType(AmbientType Type) : AmbientType;
// Raw text pieces (escapes kept): Head, then each substitution's type and the text after it.
internal sealed record AmbientTemplateLiteralType(Token Token,string Head,IReadOnlyList<(AmbientType Type,string Text)> Spans) : AmbientType;
// `x is T`, `this is T`, `asserts x` and `asserts x is T`.
internal sealed record AmbientTypePredicate(bool Asserts,Token Parameter,AmbientType? Type) : AmbientType;

internal sealed record AmbientTypeParameter(Token Name,AmbientType? Constraint,AmbientType? Default,IReadOnlyList<string> Modifiers);
// A binding-pattern parameter keeps its opening token as Name and its bound names.
internal sealed record AmbientParameter(Token Name,AmbientType? Type,bool Optional,bool Rest,IReadOnlyList<Token>? PatternNames=null);
internal sealed record AmbientSignature(IReadOnlyList<AmbientTypeParameter> TypeParameters,
    IReadOnlyList<AmbientParameter> Parameters,AmbientType? ReturnType);

// A property name is an identifier/keyword, string or numeric literal, or a
// computed entity name such as [Symbol.iterator].
internal sealed record AmbientPropertyName(IReadOnlyList<Token> Tokens,bool Computed)
{
    public string Text=>Computed?"["+string.Join(".",Tokens.Select(t=>t.Text))+"]":Tokens[0].Value??Tokens[0].Text;
}
internal abstract record AmbientMember;
internal sealed record AmbientPropertySignature(AmbientPropertyName Name,AmbientType? Type,bool Optional,bool Readonly) : AmbientMember;
internal sealed record AmbientMethodSignature(AmbientPropertyName Name,AmbientSignature Signature,bool Optional) : AmbientMember;
internal sealed record AmbientCallSignature(AmbientSignature Signature,bool Construct) : AmbientMember;
internal sealed record AmbientIndexSignature(IReadOnlyList<AmbientParameter> Parameters,AmbientType Type,bool Readonly) : AmbientMember;
internal sealed record AmbientAccessorSignature(AmbientPropertyName Name,AmbientSignature Signature,bool Setter) : AmbientMember;

internal abstract record AmbientStatement(bool Exported);
internal sealed record AmbientInterface(Token Name,IReadOnlyList<AmbientTypeParameter> TypeParameters,
    IReadOnlyList<AmbientTypeReference> Bases,IReadOnlyList<AmbientMember> Members,bool Exported) : AmbientStatement(Exported);
internal sealed record AmbientTypeAlias(Token Name,IReadOnlyList<AmbientTypeParameter> TypeParameters,AmbientType Type,bool Exported) : AmbientStatement(Exported);
// Keyword is var, let or const.
internal sealed record AmbientVariable(Token Keyword,Token Name,AmbientType? Type,bool Exported) : AmbientStatement(Exported);
internal sealed record AmbientFunction(Token Name,AmbientSignature Signature,bool Exported) : AmbientStatement(Exported);
// `declare global` has an empty Name and keeps its `global` keyword.
internal sealed record AmbientNamespace(IReadOnlyList<Token> Name,IReadOnlyList<AmbientStatement> Body,bool Exported,Token? GlobalKeyword=null) : AmbientStatement(Exported);
internal sealed record AmbientExportList(IReadOnlyList<Token> Names) : AmbientStatement(false);
// An ambient class: members carry their modifiers; no bodies or initializers.
internal sealed record AmbientClass(Token Name,IReadOnlyList<AmbientTypeParameter> TypeParameters,
    AmbientTypeReference? Base,IReadOnlyList<AmbientTypeReference> Implements,IReadOnlyList<AmbientClassMember> Members,
    bool Abstract,bool Exported) : AmbientStatement(Exported);
internal sealed record AmbientClassMember(IReadOnlyList<string> Modifiers,AmbientMember Member);
internal sealed record AmbientConstructorDeclaration(AmbientSignature Signature) : AmbientMember;

internal sealed record AmbientDeclarationFile(string File,IReadOnlyList<AmbientStatement> Statements,
    IReadOnlyList<(string Name,string Value)> ReferenceDirectives,IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>
/// Parses an ambient declaration file. The first syntax error stops the parse
/// and is reported with the upstream code; valid syntax this subset does not
/// model yet (classes, enums, imports, template literal types, initializers)
/// is reported as TSCS1001 rather than accepted.
/// </summary>
internal sealed class DeclarationParser
{
    private readonly string file;
    private List<Token> tokens;
    private int position;
    // inDisallowConditionalTypesContext: set while parsing an extends clause.
    private bool disallowConditional;
    private DeclarationParser(string file,List<Token> tokens){this.file=file;this.tokens=tokens;}

    public static AmbientDeclarationFile Parse(string file,string text)
    {
        var scanner=new Scanner(file,text,declarationFile:true);
        var diagnostics=new List<Diagnostic>();
        var statements=new List<AmbientStatement>();
        try
        {
            var tokens=scanner.Scan();
            diagnostics.AddRange(scanner.Diagnostics);
            if(diagnostics.Count==0)
            {
                var parser=new DeclarationParser(file,tokens);
                while(parser.Current.Kind!=TokenKind.End)
                    if(parser.ParseStatement(topLevel:true) is { } statement)statements.Add(statement);
            }
        }
        catch(CompilationFailure failure)
        {
            diagnostics.AddRange(scanner.Diagnostics);
            diagnostics.Add(failure.Diagnostic);
        }
        return new(file,statements,scanner.ReferenceDirectives,diagnostics);
    }

    private Token Current=>tokens[position];
    private Token Peek(int offset=1)=>tokens[Math.Min(position+offset,tokens.Count-1)];
    private Token Take()=>tokens[position++];
    private bool Is(string word)=>IsWord(Current) && Current.Text==word;
    private bool Eat(TokenKind kind){if(Current.Kind!=kind)return false;position++;return true;}
    private bool EatWord(string word){if(!Is(word))return false;position++;return true;}
    private CompilationFailure Error(string code,string message)=>new(Diagnostic.Error(file,Current,code,message));
    private CompilationFailure Unsupported(string construct)=>Error("TSCS1001",$"{construct} are not supported in declaration files yet.");
    private Token Expect(TokenKind kind,string text)
    {
        if(Current.Kind!=kind)throw Error("TS1005",$"'{text}' expected.");
        return Take();
    }
    private Token ExpectIdentifier()
    {
        if(!IsWord(Current))throw Error("TS1003","Identifier expected.");
        return Take();
    }
    // parseIdentifier: reserved words are not identifiers (parseIdentifierName
    // positions, such as property names and parts after '.', use ExpectIdentifier).
    private Token ExpectBindingIdentifier()
    {
        if(IsWord(Current) && ReservedWords.Contains(Current.Text))
            throw Error("TS1359",$"Identifier expected. '{Current.Text}' is a reserved word that cannot be used here.");
        return ExpectIdentifier();
    }
    private static readonly HashSet<string> ReservedWords=new(StringComparer.Ordinal)
    {
        "break","case","catch","class","const","continue","debugger","default","delete","do","else","enum",
        "export","extends","false","finally","for","function","if","import","in","instanceof","new","null",
        "return","super","switch","this","throw","true","try","typeof","var","void","while","with"
    };
    // Bounded recursion: deeply nested input fails closed instead of overflowing the stack.
    private const int MaximumNesting=200;
    private int nesting;
    private T Nested<T>(Func<T> parse)
    {
        if(++nesting>MaximumNesting)
        {
            nesting=0;
            throw Unsupported($"Declarations nested deeper than {MaximumNesting} levels");
        }
        try{return parse();}
        finally{if(nesting>0)nesting--;}
    }
    // Identifiers and every keyword token: their Text is the spelling.
    private static bool IsWord(Token token)=>token.Kind is not (TokenKind.String or TokenKind.Number or TokenKind.Template or TokenKind.End) &&
        token.Text.Length>0 && (char.IsLetter(token.Text[0]) || token.Text[0] is '_' or '$');
    // parseSemicolon: an explicit ';', or ASI before '}', end of file or a line break.
    private void ExpectSemicolon()
    {
        if(Eat(TokenKind.Semi))return;
        if(Current.Kind is TokenKind.Close or TokenKind.End || Current.LineBreak)return;
        throw Error("TS1005","';' expected.");
    }
    // Generic '>' may arrive glued to following punctuation; split it in place.
    private void ExpectGreaterThan()
    {
        if(Eat(TokenKind.GreaterThan))return;
        var tail=Current.Kind switch
        {
            TokenKind.RightShift=>TokenKind.GreaterThan,
            TokenKind.UnsignedRightShift=>TokenKind.RightShift,
            TokenKind.GreaterEqual=>TokenKind.Equal,
            TokenKind.RightShiftEqual=>TokenKind.GreaterEqual,
            TokenKind.UnsignedRightShiftEqual=>TokenKind.RightShiftEqual,
            _=>TokenKind.Unsupported
        };
        if(tail==TokenKind.Unsupported)throw Error("TS1005","'>' expected.");
        // Re-tokenize as '>' plus the remainder so a backtracking re-parse
        // sees the same tokens.
        var original=Current;
        tokens[position]=original with {Kind=TokenKind.GreaterThan,Text=">",Length=1};
        tokens.Insert(position+1,original with {Kind=tail,Text=original.Text[1..],Start=original.Start+1,Length=original.Length-1,LineBreak=false});
        position++;
    }
    private T WithConditionalTypes<T>(bool disallow,Func<T> parse)
    {
        bool saved=disallowConditional;
        disallowConditional=disallow;
        try{return parse();}
        finally{disallowConditional=saved;}
    }

    private AmbientStatement? ParseStatement(bool topLevel)
    {
        if(Eat(TokenKind.Semi))return null;
        var start=Current;
        bool exported=false;
        if(Current.Kind==TokenKind.Export)
        {
            Take();
            exported=true;
            if(Eat(TokenKind.Open))
            {
                var names=new List<Token>();
                while(Current.Kind!=TokenKind.Close)
                {
                    names.Add(ExpectIdentifier());
                    if(Is("as"))throw Unsupported("Export aliases");
                    if(!Eat(TokenKind.Comma))break;
                }
                Expect(TokenKind.Close,"}");
                if(Is("from"))throw Unsupported("Re-exports");
                ExpectSemicolon();
                return new AmbientExportList(names);
            }
            if(Current.Kind is TokenKind.Default or TokenKind.Equal)throw Unsupported("Default exports and export assignments");
        }
        if(Current.Kind==TokenKind.Import)throw Unsupported("Imports");
        bool declared=EatWord("declare");
        if(Current.Kind==TokenKind.Interface)return ParseInterface(exported);
        if(Current.Kind==TokenKind.Type && IsWord(Peek()) && !Peek().LineBreak)return ParseTypeAlias(exported);
        // The pinned checker's grammar check for top-level .d.ts value declarations.
        if(topLevel && !declared && !exported &&
           (Current.Kind is TokenKind.Let or TokenKind.Const or TokenKind.Function || Is("var") || Is("namespace") || Is("module") ||
            Is("class") || (Is("abstract") && Peek().Text=="class")))
            throw new CompilationFailure(Diagnostic.Error(file,start,"TS1046","Top-level declarations in .d.ts files must start with either a 'declare' or 'export' modifier."));
        if(Current.Kind is TokenKind.Let or TokenKind.Const || Is("var"))
        {
            if(Current.Kind==TokenKind.Const && Peek().Text=="enum")throw Unsupported("Enums");
            return ParseVariables(exported);
        }
        if(Current.Kind==TokenKind.Function)return ParseFunction(exported);
        if((Is("namespace") || Is("module")) && IsWord(Peek()) && !Peek().LineBreak)return ParseNamespace(exported);
        if(Is("global") && Peek().Kind==TokenKind.Open)
        {
            if(!declared)throw Error("TS1005","'declare' expected.");
            var keyword=Take();
            return new AmbientNamespace([],ParseBlock(),exported,keyword);
        }
        if(Is("module") && Peek().Kind==TokenKind.String)throw Unsupported("Ambient module declarations");
        if(Is("enum"))throw Unsupported("Enums");
        if(Is("class"))return ParseClass(exported,isAbstract:false);
        if(Is("abstract") && Peek().Text=="class" && !Peek().LineBreak){Take();return ParseClass(exported,isAbstract:true);}
        throw Error("TS1128","Declaration or statement expected.");
    }

    private List<AmbientStatement> ParseBlock()=>Nested(ParseBlockBody);
    private List<AmbientStatement> ParseBlockBody()
    {
        Expect(TokenKind.Open,"{");
        var body=new List<AmbientStatement>();
        while(Current.Kind!=TokenKind.Close)
        {
            if(Current.Kind==TokenKind.End)throw Error("TS1005","'}' expected.");
            if(ParseStatement(topLevel:false) is { } statement)body.Add(statement);
        }
        Take();
        return body;
    }

    private AmbientNamespace ParseNamespace(bool exported)
    {
        Take();
        var name=new List<Token>{ExpectBindingIdentifier()};
        while(Eat(TokenKind.Dot))name.Add(ExpectBindingIdentifier());
        return new(name,ParseBlock(),exported);
    }

    private AmbientInterface ParseInterface(bool exported)
    {
        Take();
        var name=ExpectBindingIdentifier();
        var typeParameters=ParseTypeParameters();
        var bases=new List<AmbientTypeReference>();
        if(Eat(TokenKind.Extends))
        {
            do
            {
                var reference=ParseNonArrayType();
                if(reference is not AmbientTypeReference typeReference)throw Error("TS2499","An interface can only extend an identifier/qualified-name with optional type arguments.");
                bases.Add(typeReference);
            }
            while(Eat(TokenKind.Comma));
        }
        if(Is("implements"))throw Error("TS1176","Interface declaration cannot have 'implements' clause.");
        return new(name,typeParameters,bases,ParseMembers(),exported);
    }

    private AmbientTypeAlias ParseTypeAlias(bool exported)
    {
        Take();
        var name=ExpectBindingIdentifier();
        var typeParameters=ParseTypeParameters();
        Expect(TokenKind.Equal,"=");
        var type=Is("intrinsic") && Peek().Kind is not (TokenKind.Dot or TokenKind.LessThan or TokenKind.OpenBracket)
            ? new AmbientTypeReference([Take()],[])
            : ParseType();
        ExpectSemicolon();
        return new(name,typeParameters,type,exported);
    }

    private AmbientStatement ParseVariables(bool exported)
    {
        var keyword=Take();
        var name=ExpectBindingIdentifier();
        AmbientType? type=Eat(TokenKind.Colon)?ParseType():null;
        if(Current.Kind==TokenKind.Equal)throw Unsupported("Variable initializers");
        if(Current.Kind==TokenKind.Comma)throw Unsupported("Multiple variable declarators");
        ExpectSemicolon();
        return new AmbientVariable(keyword,name,type,exported);
    }

    private AmbientFunction ParseFunction(bool exported)
    {
        Take();
        var name=ExpectBindingIdentifier();
        var signature=ParseSignature(TokenKind.Colon);
        if(Current.Kind==TokenKind.Open)throw Error("TS1183","An implementation cannot be declared in ambient contexts.");
        ExpectSemicolon();
        return new(name,signature,exported);
    }

    private List<AmbientTypeParameter> ParseTypeParameters()
    {
        var parameters=new List<AmbientTypeParameter>();
        if(!Eat(TokenKind.LessThan))return parameters;
        // The pinned checker's grammar check reports the empty list at its start.
        if(Current.Kind==TokenKind.GreaterThan)throw Error("TS1098","Type parameter list cannot be empty.");
        while(true)
        {
            var modifiers=new List<string>();
            while((Is("in") || Is("out") || Current.Kind==TokenKind.Const) && IsWord(Peek()))modifiers.Add(Take().Text);
            var name=ExpectBindingIdentifier();
            AmbientType? constraint=Eat(TokenKind.Extends)?ParseType():null;
            AmbientType? defaultType=Eat(TokenKind.Equal)?ParseType():null;
            parameters.Add(new(name,constraint,defaultType,modifiers));
            if(!Eat(TokenKind.Comma))break;
            if(Current.Kind is TokenKind.GreaterThan)break;
        }
        ExpectGreaterThan();
        return parameters;
    }

    // returnToken is ':' for declarations and members, '=>' for function types.
    private AmbientSignature ParseSignature(TokenKind returnToken)
    {
        var typeParameters=ParseTypeParameters();
        Expect(TokenKind.OpenParen,"(");
        var parameters=ParseParameters(TokenKind.CloseParen);
        Expect(TokenKind.CloseParen,")");
        AmbientType? returnType=null;
        if(returnToken==TokenKind.Arrow)
        {
            Expect(TokenKind.Arrow,"=>");
            returnType=ParseReturnType();
        }
        else if(Eat(TokenKind.Colon))returnType=ParseReturnType();
        return new(typeParameters,parameters,returnType);
    }

    private List<AmbientParameter> ParseParameters(TokenKind close)
    {
        var parameters=new List<AmbientParameter>();
        while(Current.Kind!=close)
        {
            bool rest=Eat(TokenKind.Ellipsis);
            List<Token>? patternNames=null;
            Token name;
            if(Current.Kind is TokenKind.Open or TokenKind.OpenBracket)
            {
                name=Current;
                patternNames=[];
                ParseBindingPattern(patternNames);
            }
            else name=Is("this")?Take():ExpectBindingIdentifier();
            bool optional=Eat(TokenKind.Question);
            AmbientType? type=Eat(TokenKind.Colon)?ParseType():null;
            if(Current.Kind==TokenKind.Equal)throw Error("TS2371","A parameter initializer is only allowed in a function or constructor implementation.");
            parameters.Add(new(name,type,optional,rest,patternNames));
            if(!Eat(TokenKind.Comma))break;
        }
        return parameters;
    }

    // parseArrayBindingPattern / parseObjectBindingPattern without initializers.
    private void ParseBindingPattern(List<Token> names)=>Nested(()=>{ParseBindingPatternBody(names);return true;});
    private void ParseBindingPatternBody(List<Token> names)
    {
        bool array=Current.Kind==TokenKind.OpenBracket;
        var close=array?TokenKind.CloseBracket:TokenKind.Close;
        Take();
        while(Current.Kind!=close)
        {
            if(array && Current.Kind==TokenKind.Comma){Take();continue;}
            bool rest=Eat(TokenKind.Ellipsis);
            if(!array && !rest && (IsWord(Current) || Current.Kind is TokenKind.String or TokenKind.Number) && Peek().Kind==TokenKind.Colon)
            {
                Take();
                Take();
            }
            if(Current.Kind is TokenKind.Open or TokenKind.OpenBracket)ParseBindingPattern(names);
            else names.Add(ExpectBindingIdentifier());
            if(Current.Kind==TokenKind.Equal)throw Error("TS2371","A parameter initializer is only allowed in a function or constructor implementation.");
            if(!Eat(TokenKind.Comma))break;
        }
        Expect(close,array?"]":"}");
    }

    private AmbientClass ParseClass(bool exported,bool isAbstract)
    {
        Take();
        var name=ExpectBindingIdentifier();
        var typeParameters=ParseTypeParameters();
        AmbientTypeReference? baseType=null;
        var implemented=new List<AmbientTypeReference>();
        if(Eat(TokenKind.Extends))
        {
            baseType=ParseHeritageType();
            if(Current.Kind==TokenKind.Comma)throw Error("TS1174","Classes can only extend a single class.");
        }
        if(EatWord("implements"))
        {
            do implemented.Add(ParseHeritageType());
            while(Eat(TokenKind.Comma));
        }
        return new(name,typeParameters,baseType,implemented,Nested(ParseClassMembers),isAbstract,exported);
    }

    // ExpressionWithTypeArguments limited to entity names, as in declaration files.
    private AmbientTypeReference ParseHeritageType()
    {
        var name=ParseEntityName();
        return new(name,ParseTypeArguments());
    }

    private static readonly HashSet<string> ClassModifiers=new(StringComparer.Ordinal)
        {"public","private","protected","static","readonly","abstract","declare","override","accessor"};

    private List<AmbientClassMember> ParseClassMembers()
    {
        Expect(TokenKind.Open,"{");
        var members=new List<AmbientClassMember>();
        while(Current.Kind!=TokenKind.Close)
        {
            if(Current.Kind==TokenKind.End)throw Error("TS1005","'}' expected.");
            if(Eat(TokenKind.Semi))continue;
            var modifiers=new List<string>();
            while(IsWord(Current) && ClassModifiers.Contains(Current.Text) && IsModifier(Current.Text,classMember:true))modifiers.Add(Take().Text);
            var member=ParseClassMember();
            var memberName=member switch
            {
                AmbientPropertySignature property=>property.Name,
                AmbientMethodSignature method=>method.Name,
                AmbientAccessorSignature accessor=>accessor.Name,
                _=>null
            };
            if(memberName?.Tokens[0].Kind==TokenKind.PrivateIdentifier &&
               modifiers.Any(m=>m is "private" or "public" or "protected"))
                throw Error("TS18010","An accessibility modifier cannot be used with a private identifier.");
            members.Add(new(modifiers,member));
            if(Current.Kind==TokenKind.Open)throw Error("TS1183","An implementation cannot be declared in ambient contexts.");
            if(Current.Kind==TokenKind.Equal)throw Unsupported("Class property initializers");
            ExpectSemicolon();
        }
        Take();
        return members;
    }

    private AmbientMember ParseClassMember()
    {
        if((Is("constructor") && Peek().Kind is TokenKind.OpenParen or TokenKind.LessThan) ||
            (Current.Kind==TokenKind.String && Current.Value=="constructor" && Peek().Kind==TokenKind.OpenParen))
        {
            Take();
            return new AmbientConstructorDeclaration(ParseSignature(TokenKind.Colon));
        }
        if(Current.Kind==TokenKind.OpenBracket && IsIndexSignature())
        {
            Take();
            var parameters=ParseParameters(TokenKind.CloseBracket);
            Expect(TokenKind.CloseBracket,"]");
            if(parameters.Count!=1)throw Error("TS1096","An index signature must have exactly one parameter.");
            Expect(TokenKind.Colon,":");
            return new AmbientIndexSignature(parameters,ParseType(),false);
        }
        if(IsAccessorKeyword())
        {
            bool setter=Take().Text=="set";
            var accessorName=ParsePropertyName(allowPrivate:true);
            return new AmbientAccessorSignature(accessorName,ParseSignature(TokenKind.Colon),setter);
        }
        if(Current.Kind is TokenKind.OpenParen or TokenKind.LessThan)
            throw Error("TS1068","Unexpected token. A constructor, method, accessor, or property was expected.");
        var name=ParsePropertyName(allowPrivate:true);
        bool optional=Eat(TokenKind.Question);
        if(name.Tokens[0].Kind==TokenKind.PrivateIdentifier && optional)
            throw Error("TS18019","'?' modifier cannot be used with a private identifier.");
        if(Current.Kind is TokenKind.OpenParen or TokenKind.LessThan)
            return new AmbientMethodSignature(name,ParseSignature(TokenKind.Colon),optional);
        if(Current.Kind==TokenKind.Bang)throw Unsupported("Definite assignment assertions");
        AmbientType? type=Eat(TokenKind.Colon)?ParseType():null;
        return new AmbientPropertySignature(name,type,optional,false);
    }

    private AmbientTemplateLiteralType ParseTemplateLiteralType()
    {
        var token=Take();
        string raw=token.Text;
        var spans=new List<(AmbientType Type,string Text)>();
        var text=new System.Text.StringBuilder();
        string? head=null;
        for(int i=1;i<raw.Length-1;)
        {
            if(raw[i]=='\\' && i+1<raw.Length-1){text.Append(raw,i,2);i+=2;continue;}
            if(raw[i]=='$' && raw[i+1]=='{')
            {
                int end=TemplateInterpolation.FindClose(raw,i+2);
                var type=ParseEmbeddedType(raw[(i+2)..end],token.Start+i+2);
                if(head is null)head=text.ToString();
                else spans[^1]=(spans[^1].Type,text.ToString());
                text.Clear();
                spans.Add((type,""));
                i=end+1;
                continue;
            }
            text.Append(raw[i++]);
        }
        spans[^1]=(spans[^1].Type,text.ToString());
        return new(token,head!,spans);
    }

    // A template substitution is scanned on its own and parsed as one type,
    // keeping absolute source offsets for diagnostics.
    private AmbientType ParseEmbeddedType(string source,int offset)
    {
        var scanner=new Scanner(file,source,declarationFile:true);
        List<Token> embedded;
        try{embedded=scanner.Scan();}
        catch(CompilationFailure failure){throw new CompilationFailure(failure.Diagnostic with {Start=failure.Diagnostic.Start+offset});}
        if(scanner.Diagnostics.Count>0)throw new CompilationFailure(scanner.Diagnostics[0] with {Start=scanner.Diagnostics[0].Start+offset});
        var savedTokens=tokens;
        int savedPosition=position;
        tokens=embedded.Select(t=>t with {Start=t.Start+offset}).ToList();
        position=0;
        try
        {
            if(Current.Kind==TokenKind.End)throw Error("TS1110","Type expected.");
            var type=ParseType();
            if(Current.Kind!=TokenKind.End)throw Error("TS1005","'}' expected.");
            return type;
        }
        finally{tokens=savedTokens;position=savedPosition;}
    }

    private AmbientType ParseReturnType()=>WithConditionalTypes(false,ParseTypeOrTypePredicate);

    private AmbientType ParseTypeOrTypePredicate()
    {
        // parseTypeOrTypePredicate and parseAssertsTypePredicate.
        if(Is("asserts") && !Peek().LineBreak && IsWord(Peek()))
        {
            Take();
            var parameter=Take();
            AmbientType? type=null;
            if(Is("is") && !Current.LineBreak){Take();type=ParseType();}
            return new AmbientTypePredicate(true,parameter,type);
        }
        if(IsWord(Current) && Peek().Text=="is" && IsWord(Peek()) && !Peek().LineBreak)
        {
            var parameter=Take();
            Take();
            return new AmbientTypePredicate(false,parameter,ParseType());
        }
        return ParseType();
    }

    private List<AmbientMember> ParseMembers()=>Nested(ParseMemberList);
    private List<AmbientMember> ParseMemberList()
    {
        Expect(TokenKind.Open,"{");
        var members=new List<AmbientMember>();
        while(Current.Kind!=TokenKind.Close)
        {
            if(Current.Kind==TokenKind.End)throw Error("TS1005","'}' expected.");
            members.Add(ParseMember());
            // parseTypeMemberSemicolon: ',' or ';' or ASI.
            if(Eat(TokenKind.Comma) || Eat(TokenKind.Semi))continue;
            if(Current.Kind!=TokenKind.Close && !Current.LineBreak)throw Error("TS1005","';' expected.");
        }
        Take();
        return members;
    }

    // A word acts as a modifier only when the next token can start a member name.
    private bool IsModifier(string word,bool classMember=false)=>Is(word) &&
        (!Peek().LineBreak || (classMember && word=="static")) &&
        Peek().Kind is not (TokenKind.Colon or TokenKind.OpenParen or TokenKind.Question or TokenKind.LessThan or
            TokenKind.Semi or TokenKind.Comma or TokenKind.Close or TokenKind.Equal or TokenKind.End);
    // canFollowGetOrSetKeyword: no same-line requirement for get/set.
    private bool IsAccessorKeyword()=>(Is("get") || Is("set")) &&
        (Peek().Kind is TokenKind.OpenBracket or TokenKind.String or TokenKind.Number or TokenKind.PrivateIdentifier || IsWord(Peek()));

    private AmbientMember ParseMember()
    {
        if(Current.Kind is TokenKind.OpenParen or TokenKind.LessThan)
            return new AmbientCallSignature(ParseSignature(TokenKind.Colon),false);
        if(Is("new") && Peek().Kind is TokenKind.OpenParen or TokenKind.LessThan)
        {
            Take();
            return new AmbientCallSignature(ParseSignature(TokenKind.Colon),true);
        }
        bool isReadonly=false;
        if(IsModifier("readonly")){Take();isReadonly=true;}
        if(Current.Kind==TokenKind.OpenBracket && IsIndexSignature())
        {
            Take();
            var parameters=ParseParameters(TokenKind.CloseBracket);
            Expect(TokenKind.CloseBracket,"]");
            if(parameters.Count!=1)throw Error("TS1096","An index signature must have exactly one parameter.");
            Expect(TokenKind.Colon,":");
            return new AmbientIndexSignature(parameters,ParseType(),isReadonly);
        }
        if(!isReadonly && IsAccessorKeyword())
        {
            bool setter=Take().Text=="set";
            var accessorName=ParsePropertyName();
            return new AmbientAccessorSignature(accessorName,ParseSignature(TokenKind.Colon),setter);
        }
        var name=ParsePropertyName();
        bool optional=Eat(TokenKind.Question);
        if(Current.Kind is TokenKind.OpenParen or TokenKind.LessThan)
        {
            if(isReadonly)throw Error("TS1024","'readonly' modifier can only appear on a property declaration or index signature.");
            return new AmbientMethodSignature(name,ParseSignature(TokenKind.Colon),optional);
        }
        AmbientType? type=Eat(TokenKind.Colon)?ParseType():null;
        if(Current.Kind==TokenKind.Equal)throw Error("TS1246","An interface property cannot have an initializer.");
        return new AmbientPropertySignature(name,type,optional,isReadonly);
    }

    // isUnambiguouslyIndexSignature: '[' name ':' or '[' name ',' (or a modifier/rest).
    private bool IsIndexSignature()
    {
        var next=Peek();
        if(next.Kind==TokenKind.Ellipsis || next.Kind==TokenKind.CloseBracket)return true;
        if(!IsWord(next))return false;
        return Peek(2).Kind is TokenKind.Colon or TokenKind.Comma || (Peek(2).Kind==TokenKind.Question && Peek(3).Kind==TokenKind.Colon);
    }

    private AmbientPropertyName ParsePropertyName(bool allowPrivate=false)
    {
        // Private identifiers belong to ambient class members, never to
        // interfaces, object type literals, or computed property names.
        if(Current.Kind==TokenKind.PrivateIdentifier)
        {
            if(!allowPrivate)throw Unsupported("Private identifiers outside class members");
            return new([Take()],false);
        }
        // Templates are not literal property names (isTypeMemberStart).
        if(Current.Kind==TokenKind.Template || Current.Text.StartsWith('`'))throw Error("TS1131","Property or signature expected.");
        if(Current.Kind is TokenKind.String or TokenKind.Number)return new([Take()],false);
        if(IsWord(Current))return new([Take()],false);
        if(Eat(TokenKind.OpenBracket))
        {
            var parts=new List<Token>();
            if(Current.Kind is TokenKind.String or TokenKind.Number)parts.Add(Take());
            else
            {
                parts.Add(ExpectIdentifier());
                while(Eat(TokenKind.Dot))parts.Add(ExpectIdentifier());
            }
            if(Current.Kind!=TokenKind.CloseBracket)throw Unsupported("Computed property names other than entity names");
            Take();
            return new(parts,true);
        }
        throw Error("TS1131","Property or signature expected.");
    }

    // parseType: function/constructor types, then a union with an optional
    // conditional suffix (disallowed inside an extends clause).
    private AmbientType ParseType()=>Nested(ParseTypeWorker);
    private AmbientType ParseTypeWorker()
    {
        if(IsStartOfFunctionOrConstructorType())return ParseFunctionType();
        var type=ParseUnion();
        if(!disallowConditional && !Current.LineBreak && Current.Kind==TokenKind.Extends)
        {
            Take();
            var extendsType=WithConditionalTypes(true,ParseType);
            Expect(TokenKind.Question,"?");
            var trueType=WithConditionalTypes(false,ParseType);
            Expect(TokenKind.Colon,":");
            var falseType=WithConditionalTypes(false,ParseType);
            return new AmbientConditionalType(type,extendsType,trueType,falseType);
        }
        return type;
    }

    private bool IsStartOfFunctionOrConstructorType()
    {
        if(Current.Kind==TokenKind.LessThan)return true;
        if(Current.Kind==TokenKind.OpenParen)return IsUnambiguouslyStartOfFunctionType();
        if(Is("new"))return true;
        return Is("abstract") && Peek().Text=="new";
    }

    private bool IsUnambiguouslyStartOfFunctionType()
    {
        var next=Peek();
        if(next.Kind is TokenKind.CloseParen or TokenKind.Ellipsis)return true;
        // skipParameterStart: an identifier or a balanced binding pattern.
        int after;
        if(IsWord(next))after=2;
        else if(next.Kind is TokenKind.Open or TokenKind.OpenBracket)
        {
            int depth=0;
            after=1;
            do
            {
                var kind=Peek(after).Kind;
                if(kind is TokenKind.Open or TokenKind.OpenBracket)depth++;
                else if(kind is TokenKind.Close or TokenKind.CloseBracket)depth--;
                else if(kind==TokenKind.End)return false;
                after++;
            }
            while(depth>0);
        }
        else return false;
        if(Peek(after).Kind is TokenKind.Colon or TokenKind.Comma or TokenKind.Question or TokenKind.Equal)return true;
        return Peek(after).Kind==TokenKind.CloseParen && Peek(after+1).Kind==TokenKind.Arrow;
    }

    private AmbientFunctionType ParseFunctionType()
    {
        bool isAbstract=EatWord("abstract");
        bool constructor=EatWord("new");
        return new(ParseSignature(TokenKind.Arrow),constructor,isAbstract);
    }

    private AmbientType ParseUnion()
    {
        bool leading=Eat(TokenKind.Bar);
        var members=new List<AmbientType>{ParseIntersection()};
        while(Eat(TokenKind.Bar))members.Add(ParseIntersection());
        return members.Count==1 && !leading?members[0]:new AmbientUnionType(members);
    }

    private AmbientType ParseIntersection()
    {
        bool leading=Eat(TokenKind.Amp);
        var members=new List<AmbientType>{ParseOperatorType()};
        while(Eat(TokenKind.Amp))members.Add(ParseOperatorType());
        return members.Count==1 && !leading?members[0]:new AmbientIntersectionType(members);
    }

    private AmbientType ParseOperatorType()
    {
        // parseTypeOperatorOrHigher: these words are keywords in type position.
        if(Is("keyof") || Is("unique") || Is("readonly"))
        {
            var op=Take();
            return new AmbientTypeOperator(op,ParseOperatorType());
        }
        if(Is("infer"))
        {
            Take();
            var name=ExpectBindingIdentifier();
            AmbientType? constraint=null;
            // tryParseConstraintOfInferType: keep `extends` only when it is not
            // the start of an enclosing conditional type.
            if(Current.Kind==TokenKind.Extends)
            {
                int saved=position;
                Take();
                var candidate=WithConditionalTypes(true,ParseType);
                if(disallowConditional || Current.Kind!=TokenKind.Question)constraint=candidate;
                else position=saved;
            }
            return new AmbientInferType(name,constraint);
        }
        return WithConditionalTypes(false,ParsePostfixType);
    }

    private AmbientType ParsePostfixType()
    {
        var type=ParseNonArrayType();
        while(!Current.LineBreak && Current.Kind==TokenKind.OpenBracket)
        {
            Take();
            if(Eat(TokenKind.CloseBracket)){type=new AmbientArrayType(type);continue;}
            var index=ParseType();
            Expect(TokenKind.CloseBracket,"]");
            type=new AmbientIndexedAccessType(type,index);
        }
        return type;
    }

    private AmbientType ParseNonArrayType()
    {
        switch(Current.Kind)
        {
            // A String token may be a no-substitution template literal type.
            case TokenKind.String:case TokenKind.Number:case TokenKind.True:case TokenKind.False:
                return new AmbientLiteralType(Take());
            case TokenKind.Template:
                return ParseTemplateLiteralType();
            case TokenKind.Minus when Peek().Kind==TokenKind.Number:
                Take();
                return new AmbientLiteralType(Take(),Negative:true);
            case TokenKind.Typeof:
            {
                Take();
                if(Current.Kind==TokenKind.Import)throw Unsupported("Import types");
                var name=ParseEntityName();
                return new AmbientTypeQuery(name,ParseTypeArguments());
            }
            case TokenKind.Open:
                return IsStartOfMappedType()?ParseMappedType():new AmbientTypeLiteral(ParseMembers());
            case TokenKind.OpenBracket:
                return ParseTuple();
            case TokenKind.OpenParen:
            {
                Take();
                var inner=ParseType();
                Expect(TokenKind.CloseParen,")");
                return new AmbientParenthesizedType(inner);
            }
            case TokenKind.Import:
                throw Unsupported("Import types");
        }
        if(Is("this"))return new AmbientThisType(Take());
        if(Current.Kind==TokenKind.Null)return new AmbientLiteralType(Take());
        if(Current.Kind==TokenKind.Void)return new AmbientTypeReference([Take()],[]);
        if(IsWord(Current))
        {
            var name=ParseEntityName();
            return new AmbientTypeReference(name,ParseTypeArguments());
        }
        throw Error("TS1110","Type expected.");
    }

    private List<Token> ParseEntityName()
    {
        var name=new List<Token>{ExpectBindingIdentifier()};
        while(Current.Kind==TokenKind.Dot){Take();name.Add(ExpectIdentifier());}
        return name;
    }

    private List<AmbientType> ParseTypeArguments()
    {
        var arguments=new List<AmbientType>();
        if(Current.LineBreak || Current.Kind!=TokenKind.LessThan)return arguments;
        Take();
        if(Current.Kind==TokenKind.GreaterThan)throw Unsupported("Empty type argument lists");
        do
        {
            if(Current.Kind==TokenKind.GreaterThan)throw Unsupported("Trailing commas in type arguments");
            arguments.Add(ParseType());
        }
        while(Eat(TokenKind.Comma));
        ExpectGreaterThan();
        return arguments;
    }

    private bool IsStartOfMappedType()
    {
        int offset=1;
        if(Peek(offset).Kind is TokenKind.Plus or TokenKind.Minus)
        {
            if(Peek(offset+1).Text!="readonly")return false;
            offset+=2;
        }
        else if(Peek(offset).Text=="readonly" && IsWord(Peek(offset)))offset++;
        return Peek(offset).Kind==TokenKind.OpenBracket && IsWord(Peek(offset+1)) && Peek(offset+2).Kind==TokenKind.In;
    }

    private AmbientMappedType ParseMappedType()
    {
        Take();
        string? readonlyModifier=null;
        if(Current.Kind is TokenKind.Plus or TokenKind.Minus){readonlyModifier=Take().Text;Take();}
        else if(EatWord("readonly"))readonlyModifier="readonly";
        Expect(TokenKind.OpenBracket,"[");
        var parameter=ExpectBindingIdentifier();
        Expect(TokenKind.In,"in");
        var constraint=ParseType();
        AmbientType? nameType=EatWord("as")?ParseType():null;
        Expect(TokenKind.CloseBracket,"]");
        string? optionalModifier=null;
        if(Current.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            optionalModifier=Take().Text;
            Expect(TokenKind.Question,"?");
        }
        else if(Eat(TokenKind.Question))optionalModifier="?";
        AmbientType? type=Eat(TokenKind.Colon)?ParseType():null;
        ExpectSemicolon();
        if(Current.Kind!=TokenKind.Close)throw Unsupported("Mapped types with additional members");
        Take();
        return new(readonlyModifier,parameter,constraint,nameType,optionalModifier,type);
    }

    private AmbientTupleType ParseTuple()
    {
        Take();
        var elements=new List<AmbientTupleElement>();
        while(Current.Kind!=TokenKind.CloseBracket)
        {
            bool rest=Eat(TokenKind.Ellipsis);
            Token? name=null;
            bool optional=false;
            // Named member: name ':' or name '?' ':'.
            if(IsWord(Current) && (Peek().Kind==TokenKind.Colon || (Peek().Kind==TokenKind.Question && Peek(2).Kind==TokenKind.Colon)))
            {
                name=Take();
                optional=Eat(TokenKind.Question);
                Take();
            }
            var type=ParseType();
            if(name is null && Current.Kind==TokenKind.Question){Take();optional=true;}
            elements.Add(new(name,type,optional,rest));
            if(!Eat(TokenKind.Comma))break;
        }
        Expect(TokenKind.CloseBracket,"]");
        return new(elements);
    }
}

namespace Tscs;
internal sealed record JsonProperty(JsonSyntaxNode Name,JsonSyntaxNode Value);
internal sealed record JsonSyntaxNode(string Kind,Token Token,IReadOnlyList<JsonProperty> Properties,IReadOnlyList<JsonSyntaxNode> Elements,string Number="");
internal sealed record JsonSyntaxResult(JsonSyntaxNode? Root,IReadOnlyList<Diagnostic> Diagnostics);
// Bounded parseJSONText/object/array/value port. Unsupported grammar stays explicit;
// this is not the upstream parser's general recovery or effective-options layer.
internal static class JsonSyntax
{
    public static JsonSyntaxResult Parse(string file,string text)
    {
        var scanner=new Scanner(file,text,jsonMode:true);
        try
        {
            if(text.Length>1_000_000)throw new CompilationFailure(Diagnostic.Error(file,new(TokenKind.End,"",0,0),"TSCS1001","JSON exceeds the bootstrap size limit."));
            var reader=new Reader(file,scanner.Scan());var root=reader.Parse();
            return new(root,scanner.Diagnostics.Concat(reader.Diagnostics).ToArray());
        }
        catch(CompilationFailure error){return new(null,scanner.Diagnostics.Append(error.Diagnostic).ToArray());}
    }
    private sealed class Reader(string file,List<Token> tokens)
    {
        private int position;
        private Token Current=>tokens[position];
        public List<Diagnostic> Diagnostics { get; }=[];
        private Token Take()=>tokens[position++];
        private bool Eat(TokenKind kind){if(Current.Kind!=kind)return false;Take();return true;}
        private void Unsupported()=>throw new CompilationFailure(Diagnostic.Error(file,Current,"TSCS1001","This JSON grammar or recovery path is not supported yet."));
        public JsonSyntaxNode? Parse()
        {
            if(Current.Kind==TokenKind.End)return null;
            var root=Value(0);if(Current.Kind!=TokenKind.End)Unsupported();Validate(root);return root;
        }
        private JsonSyntaxNode Value(int depth)
        {
            if(depth>128)Unsupported();var first=Current;
            if(Eat(TokenKind.Open))
            {
                var properties=new List<JsonProperty>();
                while(Current.Kind!=TokenKind.Close)
                {
                    if(Current.Kind is not (TokenKind.String or TokenKind.Identifier or TokenKind.Number or TokenKind.True or TokenKind.False or TokenKind.Null))Unsupported();
                    var name=Take();string kind=name.Kind==TokenKind.String?"String":name.Kind==TokenKind.Number?"Number":"Identifier";
                    var key=new JsonSyntaxNode(kind,name with {Value=kind=="Number"?null:name.Value??name.Text},[],[],kind=="Number"?name.Text:"");
                    if(!Eat(TokenKind.Colon))Unsupported();properties.Add(new(key,Value(depth+1)));
                    if(!Eat(TokenKind.Comma))break;
                }
                if(Current.Kind!=TokenKind.Close)Unsupported();var end=Take();
                return new("Object",first with {Length=end.Start+end.Length-first.Start},properties,[]);
            }
            if(Eat(TokenKind.OpenBracket))
            {
                var elements=new List<JsonSyntaxNode>();
                while(Current.Kind!=TokenKind.CloseBracket){elements.Add(Value(depth+1));if(!Eat(TokenKind.Comma))break;}
                if(Current.Kind!=TokenKind.CloseBracket)Unsupported();var end=Take();
                return new("Array",first with {Length=end.Start+end.Length-first.Start},[],elements);
            }
            if(Eat(TokenKind.Minus))
            {
                if(Current.Kind!=TokenKind.Number)Unsupported();var number=Take();
                return new("Number",first with {Length=number.Start+number.Length-first.Start},[],[],"-"+number.Text);
            }
            if(Current.Kind is not (TokenKind.String or TokenKind.Number or TokenKind.True or TokenKind.False or TokenKind.Null))Unsupported();
            var token=Take();return new(token.Kind.ToString(),token,[],[],token.Kind==TokenKind.Number?token.Text:"");
        }
        private void QuoteError(JsonSyntaxNode n)=>Diagnostics.Add(Diagnostic.Error(file,n.Token,"TS1327","String literal with double quotes expected."));
        private void Validate(JsonSyntaxNode node)
        {
            if(node.Kind=="String" && node.Token.Flags.HasFlag(StringTokenFlags.SingleQuote))QuoteError(node);
            foreach(var p in node.Properties){if(p.Name.Kind!="String"||p.Name.Token.Flags.HasFlag(StringTokenFlags.SingleQuote))QuoteError(p.Name);Validate(p.Value);}
            foreach(var value in node.Elements)Validate(value);
        }
    }
}

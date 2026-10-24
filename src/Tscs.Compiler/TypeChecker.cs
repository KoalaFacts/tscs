namespace Tscs;

// Primitive branches of checkVariableLikeDeclaration/checkIdentifier/checkBinaryLikeExpression.
internal sealed class TypeChecker(string file)
{
    private readonly List<Diagnostic> diagnostics=[];
    public IReadOnlyList<Diagnostic> Check(Block block) { CheckBlock(block,null);return diagnostics; }
    private void CheckBlock(Block block,Scope? parent)
    {
        var scope=Binder.Bind(file,block,parent);
        foreach(var statement in block.Statements)
        {
            switch(statement)
            {
                case Block nested: CheckBlock(nested,scope);break;
                case Variable v:
                    if(v.Name.Text is "eval" or "arguments") Error(v.Name,"TS1100",$"Invalid use of '{v.Name.Text}' in strict mode.");
                    if(parent==null && v.Name.Text=="globalThis") Error(v.Name,"TS2397","Declaration name conflicts with built-in global identifier 'globalThis'.");
                    var symbol=scope.Symbols[v.Name.Text];
                    var source=v.Initializer is {} init?TypeOf(init,scope):null;
                    symbol.Type=v.Annotation??source;
                    symbol.Initialized=v.Initializer!=null;
                    if(v.Constant&&v.Initializer==null) Error(v.Name,"TS1155","'const' declarations must be initialized.");
                    if(v.Annotation!=null&&source!=null&&source!=v.Annotation) Mismatch(v.Name,source,v.Annotation);
                    break;
                case Assignment a:
                    var target=Resolve(a.Name,scope);var assigned=TypeOf(a.Value,scope);
                    if(target==null)break;
                    if(target.Declaration.Constant) Error(a.Name,"TS2588",$"Cannot assign to '{a.Name.Text}' because it is a constant.");
                    else if(target.Type!=null&&assigned!=null&&target.Type!=assigned) Mismatch(a.Name,assigned,target.Type);
                    else target.Initialized=true;
                    break;
                case Expression e: TypeOf(e.Value,scope);break;
            }
        }
    }
    private string? TypeOf(Token token,Scope scope)
    {
        if(token.Kind==TokenKind.Number)return "number";
        if(token.Kind==TokenKind.String)return "string";
        if(token.Kind is TokenKind.True or TokenKind.False)return "boolean";
        var symbol=Resolve(token,scope);
        if(symbol!=null&&!symbol.Initialized)throw new CompilationFailure(Diagnostic.Error(file,token,"TSCS1001","Reads before initialization are not supported yet."));
        return symbol?.Type;
    }
    private Symbol? Resolve(Token name,Scope scope)
    {
        var symbol=scope.Find(name.Text);
        // Lexical declarations take precedence over the intrinsic, including TDZ checks.
        if(symbol==null&&name.Text=="globalThis")
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001","The built-in globalThis type is not supported yet."));
        if(symbol==null) Error(name,"TS2304",$"Cannot find name '{name.Text}'.");
        else if(name.Start<=symbol.Declaration.Name.Start)throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001","References before declaration are not supported yet."));
        return symbol;
    }
    private void Mismatch(Token token,string source,string target)=>Error(token,"TS2322",$"Type '{source}' is not assignable to type '{target}'.");
    private void Error(Token token,string code,string message)=>diagnostics.Add(Diagnostic.Error(file,token,code,message));
}

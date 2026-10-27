namespace Tscs;

// Primitive branches of checkVariableLikeDeclaration/checkIdentifier/checkBinaryLikeExpression.
internal sealed class TypeChecker(string file)
{
    private readonly List<Diagnostic> diagnostics=[];
    public IReadOnlyList<Diagnostic> Check(Block block) { CheckBlock(block,null);return diagnostics; }
    public IReadOnlyList<Diagnostic> CheckBound(Block block,Scope global)
    {
        CheckBlock(block,global,true);
        return diagnostics;
    }
    private void CheckBlock(Block block,Scope? parent,bool rootAlreadyBound=false)
    {
        var scope=rootAlreadyBound?parent!:Binder.Bind(file,block,parent);
        foreach(var statement in block.Statements)
        {
            switch(statement)
            {
                case Block nested: CheckBlock(nested,scope);break;
                case Variable v:
                    if(v.Name.Text is "eval" or "arguments") Error(v.Name,"TS1100",$"Invalid use of '{v.Name.Text}' in strict mode.");
                    if((parent==null || rootAlreadyBound) && v.Name.Text=="globalThis") Error(v.Name,"TS2397","Declaration name conflicts with built-in global identifier 'globalThis'.");
                    var symbol=scope.Symbols[v.Name.Text];
                    var source=v.Initializer is {} init?TypeOf(init,scope):null;
                    symbol.Type=v.Annotation??source;
                    symbol.Initialized=v.Initializer!=null;
                    if(v.Constant&&v.Initializer==null) Error(v.Name,"TS1155","'const' declarations must be initialized.");
                    if(v.Annotation!=null&&source!=null&&!Assignable(source,v.Annotation)) Mismatch(v.Name,source,v.Annotation);
                    break;
                case Assignment a:
                    var target=Resolve(a.Name,scope);var assigned=TypeOf(a.Value,scope);
                    if(target==null)break;
                    if(target.Imported) Error(a.Name,"TS2632",$"Cannot assign to '{a.Name.Text}' because it is an import.");
                    else if(target.Declaration.Constant) Error(a.Name,"TS2588",$"Cannot assign to '{a.Name.Text}' because it is a constant.");
                    else if(target.Type!=null&&assigned!=null&&!Assignable(assigned,target.Type)) Mismatch(a.Name,assigned,target.Type);
                    else target.Initialized=true;
                    break;
                case PostfixUpdate update:
                    var updated=Resolve(update.Name,scope);
                    if(updated==null)break;
                    if(!updated.Initialized)
                        throw new CompilationFailure(Diagnostic.Error(file,update.Name,"TSCS1001","Reads before initialization are not supported yet."));
                    if(updated.Imported)
                        Error(update.Name,"TS2632",$"Cannot assign to '{update.Name.Text}' because it is an import.");
                    else if(updated.Declaration.Constant)
                        Error(update.Name,"TS2588",$"Cannot assign to '{update.Name.Text}' because it is a constant.");
                    else if(updated.Type!="number")
                        throw new CompilationFailure(Diagnostic.Error(file,update.Operator,"TSCS1001","Only number postfix updates are supported yet."));
                    else updated.Initialized=true;
                    break;
                case Expression e: TypeOf(e.Value,scope);break;
            }
        }
    }
    private string? TypeOf(ValueNode value,Scope scope)
    {
        // The parser builds left-deep chains for numeric additions. Do not
        // recursively descend them: supported source can contain hundreds of
        // thousands of '+' operators under the source-size limit.
        // Post-order evaluation preserves left-to-right binding diagnostics
        // and reports the first unsupported operand at the same operator.
        var pending=new Stack<(ValueNode Node,bool Combine)>();
        var types=new Stack<string?>();
        pending.Push((value,false));
        while(pending.Count>0)
        {
            var (node,combine)=pending.Pop();
            if(node is ValueAtom atom)
            {
                types.Push(TypeOfAtom(atom.Token,scope));
                continue;
            }
            if(node is ValueUnary unary)
            {
                if(!combine)
                {
                    pending.Push((unary,true));
                    pending.Push((unary.Operand,false));
                    continue;
                }
                var operand=types.Pop();
                if(operand==null)types.Push(null);
                else if(operand=="number")types.Push("number");
                else throw new CompilationFailure(Diagnostic.Error(file,unary.Operator,"TSCS1001",
                    unary.Operator.Kind==TokenKind.Plus
                        ? "Only numeric unary plus is supported yet."
                        : "Only numeric unary minus is supported yet."));
                continue;
            }
            var binary=(ValueBinary)node;
            if(!combine)
            {
                pending.Push((binary,true));
                pending.Push((binary.Right,false));
                pending.Push((binary.Left,false));
                continue;
            }
            var right=types.Pop();
            var left=types.Pop();
            if(left==null||right==null)types.Push(null);
            else if(left=="number"&&right=="number"&&binary.Operator.Kind is TokenKind.Plus or TokenKind.Minus or TokenKind.Star)types.Push("number");
            else throw new CompilationFailure(Diagnostic.Error(file,binary.Operator,"TSCS1001",
                binary.Operator.Kind switch
                {
                    TokenKind.Star => "Only numeric multiplication expressions are supported yet.",
                    TokenKind.Minus => "Only numeric subtraction expressions are supported yet.",
                    _ => "Only numeric addition expressions are supported yet."
                }));
        }
        return types.Pop();
    }
    private string? TypeOfAtom(Token token,Scope scope)
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
        else if(symbol.File!=file && !symbol.CrossFileTypeReady)
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001","Cross-file inference requires an explicit annotation or primitive literal initializer."));
        else if(symbol.File==file && name.Start<=symbol.Declaration.Name.Start)
            throw new CompilationFailure(Diagnostic.Error(file,name,"TSCS1001","References before declaration are not supported yet."));
        return symbol;
    }
    // Bounded primitive/any assignment rule, not the full TS7 type relation.
    private static bool Assignable(string source,string target)=>source==target || source=="any" || target=="any";
    private void Mismatch(Token token,string source,string target)=>Error(token,"TS2322",$"Type '{source}' is not assignable to type '{target}'.");
    private void Error(Token token,string code,string message)=>diagnostics.Add(Diagnostic.Error(file,token,code,message));
}

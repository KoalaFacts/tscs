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
                    var annotatedType=v.Annotation is { } annotation
                        ? CompilerTypes.FromAnnotation(annotation) : null;
                    symbol.Type=annotatedType??source;
                    symbol.Initialized=v.Initializer!=null;
                    if(v.Constant&&v.Initializer==null) Error(v.Name,"TS1155","'const' declarations must be initialized.");
                    if(annotatedType!=null&&source!=null&&!CompilerTypes.IsAssignable(source,annotatedType))
                        Mismatch(v.Name,source,annotatedType);
                    break;
                case Assignment a:
                    var target=Resolve(a.Name,scope);var assigned=TypeOf(a.Value,scope);
                    if(target==null)break;
                    if(target.Imported) Error(a.Name,"TS2632",$"Cannot assign to '{a.Name.Text}' because it is an import.");
                    else if(target.Declaration.Constant) Error(a.Name,"TS2588",$"Cannot assign to '{a.Name.Text}' because it is a constant.");
                    else if(target.Type!=null&&assigned!=null&&!CompilerTypes.IsAssignable(assigned,target.Type)) Mismatch(a.Name,assigned,target.Type);
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
                    else if(!CompilerTypes.IsNumber(updated.Type))
                        throw new CompilationFailure(Diagnostic.Error(file,update.Operator,"TSCS1001","Only number postfix updates are supported yet."));
                    else updated.Initialized=true;
                    break;
                case Expression e: TypeOf(e.Value,scope);break;
            }
        }
    }
    private TypeSymbol? TypeOf(ValueNode value,Scope scope)
    {
        // The parser builds left-deep chains for numeric additions. Do not
        // recursively descend them: supported source can contain hundreds of
        // thousands of '+' operators under the source-size limit.
        // Post-order evaluation preserves left-to-right binding diagnostics
        // and reports the first unsupported operand at the same operator.
        var pending=new Stack<(ValueNode Node,bool Combine)>();
        var types=new Stack<TypeSymbol?>();
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
                else if(CompilerTypes.IsNumber(operand))types.Push(CompilerTypes.Number);
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
            else if(CompilerTypes.IsNumber(left)&&CompilerTypes.IsNumber(right)&&binary.Operator.Kind is TokenKind.Plus or TokenKind.Minus or TokenKind.Star)
                types.Push(CompilerTypes.Number);
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
    private TypeSymbol? TypeOfAtom(Token token,Scope scope)
    {
        var literal=CompilerTypes.FromLiteral(token);
        if(literal!=null)return literal;
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
    private void Mismatch(Token token,TypeSymbol source,TypeSymbol target)=>
        Error(token,"TS2322",$"Type '{source.DisplayName}' is not assignable to type '{target.DisplayName}'.");
    private void Error(Token token,string code,string message)=>diagnostics.Add(Diagnostic.Error(file,token,code,message));
}

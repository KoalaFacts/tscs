namespace Tscs;

// Name resolution over bound ambient declaration files (roadmap A2.2b). Every
// entity name in a type, `typeof` query, computed property name and heritage
// clause is looked up the way the pinned name resolver does
// (binder/nameresolver.go ResolveName): type parameters of the enclosing
// interface, class, type alias, signature or mapped type; `infer` parameters
// only in the true branch of their conditional type; parameters (values) in
// their own signature; then the exports of each enclosing namespace, then the
// globals and `globalThis`. Qualified names resolve their left side as a namespace and each
// member through its exports. A failed lookup reports the reference's
// diagnostic (TS2304, TS2503, TS2694, TS2693, TS2749); types themselves are
// not computed and nothing else is checked. Only script files and
// `declare global` blocks are resolved: module-local declarations are not
// bound (the pinned libs have none).
internal static class DeclarationResolver
{
    private const AmbientMeaning Namespace=AmbientMeaning.ValueModule|AmbientMeaning.NamespaceModule;

    // Keyword types the parser keeps as type references.
    private static readonly HashSet<string> Keywords=new(StringComparer.Ordinal)
        {"any","unknown","string","number","bigint","boolean","symbol","void","undefined","never","object"};

    private sealed class Scope(Scope? parent,IReadOnlyDictionary<string,AmbientSymbol>? table=null)
    {
        public Scope? Parent{get;}=parent;
        public IReadOnlyDictionary<string,AmbientSymbol>? Table{get;}=table;
        public HashSet<string> Types{get;}=new(StringComparer.Ordinal);
        public HashSet<string> Values{get;}=new(StringComparer.Ordinal);
    }

    public static IReadOnlyList<Diagnostic> Resolve(IEnumerable<AmbientDeclarationFile> files,AmbientGlobals globals)
    {
        var diagnostics=new List<Diagnostic>();
        // checker.go adds `globalThis`, a module whose exports are the globals.
        var table=new Dictionary<string,AmbientSymbol>(globals.Symbols,StringComparer.Ordinal);
        if(!table.ContainsKey("globalThis"))
        {
            var globalThis=new AmbientSymbol("globalThis"){Meanings=Namespace};
            foreach(var (name,symbol) in globals.Symbols)globalThis.Exports[name]=symbol;
            globalThis.Exports["globalThis"]=globalThis;
            table["globalThis"]=globalThis;
        }
        var root=new Scope(null,table);
        foreach(var file in files)
        {
            var walker=new Walker(file.File,diagnostics);
            var module=DeclarationBinder.IsModule(file);
            foreach(var statement in file.Statements)
            {
                if(statement is AmbientNamespace { Name.Count: 0 } global)
                {
                    if(module)foreach(var inner in global.Body)walker.Statement(inner,root);
                }
                else if(!module)walker.Statement(statement,root);
            }
        }
        return diagnostics;
    }

    private sealed class Walker(string file,List<Diagnostic> diagnostics)
    {
        public void Statement(AmbientStatement statement,Scope scope)
        {
            switch(statement)
            {
                case AmbientInterface i:
                {
                    var inner=TypeParameters(i.TypeParameters,scope);
                    foreach(var b in i.Bases)Type(b,inner);
                    foreach(var m in i.Members)Member(m,inner);
                    break;
                }
                case AmbientTypeAlias a:
                {
                    var inner=TypeParameters(a.TypeParameters,scope);
                    // `type Uppercase<S> = intrinsic;` (checker getTypeFromTypeAliasDeclaration).
                    if(a.Type is AmbientTypeReference { Name: [{ Text: "intrinsic" }], Arguments.Count: 0 })break;
                    Type(a.Type,inner);
                    break;
                }
                case AmbientVariable v:
                    if(v.Type is not null)Type(v.Type,scope);
                    break;
                case AmbientFunction f:
                    Signature(f.Signature,scope);
                    break;
                case AmbientClass c:
                {
                    var inner=TypeParameters(c.TypeParameters,scope);
                    // The extends clause is an expression: its name is a value.
                    if(c.Base is not null)
                    {
                        Name(c.Base.Name,AmbientMeaning.Value,inner);
                        foreach(var argument in c.Base.Arguments)Type(argument,inner);
                    }
                    foreach(var implemented in c.Implements)Type(implemented,inner);
                    foreach(var m in c.Members)Member(m.Member,inner);
                    break;
                }
                case AmbientNamespace { Name.Count: > 0 } ns:
                {
                    var inner=scope;
                    foreach(var part in ns.Name)
                    {
                        var symbol=inner.Table?.GetValueOrDefault(part.Text);
                        inner=new Scope(inner,symbol?.Exports??new Dictionary<string,AmbientSymbol>());
                    }
                    foreach(var s in ns.Body)Statement(s,inner);
                    break;
                }
            }
        }

        private Scope TypeParameters(IReadOnlyList<AmbientTypeParameter> parameters,Scope scope)
        {
            if(parameters.Count==0)return scope;
            var inner=new Scope(scope);
            foreach(var p in parameters)inner.Types.Add(p.Name.Text);
            foreach(var p in parameters)
            {
                if(p.Constraint is not null)Type(p.Constraint,inner);
                if(p.Default is not null)Type(p.Default,inner);
            }
            return inner;
        }

        private void Signature(AmbientSignature signature,Scope scope)
        {
            var inner=TypeParameters(signature.TypeParameters,scope);
            var values=new Scope(inner);
            foreach(var p in signature.Parameters)
            {
                if(p.PatternNames is { } names)foreach(var n in names)values.Values.Add(n.Text);
                else values.Values.Add(p.Name.Text);
            }
            foreach(var p in signature.Parameters)if(p.Type is not null)Type(p.Type,values);
            if(signature.ReturnType is not null)Type(signature.ReturnType,values);
        }

        private void Member(AmbientMember member,Scope scope)
        {
            switch(member)
            {
                case AmbientPropertySignature p:
                    PropertyName(p.Name,scope);
                    if(p.Type is not null)Type(p.Type,scope);
                    break;
                case AmbientMethodSignature m:
                    PropertyName(m.Name,scope);
                    Signature(m.Signature,scope);
                    break;
                case AmbientAccessorSignature a:
                    PropertyName(a.Name,scope);
                    Signature(a.Signature,scope);
                    break;
                case AmbientCallSignature c:
                    Signature(c.Signature,scope);
                    break;
                case AmbientConstructorDeclaration c:
                    Signature(c.Signature,scope);
                    break;
                case AmbientIndexSignature i:
                {
                    // An index signature is a signature declaration: its parameters are locals.
                    var values=new Scope(scope);
                    foreach(var p in i.Parameters)values.Values.Add(p.Name.Text);
                    foreach(var p in i.Parameters)if(p.Type is not null)Type(p.Type,values);
                    Type(i.Type,values);
                    break;
                }
            }
        }

        // `[Symbol.iterator]` is an expression: its name is a value.
        private void PropertyName(AmbientPropertyName name,Scope scope)
        {
            if(name.Computed)Name(name.Tokens,AmbientMeaning.Value,scope);
        }

        private void Type(AmbientType type,Scope scope)
        {
            switch(type)
            {
                case AmbientTypeReference r:
                    if(!(r.Name.Count==1 && Keywords.Contains(r.Name[0].Text)))Name(r.Name,AmbientMeaning.Type,scope);
                    foreach(var a in r.Arguments)Type(a,scope);
                    break;
                case AmbientTypeQuery q:
                    Name(q.Name,AmbientMeaning.Value,scope);
                    foreach(var a in q.Arguments)Type(a,scope);
                    break;
                case AmbientArrayType a:Type(a.Element,scope);break;
                case AmbientTupleType t:foreach(var e in t.Elements)Type(e.Type,scope);break;
                case AmbientUnionType u:foreach(var m in u.Members)Type(m,scope);break;
                case AmbientIntersectionType i:foreach(var m in i.Members)Type(m,scope);break;
                case AmbientFunctionType f:Signature(f.Signature,scope);break;
                case AmbientTypeOperator o:Type(o.Target,scope);break;
                case AmbientIndexedAccessType x:Type(x.Target,scope);Type(x.Index,scope);break;
                case AmbientParenthesizedType p:Type(p.Type,scope);break;
                case AmbientTypeLiteral l:foreach(var m in l.Members)Member(m,scope);break;
                case AmbientTemplateLiteralType t:foreach(var (span,_) in t.Spans)Type(span,scope);break;
                case AmbientTypePredicate p:if(p.Type is not null)Type(p.Type,scope);break;
                case AmbientInferType i:
                {
                    // The infer node itself declares its parameter (nameresolver.go KindInferType).
                    if(i.Constraint is null)break;
                    var inner=new Scope(scope);
                    inner.Types.Add(i.Name.Text);
                    Type(i.Constraint,inner);
                    break;
                }
                case AmbientConditionalType c:
                {
                    Type(c.Check,scope);
                    Type(c.Extends,scope);
                    var inner=new Scope(scope);
                    Infers(c.Extends,inner.Types);
                    Type(c.True,inner);
                    Type(c.False,scope);
                    break;
                }
                case AmbientMappedType m:
                {
                    var inner=new Scope(scope);
                    inner.Types.Add(m.Parameter.Text);
                    Type(m.Constraint,inner);
                    if(m.NameType is not null)Type(m.NameType,inner);
                    if(m.Type is not null)Type(m.Type,inner);
                    break;
                }
            }
        }

        // `infer` parameters declared in an extends type, excluding those of
        // nested conditional types' own extends types (binder.go getInferTypeContainer).
        private static void Infers(AmbientType type,HashSet<string> names)
        {
            switch(type)
            {
                case AmbientInferType i:names.Add(i.Name.Text);if(i.Constraint is not null)Infers(i.Constraint,names);break;
                case AmbientConditionalType c:Infers(c.Check,names);Infers(c.True,names);Infers(c.False,names);break;
                case AmbientTypeReference r:foreach(var a in r.Arguments)Infers(a,names);break;
                case AmbientTypeQuery q:foreach(var a in q.Arguments)Infers(a,names);break;
                case AmbientArrayType a:Infers(a.Element,names);break;
                case AmbientTupleType t:foreach(var e in t.Elements)Infers(e.Type,names);break;
                case AmbientUnionType u:foreach(var m in u.Members)Infers(m,names);break;
                case AmbientIntersectionType i:foreach(var m in i.Members)Infers(m,names);break;
                case AmbientFunctionType f:InfersInSignature(f.Signature,names);break;
                case AmbientTypeOperator o:Infers(o.Target,names);break;
                case AmbientIndexedAccessType x:Infers(x.Target,names);Infers(x.Index,names);break;
                case AmbientParenthesizedType p:Infers(p.Type,names);break;
                case AmbientTypeLiteral l:foreach(var m in l.Members)InfersInMember(m,names);break;
                case AmbientTemplateLiteralType t:foreach(var (span,_) in t.Spans)Infers(span,names);break;
                case AmbientTypePredicate p:if(p.Type is not null)Infers(p.Type,names);break;
                case AmbientMappedType m:
                    Infers(m.Constraint,names);
                    if(m.NameType is not null)Infers(m.NameType,names);
                    if(m.Type is not null)Infers(m.Type,names);
                    break;
            }
        }

        private static void InfersInSignature(AmbientSignature signature,HashSet<string> names)
        {
            foreach(var p in signature.Parameters)if(p.Type is not null)Infers(p.Type,names);
            if(signature.ReturnType is not null)Infers(signature.ReturnType,names);
        }

        private static void InfersInMember(AmbientMember member,HashSet<string> names)
        {
            switch(member)
            {
                case AmbientPropertySignature p:if(p.Type is not null)Infers(p.Type,names);break;
                case AmbientMethodSignature m:InfersInSignature(m.Signature,names);break;
                case AmbientAccessorSignature a:InfersInSignature(a.Signature,names);break;
                case AmbientCallSignature c:InfersInSignature(c.Signature,names);break;
                case AmbientIndexSignature i:Infers(i.Type,names);break;
            }
        }

        // An entity name: the left side is a namespace, each member an export of it.
        private void Name(IReadOnlyList<Token> name,AmbientMeaning meaning,Scope scope)
        {
            var first=name[0];
            if(name.Count==1)
            {
                if(Lookup(first.Text,meaning,scope,out _))return;
                Lookup(first.Text,~AmbientMeaning.None,scope,out var other);
                diagnostics.Add(meaning==AmbientMeaning.Type && other is not null && (other.Meanings&AmbientMeaning.Value)!=0
                    ?Error(first,"TS2749",$"'{first.Text}' refers to a value, but is being used as a type here. Did you mean 'typeof {first.Text}'?")
                    :meaning==AmbientMeaning.Value && other is not null && (other.Meanings&AmbientMeaning.Type)!=0
                    ?Error(first,"TS2693",$"'{first.Text}' only refers to a type, but is being used as a value here.")
                    :Error(first,"TS2304",$"Cannot find name '{first.Text}'."));
                return;
            }
            // `typeof x.y` on a variable is a property access the resolver does not model.
            if(meaning==AmbientMeaning.Value && Lookup(first.Text,AmbientMeaning.Value,scope,out var value)
                && (value is null || (value.Meanings&AmbientMeaning.ValueModule)==0))return;
            if(!Lookup(first.Text,Namespace,scope,out var symbol) || symbol is null)
            {
                diagnostics.Add(meaning==AmbientMeaning.Value
                    ?Error(first,"TS2304",$"Cannot find name '{first.Text}'.")
                    :Error(first,"TS2503",$"Cannot find namespace '{first.Text}'."));
                return;
            }
            for(int i=1;i<name.Count;i++)
            {
                var wanted=i==name.Count-1?meaning:Namespace;
                if(!symbol.Exports.TryGetValue(name[i].Text,out var member) || (member.Meanings&wanted)==0)
                {
                    // In a `typeof` query a class, function or variable merged with the
                    // namespace makes the rest a property access (`typeof C.staticMember`),
                    // which the resolver does not model.
                    if(meaning==AmbientMeaning.Value && (symbol.Meanings&AmbientMeaning.Value&~AmbientMeaning.ValueModule)!=0)return;
                    var left=string.Join(".",name.Take(i).Select(t=>t.Text));
                    diagnostics.Add(Error(name[i],"TS2694",$"Namespace '{left}' has no exported member '{name[i].Text}'."));
                    return;
                }
                symbol=member;
            }
        }

        // A type parameter or parameter found in a scope has no symbol here.
        private static bool Lookup(string name,AmbientMeaning meaning,Scope? scope,out AmbientSymbol? symbol)
        {
            for(;scope is not null;scope=scope.Parent)
            {
                if((meaning&AmbientMeaning.Type)!=0 && scope.Types.Contains(name)){symbol=null;return true;}
                // Parameters are plain values: a namespace lookup passes over them.
                if((meaning&AmbientMeaning.Value)!=0 && (meaning&Namespace)!=meaning && scope.Values.Contains(name)){symbol=null;return true;}
                if(scope.Table is not null && scope.Table.TryGetValue(name,out var found) && (found.Meanings&meaning)!=0)
                {
                    symbol=found;
                    return true;
                }
            }
            symbol=null;
            return false;
        }

        private Diagnostic Error(Token token,string code,string message)=>Diagnostic.Error(file,token,code,message);
    }
}

namespace Tscs;

// Declaration merging requires identity, not mere assignability or diagnostic
// printing order. The checker intentionally flattens intersections for value
// checking; retain their SOURCE shape here without changing normal type unions.
internal static class DeclarationTypeIdentity
{
    internal static ObjectTypeProperty? DirectProperty(TypeSyntax declaration,string name)
    {
        var body=declaration switch
        {
            ObjectTypeSyntax shape=>shape,
            InterfaceExtensionTypeSyntax inherited=>inherited.Body,
            _=>null
        };
        return body?.Properties.FirstOrDefault(property=>
            string.Equals(Parser.PropertyName(property.Name),name,StringComparison.Ordinal));
    }

    internal static bool Same(TypeSyntax left,Scope leftScope,string leftFile,
        TypeSyntax right,Scope rightScope,string rightFile)
    {
        // Preserve the existing finite semantic type gate, including 'any'
        // versus number and readonly/optional/numeric identity. Do not change
        // CompilerTypes.Union or callable-union normalization.
        if(!CompilerTypes.SameStructuralInput(
            TypeResolver.Resolve(left,leftScope,leftFile),
            TypeResolver.Resolve(right,rightScope,rightFile)))return false;
        if(!HasIntersection(left,leftScope) && !HasIntersection(right,rightScope))
            return true;
        return SameSyntax(left,leftScope,leftFile,right,rightScope,rightFile,0);
    }

    internal static bool HasIntersection(TypeSyntax syntax,Scope scope,int depth=0)
    {
        // Unknown cycles cannot prove declaration identity.
        if(depth>64)return true;
        if(syntax is TypeQuerySyntax query)
        {
            // Follow the declared annotations of the queried value. A value
            // inferred from type-free literal syntax cannot carry intersection
            // provenance, so its checked type is its declaration identity.
            // Any other inferred value (an 'as' target, a call, another
            // binding) has no source shape to inspect and cannot prove identity.
            var value=scope.Find(query.Root.Text);
            var owner=value?.DeclaringScope??scope;
            return value?.Declaration switch
            {
                Variable { Annotation: { } annotation }=>HasIntersection(annotation,owner,depth+1),
                // 'x as T' declares x with exactly the asserted type T.
                Variable { Annotation: null,Initializer: { } initializer } when
                    AssertedType(initializer) is { } asserted=>HasIntersection(asserted,owner,depth+1),
                Variable { Annotation: null,Initializer: { } initializer } variable when
                    query.Members.Count==0 && TypeFree(initializer,0,variable.Constant)=>false,
                FunctionDeclaration function=>
                    function.Parameters.Any(p=>HasIntersection(p.Annotation,owner,depth+1)) ||
                    HasIntersection(function.ReturnAnnotation,owner,depth+1),
                _=>true
            };
        }
        return syntax switch
        {
            IntersectionTypeSyntax=>true,
            NamedTypeSyntax named when scope.FindType(named.Name.Text) is { } binding=>
                HasIntersection(binding,named,scope,depth),
            ObjectTypeSyntax obj=>obj.Properties.Any(p=>HasIntersection(p.Type,scope,depth+1)),
            InterfaceExtensionTypeSyntax inherited=>
                HasIntersection(inherited.Body,scope,depth+1) ||
                inherited.Bases.Any(p=>HasIntersection(p,scope,depth+1)),
            ArrayTypeSyntax array=>HasIntersection(array.Element,scope,depth+1),
            TupleTypeSyntax tuple=>tuple.Elements.Any(p=>HasIntersection(p,scope,depth+1)),
            TupleRestTypeSyntax rest=>HasIntersection(rest.Target,scope,depth+1),
            FunctionTypeSyntax function=>
                function.Parameters.Any(p=>HasIntersection(p.Type,scope,depth+1)) ||
                HasIntersection(function.ReturnType,scope,depth+1),
            UnionTypeSyntax union=>union.Members.Any(p=>HasIntersection(p,scope,depth+1)),
            // A key set has no object-intersection provenance: keyof (A&B) and
            // the literal union of its keys are the same declaration type.
            KeyofTypeSyntax=>false,
            // A literal property projection carries only the selected
            // property's provenance; anything else stays conservative.
            IndexedAccessTypeSyntax indexed=>
                Project(indexed,scope,"",depth+1) is { } selected
                    ? HasIntersection(selected.Syntax,selected.Scope,depth+1)
                    : HasIntersection(indexed.Target,scope,depth+1) ||
                      HasIntersection(indexed.Index,scope,depth+1),
            _=>false
        };
    }

    // Every declaration of a merged generic interface is read in the same
    // instantiation scope, so intersections reached through a type parameter
    // used only by a later declaration still count.
    private static bool HasIntersection(TypeBinding binding,NamedTypeSyntax named,Scope scope,int depth)
    {
        var body=BodyScope(binding,named,scope,"");
        return HasIntersection(binding.Syntax,body,depth+1) ||
            binding.MergedInterfaces.Any(item=>HasIntersection(item.Syntax,
                binding.TypeParameters.Count==0?item.Owner:body,depth+1));
    }

    private static bool SameSyntax(TypeSyntax left,Scope leftScope,string leftFile,
        TypeSyntax right,Scope rightScope,string rightFile,int depth)
    {
        // Running out of the bounded depth proves neither identity nor a
        // difference: fail closed as unsupported, never as a TS2403/TS2717.
        if(depth>64)
            throw new CompilationFailure(Diagnostic.Error(rightFile,TypeResolver.Location(right),"TSCS1001",
                "Declaration type identity exceeds the bounded compiler subset."));
        // Compare what derived syntax denotes, not the derived node itself:
        // 'typeof x' is x's declared type and P["k"] is P's property type.
        if(Unwrap(left,leftScope,leftFile,depth) is { } l)
            return SameSyntax(l.Syntax,l.Scope,l.File,right,rightScope,rightFile,depth+1);
        if(Unwrap(right,rightScope,rightFile,depth) is { } r)
            return SameSyntax(left,leftScope,leftFile,r.Syntax,r.Scope,r.File,depth+1);
        if(left is NamedTypeSyntax ln && leftScope.FindType(ln.Name.Text) is { } lb)
        {
            // The same non-generic binding is the same type; two
            // instantiations of one generic alias are compared by what
            // their bodies denote with the given arguments.
            if(lb.TypeParameters.Count==0 && right is NamedTypeSyntax rn &&
               ReferenceEquals(lb,rightScope.FindType(rn.Name.Text)))return true;
            MergedGenericIdentity(lb,ln,leftFile);
            return SameSyntax(lb.Syntax,BodyScope(lb,ln,leftScope,leftFile),lb.File,right,rightScope,rightFile,depth+1);
        }
        if(right is NamedTypeSyntax rn2 && rightScope.FindType(rn2.Name.Text) is { } rb)
        {
            MergedGenericIdentity(rb,rn2,rightFile);
            return SameSyntax(left,leftScope,leftFile,rb.Syntax,BodyScope(rb,rn2,rightScope,rightFile),rb.File,depth+1);
        }

        if(!HasIntersection(left,leftScope,depth) &&
           !HasIntersection(right,rightScope,depth))
            return CompilerTypes.SameStructuralInput(
                TypeResolver.Resolve(left,leftScope,leftFile),
                TypeResolver.Resolve(right,rightScope,rightFile));

        // Intersections must be compared as intersections, including inside
        // object properties, arrays, callbacks, aliases and other unions.
        // In particular ({a:number}&{b:string}) is NOT the same declaration
        // type as {a:number;b:string}, though their checked object views agree.
        switch((left,right))
        {
            case (IntersectionTypeSyntax a,IntersectionTypeSyntax b):
                return Unordered(a.Members,leftScope,leftFile,
                    b.Members,rightScope,rightFile,depth+1);
            case (UnionTypeSyntax a,UnionTypeSyntax b):
                return Unordered(a.Members,leftScope,leftFile,
                    b.Members,rightScope,rightFile,depth+1);
            case (ObjectTypeSyntax a,ObjectTypeSyntax b):
                if(a.Properties.Count!=b.Properties.Count)return false;
                foreach(var p in a.Properties)
                {
                    var other=b.Properties.FirstOrDefault(q=>
                        Parser.PropertyName(q.Name)==Parser.PropertyName(p.Name) &&
                        q.Name.Kind==p.Name.Kind);
                    if(other is null || p.Readonly!=other.Readonly ||
                       p.Optional!=other.Optional ||
                       !SameSyntax(p.Type,leftScope,leftFile,
                           other.Type,rightScope,rightFile,depth+1))return false;
                }
                return true;
            case (ArrayTypeSyntax a,ArrayTypeSyntax b):
                return a.Readonly==b.Readonly &&
                    SameSyntax(a.Element,leftScope,leftFile,
                        b.Element,rightScope,rightFile,depth+1);
            case (TupleTypeSyntax a,TupleTypeSyntax b):
                return a.Readonly==b.Readonly && a.OptionalStart==b.OptionalStart &&
                    a.Elements.Count==b.Elements.Count &&
                    a.Elements.Zip(b.Elements).All(pair=>
                        SameSyntax(pair.First,leftScope,leftFile,
                            pair.Second,rightScope,rightFile,depth+1));
            case (TupleRestTypeSyntax a,TupleRestTypeSyntax b):
                return SameSyntax(a.Target,leftScope,leftFile,
                    b.Target,rightScope,rightFile,depth+1);
            case (FunctionTypeSyntax a,FunctionTypeSyntax b):
                return a.Parameters.Count==b.Parameters.Count &&
                    a.Parameters.Zip(b.Parameters).All(pair=>
                        pair.First.Optional==pair.Second.Optional &&
                        pair.First.Rest==pair.Second.Rest &&
                        SameSyntax(pair.First.Type,leftScope,leftFile,
                            pair.Second.Type,rightScope,rightFile,depth+1)) &&
                    SameSyntax(a.ReturnType,leftScope,leftFile,
                        b.ReturnType,rightScope,rightFile,depth+1);
            case (InterfaceExtensionTypeSyntax a,InterfaceExtensionTypeSyntax b):
                return SameSyntax(a.Body,leftScope,leftFile,
                           b.Body,rightScope,rightFile,depth+1) &&
                    Unordered(a.Bases,leftScope,leftFile,
                        b.Bases,rightScope,rightFile,depth+1);
            case (KeyofTypeSyntax a,KeyofTypeSyntax b):
                return SameSyntax(a.Target,leftScope,leftFile,
                    b.Target,rightScope,rightFile,depth+1);
            case (IndexedAccessTypeSyntax a,IndexedAccessTypeSyntax b):
                return SameSyntax(a.Target,leftScope,leftFile,
                           b.Target,rightScope,rightFile,depth+1) &&
                    SameSyntax(a.Index,leftScope,leftFile,
                        b.Index,rightScope,rightFile,depth+1);
            case (TypeQuerySyntax a,TypeQuerySyntax b):
                return ReferenceEquals(leftScope.Find(a.Root.Text),rightScope.Find(b.Root.Text)) &&
                    a.Members.Select(x=>x.Text).SequenceEqual(b.Members.Select(x=>x.Text));
            // An inherited member's provenance is spread over its bases; the
            // reference compares the merged members, which is not modelled
            // against another shape here.
            case (InterfaceExtensionTypeSyntax,_) or (_,InterfaceExtensionTypeSyntax):
                throw new CompilationFailure(Diagnostic.Error(rightFile,TypeResolver.Location(right),"TSCS1001",
                    "Declaration identity of an extended interface with intersections is not supported yet."));
            default:
                // No proof of source identity: fail closed, not a compatibility PASS.
                return false;
        }
    }

    // One declared member of a generic interface or of one of its bases.
    // 'Substitution' maps the base's type parameter names to the heritage
    // arguments, which are read in 'ArgumentScope' (the declaring generic
    // interface's scope, where its own type parameters stay opaque).
    internal readonly record struct MemberSide(ObjectTypeProperty Property,Scope Scope,
        IReadOnlyDictionary<string,TypeSyntax>? Substitution=null,Scope? ArgumentScope=null);

    private readonly record struct Context(Scope Scope,
        IReadOnlyDictionary<string,TypeSyntax>? Substitution,Scope? ArgumentScope);

    // The member 'key' as directly declared by the single-declaration
    // interface that 'named' references (read in 'scope'), with its type
    // parameters bound to the heritage arguments; null when the member is
    // inherited or merged, or the base is not a plain interface.
    internal static MemberSide? BaseMember(NamedTypeSyntax named,Scope scope,string key)
    {
        if(scope.FindType(named.Name.Text) is not { IsInterface: true } binding ||
           binding.MergedInterfaces.Count!=0)return null;
        var arguments=named.TypeArguments??[];
        if(arguments.Count!=binding.TypeParameters.Count)return null;
        if(DirectProperty(binding.Syntax,key) is not { } property)return null;
        var substitution=new Dictionary<string,TypeSyntax>(StringComparer.Ordinal);
        for(int i=0;i<arguments.Count;i++)
            if(!substitution.TryAdd(binding.TypeParameters[i].Text,arguments[i]))return null;
        return new MemberSide(property,binding.Owner,substitution,scope);
    }

    // Whether two member declarations are the same type by syntax alone:
    // identical modifiers, and type syntax that matches node for node, with
    // named references denoting the same binding. 'x:T' matches a base
    // 'x:U' only through 'extends B<T>'. No match proves nothing: callers
    // fail closed, never report.
    internal static bool SameMember(MemberSide left,MemberSide right)=>
        left.Property.Readonly==right.Property.Readonly &&
        left.Property.Optional==right.Property.Optional &&
        left.Property.Name.Kind==right.Property.Name.Kind &&
        SameDeclared(left.Property.Type,new(left.Scope,left.Substitution,left.ArgumentScope),
            right.Property.Type,new(right.Scope,right.Substitution,right.ArgumentScope),0);

    private static bool SameDeclared(TypeSyntax left,Context l,TypeSyntax right,Context r,int depth)
    {
        if(depth>64)return false;
        // A substituted argument is read in the declaring interface's scope,
        // where no further substitution applies.
        if(Substituted(left,l) is { } ls)
            return SameDeclared(ls,new(l.ArgumentScope!,null,null),right,r,depth+1);
        if(Substituted(right,r) is { } rs)
            return SameDeclared(left,l,rs,new(r.ArgumentScope!,null,null),depth+1);
        bool Each(IReadOnlyList<TypeSyntax> a,IReadOnlyList<TypeSyntax> b)=>
            a.Count==b.Count && a.Zip(b).All(pair=>SameDeclared(pair.First,l,pair.Second,r,depth+1));
        // Unions are unordered. Match every source arm to a distinct target
        // arm instead of assuming syntax order; preserve opaque type-parameter
        // binding identity and never infer equality from placeholder 'unknown'.
        bool UnorderedMembers(IReadOnlyList<TypeSyntax> a,IReadOnlyList<TypeSyntax> b)
        {
            if(a.Count!=b.Count)return false;
            var used=new bool[b.Count];
            foreach(var item in a)
            {
                var matched=false;
                for(var i=0;i<b.Count;i++)
                {
                    if(used[i] || !SameDeclared(item,l,b[i],r,depth+1))continue;
                    used[i]=true;
                    matched=true;
                    break;
                }
                if(!matched)return false;
            }
            return true;
        }
        // Keep the already-registered, conservative behavior for unions
        // with no opaque generic parameter. This A2.3 step proves only
        // reordered declarations involving a generic parameter; it does not
        // silently upgrade unrelated fail-closed cases in the checker.
        bool ContainsOpaqueParameter(TypeSyntax syntax,Context context,int level)
        {
            if(level>64)return false;
            if(syntax is NamedTypeSyntax named)
            {
                if(named.TypeArguments is null &&
                   context.Substitution?.ContainsKey(named.Name.Text)==true)
                    return true;
                if(context.Scope.FindType(named.Name.Text) is { } binding &&
                   ReferenceEquals(binding.Resolved,CompilerTypes.Unknown) &&
                   binding.Syntax is PrimitiveTypeSyntax { Name.Text:"unknown" })
                    return true;
                return (named.TypeArguments??[]).Any(arg=>
                    ContainsOpaqueParameter(arg,context,level+1));
            }
            return syntax switch
            {
                ArrayTypeSyntax a=>ContainsOpaqueParameter(a.Element,context,level+1),
                TupleTypeSyntax t=>t.Elements.Any(e=>ContainsOpaqueParameter(e,context,level+1)),
                TupleRestTypeSyntax t=>ContainsOpaqueParameter(t.Target,context,level+1),
                UnionTypeSyntax u=>u.Members.Any(e=>ContainsOpaqueParameter(e,context,level+1)),
                IntersectionTypeSyntax i=>i.Members.Any(e=>ContainsOpaqueParameter(e,context,level+1)),
                ObjectTypeSyntax o=>o.Properties.Any(p=>ContainsOpaqueParameter(p.Type,context,level+1)),
                FunctionTypeSyntax f=>
                    f.Parameters.Any(p=>ContainsOpaqueParameter(p.Type,context,level+1)) ||
                    ContainsOpaqueParameter(f.ReturnType,context,level+1),
                _=>false
            };
        }
        switch((left,right))
        {
            case (PrimitiveTypeSyntax a,PrimitiveTypeSyntax b):
                return a.Name.Text==b.Name.Text;
            case (LiteralTypeSyntax a,LiteralTypeSyntax b):
                return a.Negative==b.Negative && a.Literal.Kind==b.Literal.Kind &&
                    (a.Literal.Value??a.Literal.Text)==(b.Literal.Value??b.Literal.Text);
            case (NamedTypeSyntax a,NamedTypeSyntax b):
                return l.Scope.FindType(a.Name.Text) is { } binding &&
                    ReferenceEquals(binding,r.Scope.FindType(b.Name.Text)) &&
                    Each(a.TypeArguments??[],b.TypeArguments??[]);
            case (ArrayTypeSyntax a,ArrayTypeSyntax b):
                return a.Readonly==b.Readonly && SameDeclared(a.Element,l,b.Element,r,depth+1);
            case (TupleTypeSyntax a,TupleTypeSyntax b):
                return a.Readonly==b.Readonly && a.OptionalStart==b.OptionalStart && Each(a.Elements,b.Elements);
            case (TupleRestTypeSyntax a,TupleRestTypeSyntax b):
                return SameDeclared(a.Target,l,b.Target,r,depth+1);
            case (UnionTypeSyntax a,UnionTypeSyntax b):
                return ContainsOpaqueParameter(a,l,depth+1) ||
                       ContainsOpaqueParameter(b,r,depth+1)
                    ?UnorderedMembers(a.Members,b.Members)
                    :Each(a.Members,b.Members);
            case (IntersectionTypeSyntax a,IntersectionTypeSyntax b):
                // Intersection provenance has separate declaration-identity
                // rules; do not broaden it with union normalization.
                return Each(a.Members,b.Members);
            case (ObjectTypeSyntax a,ObjectTypeSyntax b):
            {
                if(a.Properties.Count!=b.Properties.Count)return false;
                var remaining=b.Properties.ToList();
                foreach(var property in a.Properties)
                {
                    var index=remaining.FindIndex(candidate=>
                        Parser.PropertyName(property.Name)==Parser.PropertyName(candidate.Name) &&
                        property.Name.Kind==candidate.Name.Kind &&
                        property.Readonly==candidate.Readonly && property.Optional==candidate.Optional &&
                        SameDeclared(property.Type,l,candidate.Type,r,depth+1));
                    if(index<0)return false;
                    remaining.RemoveAt(index);
                }
                return true;
            }
            case (FunctionTypeSyntax a,FunctionTypeSyntax b):
                return a.Parameters.Count==b.Parameters.Count && a.Parameters.Zip(b.Parameters).All(pair=>
                        pair.First.Optional==pair.Second.Optional && pair.First.Rest==pair.Second.Rest &&
                        SameDeclared(pair.First.Type,l,pair.Second.Type,r,depth+1)) &&
                    SameDeclared(a.ReturnType,l,b.ReturnType,r,depth+1);
            default:
                // keyof, indexed access, typeof queries and anything else:
                // no syntactic proof of identity here.
                return false;
        }
    }

    private static TypeSyntax? Substituted(TypeSyntax syntax,Context context)=>
        context.Substitution is { } map && syntax is NamedTypeSyntax { TypeArguments: null } named &&
        map.TryGetValue(named.Name.Text,out var argument) ? argument : null;

    // Comparing only the first declaration's body would ignore the
    // intersection provenance of later declarations: fail closed.
    private static void MergedGenericIdentity(TypeBinding binding,NamedTypeSyntax reference,string file)
    {
        if(binding.TypeParameters.Count!=0 && binding.MergedInterfaces.Count!=0)
            throw new CompilationFailure(Diagnostic.Error(file,reference.Name,"TSCS1001",
                "Declaration identity of merged generic interface instantiations with intersections is not supported yet."));
    }

    // A generic alias body denotes a type only in an instantiation scope.
    // Arity errors were already reported when the types were resolved.
    private static Scope BodyScope(TypeBinding binding,NamedTypeSyntax reference,Scope scope,string file)=>
        binding.TypeParameters.Count==0
            ? binding.Owner
            : TypeResolver.Instantiate(binding,reference,scope,file);

    private readonly record struct Denoted(TypeSyntax Syntax,Scope Scope,string File);

    private static Denoted? Unwrap(TypeSyntax syntax,Scope scope,string file,int depth)=>syntax switch
    {
        TypeQuerySyntax { Members.Count:0 } query when
            scope.Find(query.Root.Text) is { Declaration: Variable { Annotation: { } annotation } } value=>
            new Denoted(annotation,value.DeclaringScope??scope,value.File),
        TypeQuerySyntax { Members.Count:0 } query when
            scope.Find(query.Root.Text) is { Declaration: Variable { Annotation: null,Initializer: { } initializer } } value &&
            AssertedType(initializer) is { } asserted=>
            new Denoted(asserted,value.DeclaringScope??scope,value.File),
        // A function declaration's type is the signature its required
        // annotations spell, so two functions with the same annotated
        // signature denote the same declaration type.
        TypeQuerySyntax { Members.Count:0 } query when
            scope.Find(query.Root.Text) is { Declaration: FunctionDeclaration function } value=>
            new Denoted(new FunctionTypeSyntax(function.Name,
                    function.Parameters.Select(p=>new FunctionTypeParameter(
                        p.Name,p.Annotation,!p.Rest && (p.Optional || p.DefaultValue is not null),p.Rest)).ToList(),
                    function.ReturnAnnotation),
                value.DeclaringScope??scope,value.File),
        IndexedAccessTypeSyntax indexed=>Project(indexed,scope,file,depth),
        _=>null
    };

    // Whether the member 'key' of a heritage base can carry intersection
    // provenance. Only the declarations of that member are inspected:
    // optional ones and every reopened declaration count; an own
    // declaration shadows every inherited one; otherwise every inherited
    // path counts. An unrelated member never does. Shapes this walk does not
    // model (aliases of intersections, unions, queries) stay conservative.
    internal static bool MemberHasIntersection(TypeSyntax syntax,Scope scope,string file,string key,int depth=0)
    {
        if(depth>64)return true;
        switch(syntax)
        {
            case NamedTypeSyntax named when scope.FindType(named.Name.Text) is { } binding:
            {
                var body=BodyScope(binding,named,scope,file);
                var declarations=binding.MergedInterfaces
                    .Select(item=>(item.Syntax,Scope:binding.TypeParameters.Count==0?item.Owner:body,item.File))
                    .Prepend((binding.Syntax,Scope:body,binding.File)).ToList();
                // Own members of every merged declaration shadow all bases.
                var own=declarations.Where(d=>OwnBody(d.Syntax) is { } shape && Declares(shape,key)).ToList();
                if(own.Count!=0)
                    return own.Any(d=>MemberHasIntersection(OwnBody(d.Syntax)!,d.Scope,d.File,key,depth+1));
                return declarations.Any(d=>MemberHasIntersection(d.Syntax,d.Scope,d.File,key,depth+1));
            }
            case ObjectTypeSyntax shape:
                return shape.Properties.Any(p=>
                    string.Equals(Parser.PropertyName(p.Name),key,StringComparison.Ordinal) &&
                    HasIntersection(p.Type,scope));
            case InterfaceExtensionTypeSyntax inherited:
                return Declares(inherited.Body,key)
                    ? MemberHasIntersection(inherited.Body,scope,file,key,depth+1)
                    : inherited.Bases.Any(b=>MemberHasIntersection(b,scope,file,key,depth+1));
            default:
                return HasIntersection(syntax,scope);
        }
    }

    private static ObjectTypeSyntax? OwnBody(TypeSyntax syntax)=>syntax switch
    {
        ObjectTypeSyntax shape=>shape,
        InterfaceExtensionTypeSyntax inherited=>inherited.Body,
        _=>null
    };

    private static bool Declares(ObjectTypeSyntax shape,string key)=>
        shape.Properties.Any(p=>string.Equals(Parser.PropertyName(p.Name),key,StringComparison.Ordinal));

    private static Denoted? Project(IndexedAccessTypeSyntax indexed,Scope scope,string file,int depth)
    {
        if(indexed.Index is not LiteralTypeSyntax { Negative:false } literal ||
           CompilerTypes.FromLiteralExact(literal.Literal,false) is not
               LiteralTypeSymbol { Kind:PrimitiveKind.String } key)return null;
        var found=Select(indexed.Target,scope,file,key.Value,depth+1);
        return found is { Known:true,Value: { } value } ? value : null;
    }

    private readonly record struct Selection(bool Known,Denoted? Value)
    {
        public static readonly Selection Unknown=new(false,null);
        public static readonly Selection Absent=new(true,null);
    }

    private static Selection Select(TypeSyntax syntax,Scope scope,string file,string key,int depth)
    {
        if(depth>64)return Selection.Unknown;
        switch(syntax)
        {
            case NamedTypeSyntax named when scope.FindType(named.Name.Text) is { } binding:
                // A projection of a merged generic instantiation cannot be
                // proved identical or different yet: fail closed, never TS2403.
                MergedGenericIdentity(binding,named,file);
                return binding.MergedInterfaces.Count!=0
                    ? Selection.Unknown
                    : Select(binding.Syntax,BodyScope(binding,named,scope,file),binding.File,key,depth+1);
            case TypeQuerySyntax when Unwrap(syntax,scope,file,depth) is { } denoted:
                return Select(denoted.Syntax,denoted.Scope,denoted.File,key,depth+1);
            case ObjectTypeSyntax shape:
            {
                var matches=shape.Properties.Where(p=>
                    string.Equals(Parser.PropertyName(p.Name),key,StringComparison.Ordinal)).ToList();
                if(matches.Count==0)return Selection.Absent;
                return matches.Count==1 && !matches[0].Optional
                    ? new Selection(true,new Denoted(matches[0].Type,scope,file))
                    : Selection.Unknown;
            }
            case InterfaceExtensionTypeSyntax inherited:
            {
                var own=Select(inherited.Body,scope,file,key,depth+1);
                if(!own.Known || own.Value is not null)return own;
                return Single(inherited.Bases.Select(b=>Select(b,scope,file,key,depth+1)));
            }
            case IntersectionTypeSyntax both:
                return Single(both.Members.Select(m=>Select(m,scope,file,key,depth+1)));
            default:
                return Selection.Unknown;
        }
    }

    private static Selection Single(IEnumerable<Selection> parts)
    {
        Denoted? value=null;
        foreach(var part in parts)
        {
            if(!part.Known)return Selection.Unknown;
            if(part.Value is null)continue;
            if(value is not null)return Selection.Unknown;
            value=part.Value;
        }
        return new Selection(true,value);
    }

    // Literal-only initializer syntax: its inferred type is built from
    // literals and operators alone, never from a type annotation, assertion,
    // call result or other binding that could carry an intersection.
    // A bare top-level 'null' is literal-only only for a const: an unannotated
    // 'let x=null' is an implicit any in the reference, while 'const x=null'
    // is null and a nested null ({x:null}, [null]) keeps the null type.
    private static bool TypeFree(ValueNode value,int depth,bool nullOk)
    {
        if(depth>64)return false;
        return value switch
        {
            ValueAtom { Shorthand:false } atom=>atom.Token.Kind is
                TokenKind.Number or TokenKind.String or TokenKind.Template or
                TokenKind.True or TokenKind.False || (nullOk && atom.Token.Kind==TokenKind.Null),
            ValueTemplate template=>template.Expressions.All(e=>TypeFree(e,depth+1,false)),
            ValueUnary unary=>TypeFree(unary.Operand,depth+1,false),
            ValueBinary binary=>TypeFree(binary.Left,depth+1,false) && TypeFree(binary.Right,depth+1,false),
            ValueParenthesized inner=>TypeFree(inner.Expression,depth+1,nullOk),
            ValueConditional conditional=>TypeFree(conditional.Condition,depth+1,false) &&
                TypeFree(conditional.WhenTrue,depth+1,false) && TypeFree(conditional.WhenFalse,depth+1,false),
            ValueObject shape=>shape.Properties.All(p=>
                !p.Spread && !p.Method && !p.Computed && TypeFree(p.Value,depth+1,true)),
            ValueArray array=>array.Elements.All(e=>TypeFree(e,depth+1,true)),
            _=>false
        };
    }

    // The target of a plain 'as T' assertion ('as const' and 'satisfies' excluded).
    private static TypeSyntax? AssertedType(ValueNode value)=>value switch
    {
        ValueParenthesized inner=>AssertedType(inner.Expression),
        ValueTypeOperator { Keyword.Text:"as",Target: var target } when
            target is not NamedTypeSyntax { Name.Kind: TokenKind.Const }=>target,
        _=>null
    };

    private static bool Unordered<T>(IReadOnlyList<T> left,Scope leftScope,string leftFile,
        IReadOnlyList<T> right,Scope rightScope,string rightFile,int depth)
        where T:TypeSyntax
    {
        if(left.Count!=right.Count)return false;
        var used=new bool[right.Count];
        foreach(var item in left)
        {
            var found=false;
            for(var i=0;i<right.Count;i++)
            {
                if(used[i] || !SameSyntax(item,leftScope,leftFile,
                       right[i],rightScope,rightFile,depth+1))continue;
                used[i]=true;
                found=true;
                break;
            }
            if(!found)return false;
        }
        return true;
    }
}

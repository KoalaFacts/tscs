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
            // whose type is inferred (an initializer, an 'as' target) has no
            // source shape to inspect here, so it cannot prove identity.
            var value=scope.Find(query.Root.Text);
            var owner=value?.DeclaringScope??scope;
            return value?.Declaration switch
            {
                Variable { Annotation: { } annotation }=>HasIntersection(annotation,owner,depth+1),
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
                HasIntersection(binding.Syntax,binding.Owner,depth+1) ||
                binding.MergedInterfaces.Any(item=>HasIntersection(item.Syntax,item.Owner,depth+1)),
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

    private static bool SameSyntax(TypeSyntax left,Scope leftScope,string leftFile,
        TypeSyntax right,Scope rightScope,string rightFile,int depth)
    {
        if(depth>64)return false;
        // Compare what derived syntax denotes, not the derived node itself:
        // 'typeof x' is x's declared type and P["k"] is P's property type.
        if(Unwrap(left,leftScope,leftFile,depth) is { } l)
            return SameSyntax(l.Syntax,l.Scope,l.File,right,rightScope,rightFile,depth+1);
        if(Unwrap(right,rightScope,rightFile,depth) is { } r)
            return SameSyntax(left,leftScope,leftFile,r.Syntax,r.Scope,r.File,depth+1);
        if(left is NamedTypeSyntax ln && leftScope.FindType(ln.Name.Text) is { } lb)
        {
            if(right is NamedTypeSyntax rn &&
               ReferenceEquals(lb,rightScope.FindType(rn.Name.Text)))return true;
            return SameSyntax(lb.Syntax,lb.Owner,lb.File,right,rightScope,rightFile,depth+1);
        }
        if(right is NamedTypeSyntax rn2 && rightScope.FindType(rn2.Name.Text) is { } rb)
            return SameSyntax(left,leftScope,leftFile,rb.Syntax,rb.Owner,rb.File,depth+1);

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
            default:
                // No proof of source identity: fail closed, not a compatibility PASS.
                return false;
        }
    }

    private readonly record struct Denoted(TypeSyntax Syntax,Scope Scope,string File);

    private static Denoted? Unwrap(TypeSyntax syntax,Scope scope,string file,int depth)=>syntax switch
    {
        TypeQuerySyntax { Members.Count:0 } query when
            scope.Find(query.Root.Text) is { Declaration: Variable { Annotation: { } annotation } } value=>
            new Denoted(annotation,value.DeclaringScope??scope,value.File),
        IndexedAccessTypeSyntax indexed=>Project(indexed,scope,file,depth),
        _=>null
    };

    // Selects the declared syntax of one required property named by a string
    // literal key. Optional members, index signatures, merged interfaces,
    // unknown forms and properties declared by several intersection members
    // return null, so the caller keeps its conservative comparison.
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
                return binding.MergedInterfaces.Count!=0
                    ? Selection.Unknown
                    : Select(binding.Syntax,binding.Owner,binding.File,key,depth+1);
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

    private static bool Unordered<T>(IReadOnlyList<T> left,Scope leftScope,string leftFile,
        IReadOnlyList<T> right,Scope rightScope,string rightFile,int depth)
        where T:TypeSyntax
    {
        if(left.Count!=right.Count || depth>64)return false;
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

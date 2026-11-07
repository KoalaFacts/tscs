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
        if(syntax is TypeQuerySyntax query &&
           scope.Find(query.Root.Text) is { } value &&
           value.Declaration is Variable { Annotation: { } annotation })
            return HasIntersection(annotation,value.DeclaringScope??scope,depth+1);
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
            KeyofTypeSyntax keyed=>HasIntersection(keyed.Target,scope,depth+1),
            IndexedAccessTypeSyntax indexed=>
                HasIntersection(indexed.Target,scope,depth+1) ||
                HasIntersection(indexed.Index,scope,depth+1),
            _=>false
        };
    }

    private static bool SameSyntax(TypeSyntax left,Scope leftScope,string leftFile,
        TypeSyntax right,Scope rightScope,string rightFile,int depth)
    {
        if(depth>64)return false;
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

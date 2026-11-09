using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3 prerequisite: single-declaration generic interfaces with plain type
// parameters, instantiated by substitution like generic type aliases.
// Reopening with an identical parameter list merges per instantiation;
// heritage instantiates its bases. Different lists, members shared with a
// base and unproved declarations fail closed; bounded constraints/defaults work.
public sealed class GenericInterfaceTests
{
    [Theory]
    [InlineData("interface Box<T>{value:T}let b:Box<number>={value:1};let n:number=b.value;")]
    [InlineData("interface Box<T extends string>{value:T}let b:Box<\"hi\">={value:\"hi\"};")]
    [InlineData("interface Pair<A,B>{first:A;second:B}let p:Pair<number,string>={first:1,second:\"x\"};let s:string=p.second;")]
    [InlineData("interface Getter<A,B>{get(a:A):B}let g:Getter<number,string>={get:(a:number):string=>\"x\"};let s:string=g.get(1);")]
    [InlineData("interface Box<T>{readonly value:T;extra?:T}let b:Box<string>={value:\"a\"};let s:string=b.value;")]
    [InlineData("interface Box<T>{value:T}type Wrap<T>=Box<Box<T>>;let w:Wrap<number>={value:{value:40}};let n:number=w.value.value;")]
    [InlineData("interface Box<T>{value:T}interface Holder{box:Box<boolean>}let h:Holder={box:{value:true}};")]
    [InlineData("interface List<T>{items:T[]}let l:List<number>={items:[1,2]};let n:number=l.items[0];")]
    // The type parameter shadows an outer type of the same name.
    [InlineData("type T=string;interface Box<T>{value:T}let b:Box<number>={value:1};let s:T=\"x\";")]
    // A generic interface may be declared after its use.
    [InlineData("let b:Box<boolean>={value:true};interface Box<T>{value:T}")]
    public void GenericInterfacesInstantiateBySubstitution(string source)=>
        Assert.Empty(Compilation.Check("generic-interface.ts",source));

    [Theory]
    [InlineData("interface Box<T>{value:T}let b:Box<number>={value:\"x\"};","TS2322")]
    [InlineData("interface Box<T>{value:T}let b:Box<string>={value:1};","TS2322")]
    [InlineData("interface Getter<A,B>{get(a:A):B}let g:Getter<number,string>={get:(a:number):string=>\"x\"};let n:number=g.get(1);","TS2322")]
    [InlineData("interface Box<T>{value:T}let b:Box={value:1};","TS2314")]
    [InlineData("interface Box<T>{value:T}let b:Box<number,string>={value:1};","TS2314")]
    [InlineData("interface N{value:number}let n:N<string>={value:1};","TS2315")]
    [InlineData("interface Box<T>{value:Missing}","TS2304")]
    [InlineData("interface Box<T>{value:T}let b:Box<Missing>={value:1};","TS2304")]
    [InlineData("type Box<T>={value:T};interface Box<T>{value:T}","TS2300")]
    [InlineData("interface Box<number>{value:number}","TS2368")]
    [InlineData("interface Box<T,string>{value:T}","TS2368")]
    [InlineData("interface number<T>{value:T}","TS2427")]
    [InlineData("interface string<T>{value:T}let r=7;","TS2427")]
    [InlineData("interface symbol{value:number}","TS2427")]
    public void GenericInterfaceErrorsAreReported(string source,string code)=>
        Assert.Contains(Compilation.Check("generic-interface-bad.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("interface Box<out T>{value:T}")]
    [InlineData("interface Box<T,T>{value:T}")]
    [InlineData("interface Box<>{value:number}")]
    // Reopening with a different parameter list is TS2428 (not modelled yet).
    [InlineData("interface Box<T>{value:T}interface Box<U>{other:U}")]
    // Exported and local declarations of one name are TS2395 (not modelled yet).
    [InlineData("export interface B<T>{a:T} interface B<T>{b:T} let x:B<string>={a:\"\",b:\"\"};")]
    [InlineData("interface B<T>{a:T} export interface B<T>{b:T} let x:B<string>={a:\"\",b:\"\"};")]
    [InlineData("export interface B{a:number} interface B{b:number}")]
    // Redeclared members need opaque type parameters to compare (TS2717/TS2687).
    [InlineData("interface Box<T>{value:T}interface Box<T>{value:string}let b:Box<string>={value:\"x\"};")]
    [InlineData("interface Box<T>{value:T}interface Box<T>{readonly value:T}")]
    [InlineData("interface Box<T>{value:T}interface Box<T>{value:T}")]
    [InlineData("interface Box<T>{value:T}interface Box<T,U>{other:U}")]
    [InlineData("interface Box<T>{value:T}interface Box{other:number}")]
    [InlineData("interface Box{other:number}interface Box<T>{value:T}")]
    [InlineData("interface List<T>{value:T;next:List<T>|undefined}")]
    [InlineData("interface Keys<T>{k:keyof T}")]
    // 'Array<T>' always denotes the built-in array; a declaration would be ignored.
    [InlineData("interface Array<T>{length:string}")]
    [InlineData("interface ReadonlyArray<T>{length:number}")]
    // Bootstrap globals come from the validated library contract (TS2428/TS2317).
    [InlineData("interface Number<T>{v:T}let o:Number<string>={v:\"x\"};")]
    [InlineData("interface Object<T>{v:T}")]
    [InlineData("interface String<T>{v:T}")]
    public void UnsupportedGenericInterfaceFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("generic-interface-unsupported.ts",source),d=>d.Code=="TSCS1001");

    // Reopened generic interfaces with identical parameter lists merge per
    // instantiation, in either file order.
    [Theory]
    [InlineData("interface Box<T>{value:T}interface Box<T>{other:T}let b:Box<number>={value:1,other:2};let n:number=b.other;")]
    [InlineData("interface Pair<A,B>{first:A}interface Pair<A,B>{second:B}let p:Pair<number,string>={first:1,second:\"x\"};let s:string=p.second;")]
    [InlineData("interface Box<T>{value:number}interface Box<T>{other:T}let b:Box<string>={value:1,other:\"x\"};")]
    [InlineData("let b:Box<boolean>={value:true,other:false};interface Box<T>{value:T}interface Box<T>{other:T}")]
    public void ReopenedGenericInterfacesMerge(string source)=>
        Assert.Empty(Compilation.Check("generic-interface-merge.ts",source));

    [Theory]
    [InlineData("interface Box<T>{value:T}interface Box<T>{other:T}let b:Box<number>={value:1};","TS2741")]
    [InlineData("interface Box<T>{value:T}interface Box<T>{other:T}let b:Box<number>={value:1,other:\"x\"};","TS2322")]
    [InlineData("interface Box<T>{value:T}interface Box<T>{other:Missing}","TS2304")]
    [InlineData("interface Box<T>{value:T}interface Box<T>{other:T}let b:Box={value:1,other:2};","TS2314")]
    public void ReopenedGenericInterfaceErrorsAreReported(string source,string code)=>
        Assert.Contains(Compilation.Check("generic-interface-merge-bad.ts",source),d=>d.Code==code);

    [Fact]
    public void ReopenedGenericInterfaceMergesAcrossFiles()
    {
        foreach(var (first,second) in new[]{("a.d.ts","b.d.ts"),("b.d.ts","a.d.ts")})
        {
            var files=new Dictionary<string,string>
            {
                ["a.d.ts"]="interface Box<T>{value:T}",
                ["b.d.ts"]="interface Box<T>{other:T}"
            };
            Assert.Empty(Compilation.CheckFiles([
                new SourceInput(first,files[first]),
                new SourceInput(second,files[second]),
                new SourceInput("entry.ts","let b:Box<number>={value:1,other:2};let n:number=b.other;")
            ]));
        }
    }

    // Merged declarations still compare instantiations for repeated ambient var.
    [Theory]
    [InlineData("Box2<number>","{value:number;other:number}",false)]
    [InlineData("Box2<number>","Box2<number>",false)]
    [InlineData("Box2<number>","Box2<string>",true)]
    [InlineData("Box2<number>","{value:number}",true)]
    public void RepeatedAmbientVarComparesMergedInstantiations(string left,string right,bool differs)
    {
        foreach(var (first,second) in new[]{(left,right),(right,left)})
        {
            var diagnostics=Compilation.CheckFiles([
                new SourceInput("types.d.ts","interface Box2<T>{value:T}interface Box2<T>{other:T}"),
                new SourceInput("a.d.ts",$"declare var item:{first};"),
                new SourceInput("b.d.ts",$"declare var item:{second};"),
                new SourceInput("entry.ts","let result=7;")
            ]);
            if(differs)Assert.Contains(diagnostics,d=>d.Code=="TS2403");
            else Assert.Empty(diagnostics);
        }
    }

    // A projection of a merged generic instantiation fails closed (review finding):
    // never an invented TS2403 for equal types, in either declaration order.
    [Theory]
    [InlineData("Box2<A&B>[\"value\"]","number")]
    [InlineData("number","Box2<A&B>[\"value\"]")]
    public void MergedGenericProjectionIdentityFailsClosed(string first,string second)
    {
        var diagnostics=Compilation.CheckFiles([
            new SourceInput("types.d.ts","type A={a:number};type B={b:string};interface Box2<T>{value:number}interface Box2<T>{other:T}"),
            new SourceInput("a.d.ts",$"declare var item:{first};"),
            new SourceInput("b.d.ts",$"declare var item:{second};"),
            new SourceInput("entry.ts","let result=7;")
        ]);
        Assert.Contains(diagnostics,d=>d.Code=="TSCS1001");
        Assert.DoesNotContain(diagnostics,d=>d.Code=="TS2403");
    }

    // Intersection provenance in a later declaration is not compared yet.
    [Fact]
    public void MergedGenericIntersectionIdentityFailsClosed()
    {
        var diagnostics=Compilation.CheckFiles([
            new SourceInput("types.d.ts","type A={a:number};type B={b:string};interface Box2<T>{value:number}interface Box2<T>{other:T}"),
            new SourceInput("a.d.ts","declare var item:Box2<A&B>;"),
            new SourceInput("b.d.ts","declare var item:Box2<{a:number;b:string}>;"),
            new SourceInput("entry.ts","let result=7;")
        ]);
        Assert.Contains(diagnostics,d=>d.Code=="TSCS1001");
    }

    private const string Prelude="type A={a:number};type B={b:string};type P=A&B;interface Box<T>{value:T}";

    // Repeated ambient var declarations compare instantiations by identity.
    [Theory]
    [InlineData("Box<number>","{value:number}")]
    [InlineData("Box<number>","Box<number>")]
    [InlineData("Box<P>","Box<P>")]
    [InlineData("Box<P>","{value:A&B}")]
    public void RepeatedAmbientVarAcceptsIdenticalInstantiations(string left,string right)
    {
        foreach(var (first,second) in new[]{(left,right),(right,left)})
            Assert.Empty(Compilation.CheckFiles([
                new SourceInput("types.d.ts",Prelude),
                new SourceInput("a.d.ts",$"declare var item:{first};"),
                new SourceInput("b.d.ts",$"declare var item:{second};"),
                new SourceInput("entry.ts","let result=7;")
            ]));
    }

    [Theory]
    [InlineData("Box<number>","Box<string>")]
    [InlineData("Box<number>","{value:string}")]
    [InlineData("Box<P>","Box<{a:number;b:string}>")]
    [InlineData("Box<P>","{value:{a:number;b:string}}")]
    public void RepeatedAmbientVarRejectsDifferentInstantiations(string left,string right)
    {
        foreach(var (first,second) in new[]{(left,right),(right,left)})
            Assert.Contains(Compilation.CheckFiles([
                new SourceInput("types.d.ts",Prelude),
                new SourceInput("a.d.ts",$"declare var item:{first};"),
                new SourceInput("b.d.ts",$"declare var item:{second};"),
                new SourceInput("entry.ts","let result=7;")
            ]),d=>d.Code=="TS2403");
    }

    // Heritage instantiates generic bases in the declaration's instantiation
    // scope; non-generic interfaces may extend instantiated generic bases.
    [Theory]
    [InlineData("interface Base<T>{value:T}interface Box<T> extends Base<T>{other:T}let b:Box<number>={value:1,other:2};let n:number=b.value;")]
    [InlineData("interface Base{a:number}interface Box<T> extends Base{value:T}let b:Box<string>={a:1,value:\"x\"};")]
    [InlineData("interface Base<T>{value:T}interface N extends Base<string>{n:number}let x:N={value:\"s\",n:1};let s:string=x.value;")]
    [InlineData("interface A<T>{a:T}interface B<U>{b:U}interface C<T,U> extends A<T>,B<U[]>{c:boolean}let c:C<number,string>={a:1,b:[\"x\"],c:true};")]
    [InlineData("let b:Box<number>={value:1,other:2};interface Box<T> extends Base<T>{other:T}interface Base<T>{value:T}")]
    public void GenericInterfaceHeritageInstantiatesBases(string source)=>
        Assert.Empty(Compilation.Check("generic-interface-heritage.ts",source));

    // A member a generic interface shares with its bases, declared
    // identically once the bases' type parameters are replaced by the
    // heritage arguments: the pinned Go reference reports nothing.
    [Theory]
    [InlineData("interface Base<T>{value:T}interface Box<T> extends Base<T>{value:T}")]
    [InlineData("interface Base<U>{value:U}interface Box<T> extends Base<T>{value:T}let b:Box<number>={value:1};let n:number=b.value;")]
    [InlineData("interface Base<U>{value:U[]}interface Box<T> extends Base<T>{value:T[]}")]
    [InlineData("interface Base<K,V>{f:(k:K)=>V}interface Box<A,B> extends Base<A,B>{f:(k:A)=>B}")]
    [InlineData("interface Base<T>{value:T}interface Box<T> extends Base<string>{value:string}")]
    [InlineData("interface Base{n:number}interface Box<T> extends Base{n:number;value:T}let b:Box<string>={n:1,value:\"x\"};")]
    [InlineData("interface A<T>{x:T}interface B<U>{x:U}interface C<T> extends A<T>,B<T>{}let c:C<number>={x:1};")]
    [InlineData("interface A<T>{readonly x?:T[]}interface B<U>{readonly x?:U[]}interface C<T> extends A<T>,B<T>{}")]
    [InlineData("let b:Box<number>={value:1};interface Box<T> extends Base<T>{value:T}interface Base<U>{value:U}")]
    [InlineData("interface Base<U>{value:U;tag:string|1}interface Box<T> extends Base<T>{tag:string|1}")]
    public void GenericInterfaceSharedMembersDeclaredIdenticallyCompile(string source)=>
        Assert.Empty(Compilation.Check("generic-interface-shared-members.ts",source));

    // Instantiations of an accepted declaration are still checked.
    [Theory]
    [InlineData("interface Base<U>{value:U}interface Box<T> extends Base<T>{value:T}let b:Box<number>={value:\"x\"};","TS2322",87)]
    [InlineData("interface A<T>{x:T}interface B<U>{x:U}interface C<T> extends A<T>,B<T>{}let c:C<number>={x:\"s\"};let s:string=c.x;","TS2322",90)]
    public void GenericInterfaceSharedMemberInstantiationsAreChecked(string source,string code,int column)
    {
        var diagnostic=Compilation.Check("generic-interface-shared-members-bad.ts",source).First();
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(column-1,diagnostic.Start);
    }

    // Codes and positions identical to the pinned Go reference.
    [Theory]
    [InlineData("interface Base<T>{value:T}interface Box<T> extends Base<T>{other:T}let b:Box<number>={value:\"x\",other:2};","TS2322",87)]
    [InlineData("interface Base<T>{value:T}interface Box<T> extends Base<T>{other:T}let b:Box<number>={other:2};","TS2741",72)]
    [InlineData("interface Base<T>{value:T}interface Box<T> extends Base<T>{other:T}let b:Box<number>={value:1,other:2,extra:3};","TS2353",103)]
    [InlineData("interface Base<T>{value:T}interface Box<T> extends Base<T>{other:T}let b:Box<number>={value:1,other:2};let s:string=b.value;","TS2322",108)]
    [InlineData("interface Base<T>{value:T}interface N extends Base<string>{n:number}let x:N={value:1,n:1};","TS2322",78)]
    [InlineData("interface Base<T>{value:T}interface N extends Base{n:number}","TS2314",47)]
    [InlineData("interface Base{value:number}interface N extends Base<string>{n:number}","TS2315",49)]
    [InlineData("interface Box<T> extends Missing<T>{value:T}","TS2304",26)]
    [InlineData("interface Base<T>{value:T}interface N extends Base<Missing>{n:number}","TS2304",52)]
    [InlineData("interface Base<T>{value:T}interface N extends Base<number>{value:string}","TS2430",37)]
    public void GenericInterfaceHeritageErrorsAreReported(string source,string code,int column)
    {
        var diagnostic=Assert.Single(Compilation.Check("generic-interface-heritage-bad.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(column-1,diagnostic.Start);
    }

    // The reference compares shared members with opaque type parameters
    // (TS2430 / TS2320 / no error); the 'unknown' placeholder cannot, so
    // shared members, non-object bases and heritage on reopened generic
    // interfaces fail closed.
    [Theory]
    [InlineData("interface Base<T>{value:T}interface Box<T> extends Base<T>{value:string}")]
    [InlineData("interface A<T>{x:T}interface B<T>{x:unknown}interface C<T> extends A<T>,B<T>{}")]
    // Not syntactically identical after substitution: no proof either way.
    [InlineData("interface Base<T>{value:T}interface Box<T> extends Base<T[]>{value:T}")]
    [InlineData("interface Base<T>{value:T}interface Box<T,U> extends Base<U>{value:T}")]
    [InlineData("interface Base<T>{value:T}interface Box<T> extends Base<T>{readonly value:T}")]
    [InlineData("interface Base<T>{value?:T}interface Box<T> extends Base<T>{value:T}")]
    [InlineData("interface A<T>{x:T}interface B<T>{x:T}interface C<T,U> extends A<T>,B<U>{}")]
    [InlineData("interface Base<T>{value:keyof T}interface Box<T> extends Base<T>{value:keyof T}")]
    // Members reached through a base's own heritage or a merged base.
    [InlineData("interface Root<T>{value:T}interface Base<T> extends Root<T>{}interface Box<T> extends Base<T>{value:T}")]
    [InlineData("interface Base<T>{value:T}interface Base<T>{other:T}interface Box<T> extends Base<T>{value:T}")]
    [InlineData("let b:Box<number>={value:\"x\"};interface Box<T> extends Base<T>{value:string}interface Base<T>{value:T}")]
    [InlineData("interface Box<T> extends T{value:T}")]
    [InlineData("interface Base<T>{value:T}interface Box<T> extends Base{other:T}")]
    [InlineData("interface Box<T> extends Array<T>{value:T}")]
    [InlineData("interface Base<T>{value:T}interface Box<T>{a:T}interface Box<T> extends Base<T>{b:T}")]
    public void UnsupportedGenericInterfaceHeritageFailsClosed(string source)=>
        Assert.Contains(Compilation.Check("generic-interface-heritage-unsupported.ts",source),d=>d.Code=="TSCS1001");


    // A2.3: concrete aliases denote their checked types when shared by
    // generic heritage. Mismatches and modifier changes still fail closed.
    [Theory]
    [InlineData("type Tag=string|1;interface Base<T>{value:T;tag:Tag}interface Box<T> extends Base<T>{tag:1|string}let b:Box<number>={value:8,tag:1};")]
    [InlineData("type A=1|string;type B=string|1;interface Base<T>{value:T;tag:A}interface Box<T> extends Base<T>{tag:B}let b:Box<number>={value:8,tag:1};")]
    [InlineData("type Shape={x:number;y:string};interface Base<T>{value:T;meta:Shape}interface Box<T> extends Base<T>{meta:{y:string;x:number}}let b:Box<number>={value:8,meta:{x:1,y:\"ok\"}};")]
    public void ConcreteAliasSharedByGenericHeritageCompiles(string source)=>
        Assert.Empty(Compilation.Check("generic-alias-shared-member.ts",source));

    [Theory]
    [InlineData("type Tag=string|1;interface Base<T>{tag:Tag}interface Box<T> extends Base<T>{tag:number|string}")]
    [InlineData("type Shape={x:number;y:string};interface Base<T>{meta:Shape}interface Box<T> extends Base<T>{meta:{x:number;y:number}}")]
    [InlineData("type Shape={x:number};interface Base<T>{readonly meta:Shape}interface Box<T> extends Base<T>{meta:{x:number}}")]
    public void ConcreteAliasSharedByGenericHeritageRejectsConflicts(string source)=>
        Assert.Contains(Compilation.Check("generic-alias-shared-conflict.ts",source),
            d=>d.Code=="TSCS1001");

    // Pinned Go accepts this exact concrete-union reordered member, in both
    // syntactic orders (see PR #249 independent reference review). It used to
    // be misregistered as an unsupported negative case.
    [Theory]
    [InlineData("string|1","1|string")]
    [InlineData("1|string","string|1")]
    public void ConcreteUnionSharedByGenericHeritageIsIdentical(string baseType,string derivedType)
    {
        var source=$"interface Base<U>{{value:U;tag:{baseType}}}" +
            $"interface Box<T> extends Base<T>{{tag:{derivedType}}}" +
            "const item:Box<number>={value:8,tag:1};let answer:number=item.value;";
        Assert.Empty(Compilation.Check("generic-concrete-shared-union.ts",source));
    }

    // Different members remain rejected; changing comparison order cannot
    // manufacture identity or allow emit of an invalid derived interface.
    [Theory]
    [InlineData("string|1","number|string")]
    [InlineData("1|string","string|number")]
    public void ConcreteUnionSharedByGenericHeritageRejectsConflicts(string baseType,string derivedType)
    {
        var source=$"interface Base<U>{{value:U;tag:{baseType}}}" +
            $"interface Box<T> extends Base<T>{{tag:{derivedType}}}" +
            "let result:number=7;";
        Assert.Contains(Compilation.Check("generic-concrete-conflict.ts",source),
            d=>d.Code=="TSCS1001");
    }

    [Theory]
    [InlineData("Box<number>","{value:number;other:number}",false)]
    [InlineData("Box<number>","Box<string>",true)]
    [InlineData("Box<number>","{value:number}",true)]
    public void RepeatedAmbientVarComparesInheritedInstantiations(string left,string right,bool differs)
    {
        foreach(var (first,second) in new[]{(left,right),(right,left)})
        {
            var diagnostics=Compilation.CheckFiles([
                new SourceInput("types.d.ts","interface Base<T>{value:T}interface Box<T> extends Base<T>{other:T}"),
                new SourceInput("a.d.ts",$"declare var item:{first};"),
                new SourceInput("b.d.ts",$"declare var item:{second};"),
                new SourceInput("entry.ts","let result=7;")
            ]);
            if(differs)Assert.Contains(diagnostics,d=>d.Code=="TS2403");
            else Assert.Empty(diagnostics);
        }
    }

    // Go accepts this pair; identity of an extended interface against an
    // intersection-bearing shape is not modelled, so it fails closed rather
    // than reporting TS2403.
    [Fact]
    public void RepeatedAmbientVarInheritedIntersectionIdentityFailsClosed()
    {
        var diagnostics=Compilation.CheckFiles([
            new SourceInput("types.d.ts","interface Base<T>{value:T}interface Box<T> extends Base<T>{other:T}"),
            new SourceInput("a.d.ts","declare var item:Box<{a:1}&{b:2}>;"),
            new SourceInput("b.d.ts","declare var item:{value:{a:1}&{b:2};other:{a:1}&{b:2}};"),
            new SourceInput("entry.ts","let result=7;")
        ]);
        Assert.Contains(diagnostics,d=>d.Code=="TSCS1001");
        Assert.DoesNotContain(diagnostics,d=>d.Code=="TS2403");
    }
}

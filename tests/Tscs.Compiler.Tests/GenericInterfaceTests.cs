using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3 prerequisite: single-declaration generic interfaces with plain type
// parameters, instantiated by substitution like generic type aliases.
// Reopening with an identical parameter list merges per instantiation;
// different lists, heritage, constraints and defaults fail closed.
public sealed class GenericInterfaceTests
{
    [Theory]
    [InlineData("interface Box<T>{value:T}let b:Box<number>={value:1};let n:number=b.value;")]
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
    [InlineData("interface Box<T extends string>{value:T}")]
    [InlineData("interface Box<T=number>{value:T}")]
    [InlineData("interface Box<out T>{value:T}")]
    [InlineData("interface Box<T,T>{value:T}")]
    [InlineData("interface Box<>{value:number}")]
    [InlineData("interface Base{a:number}interface Box<T> extends Base{value:T}")]
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
}

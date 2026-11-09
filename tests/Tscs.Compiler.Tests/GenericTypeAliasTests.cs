using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3 prerequisite: plain generic type aliases instantiated by substitution.
// Bounded constraints/defaults share the checked path; generic functions stay out.
public sealed class GenericTypeAliasTests
{
    [Theory]
    [InlineData("type Box<T>={value:T};let b:Box<number>={value:1};let n:number=b.value;")]
    [InlineData("type Box<T extends string>={value:T};let b:Box<\"a\">={value:\"a\"};")]
    [InlineData("type Pair<A,B>={first:A;second:B};let p:Pair<number,string>={first:1,second:\"x\"};let s:string=p.second;")]
    [InlineData("type Box<T>={value:T};type Wrap<T>=Box<Box<T>>;let w:Wrap<number>={value:{value:40}};let n:number=w.value.value;")]
    [InlineData("type Fn<T>=(v:T)=>T;let f:Fn<number>=(v:number):number=>v+1;let n:number=f(1);")]
    [InlineData("type List<T>=T[];let l:List<number>=[1,2];let n:number=l[0];")]
    [InlineData("type Maybe<T>=T|undefined;let m:Maybe<number>=undefined;")]
    // The type parameter shadows an outer type of the same name.
    [InlineData("type T=string;type Box<T>={value:T};let b:Box<number>={value:1};let s:T=\"x\";")]
    // Unused parameters still accept any argument type.
    [InlineData("type Const<T>=number;let n:Const<string>=1;")]
    // A generic alias may be declared after its use.
    [InlineData("let b:Box<boolean>={value:true};type Box<T>={value:T};")]
    public void GenericAliasesInstantiateBySubstitution(string source)=>
        Assert.Empty(Compilation.Check("generic-alias.ts",source));

    [Theory]
    [InlineData("type Box<T>={value:T};let b:Box<number>={value:\"x\"};","TS2322")]
    [InlineData("type Box<T>={value:T};let b:Box<string>={value:1};","TS2322")]
    [InlineData("type Pair<A,B>={first:A;second:B};let p:Pair<number,string>={first:1,second:2};","TS2322")]
    [InlineData("type Box<T>={value:T};let b:Box={value:1};","TS2314")]
    [InlineData("type Box<T>={value:T};let b:Box<number,string>={value:1};","TS2314")]
    [InlineData("type N=number;let n:N<string>=1;","TS2315")]
    [InlineData("type Box<T>={value:Missing};","TS2304")]
    [InlineData("type Box<T>={value:T};let b:Box<Missing>={value:1};","TS2304")]
    [InlineData("type Box<T>={value:T};type Box<U>={other:U};","TS2300")]
    [InlineData("type Box<number>={value:number};","TS2368")]
    [InlineData("type Box<string>={value:string};","TS2368")]
    [InlineData("type Box<boolean>={value:number};","TS2368")]
    [InlineData("type Box<any>={value:number};","TS2368")]
    [InlineData("type Box<unknown>={value:number};","TS2368")]
    [InlineData("type Box<undefined>={value:number};","TS2368")]
    [InlineData("type Box<never>={value:number};","TS2368")]
    [InlineData("type Box<object>={value:number};","TS2368")]
    [InlineData("type Box<bigint>={value:number};","TS2368")]
    [InlineData("type Box<T,symbol>={value:T};","TS2368")]
    public void GenericAliasErrorsAreReported(string source,string code)
    {
        var diagnostics=Compilation.Check("generic-alias-bad.ts",source);
        Assert.Contains(diagnostics,d=>d.Code==code);
    }

    [Theory]
    [InlineData("type Box<in T>={value:T};")]
    [InlineData("type Box<T,T>={value:T};")]
    [InlineData("type Box<>={value:number};")]
    [InlineData("type Keys<T>=keyof T;")]
    [InlineData("type Rec<T>={next:Rec<T>};let r:Rec<number>={next:null as any};")]
    public void UnsupportedGenericFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("generic-alias-unsupported.ts",source),d=>d.Code=="TSCS1001");

    private const string Prelude="type A={a:number};type B={b:string};type P=A&B;type Box<T>={value:T};";

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

    // Exhausting the bounded identity depth is unsupported, not a type
    // difference: the reference accepts these equal 13-level instantiations.
    [Fact]
    public void DeepInstantiationIdentityFailsClosedInsteadOfReportingTs2403()
    {
        var diagnostics=Compilation.CheckFiles([
            new SourceInput("x.d.ts","type Pair<A,B>={first:A;second:B};type D<T>=Pair<T,T>;declare var v:D<D<D<D<D<D<D<D<D<D<D<D<D<number>>>>>>>>>>>>>;"),
            new SourceInput("y.d.ts","type Pair2<A,B>={first:A;second:B};type D2<T>=Pair2<T,T>;declare var v:D2<D2<D2<D2<D2<D2<D2<D2<D2<D2<D2<D2<D2<number>>>>>>>>>>>>>;"),
            new SourceInput("entry.ts","let result=7;")
        ]);
        Assert.DoesNotContain(diagnostics,d=>d.Code=="TS2403");
        Assert.Contains(diagnostics,d=>d.Code=="TSCS1001");
    }
}

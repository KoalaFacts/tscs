using Tscs;
using Xunit;

namespace Tscs.Tests;

// Defaulted type parameters are applied in declaration scope, after
// preceding explicit or defaulted type arguments.
public sealed class GenericDeclarationDefaultsTests
{
    [Theory]
    [InlineData("type Box<T=number>={value:T};let box:Box={value:8};let result:number=box.value;")]
    [InlineData("type Box<T=number>={value:T};let box:Box<string>={value:\"yes\"};let result:string=box.value;")]
    [InlineData("type Pair<T,U=T[]>={left:T;right:U};let pair:Pair<number>={left:1,right:[2,3]};let v:number=pair.right[0];")]
    [InlineData("type Named=string;type Box<T=Named>={value:T};let box:Box={value:\"yes\"};")]
    [InlineData("interface Box<T=number>{value:T}let box:Box={value:8};let result:number=box.value;")]
    [InlineData("interface Box<T=string>{value:T}let box:Box<number>={value:8};")]
    [InlineData("interface Base<T=number>{value:T}interface Child extends Base{extra:number}let item:Child={value:7,extra:1};")]
    [InlineData("interface Pair<T,U=T>{first:T;second:U}let p:Pair<string>={first:\"a\",second:\"b\"};")]
    [InlineData("type First<T,U=number>={first:T;second:U};type Second<T=string>=First<T>;let x:Second={first:\"a\",second:2};")]
    public void DefaultedDeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("generic-default.ts",source));

    [Theory]
    [InlineData("type Box<T=number>={value:T};let b:Box={value:\"no\"};","TS2322")]
    [InlineData("interface Box<T=string>{value:T}let b:Box={value:1};","TS2322")]
    [InlineData("type Pair<T,U=number>={first:T;second:U};let x:Pair={first:1,second:2};","TS2314")]
    [InlineData("type Box<T=number>={value:T};let b:Box<number,string>={value:1};","TS2314")]
    [InlineData("type Box<T=Missing>={value:T};","TS2304")]
    [InlineData("interface Box<T=Missing>{value:T}","TS2304")]
    [InlineData("type Box<T=Later,Later=string>={value:T};","TS2744")]
    public void DefaultedDeclarationsReportErrors(string source,string code)=>
        Assert.Contains(Compilation.Check("generic-default-error.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("interface Base<T=number>{value:T}interface Child<U=string> extends Base{value:number;other:U}let x:Child={value:8,other:\"ok\"};")]
    [InlineData("interface Base<T=number,U=T[]>{first:T;second:U}interface Child<Z=string> extends Base{first:number;second:number[];other:Z}let x:Child={first:8,second:[8],other:\"ok\"};")]
    [InlineData("type Payload<T=number>={value:T};interface Base<T=number>{payload:Payload<T>}interface Child<Z=boolean> extends Base{payload:Payload;flag:Z}let x:Child={payload:{value:8},flag:true};")]
    public void DefaultedSharedHeritageMembersAreIdentical(string source)=>
        Assert.Empty(Compilation.Check("default-heritage-identity.ts",source));

    [Theory]
    [InlineData("interface Base<T=number>{value:T}interface Child<U=string> extends Base{value:string;other:U}")]
    [InlineData("interface Base<T=number,U=T[]>{first:T;second:U}interface Child<Z=boolean> extends Base{first:number;second:string[];flag:Z}")]
    public void ConflictingDefaultedHeritageMembersFailClosed(string source)=>
        Assert.NotEmpty(Compilation.Check("bad-default-heritage-identity.ts",source));

    [Theory]
    [InlineData("type Box<T=number,U>={left:T;right:U};")]
    [InlineData("interface Box<T=number,U>{left:T;right:U}")]
    [InlineData("interface Box<T=number>{value:T}interface Box<T>{other:T}")]
    [InlineData("interface Box<out T=number>{value:T}")]
    public void UnsupportedDefaultFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("generic-default-unsupported.ts",source),d=>d.Code=="TSCS1001");
}

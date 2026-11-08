using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class GenericDefaultScopeTests
{
    [Theory]
    [InlineData("type T=number; type Box<T=T>={value:T};")]
    [InlineData("type T=number; interface Box<T=T>{value:T}")]
    [InlineData("type U=number; type Pair<T=U,U=string>={left:T;right:U};")]
    [InlineData("type U=number; interface Pair<T=U,U=string>{left:T;right:U}")]
    [InlineData("type T=number; type Box<T=T>={value:T}; let x:Box={value:1};")]
    [InlineData("type T=number; type Box<T=T>={value:T}; let x:Box<string>={value:\"a\"};")]
    [InlineData("type U=number; interface Pair<T=U,U=string>{left:T;right:U} let x:Pair<number,string>={left:1,right:\"a\"};")]
    [InlineData("type Box<T=T>={value:T};")]
    [InlineData("type Box<T=Later,Later=string>={value:T};")]
    [InlineData("type T=number; type Box<T={items:T[]}>={value:T};")]
    [InlineData("type U=number; interface Pair<T={items:U[]},U=string>{value:T}")]
    public void SelfAndLaterParametersCannotCaptureOuterTypes(string source)=>
        Assert.Contains(Compilation.Check("generic-default-scope.ts",source),d=>d.Code=="TS2744");

    [Theory]
    [InlineData("type T=string; type Pair<T=number,U=T>={left:T;right:U}; let x:Pair={left:1,right:2};")]
    [InlineData("type Named=number; type Box<T=Named>={value:T}; let x:Box={value:1};")]
    [InlineData("type T=number; type Named=T; type Box<T=Named>={value:T}; let x:Box={value:1};")]
    [InlineData("const T=1; type Box<T=typeof T>={value:T}; let x:Box={value:1};")]
    [InlineData("type Pair<T=number,U=T[],V=U>={value:V}; let x:Pair={value:[1]};")]
    public void EarlierParametersAndActualOuterReferencesRemainValid(string source)=>
        Assert.Empty(Compilation.Check("generic-default-scope-control.ts",source));
}

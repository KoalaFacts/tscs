using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3 prerequisite: the `never` keyword type in annotations and type
// arguments. Before this, `never` resolved as a name and reported an invented
// TS2304 ("Cannot find name 'never'") that the pinned reference never gives.
public sealed class NeverTypeTests
{
    [Theory]
    [InlineData("declare let n:never;let x:number=n;let s:string=n;")]
    [InlineData("declare let n:never;let c:never=n;let d:unknown=n;")]
    [InlineData("let u:number|never=1;let v:number=u;")]
    [InlineData("let a:never[]=[];let b:number[]=a;")]
    [InlineData("let f:()=>never=()=>{throw 1;};")]
    [InlineData("type Id<T>=T;declare let n:Id<never>;let x:number=n;")]
    public void NeverIsAssignableToEveryType(string source)=>
        Assert.Empty(Compilation.Check("never.ts",source));

    [Theory]
    // Expected start offsets match the pinned reference's positions.
    [InlineData("let c:never=1;","TS2322",4)]
    [InlineData("type Id<T>=T;let b:Id<never>=1;","TS2322",17)]
    [InlineData("interface Box<T>{v:T}let b:Box<never>={v:1};","TS2322",39)]
    [InlineData("declare let a:any;let c:never=a;","TS2322",22)]
    [InlineData("declare let u:unknown;let c:never=u;","TS2322",26)]
    [InlineData("let x:never=undefined;","TS2322",4)]
    [InlineData("let o:{a:never}={a:1};","TS2322",17)]
    [InlineData("declare let n:never;n.foo;","TS2339",22)]
    public void NothingButNeverIsAssignableToNever(string source,string code,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("never-bad.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
        Assert.DoesNotContain("Cannot find name",diagnostic.Message);
    }

    [Fact]
    public void NeverIsNotGeneric()
    {
        var diagnostic=Assert.Single(Compilation.Check("never-generic.ts","let x:never<number>=1;"));
        Assert.Equal("TSCS1001",diagnostic.Code);
    }
}

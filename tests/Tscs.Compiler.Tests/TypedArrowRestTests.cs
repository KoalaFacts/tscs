using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class TypedArrowRestTests
{
    [Theory]
    [InlineData("""const f=(...xs:number[]):number=>xs.length;const result:number=f(1,2,3);""")]
    [InlineData("""const f=(...xs:number[]):number=>xs.length;const result:number=f();""")]
    [InlineData("""const f=(...xs:string[]):number=>xs.length;const result:number=f("a","b");""")]
    [InlineData("""const f=(n:number,...xs:number[]):number=>n+xs.length;const result:number=f(40,1,2);""")]
    [InlineData("""const f=(base:number,...xs:number[]):number=>{let total:number=base;for(const x of xs){total+=x;}return total;};const result:number=f(20,21,1);""")]
    [InlineData("""type F=(base:number,...xs:number[])=>number;const f:F=(base:number,...xs:number[])=>base+xs.length;const result:number=f(40,1,2);""")]
    [InlineData("""const f=(n:number=21,...xs:number[]):number=>n+xs.length;const result:number=f(40,1,2);""")]
    [InlineData("""const f=(n?:number,...xs:number[]):number=>xs.length;const result:number=f(42,1,2);""")]
    public void TypedRestArrowCompiles(string source) =>
        Assert.Empty(Compilation.Check("arrow-rest.ts",source));

    [Theory]
    [InlineData("""const f=(...xs:number):number=>42;""","TS2370")]
    [InlineData("""const f=(...xs?:number[]):number=>42;""","TS1047")]
    [InlineData("""const f=(...xs?:number[]):number=>42;""","TS2370")]
    [InlineData("""const f=(...xs:number[]=[]):number=>42;""","TS1048")]
    [InlineData("""const f=(...xs:number[],n:number):number=>n;""","TS1014")]
    [InlineData("""const f=(...xs:number[]):number=>xs.length;f("bad");""","TS2345")]
    [InlineData("""const f=(n:number,...xs:number[]):number=>n;f(42,true);""","TS2345")]
    [InlineData("""const f=(...xs:number[]):string=>xs.length;""","TS2322")]
    [InlineData("""const f=(...xs:string[]):number=>xs.length;f(42);""","TS2345")]
    public void InvalidRestArrowsAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-arrow-rest.ts",source), d => d.Code==code);
}

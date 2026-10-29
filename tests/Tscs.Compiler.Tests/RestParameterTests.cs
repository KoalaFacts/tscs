using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class RestParameterTests
{
    [Theory]
    [InlineData("function f(...items:number[]):number{return items.length;}let r:number=f();")]
    [InlineData("function f(...items:number[]):number{return items.length;}let r:number=f(1,2,3);")]
    [InlineData("function f(base:number,...items:number[]):number{return base+items.length;}let r:number=f(40,1,2);")]
    [InlineData("function f(...words:string[]):number{return words.length;}let r:number=f(\"a\",\"b\");")]
    [InlineData("function f(...words:string[]):number{return words.length;}let r:number=f();")]
    [InlineData("function f(base:number=21,...items:number[]):number{return base+items.length;}let r:number=f();")]
    [InlineData("function f(base?:number,...items:string[]):number{return items.length;}let r:number=f(21,\"a\",\"b\");")]
    [InlineData("function f(...items:number[]):number{let total:number=0;for(const item of items){total+=item;}return total;}let r:number=f(20,21,1);")]
    [InlineData("export function f(base:number,...items:number[]):number{return base+items.length;}")]
    public void SupportedRestParametersCompile(string source)=>
        Assert.Empty(Compilation.Check("source.ts",source));

    [Theory]
    [InlineData("function f(...items:number[]):number{return items.length;}f(\"bad\");","TS2345")]
    [InlineData("function f(...items:string[]):number{return items.length;}f(\"ok\",21);","TS2345")]
    [InlineData("function f(base:number,...items:number[]):number{return base;}f(1,\"bad\");","TS2345")]
    [InlineData("function f(...items:number[]):number{return items.length;}let n:number=f(1);f(\"bad\");","TS2345")]
    [InlineData("function f(...items:number):number{return 1;}","TS2370")]
    [InlineData("function f(...items?:number[]):number{return 1;}","TS1047")]
    [InlineData("function f(...items:number[]=[]):number{return 1;}","TS1048")]
    [InlineData("function f(...items:number[],extra:number):number{return 1;}","TS1014")]
    public void InvalidRestParametersAreRejected(string source,string code)=>
        Assert.Contains(Compilation.Check("source.ts",source),d=>d.Code==code);
}

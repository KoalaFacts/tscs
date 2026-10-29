using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class SpreadArgumentTests
{
    [Theory]
    [InlineData("function f(...v:number[]):number{return v.length;}let xs:number[]=[1,2];let n:number=f(...xs);")]
    [InlineData("function f(base:number,...v:number[]):number{return base+v.length;}let xs:number[]=[1,2];let n:number=f(40,...xs);")]
    [InlineData("function f(...v:string[]):number{return v.length;}let n:number=f(...[\"a\",\"b\"]);")]
    [InlineData("function f(...v:number[]):number{return v.length;}let xs:number[]=[];let n:number=f(...xs);")]
    [InlineData("function f(base:number=21,...v:number[]):number{return base+v.length;}let xs:number[]=[1,2];let n:number=f(40,...xs);")]
    [InlineData("function f(...v:number[]):number{return v[0]+v[1];}let xs:number[]=[20,22];let n:number=f(...xs);")]
    [InlineData("function f(...v:number[]):number{return v.length;}let n:number=f(...[20,21]);")]
    [InlineData("function f(base:number,...v:number[]):number{return base+v.length;}let xs:number[]=[1];let n:number=f(41,...xs);")]
    public void TrailingCheckedArraySpreadCanCallRestFunction(string source) =>
        Assert.Empty(Compilation.Check("source.ts",source));

    [Theory]
    [InlineData("function f(...v:number[]):number{return v.length;}let xs:string[]=[\"bad\"];f(...xs);","TS2345")]
    [InlineData("function f(base:number,...v:number[]):number{return base;}let xs:string[]=[\"bad\"];f(1,...xs);","TS2345")]
    [InlineData("function f(...v:number[]):number{return v.length;}let xs:boolean[]=[true];f(...xs);","TS2345")]
    [InlineData("function f(n:number):number{return n;}let xs:number[]=[1];f(...xs);","TS2556")]
    [InlineData("function f(...v:number[]):number{return v.length;}f(...21);","TSCS1001")]
    [InlineData("function f(n:number,...v:number[]):number{return n;}let xs:number[]=[1];f(...xs);","TSCS1001")]
    [InlineData("function f(...v:number[]):number{return v.length;}let xs:number[]=[1];f(...xs,1);","TSCS1001")]
    [InlineData("function f(...v:number[]):number{return v.length;}let xs:number[]=[1];f(...xs,...xs);","TSCS1001")]
    public void UnsafeOrUnsupportedSpreadFailsWithoutEmitting(string source,string code) =>
        Assert.Contains(Compilation.Check("source.ts",source),d=>d.Code==code);
}

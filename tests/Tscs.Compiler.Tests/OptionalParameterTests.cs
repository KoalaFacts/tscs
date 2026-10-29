using Tscs;
using Xunit;
namespace Tscs.Tests;
public sealed class OptionalParameterTests
{
    [Theory]
    [InlineData("function f(n?:number):void{return;}f();")]
    [InlineData("function f(n?:number):void{return;}f(21);")]
    [InlineData("function f(n:number,x?:number):number{return n;}let v:number=f(21);")]
    [InlineData("function f(n:number,x?:number):number{return n;}let v:number=f(21,2);")]
    [InlineData("function f(a?:number,b?:number):number{return 42;}let v:number=f();")]
    [InlineData("function f(a?:number,b?:number):number{return 42;}let v:number=f(1,2);")]
    [InlineData("function f(n:number,s?:string):number{return n;}let v:number=f(21,\"okay\");")]
    [InlineData("function f(n?:number):number{if(typeof n===\"number\"){return n;}return 0;}let v:number=f();")]
    public void OptionalCallsCompile(string source)=>Assert.Empty(Compilation.Check("source.ts",source));
    [Theory]
    [InlineData("function f(n:number,x?:number):number{return n;}f();","TS2554")]
    [InlineData("function f(n:number,x?:number):number{return n;}f(1,2,3);","TS2554")]
    [InlineData("function f(n?:number):void{return;}f(\"bad\");","TS2345")]
    [InlineData("function f(n:number,s?:string):number{return n;}f(1,false);","TS2345")]
    [InlineData("function f(n?:number,x:number):number{return x;}","TS1016")]
    public void InvalidOptionalCallsAreRejected(string source,string code)=>
        Assert.Contains(Compilation.Check("source.ts",source),d=>d.Code==code);
}

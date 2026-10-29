using Tscs;
using Xunit;
namespace Tscs.Tests;
public sealed class DefaultParameterTests
{
    [Theory]
    [InlineData("function f(n:number=21):number{return n;}let v:number=f();")]
    [InlineData("function f(n:number=21):number{return n;}let v:number=f(42);")]
    [InlineData("function f(a:number,b:number=1):number{return a+b;}let v:number=f(41);")]
    [InlineData("function f(a:number,b:number=a):number{return a+b;}let v:number=f(21);")]
    [InlineData("function f(a:number,b:number=a+1):number{return a+b;}let v:number=f(20);")]
    [InlineData("function f(a:number=21,b:number=21):number{return a+b;}let v:number=f();")]
    [InlineData("function f(s:string=\"ok\"):string{return s;}let x:string=f();")]
    [InlineData("function f(flag:boolean=true):boolean{return flag;}let v:boolean=f();")]
    [InlineData("function f(n:number=21):void{return;}f();")]
    [InlineData("function f(a:number,b:number=a*2):number{return b;}let v:number=f(21);")]
    public void ValidTypedDefaultsCompile(string source)=>Assert.Empty(Compilation.Check("source.ts",source));

    [Theory]
    [InlineData("function f(n:number=\"wrong\"):number{return n;}", "TS2322")]
    [InlineData("function f(n:number=true):number{return n;}", "TS2322")]
    [InlineData("function f(n:number=21):number{return n;}f(\"bad\");", "TS2345")]
    [InlineData("function f(n:number=21):number{return n;}f(21,2);", "TS2554")]
    [InlineData("function f(n:number,b:number=1):number{return n;}f();", "TS2554")]
    [InlineData("function f(n?:number=21):number{return 21;}", "TS1015")]
    public void InvalidDefaultsAreRejected(string source,string code)=>
        Assert.Contains(Compilation.Check("source.ts",source),d=>d.Code==code);

    [Fact]
    public void NonTrailingDefaultFailsClosed()=>
        Assert.Contains(Compilation.Check("source.ts",
            "function f(a:number=1,b:number):number{return a+b;}"),
            d=>d.Code=="TSCS1001");
}

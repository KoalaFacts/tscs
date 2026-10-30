using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class FunctionExpressionTests
{
    [Theory]
    [InlineData("""const f=function(n:number):number{return n+1;};const result:number=f(41);""")]
    [InlineData("""const f=function(n:number){return n+n;};const result:number=f(21);""")]
    [InlineData("""const f=function():number{return 42;};const result:number=f();""")]
    [InlineData("""const offset:number=1;const f=function(n:number):number{return n+offset;};const result:number=f(41);""")]
    [InlineData("""type Fn=(n:number)=>number;const f:Fn=function(n:number):number{return n+1;};const result:number=f(41);""")]
    [InlineData("""function apply(fn:(n:number)=>number,n:number):number{return fn(n);}const result:number=apply(function(n:number):number{return n+n;},21);""")]
    [InlineData("""const make=function(n:number):(m:number)=>number{return function(m:number):number{return n+m;};};const f=make(20);const result:number=f(22);""")]
    [InlineData("""const f=function(n:number=21):number{return n+n;};const result:number=f();""")]
    [InlineData("""const f=function(n:number,...xs:number[]):number{return n+xs.length;};const result:number=f(40,1,2);""")]
    [InlineData("""const f=function(n:number):number{if(n>0){return n;}else{return -n;}};const result:number=f(-42);""")]
    [InlineData("""const f=function():void{const result:number=42;};f();""")]
    [InlineData("""const f=function self(n:number):number{if(n<=0){return 42;}return self(n-1);};const result:number=f(3);""")]
    [InlineData("""const name:number=21;const f=function name(n:number):number{return n+n;};const result:number=f(name);""")]
    [InlineData("""const f=function self(self:number):number{return self+1;};const result:number=f(41);""")]
    [InlineData("""type Fn=(n:number)=>number;const f:Fn=function named(n:number):number{return n+n;};const result:number=f(21);""")]
    [InlineData("""function apply(fn:(n:number)=>number,n:number):number{return fn(n);}const result:number=apply(function local(n:number):number{return n+n;},21);""")]
    [InlineData("""const make=function outer(n:number):(v:number)=>number{return function inner(v:number):number{return n+v;};};const f=make(20);const result:number=f(22);""")]
    [InlineData("""const f=function rest(base:number,...xs:number[]):number{let sum:number=base;for(const item of xs){sum+=item;}return sum;};const result:number=f(20,21,1);""")]
    [InlineData("""const f=function self(n:number=21):number{return n+n;};const result:number=f();""")]
    public void AnonymousTypedFunctionsUseTheRealChecker(string source) =>
        Assert.Empty(Compilation.Check("function-expression.ts", source));

    [Theory]
    [InlineData("""const f=function(n:number):number{return "wrong";};""", "TS2322")]
    [InlineData("""const f=function(n:number):number{return n;};f("bad");""", "TS2345")]
    [InlineData("""const f=function(n:number):number{return n;};f();""", "TS2554")]
    [InlineData("""const f=function(n:number):number{return unknownValue;};""", "TS2304")]
    [InlineData("""const f=function(n:number):number{if(n>0){return n;}};""", "TSCS1001")]
    [InlineData("""const f=function(n){return n;};""", "TSCS1001")]
    [InlineData("""const f=function named(n:number){return n;};""", "TSCS1001")]
    [InlineData("""const f=function named(n:number):number{return n;};named(42);""", "TS2304")]
    [InlineData("""const f=function named(n:number):number{return named("bad");};""", "TS2345")]
    [InlineData("""const f=function named(n:number):number{named=named;return n;};""", "TS2588")]
    [InlineData("""const f=function named(n:number):number{if(n>0){return named(n-1);}};""", "TSCS1001")]
    [InlineData("""const f=function(n:number="bad"):number{return n;};""", "TS2322")]
    [InlineData("""const f=function(...xs:number):number{return 42;};""", "TS2370")]
    [InlineData("""const f=function(n:number){if(n>0){return n;}return;};""", "TSCS1001")]
    public void InvalidOrUnsupportedFunctionsFailClosed(string source, string code) =>
        Assert.Contains(Compilation.Check("invalid-function-expression.ts",source),d=>d.Code==code);
}

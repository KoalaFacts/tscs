using Tscs;
using Xunit;

namespace Tscs.Tests;

// Product source semantics; not original TS7 case acceptance.
public sealed class TryFinallyTests
{
    [Theory]
    [InlineData("function f(n:number):number {try {return n+1;} finally {let x:number=1;}}")]
    [InlineData("function f(n:number):number {try {throw n;} finally {return 42;}}")]
    [InlineData("function f(n:number):number {try {return n;} finally {throw \"cleanup\";}}")]
    [InlineData("function f(n:number):number {try {if(n<0){throw n;} return n+1;} finally {let done:boolean=true;}}")]
    [InlineData("function f(n:number):number {let x:number=0;try {while(n>0){break;}} finally {x+=1;} return x+41;}")]
    [InlineData("function f(n:number):number {let x:number=0;while(n>0){try {n--;if(n===1){break;}} finally {x+=1;}}return x+40;}")]
    [InlineData("const item={try:21,finally:21};let total:number=item.try+item.finally;")]
    [InlineData("let counter:number=0;try {counter=41;} finally {counter+=1;}")]
    public void CleanupAndAbruptOverridesAreChecked(string source) =>
        Assert.Empty(Compilation.Check("try-finally.ts",source));

    [Theory]
    [InlineData("function f():number {try {return 42;}}","TSCS1001")]
    [InlineData("function f():number {try {return 42;} catch(e) {return 21;}}","TSCS1001")]
    [InlineData("function f():number {try {return 42;} finally {return \"bad\";}}","TS2322")]
    [InlineData("function f():number {try {return 42;} finally {throw missing;}}","TS2304")]
    [InlineData("function f():number {try {return \"bad\";} finally {let n:number=1;}}","TS2322")]
    [InlineData("function f(n:number):number {try {if(n>0){return n;}} finally {let x:number=1;}}","TSCS1001")]
    [InlineData("try {let x:number=21;} finally {let y:number=\"wrong\";}","TS2322")]
    public void InvalidCleanupSourceCannotCompile(string source,string code) =>
        Assert.Contains(Compilation.Check("try-finally-invalid.ts",source),d=>d.Code==code);
}

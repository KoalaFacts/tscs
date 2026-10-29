using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class VoidReturnTests
{
    [Theory]
    [InlineData("function f():void { } f();")]
    [InlineData("function f(n:number):void {if(n>0){return;}let x:number=n+1;} f(-2);")]
    [InlineData("function f():void {return;} let result:void=f();")]
    [InlineData("function inner():void {return;} function outer():void {return inner();} outer();")]
    [InlineData("function f(n:number):void {try{if(n>0){return;}}finally{let x:number=n;}}")]
    [InlineData("function f(n:number):void {let x:number=0;while(x<n){x++;if(x===2){return;}}}")]
    [InlineData("function f():void {return\n 1;}")]
    public void VoidFunctionsAllowNormalCompletionAndBareReturn(string source) =>
        Assert.Empty(Compilation.Check("source.ts",source));

    [Theory]
    [InlineData("function f():void {return 21;}", "TS2322")]
    [InlineData("function f():void {return \"wrong\";}", "TS2322")]
    [InlineData("function f():void {} let n:number=f();", "TS2322")]
    [InlineData("function f():number {return;}", "TSCS1001")]
    [InlineData("function f():number {return\n 1;}", "TSCS1001")]
    [InlineData("function f():void {return missing;}", "TS2304")]
    [InlineData("function f():number {let n:number=42;}", "TSCS1001")]
    public void InvalidValueAndMissingValueReturnsAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("source.ts",source),d=>d.Code==code);

    [Fact]
    public void VoidAndNumberRetainDistinctSemanticIdentities()
    {
        Assert.Same(CompilerTypes.Void,CompilerTypes.FromAnnotation("void"));
        Assert.True(CompilerTypes.IsAssignable(CompilerTypes.Void,CompilerTypes.Void));
        Assert.True(CompilerTypes.IsAssignable(CompilerTypes.Void,CompilerTypes.Any));
        Assert.False(CompilerTypes.IsAssignable(CompilerTypes.Void,CompilerTypes.Number));
        Assert.False(CompilerTypes.IsAssignable(CompilerTypes.Number,CompilerTypes.Void));
    }
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed semantic checks, not original upstream TS7 acceptance.
public sealed class NullLiteralTests
{
    [Theory]
    [InlineData("let absent:null=null;")]
    [InlineData("let maybe:number|null=null;")]
    [InlineData("let maybe:number|null=21;")]
    [InlineData("type Maybe={value:number}|null;let m:Maybe=null;")]
    [InlineData("function id(n:number|null):number|null{return n;}let x:number|null=id(null);")]
    [InlineData("function read(m:{value:number}|null):number{return m?.value ?? 42;}let n:number=read(null);")]
    [InlineData("let m:{value:number}|null={value:21};let n:number=m?.value ?? 42;")]
    [InlineData("let m:number|null=null;let n:number=m ?? 42;")]
    [InlineData("let o:{value:null}={value:null};let n:null=o.value;")]
    [InlineData("let m:number|null=null;let n:number=m!;")]
    [InlineData("let m:null=null;let n:boolean=m===null;")]
    [InlineData("let m:null=null;let x:undefined=void m;")]
    [InlineData("type Fn=(n:number)=>number;let f:Fn|null=null;let n:number=f?.(21) ?? 42;")]
    [InlineData("let m:(number|null)[]=[null,21];let n:number|null=m[0];")]
    public void ExplicitNullCanFlowThroughTypedExpressions(string source) =>
        Assert.Empty(Compilation.Check("null-values.ts",source));

    [Theory]
    [InlineData("let wrong:number=null;","TS2322")]
    [InlineData("let wrong:string=null;","TS2322")]
    [InlineData("let wrong:undefined=null;","TS2322")]
    [InlineData("let wrong:null=21;","TS2322")]
    [InlineData("let wrong:null=\"bad\";","TS2322")]
    [InlineData("let wrong:number|null=\"bad\";","TS2322")]
    [InlineData("let wrong:null=missing;","TS2304")]
    [InlineData("function f(n:number|null):number{return n;}","TS2322")]
    [InlineData("let o:{value:number}|null=null;let wrong:number=o.value;","TS18047")]
    [InlineData("let o:{value:number}|null=null;let wrong:number=o?.missing ?? 42;","TS2339")]
    public void WrongNullishContractsAreNotSilentlyAccepted(string source,string code) =>
        Assert.Contains(Compilation.Check("null-invalid.ts",source),diagnostic=>diagnostic.Code==code);

    [Fact]
    public void NullIsDistinctFromUndefinedAndNoLongerSurvivesNonNullAssertions()
    {
        Assert.True(CompilerTypes.IsAssignable(CompilerTypes.Null,CompilerTypes.Null));
        Assert.False(CompilerTypes.IsAssignable(CompilerTypes.Null,CompilerTypes.Undefined));
        Assert.False(CompilerTypes.IsAssignable(CompilerTypes.Null,CompilerTypes.Number));
        Assert.True(CompilerTypes.ContainsNullish(CompilerTypes.Union([CompilerTypes.Null,CompilerTypes.Number])));
        Assert.Equal(CompilerTypes.Number,CompilerTypes.NonNullAssertionResult(
            CompilerTypes.Union([CompilerTypes.Null,CompilerTypes.Number,CompilerTypes.Undefined])));
        Assert.Equal(CompilerTypes.Number,CompilerTypes.CoalescingResult(
            CompilerTypes.Union([CompilerTypes.Null,CompilerTypes.Number]),CompilerTypes.Number));
    }
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed product regression, not original TypeScript 7 acceptance.
public sealed class NullishCoalescingTests
{
    [Theory]
    [InlineData("let c:{value?:number}={};let n:number=c.value ?? 42;")]
    [InlineData("let c:{value?:number}={value:21};let n:number=(c.value ?? 0)*2;")]
    [InlineData("let c:{value?:number}={};let n:number=c.value ?? (21+21);")]
    [InlineData("let c:{value?:number}={};let n:number=c.value ?? c.value ?? 42;")]
    [InlineData("let c:{value?:number}={};let n:number=c.value ?? (true ? 42 : 0);")]
    [InlineData("function choose(c:{value?:number}):number{return c.value ?? 42;}")]
    [InlineData("let c:{label?:string}={};let s:string=c.label ?? \"fallback\";")]
    [InlineData("interface Op{run?(n:number):number}let op:Op={};let cb:(n:number)=>number=op.run ?? ((n:number):number=>n+n);")]
    [InlineData("let c:{value?:number}={};let n:number=(c.value ?? 42)*1;")]
    [InlineData("let c:{value?:number}={};let n:number=c.value ?? undefined ?? 42;")]
    public void NullableFallbacksAreTyped(string source) =>
        Assert.Empty(Compilation.Check("nullish.ts",source));

    [Theory]
    [InlineData("let c:{value?:number}={};let n:number=c.value ?? \"bad\";","TS2322")]
    [InlineData("let c:{value?:number}={};let n:number=c.value ?? missing;","TS2304")]
    [InlineData("let c:{label?:string}={};let n:number=c.label ?? \"fallback\";","TS2322")]
    [InlineData("let c:{value?:number}={};let n:number=c.value ?? false;","TS2322")]
    [InlineData("let c:{value?:number}={};let n:number=c.value ?? true;","TS2322")]
    public void InvalidFallbacksCannotBecomeAny(string source,string code) =>
        Assert.Contains(Compilation.Check("nullish-bad.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("let c:{value?:number}={};let n:number=c.value ?? 42 || 0;")]
    [InlineData("let c:{value?:number}={};let n:number=c.value || 0 ?? 42;")]
    [InlineData("let c:{value?:number}={};let n:number=c.value ?? true && false;")]
    public void MixedBareLogicalOperatorsAreRejected(string source) =>
        Assert.Contains(Compilation.Check("nullish-mixed.ts",source),d=>d.Code=="TSCS1001");
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded source semantics, not original upstream TS7 acceptance.
public sealed class OptionalChainTests
{
    [Theory]
    [InlineData("function choose(o:{value:number}|undefined):number{return o?.value ?? 42;}")]
    [InlineData("function choose(o:{value?:number}|undefined):number{return o?.value ?? 42;}")]
    [InlineData("function choose(o:{inner?:{value:number}}|undefined):number{return o?.inner?.value ?? 42;}")]
    [InlineData("function choose(o:{value:number}|undefined):number{return o?.[\"value\"] ?? 42;}")]
    [InlineData("function choose(o:number[]|undefined):number{return o?.[0] ?? 42;}")]
    [InlineData("function choose(o:{value:number}):number{return o?.value;}")]
    [InlineData("function choose(o:{value:number}|undefined):number{return o?.value! ?? 42;}")]
    [InlineData("function choose(o:{value:number}|undefined):number{return o?.value ?? o?.value ?? 42;}")]
    [InlineData("function choose(o:{value:number}|undefined):number{return o?.[\"value\"] ?? (21+21);}")]
    [InlineData("function choose(o:string|undefined):number{return o?.length ?? 42;}")]
    public void OptionalAccessHasCheckedResultAndReceiver(string source) =>
        Assert.Empty(Compilation.Check("optional-chain.ts",source));

    [Theory]
    [InlineData("function bad(o:{value:number}|undefined):string{return o?.value ?? 42;}","TS2322")]
    [InlineData("function bad(o:{value:number}|undefined):number{return o?.missing ?? 42;}","TS2339")]
    [InlineData("function bad(o:{value:number}|undefined):boolean{return o?.value ?? 42;}","TS2322")]
    [InlineData("function bad(o:{value:number}|undefined):number{return o?.value ?? \"bad\";}","TS2322")]
    [InlineData("function bad(o:{value:number}|undefined):number{return o?.[\"missing\"] ?? 42;}","TS2339")]
    [InlineData("function bad(o:{value:number}|undefined):number{return o.value;}","TS2339")]
    [InlineData("let o:{value:number}|undefined={value:21};o?.value=42;","TSCS1001")]
    [InlineData("let o:{value:number}|undefined={value:21};o?.value+=1;","TSCS1001")]
    [InlineData("let o:{value:number}|undefined={value:21};o?.value++;","TSCS1001")]
    [InlineData("let o:{value:number}|undefined={value:21};++o?.value;","TSCS1001")]
    [InlineData("function f(o:{value:number}|undefined):number{return o?.(21);}","TSCS1001")]
    public void MissingMembersAndInvalidOptionalTargetsFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("optional-chain-bad.ts",source),d=>d.Code==code);
}

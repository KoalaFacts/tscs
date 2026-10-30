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
    [InlineData("function choose(o:{inner:{value:number}}|undefined):number{return o?.inner.value ?? 42;}")]
    [InlineData("function choose(o:{inner:{value:number}}|undefined):number{return o?.[\"inner\"].value ?? 42;}")]
    [InlineData("function choose(o:{inner:{value:number}}|undefined):number{return o?.inner[\"value\"] ?? 42;}")]
    [InlineData("function choose(o:{items:number[]}|undefined):number{return o?.items[0] ?? 42;}")]
    [InlineData("function choose(o:{fn:(n:number)=>number}|undefined):number{return o?.fn(21) ?? 42;}")]
    [InlineData("function choose(o:{inner:{fn:(n:number)=>number}}|undefined):number{return o?.inner.fn(21) ?? 42;}")]
    [InlineData("function choose(o:{fn:(n:number)=>{value:number}}|undefined):number{return o?.fn(21).value ?? 42;}")]
    public void OptionalAccessHasCheckedResultAndReceiver(string source) =>
        Assert.Empty(Compilation.Check("optional-chain.ts",source));

    // A generated optional-chain guard must never shadow values captured
    // by an arrow in a call argument (expression or braced-body form).
    [Theory]
    [InlineData("function f(o:{fn:(g:()=>number)=>number}|undefined,__tscs_power_0:number):number|undefined{return o?.fn(():number=>__tscs_power_0);}")]
    [InlineData("function f(o:{fn:(g:()=>number)=>number}|undefined,__tscs_power_1:number):number|undefined{return o?.fn(():number=>{return __tscs_power_1;});}")]
    public void OptionalContinuationPreservesNestedArrowFreeNames(string source) =>
        Assert.Empty(Compilation.Check("optional-chain-capture.ts",source));

    [Theory]
    [InlineData("function bad(o:{value:number}|undefined):string{return o?.value ?? 42;}","TS2322")]
    [InlineData("function bad(o:{value:number}|undefined):number{return o?.missing ?? 42;}","TS2339")]
    [InlineData("function bad(o:{value:number}|undefined):boolean{return o?.value ?? 42;}","TS2322")]
    [InlineData("function bad(o:{value:number}|undefined):number{return o?.value ?? \"bad\";}","TS2322")]
    [InlineData("function bad(o:{inner:{value:number}}|undefined):number{return o?.inner.missing ?? 42;}","TS2339")]
    [InlineData("function bad(o:{inner:{value:number}}|undefined):string{return o?.inner.value ?? 42;}","TS2322")]
    [InlineData("function bad(o:{fn:(n:number)=>number}|undefined):number{return o?.fn(\"x\") ?? 42;}","TS2345")]
    [InlineData("function bad(o:{fn:(n:number)=>number}|undefined):number{return o?.fn() ?? 42;}","TS2554")]
    [InlineData("function bad(o:{inner?:{value:number}}|undefined):number{return o?.inner.value ?? 42;}","TS2339")]
    [InlineData("function bad(o:{value:number}|undefined):number{return o?.[\"missing\"] ?? 42;}","TS2339")]
    [InlineData("function bad(o:{value:number}|undefined):number{return o.value;}","TS2339")]
    [InlineData("let o:{value:number}|undefined={value:21};o?.value=42;","TSCS1001")]
    [InlineData("let o:{value:number}|undefined={value:21};o?.value+=1;","TSCS1001")]
    [InlineData("let o:{value:number}|undefined={value:21};o?.value++;","TSCS1001")]
    [InlineData("let o:{value:number}|undefined={value:21};++o?.value;","TSCS1001")]
    [InlineData("function f(o:{value:number}|undefined):number{return o?.(21);}","TSCS1001")]
    public void MissingMembersAndInvalidOptionalTargetsFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("optional-chain-bad.ts",source),d=>d.Code==code);

    // These are legal TypeScript but not yet supported by the bounded emitter.
    // Never accept them and silently emit a throwing ES2015 continuation.
    [Theory]
    [InlineData("function f(o:{inner:{value:number}}|undefined):number|undefined{return o?.inner!.value;}")]
    [InlineData("type F=(n:number)=>{value:number};function f(g:F|undefined):number{return g?.(21).value ?? 42;}")]
    [InlineData("function f(o:{inner:{fn:(n:number)=>number}}|undefined):number{return o?.inner.fn?.(21) ?? 42;}")]
    public void UnimplementedOptionalContinuationsNeverSilentlyEmit(string source) =>
        Assert.Contains(Compilation.Check("optional-chain-unimplemented.ts",source),d=>d.Code=="TSCS1001");
}

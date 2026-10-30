using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed bounded optional-call semantics, not original TS7 acceptance.
public sealed class OptionalCallTests
{
    [Theory]
    [InlineData("type F=(n:number)=>number;function use(f:F|undefined):number{return f?.(21) ?? 42;}")]
    [InlineData("interface Box{run?(n:number):number;}function use(b:Box):number{return b.run?.(21) ?? 42;}")]
    [InlineData("interface Box{run?(n:number):number;}function use(b:Box):number{return b[\"run\"]?.(21) ?? 42;}")]
    [InlineData("interface Box{run?(n:number):number;}function use(b:Box):number{return (b.run)?.(21) ?? 42;}")]
    [InlineData("type F=(n:number)=>number;function use(f:F|undefined):number{return (f)?.(21) ?? 42;}")]
    [InlineData("function use(f:(n:number)=>number):number{return f?.(21);}")]
    [InlineData("type F=(n:number)=>number;function use(f:F|undefined):number{return f?.(21) ?? f?.(21) ?? 42;}")]
    [InlineData("type F=(n:number,...values:number[])=>number;function use(f:F|undefined,items:number[]):number{return f?.(21,...items) ?? 42;}")]
    [InlineData("type F=(n:number)=>number;function use(f:F|undefined):number{return f?.(21) ?? 42;}")]
    public void OptionalCallsKeepFunctionSignaturesAndUndefined(string source) =>
        Assert.Empty(Compilation.Check("optional-call.ts",source));

    [Theory]
    [InlineData("type F=(n:number)=>number;function bad(f:F|undefined):string{return f?.(21) ?? 42;}","TS2322")]
    [InlineData("type F=(n:number)=>number;function bad(f:F|undefined):number{return f?.(\"x\") ?? 42;}","TS2345")]
    [InlineData("type F=(n:number)=>number;function bad(f:F|undefined):number{return f?.() ?? 42;}","TS2554")]
    [InlineData("interface Box{run?(n:number):number;}function bad(b:Box):number{return b.run?.(\"x\") ?? 42;}","TS2345")]
    [InlineData("function bad(n:number):number{return n?.(21) ?? 42;}","TSCS1001")]
    [InlineData("function bad(o:{run:(n:number)=>number}|undefined):number{return o?.run?.(21) ?? 42;}","TSCS1001")]
    [InlineData("function bad(o:{run:(n:number)=>number}|undefined):number{return (o?.run)?.(21) ?? 42;}","TSCS1001")]
    [InlineData("type F=(n:number)=>number;function bad(f:F|undefined):number{return f?.(21).value ?? 42;}","TSCS1001")]
    public void InvalidOrUnimplementedOptionalCallsFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("optional-call-invalid.ts",source),d=>d.Code==code);
}

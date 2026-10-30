using Tscs;
using Xunit;

namespace Tscs.Tests;

// Product semantics only; original upstream TS7 acceptance is tracked separately.
public sealed class NonNullAssertionTests
{
    [Theory]
    [InlineData("let o:{value?:number}={value:21};let n:number=o.value!;")]
    [InlineData("let o:{value?:number}={value:21};let n:number=o[\"value\"]!;")]
    [InlineData("let o:{nested?:{value:number}}={nested:{value:42}};let n:number=o.nested!.value;")]
    [InlineData("interface Op{run?(n:number):number}let o:Op={run:(n:number)=>n+n};let n:number=o.run!(21);")]
    [InlineData("interface Op{run?(n:number):number}let o:Op={run:(n:number)=>n+n};let f:(n:number)=>number=o.run!;")]
    [InlineData("let value:number|undefined=42;let n:number=value!;")]
    [InlineData("let value:number|undefined=42;let n:number=value!!;")]
    [InlineData("let o:{value?:number}={value:21};let n:number=o.value!+21;")]
    [InlineData("let o:{value?:number}={value:21};let n:number=o.value! ?? 42;")]
    [InlineData("let o:{run?:(n:number)=>number}={run:(n:number)=>n+n};let n:number=o[\"run\"]!(21);")]
    [InlineData("function unwrap(x:number|undefined):number{return x!;}")]
    [InlineData("let n:number=42;let m:number=n!;")]
    [InlineData("let value:any=21;let n:number=value!;")]
    [InlineData("let o:{value?:number}={value:21};let n:number=(o.value!) + 21;")]
    public void NonNullAssertionsUseCheckedOperandTypes(string source) =>
        Assert.Empty(Compilation.Check("non-null.ts",source));

    [Theory]
    [InlineData("let o:{value?:number}={};let n:string=o.value!;","TS2322")]
    [InlineData("let o:{value?:number}={};let n:number=o.missing!;","TS2339")]
    [InlineData("let o:{label?:string}={};let n:number=o.label!;","TS2322")]
    [InlineData("let n:number=missing!;","TS2304")]
    [InlineData("let o:{value?:number}={};let n:number=o.value;","TS2322")]
    [InlineData("interface Op{run?(n:number):number}let o:Op={};o.run(21);","TSCS1001")]
    [InlineData("let o:{run?:(n:number)=>number}={};o.run!(\"bad\");","TS2345")]
    [InlineData("let o:{run?:(n:number)=>number}={};o.run!();","TS2554")]
    [InlineData("let o:{value?:number}={};o.value!(21);","TSCS1001")]
    public void AssertionDoesNotSuppressUnrelatedErrors(string source,string code) =>
        Assert.Contains(Compilation.Check("non-null-invalid.ts",source),d=>d.Code==code);
}

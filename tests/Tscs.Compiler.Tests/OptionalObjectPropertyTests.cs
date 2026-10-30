using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed implementation checks, not original TS7 harness acceptance.
public sealed class OptionalObjectPropertyTests
{
    [Theory]
    [InlineData("interface Option { value?: number; label: string } let item: Option = { label: \"ok\" };")]
    [InlineData("type Option = { value?: number }; const item: Option = {};")]
    [InlineData("type Option = { value?: number }; const item: Option = { value: 42 };")]
    [InlineData("let item: { value?: number } = {}; item.value = 42;")]
    [InlineData("let item: { value?: number } = { value: undefined };")]
    [InlineData("let item: { value?: number } = {}; let maybe: number | undefined = item.value;")]
    [InlineData("let item: { value?: number } = {}; type Value = typeof item.value; let result: Value = undefined;")]
    [InlineData("interface Base { value?: number } interface Child extends Base { value: number } let child: Child = { value: 42 };")]
    [InlineData("interface Base { value?: number } interface Child extends Base { label: string } let child: Child = { label: \"yes\" };")]
    [InlineData("type Box = { value?: number }; type Keys = keyof Box; let key: Keys = \"value\";")]
    [InlineData("type Box = { value?: number }; type Value = Box[\"value\"]; let maybe: Value = undefined;")]
    [InlineData("type Box = { value?: number }; type Required = { value: number }; type Both = Box & Required; let both: Both = { value: 42 };")]
    [InlineData("let source: { value?: number } = {}; let target: { value?: number } = source;")]
    [InlineData("let data: { readonly value?: number } = {}; let maybe: number | undefined = data.value;")]
    [Theory]
    [InlineData("interface Op{run?(n:number):number}const op:Op={};")]
    [InlineData("interface Op{run?(n:number):number}const op:Op={run:(n:number)=>n+n};")]
    [InlineData("type Op={run?(n:number):number};let op:Op={};")]
    [InlineData("interface Base{run?(n:number):number}interface Child extends Base{label:string}const item:Child={label:\"ok\"};")]
    [InlineData("interface Base{run?(n:number):number}interface Child extends Base{run(n:number):number}const item:Child={run:(n:number)=>n+n};")]
    [InlineData("type Op={run?(n:number):number};type Keys=keyof Op;let key:Keys=\"run\";")]
    [InlineData("type Op={run?(n:number):number};type Run=Op[\"run\"];let callback:Run=undefined;")]
    [InlineData("interface Op{run?(n:number):number}let op:Op={};let maybe:Op[\"run\"]=op.run;")]
    public void OptionalMethodsUseCheckedCallableContracts(string source) =>
        Assert.Empty(Compilation.Check("optional-method.ts",source));

    public void CheckedOptionalContractsCompile(string source) =>
        Assert.Empty(Compilation.Check("optional-data.ts",source));

    [Theory]
    [InlineData("let item: { value?: number } = { value: \"bad\" };","TS2322")]
    [InlineData("let item: { value?: number } = { extra: 42 };","TS2353")]
    [InlineData("let item: { value?: number } = {}; let result: number = item.value;","TS2322")]
    [InlineData("let item: { value?: number } = {}; item.value = \"wrong\";","TS2322")]
    [InlineData("let item: { value?: number } = {}; let required: { value: number } = item;","TS2741")]
    [InlineData("interface Parent { value: number } interface Child extends Parent { value?: number }","TS2430")]
    [InlineData("let item: { readonly value?: number } = {}; item.value = 42;","TS2540")]
    [InlineData("interface Op{run?(n:number):number}const op:Op={run:42};","TS2322")]
    [InlineData("interface Op{run?(n:number):number}const op:Op={extra:42};","TS2353")]
    [InlineData("interface Base{run(n:number):number}interface Child extends Base{run?(n:number):number}","TS2430")]
    [InlineData("interface Op{run?(n:number):number}const op:Op={};op.run(21);","TSCS1001")]
    [InlineData("interface Op{run?(n:number):number}const op:Op={run:(n:string)=>42};","TS2322")]
    [InlineData("interface Op{run?(n:number):number}const op:Op={};let cb:(n:number)=>number=op.run;","TS2322")]
    [InlineData("let item: { value?: number } = {}; item.missing;","TS2339")]
    [InlineData("let item: { value?: number } = { value: 1, extra: 2 };","TS2353")]
    public void InvalidOptionalContractsFail(string source,string code) =>
        Assert.Contains(Compilation.Check("optional-bad.ts",source),d=>d.Code==code);
}

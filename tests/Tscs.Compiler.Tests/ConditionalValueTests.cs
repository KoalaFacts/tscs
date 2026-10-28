using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ConditionalValueTests
{
    [Theory]
    [InlineData("let n: number = true ? 42 : 0;")]
    [InlineData("function choose(flag: boolean): number { return flag ? 21 : 0; } let n: number = choose(true);")]
    [InlineData("function choose(a: boolean,b: boolean): number { return a ? 1 : b ? 2 : 3; } let n: number = choose(false,true);")]
    [InlineData("function calculate(flag: boolean): number { return (flag ? 21 : 0) * 2; } let n: number = calculate(true);")]
    [InlineData("let o: { value: number } = true ? { value: 21 } : { value: 42 }; let n: number = o.value;")]
    [InlineData("let list: number[] = false ? [1,2] : [21,42]; let n: number = list[0];")]
    [InlineData("const result = true ? 21 : \"bad\"; let value: number | string = result;")]
    [InlineData("let n: number = true || false ? 42 : 0;")]
    [InlineData("let n: number = true ? (false ? 1 : 42) : 0;")]
    [InlineData("type Result = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function read(r: Result): number { return r.kind === \"ok\" ? r.value : 0; } let n: number = read({ kind: \"ok\", value: 42 });")]
    [InlineData("type Input = number | string; function score(v: Input): number { return typeof v === \"number\" ? v * 2 : 0; } let n: number = score(21);")]
    [InlineData("type Label = \"ready\" | \"done\"; function label(flag: boolean): Label { return flag ? \"ready\" : \"done\"; }")]
    [InlineData("let n: number = 21 > 0 ? 42 : 0;")]
    [InlineData("const v = false ? { count: 1 } : { count: 42 }; let n: number = v.count;")]
    [InlineData("let flag: boolean = false ? true : false;")]
    public void ConditionalValuesCompileThroughTheSharedChecker(string source) =>
        Assert.Empty(Compilation.Check("conditional.ts",source));

    [Theory]
    [InlineData("let n: number = true ? \"bad\" : 0;", "TS2322")]
    [InlineData("function wrong(flag: boolean): number { return flag ? 0 : \"bad\"; }", "TS2322")]
    [InlineData("let n: number = true ? false : 42;", "TS2322")]
    [InlineData("let value: string = false ? \"ok\" : 42;", "TS2322")]
    [InlineData("let n: number = true ? { value: 1 } : 42;", "TS2322")]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function wrong(p: Packet): number { return p.kind === \"ok\" ? p.message : 0; }", "TS2339")]
    [InlineData("let shape: { value: number } = true ? { value: 21, extra: 1 } : { value: 42 };", "TS2353")]
    [InlineData("let n: number = true ? missingIdentifier : 0;", "TS2304")]
    public void UnsafeConditionalValuesAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("conditional.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("const x = 1 ? 21 : 0;")]
    [InlineData("const x = true ? 21;")]
    [InlineData("let x: number = true ? 1 : false ? 2;")]
    public void UnsupportedOrMalformedConditionsFailClosed(string source) =>
        Assert.NotEmpty(Compilation.Check("conditional.ts",source));
}

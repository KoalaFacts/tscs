using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed compiler checks, not original TS7 upstream acceptance.
public sealed class ReadonlyArrayTests
{
    [Theory]
    [InlineData("let values: readonly number[] = [21,21]; let n: number = values[0];")]
    [InlineData("let values: readonly number[] = [21,21]; let n: number = values.length;")]
    [InlineData("type Values = readonly number[]; let values: Values = [21,21];")]
    [InlineData("let mutable: number[] = [21]; let view: readonly number[] = mutable;")]
    [InlineData("let values: readonly string[] = [\"a\"]; let s: string = values[0];")]
    [InlineData("let values: readonly (number | string)[] = [21,\"a\"]; let n: number | string = values[1];")]
    [InlineData("let values: readonly number[][] = [[21]]; values[0][0] = 42;")]
    [InlineData("function sum(values: readonly number[]): number { let n: number = 0; for (const item of values) { n += item; } return n; } let result: number = sum([21,21]);")]
    public void ReadonlyArraysAllowReadsAndMutableSources(string source) =>
        Assert.Empty(Compilation.Check("readonly-array.ts",source));

    [Theory]
    [InlineData("let values: readonly number[] = [21]; values[0] = 42;", "TS2542")]
    [InlineData("let values: readonly number[] = [21]; values[0] += 1;", "TS2542")]
    [InlineData("let values: readonly number[] = [21]; ++values[0];", "TS2542")]
    [InlineData("let values: readonly number[] = [21]; values.length = 0;", "TS2540")]
    [InlineData("let values: readonly number[] = [21]; let writable: number[] = values;", "TS4104")]
    [InlineData("let values: readonly number[] = [21]; let name: string = values[0];", "TS2322")]
    [InlineData("let values: readonly number[][] = [[21]]; values[0] = [42];", "TS2542")]
    public void ReadonlyArraysRejectUnsafeMutations(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-readonly-array.ts",source),d=>d.Code==code);
}

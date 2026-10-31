using Tscs;
using Xunit;

namespace Tscs.Tests;

// Real source-backed semantics, not original TS7 upstream acceptance.
public sealed class GenericArrayTypeTests
{
    [Theory]
    [InlineData("let xs: Array<number> = [21]; xs[0] = 42; let n: number = xs[0];")]
    [InlineData("let xs: ReadonlyArray<number> = [21]; let n: number = xs[0];")]
    [InlineData("type Data = ReadonlyArray<number>; let xs: Data = [21]; let n: number = xs.length;")]
    [InlineData("let xs: ReadonlyArray<number | string> = [21,\"a\"]; let n: number | string = xs[1];")]
    [InlineData("let xs: ReadonlyArray<Array<number>> = [[21]]; xs[0][0] = 42;")]
    [InlineData("let xs: Array<ReadonlyArray<number>> = [[21]]; let n: number = xs[0][0];")]
    [InlineData("let xs: ReadonlyArray<number>[] = [[21]]; xs[0] = [42];")]
    [InlineData("function total(xs: ReadonlyArray<number>): number { let sum: number = 0; for (const x of xs) { sum += x; } return sum; } let n: number = total([21,21]);")]
    [InlineData("let xs: Array<Array<number>>=[[21]]; xs[0][0]=42;")]
    [InlineData("let xs: ReadonlyArray<number>= [21]; let n: number=xs[0];")]
    [InlineData("const f=(n:number):Array<number>=>[n]; let x:number=f(21)[0];")]
    [InlineData("const f=(n:number):Array<ReadonlyArray<number>>=>[[n]]; let x:number=f(21)[0][0];")]
    public void IntrinsicGenericsShareTheExistingArrayPipeline(string source) =>
        Assert.Empty(Compilation.Check("generic-array.ts",source));

    [Theory]
    [InlineData("let xs: ReadonlyArray<number> = [21]; xs[0] = 22;", "TS2542")]
    [InlineData("let xs: ReadonlyArray<number> = [21]; xs.length = 0;", "TS2540")]
    [InlineData("let xs: ReadonlyArray<number> = [21]; let ys: Array<number> = xs;", "TS4104")]
    [InlineData("let xs: Array<number> = [21]; let s: string = xs[0];", "TS2322")]
    [InlineData("let xs: Array<ReadonlyArray<number>> = [[21]]; xs[0][0] = 22;", "TS2542")]
    [InlineData("let xs: ReadonlyArray<Array<number>> = [[21]]; xs[0] = [22];", "TS2542")]
    [InlineData("let xs: Array<number,string> = [21];", "TSCS1001")]
    [InlineData("let xs: readonly Array<number> = [21];", "TSCS1001")]
    [InlineData("type Array = number; let xs: Array<number> = [21];", "TS2315")]
    public void IntrinsicGenericsRejectInvalidWritesOrUnsupportedForms(string source,string expected) =>
        Assert.Contains(Compilation.Check("invalid-generic-array.ts",source), d=>d.Code==expected);
}

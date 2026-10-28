using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ArrayIterationTests
{
    [Theory]
    [InlineData("const values = [20, 21]; values[0] = 21; let result: number = values[0] + values[1];")]
    [InlineData("let values: number[] = [1, 2, 3]; let size: number = values.length; values[1] = 21;")]
    [InlineData("const xs = [1, \"text\"]; let first: number | string = xs[0];")]
    [InlineData("let empty: number[] = []; let size: number = empty.length;")]
    [InlineData("let matrix: number[][] = [[1, 2], [3, 4]]; matrix[0][1] = 21; let n: number = matrix[0][1];")]
    [InlineData("const objects = [{ value: 21 }, { value: 22 }]; let n: number = objects[0].value;")]
    [InlineData("let objects: { value: number }[] = [{ value: 21 }, { value: 22 }]; objects[0].value = 42;")]
    [InlineData("let objects: { value: number }[] = []; objects = [{ value: 42 }];")]
    [InlineData("function head(xs: number[]): number { return xs[0]; } let n: number = head([42]);")]
    [InlineData("function add(xs: number[]): number { let total: number = 0; for (const x of xs) { total = total + x; } return total; } let n: number = add([21, 21]);")]
    [InlineData("let sum: number = 0; for (let value of [20, 22]) { sum = sum + value; } let result: number = sum;")]
    [InlineData("const xs = [1, 2]; { let x: number = 0; for (const x of xs) { x; } x = 1; }")]
    [InlineData("const xs: number[][] = [[21, 21]]; for (const pair of xs) { let n: number = pair[0] + pair[1]; }")]
    [InlineData("const data = { for: 21 }; let x: number = data.for;")]
    [InlineData("for (let i = 0; i < 2; i++) {}")]
    [InlineData("const xs = [1]; xs[0]++;")]
    public void ArrayProgramsFollowTheSharedCompilerPipeline(string source) =>
        Assert.Empty(Compilation.Check("arrays.ts", source));

    [Theory]
    [InlineData("const xs = [1, 2]; xs[0] = \"bad\";", "TS2322")]
    [InlineData("const xs = [1, 2]; xs.missing;", "TS2339")]
    [InlineData("const xs = [1, 2]; xs[\"0\"];", "TSCS1001")]
    [InlineData("const xs = [1, 2]; for (const item of xs) { item = 3; }", "TS2588")]
    [InlineData("for (const item of 42) { item; }", "TSCS1001")]
    [InlineData("let value: number; for (const item of [42]) { value = item; } value;", "TSCS1001")]
    [InlineData("const xs = [];", "TSCS1001")]
    [InlineData("let xs: number[] = [1, \"text\"];", "TS2322")]
    [InlineData("let matrix: number[][] = [1, 2];", "TS2322")]
    [InlineData("let xs: { value: number }[] = [{ value: \"bad\" }];", "TS2322")]
    [InlineData("let xs: { value: number }[] = [{ value: 1, extra: true }];", "TS2353")]
    [InlineData("const xs = [, 1];", "TSCS1001")]
    [InlineData("const xs = [1, 2;", "TSCS1001")]
    [InlineData("let xs: [number, string] = [1, \"x\"];", "TSCS1001")]
    [InlineData("const xs = [\"text\"]; xs[0]++;", "TSCS1001")]
    public void UnsupportedOrIncorrectArrayProgramsDoNotSucceed(string source, string diagnostic) =>
        Assert.Contains(Compilation.Check("arrays.ts",source), d=>d.Code==diagnostic);

    [Fact]
    public void IndexTypeErrorIsAssociatedWithSourceBracket()
    {
        const string source="/* 😀 */ const xs = [42]; xs[\"wrong\"];";
        var error=Assert.Single(Compilation.Check("arrays.ts",source));
        Assert.Equal("TSCS1001",error.Code);
        Assert.Equal(source.LastIndexOf('['),error.Start);
    }

    [Fact]
    public void ArrayNestingIsBoundedWithoutRecursiveStackFailure()
    {
        var literal=string.Concat(Enumerable.Repeat("[",70))+"1"+new string(']',70);
        var type="number"+string.Concat(Enumerable.Repeat("[]",70));
        Assert.Contains(Compilation.Check("deep.ts","const x = "+literal+";"),d=>d.Code=="TSCS1001");
        Assert.Contains(Compilation.Check("deep.ts","let x: "+type+";"),d=>d.Code=="TSCS1001");
    }
}

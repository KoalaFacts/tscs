using Tscs;
using Xunit;

namespace Tscs.Tests;

// A deliberately bounded compiler feature: number + number, including chained
// additions and number-typed references. This is not all binary expressions.
public class NumericAdditionTests
{
    [Theory]
    [InlineData("let n = 1 + 2;")]
    [InlineData("let n: number = 1 + 2;")]
    [InlineData("const n = 1 + 2 + 3;")]
    [InlineData("let x: number = 1; let y: number = x + 2;")]
    [InlineData("let x: number = 1; let y: number = 2 + x;")]
    [InlineData("let n: any = 1 + 2;")]
    [InlineData("let n: number = 1; n = n + 2;")]
    public void NumberAdditionChecksWithoutErrors(string source) =>
        Assert.Empty(Compilation.Check("test.ts", source));

    [Fact]
    public void AdditionInfersNumberAndPreservesMismatchDiagnostic()
    {
        var diagnostic = Assert.Single(Compilation.Check("test.ts", "let n: string = 1 + 2;"));
        Assert.Multiple(
            () => Assert.Equal("TS2322", diagnostic.Code),
            () => Assert.Equal(4, diagnostic.Start),
            () => Assert.Equal(1, diagnostic.Length));
    }

    [Theory]
    [InlineData("let x = \"a\" + 2;")]
    [InlineData("let x = true + 2;")]
    [InlineData("let x: any = 1; let y = x + 2;")]
    public void NonNumericAdditionIsNotClaimedAsImplemented(string source) =>
        Assert.Contains(Compilation.Check("test.ts", source), d => d.Code == "TSCS1001");

    [Theory]
    [InlineData("+1;")]
    [InlineData("let x = +1;")]
    [InlineData("let x = 1 + +2;")]
    public void UnaryPlusRemainsUnsupported(string source) =>
        Assert.Contains(Compilation.Check("test.ts", source), d => d.Code == "TSCS1001");
}

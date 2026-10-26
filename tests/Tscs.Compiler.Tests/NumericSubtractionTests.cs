using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded binary numeric subtraction; these are real C# compiler assertions,
// not original TS7 compiler-runner acceptance or empty compatibility stubs.
public sealed class NumericSubtractionTests
{
    [Theory]
    [InlineData("let n = 7 - 3;")]
    [InlineData("let n: number = 7 - 3;")]
    [InlineData("const n = 10 - 3 - 2;")]
    [InlineData("let n = 1 + 8 - 3;")]
    [InlineData("let n = 9 - 3 + 1;")]
    [InlineData("let n = 10 - 2 * 3;")]
    [InlineData("let n = 10 * 2 - 3;")]
    [InlineData("let x: number = 3; let n = x - 2;")]
    [InlineData("let n: number = 3; n = n - 1;")]
    public void NumericDifferenceTypesAreAccepted(string source) =>
        Assert.Empty(Compilation.Check("test.ts", source));

    [Fact]
    public void BinarySubtractionStillChecksAssignmentTypes()
    {
        var d = Assert.Single(Compilation.Check("test.ts", "let s: string = 7 - 3;"));
        Assert.Equal("TS2322", d.Code);
        Assert.Equal(4, d.Start);
    }

    [Theory]
    [InlineData("let n = 7 - \"x\";")]
    [InlineData("let n = true - 2;")]
    [InlineData("let n: any = 1; let m = n - 1;")]
    public void NonNumberOperandsStayUnsupported(string source) =>
        Assert.Contains(Compilation.Check("test.ts", source), d => d.Code == "TSCS1001");

    [Theory]
    [InlineData("let n = 2 -- 1;")]
    [InlineData("let n = 2--1;")]
    [InlineData("let n = --1;")]
    public void DecrementFormsRemainUnsupported(string source) =>
        Assert.Contains(Compilation.Check("test.ts", source), d => d.Code == "TSCS1001");

    [Theory]
    [InlineData("let n = -3;")]
    [InlineData("let n: number = -3;")]
    [InlineData("let n = 2 - -1;")]
    [InlineData("let n = - -1;")]
    [InlineData("let n = 2 * -3;")]
    [InlineData("let n = -3 * 2 + 5;")]
    [InlineData("let n: number = -1; n = -n;")]
    [InlineData("let n = -1; let m = -n;")]
    public void UnaryNumericMinusIsAccepted(string source) =>
        Assert.Empty(Compilation.Check("test.ts", source));

    [Theory]
    [InlineData("let n = -\"x\";")]
    [InlineData("let n = -true;")]
    [InlineData("let n: any = 1; let m = -n;")]
    public void UnaryMinusNonNumericOperandsRemainUnsupported(string source) =>
        Assert.Contains(Compilation.Check("test.ts", source), d => d.Code == "TSCS1001");

    [Fact]
    public void UnaryMinusRetainsMissingNameDiagnostic() =>
        Assert.Equal("TS2304", Assert.Single(Compilation.Check("test.ts", "let n = -missing;")).Code);

    [Fact]
    public void LongUnaryMinusChainDoesNotOverflowChecker() =>
        Assert.Empty(Compilation.Check("test.ts", "let n = " + string.Concat(Enumerable.Repeat("- ", 20000)) + "1;"));

    [Fact]
    public void SubtractionAndAdditionAssociateLeftToRight()
    {
        var scanner = new Scanner("test.ts", "let n = 9 - 3 + 2;");
        var parser = new Parser("test.ts", scanner.Scan());
        var v = Assert.IsType<Variable>(Assert.Single(parser.Parse().Statements));
        Assert.Empty(parser.Diagnostics);
        var sum = Assert.IsType<ValueBinary>(v.Initializer);
        Assert.Equal("+", sum.Operator.Text);
        var difference = Assert.IsType<ValueBinary>(sum.Left);
        Assert.Equal("-", difference.Operator.Text);
    }

    [Fact]
    public void MultiplicationBindsBeforeSubtraction()
    {
        var scanner = new Scanner("test.ts", "let n = 9 - 3 * 2;");
        var parser = new Parser("test.ts", scanner.Scan());
        var v = Assert.IsType<Variable>(Assert.Single(parser.Parse().Statements));
        Assert.Empty(parser.Diagnostics);
        var difference = Assert.IsType<ValueBinary>(v.Initializer);
        Assert.Equal("-", difference.Operator.Text);
        var product = Assert.IsType<ValueBinary>(difference.Right);
        Assert.Equal("*", product.Operator.Text);
    }

    [Fact]
    public void TwoUnresolvedSubtractionOperandsRetainBothDiagnostics()
    {
        var ds = Compilation.Check("test.ts", "let n = missingLeft - missingRight;");
        Assert.Equal(new[] { "TS2304", "TS2304" }, ds.Select(d => d.Code));
        Assert.Equal(new[] { 8, 22 }, ds.Select(d => d.Start));
    }
}

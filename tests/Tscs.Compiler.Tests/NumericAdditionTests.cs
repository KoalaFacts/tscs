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
    public void NumericLiteralCompoundAdditionRetainsExistingValidBehavior() =>
        Assert.Empty(Compilation.Check("test.ts", "let n: 1 = 1; n += 1;"));

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
    [InlineData("let x = true + 2;")]
    [InlineData("let x = 2 + false;")]
    [InlineData("let x = true + false;")]
    public void UnsupportedNonStringAdditionRemainsExplicitlyRejected(string source) =>
        Assert.Contains(Compilation.Check("test.ts", source), d => d.Code == "TSCS1001");

    // Genuine positive compiler assertions: source is parsed and type-checked.
    // This bounded numeric unary '+' does not imply any/JS coercion or emit.
    [Theory]
    [InlineData("+1;")]
    [InlineData("let x = +1;")]
    [InlineData("let x = 1 + +2;")]
    [InlineData("let x = 2 * +3;")]
    [InlineData("let x = - +3;")]
    [InlineData("let x = + -3;")]
    [InlineData("let x = + +3;")]
    [InlineData("let x: number = 1; x = +x;")]
    public void NumericUnaryPlusChecksWithoutErrors(string source) =>
        Assert.Empty(Compilation.Check("test.ts", source));

    [Theory]
    [InlineData("let x = +true;")]
    [InlineData("let x = +\"s\";")]
    [InlineData("let x: any = 1; let y = +x;")]
    public void UnaryPlusNonNumericOperandsStayExplicitlyUnsupported(string source) =>
        Assert.Contains(Compilation.Check("test.ts", source), d => d.Code == "TSCS1001");

    [Fact]
    public void UnaryPlusRetainsMissingIdentifierDiagnostic() =>
        Assert.Equal("TS2304", Assert.Single(Compilation.Check("test.ts", "let n = +missing;")).Code);

    [Fact]
    public void UnaryPlusPreservesAssignmentMismatch() =>
        Assert.Equal("TS2322", Assert.Single(Compilation.Check("test.ts", "let s: string = +1;")).Code);

    [Fact]
    public void UnaryPlusPrecedesMultiplicationInAst()
    {
        var scanner = new Scanner("test.ts", "let n = +3 * 2;");
        var parser = new Parser("test.ts", scanner.Scan());
        var variable = Assert.IsType<Variable>(Assert.Single(parser.Parse().Statements));
        Assert.Empty(parser.Diagnostics);
        var product = Assert.IsType<ValueBinary>(variable.Initializer);
        Assert.Equal("*", product.Operator.Text);
        var positive = Assert.IsType<ValueUnary>(product.Left);
        Assert.Equal("+", positive.Operator.Text);
        Assert.Equal("3", Assert.IsType<ValueAtom>(positive.Operand).Token.Text);
    }

    [Fact]
    public void LongUnaryPlusChainAvoidsParserAndCheckerRecursion() =>
        Assert.Empty(Compilation.Check("test.ts", "let n = " + string.Concat(Enumerable.Repeat("+ ", 20000)) + "1;"));

    [Fact]
    public void LeftDeepAdditionKeepsBothUnresolvedNamesInSourceOrder()
    {
        var diagnostics=Compilation.Check("test.ts", "let n = 1 + firstMissing + secondMissing;");
        Assert.Equal(new[] { "TS2304", "TS2304" }, diagnostics.Select(d=>d.Code));
        Assert.Equal(new[] { 12, 27 }, diagnostics.Select(d=>d.Start));
    }

    [Fact]
    public void EarlierUnsupportedOperandStopsBeforeLaterUnresolvedName()
    {
        var diagnostic=Assert.Single(Compilation.Check("test.ts", "let n = true + 2 + missing;"));
        Assert.Equal("TSCS1001", diagnostic.Code);
    }

}

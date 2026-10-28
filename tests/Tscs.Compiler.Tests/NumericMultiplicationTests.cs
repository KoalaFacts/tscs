using Tscs;
using Xunit;

namespace Tscs.Tests;

// Compiler feature boundary: numeric multiplication with TypeScript's
// multiplicative precedence. Unsupported operand and malformed-star guards remain.
public sealed class NumericMultiplicationTests
{
    [Theory]
    [InlineData("let n = 2 * 3;")]
    [InlineData("let n: number = 2 * 3;")]
    [InlineData("const n = 2 * 3 * 4;")]
    [InlineData("let a: number = 5; let n = a * 3;")]
    [InlineData("let a: number = 5; let n = 3 * a;")]
    [InlineData("let n = 1 + 2 * 3;")]
    [InlineData("let n = 2 * 3 + 4;")]
    [InlineData("let n = 1 + 2 * 3 + 4 * 5;")]
    [InlineData("let n: number = 1; n = n * 2;")]
    public void NumericProductsAreChecked(string source) =>
        Assert.Empty(Compilation.Check("test.ts",source));

    [Fact]
    public void MultiplicationRetainsAssignmentTypeError()
    {
        var diagnostic=Assert.Single(Compilation.Check("test.ts","let n: string = 2 * 3;"));
        Assert.Equal("TS2322",diagnostic.Code);
        Assert.Equal(4,diagnostic.Start);
    }

    [Theory]
    [InlineData("let n = 2 * \"x\";")]
    [InlineData("let n = true * 2;")]
    [InlineData("let a: any = 2; let n = a * 3;")]
    public void UnsupportedOperandsNeverPass(string source) =>
        Assert.Contains(Compilation.Check("test.ts",source),d=>d.Code=="TSCS1001");

    [Theory]
    [InlineData("let n = *2;")]
    [InlineData("let n = 2 *** 3;")]
    public void UnsupportedStarFormsRemainFailClosed(string source) =>
        Assert.Contains(Compilation.Check("test.ts",source),d=>d.Code=="TSCS1001");

    [Fact]
    public void MultiplicationBindsMoreTightlyThanAddition()
    {
        var scanner=new Scanner("test.ts","let n = 1 + 2 * 3;");
        var parser=new Parser("test.ts",scanner.Scan());
        var variable=Assert.IsType<Variable>(Assert.Single(parser.Parse().Statements));
        Assert.Empty(parser.Diagnostics);
        var root=Assert.IsType<ValueBinary>(variable.Initializer);
        Assert.Equal("+",root.Operator.Text);
        var right=Assert.IsType<ValueBinary>(root.Right);
        Assert.Equal("*",right.Operator.Text);
    }

    [Fact]
    public void ProductBeforeSumKeepsTheProductOnTheLeft()
    {
        var scanner=new Scanner("test.ts","let n = 2 * 3 + 4;");
        var parser=new Parser("test.ts",scanner.Scan());
        var variable=Assert.IsType<Variable>(Assert.Single(parser.Parse().Statements));
        Assert.Empty(parser.Diagnostics);
        var root=Assert.IsType<ValueBinary>(variable.Initializer);
        Assert.Equal("+",root.Operator.Text);
        var left=Assert.IsType<ValueBinary>(root.Left);
        Assert.Equal("*",left.Operator.Text);
    }

    [Fact]
    public void UnresolvedNamesKeepSourceOrderAcrossMultiplication()
    {
        var diagnostics=Compilation.Check("test.ts","let n = missingOne * missingTwo;");
        Assert.Equal(new[]{"TS2304","TS2304"}, diagnostics.Select(d=>d.Code));
        Assert.Equal(new[]{8,21},diagnostics.Select(d=>d.Start));
    }
}

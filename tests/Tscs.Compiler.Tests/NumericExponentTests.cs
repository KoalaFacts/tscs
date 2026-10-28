using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class NumericExponentTests
{
    [Theory]
    [InlineData("let n: number = 2 ** 3 ** 2;")]
    [InlineData("let n: number = (2 ** 3) ** 2;")]
    [InlineData("let n: number = 2 ** -2;")]
    [InlineData("let n: number = (-2) ** 2;")]
    [InlineData("let n: number = -(2 ** 2);")]
    [InlineData("let n: number = 3 * 2 ** 3 + 1;")]
    [InlineData("let n: number = 2 ** 3 * 4;")]
    [InlineData("let n: number = 1 << 2 ** 2;")]
    [InlineData("function power(a: number,b: number): number { return a ** b; } let n: number = power(2,3);")]
    [InlineData("let n: number = 2; n **= 5;")]
    [InlineData("type P = { value: number }; let p: P = { value: 3 }; p.value **= 2;")]
    [InlineData("let values: number[] = [2,3]; values[0] **= values[1];")]
    [InlineData("for (let i: number = 2; i < 16; i **= 2) {}")]
    [InlineData("let n: number = 2 ** 3 / 4 % 3;")]
    public void NumericExponentCompiles(string source) =>
        Assert.Empty(Compilation.Check("exponent.ts",source));

    [Theory]
    [InlineData("let n: string = 2 ** 3;", "TS2322")]
    [InlineData("let n: boolean = 2 ** 3;", "TS2322")]
    [InlineData("const n: number = 2; n **= 3;", "TS2588")]
    [InlineData("const n: number = 2; n **= -1;", "TS2588")]
    [InlineData("let n: number = true ** 2;", "TSCS1001")]
    [InlineData("let n: number = 2 ** false;", "TSCS1001")]
    [InlineData("let n: number = -2 ** 2;", "TSCS1001")]
    [InlineData("let n: number = +2 ** 2;", "TSCS1001")]
    public void InvalidExponentFailsClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("exponent.ts",source),d=>d.Code==code);

    [Fact]
    public void ScannerSeparatesPowerPowerEqualMultiplyAndJson()
    {
        var kinds=new Scanner("exponent.ts","let n = 2 ** 3; n **= 2; n *= 2; n * 2;").Scan()
            .Select(t=>t.Kind).ToArray();
        Assert.Equal(1,kinds.Count(k=>k==TokenKind.Power));
        Assert.Equal(1,kinds.Count(k=>k==TokenKind.PowerEqual));
        Assert.Equal(1,kinds.Count(k=>k==TokenKind.StarEqual));
        Assert.Equal(1,kinds.Count(k=>k==TokenKind.Star));
        Assert.Throws<CompilationFailure>(()=>new Scanner("settings.json","{\"x\":2 ** 3}",jsonMode:true).Scan());
    }

    [Fact]
    public void PowerIsRightAssociativeAboveProductAndUnary()
    {
        var root=new Parser("exponent.ts",new Scanner("exponent.ts",
            "let n: number = -2 * 3 ** 4 ** 2;").Scan()).Parse();
        var binary=Assert.IsType<ValueBinary>(Assert.IsType<Variable>(Assert.Single(root.Statements)).Initializer);
        Assert.Equal(TokenKind.Star,binary.Operator.Kind);
        Assert.IsType<ValueUnary>(binary.Left);
        var power=Assert.IsType<ValueBinary>(binary.Right);
        Assert.Equal(TokenKind.Power,power.Operator.Kind);
        Assert.Equal(TokenKind.Power,Assert.IsType<ValueBinary>(power.Right).Operator.Kind);
    }

    [Fact]
    public void DeepRightAssociativeExponentIsBounded()
    {
        var input="let x: number = "+string.Join(" ** ",Enumerable.Repeat("2",70))+";";
        Assert.Contains(Compilation.Check("deep.ts",input),d=>d.Code=="TSCS1001");
    }
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class NumericLiteralTests
{
    [Theory]
    [InlineData("let n: number = 0.25;")]
    [InlineData("let n: number = .5;")]
    [InlineData("let n: number = 3.;")]
    [InlineData("let n: number = 1e3;")]
    [InlineData("let n: number = 2E-3;")]
    [InlineData("let n: number = 1.25e+2;")]
    [InlineData("let n: number = 1.e2;")]
    [InlineData("let n: number = 0.1 + .2 * 3e1;")]
    [InlineData("type Fraction = .5; let n: Fraction = 0.50;")]
    [InlineData("type Hundred = 1e2; let n: Hundred = 100.0;")]
    [InlineData("type Zero = -0; let n: Zero = -0.0;")]
    [InlineData("type Negative = -1.5; let n: Negative = -1.50e0;")]
    [InlineData("type Fraction = 0.5 | 1e0; let n: Fraction = .5;")]
    [InlineData("function half(v: number): number { return v * 0.5; } let n: number = half(84);")]
    [InlineData("type Box = { rate: 1.5 }; let box: Box = { rate: 1.50 };")]
    [InlineData("let values: number[] = [.25, 1e1]; values[0] += 0.25;")]
    public void FractionalAndExponentNumbersFlowThroughCheckedExpressions(string source) =>
        Assert.Empty(Compilation.Check("numeric-literals.ts",source));

    [Theory]
    [InlineData("let n: string = .5;", "TS2322")]
    [InlineData("let n: boolean = 2e3;", "TS2322")]
    [InlineData("type Half = 0.5; let n: Half = 0.25;", "TS2322")]
    [InlineData("let n: 2 = 1e0;", "TS2322")]
    [InlineData("type Negative = -0.5; let n: Negative = 0.5;", "TS2322")]
    [InlineData("function take(n: 1.5): number { return n; } let n: number = take(1.25);", "TS2345")]
    public void NumericLiteralRelationsRejectIncorrectTypes(string source,string code) =>
        Assert.Contains(Compilation.Check("numeric-literals.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("let n = 01.2;")]
    [InlineData("let n = 1e;")]
    [InlineData("let n = 1E+;")]
    [InlineData("let n = 1e-;")]
    [InlineData("let n = 1_000;")]
    [InlineData("let n = 0x10;")]
    [InlineData("let n = 1e9999;")]
    [InlineData("let n = 1.2.3;")]
    public void UnsupportedOrMalformedNumericFormsRemainRejected(string source) =>
        Assert.Contains(Compilation.Check("numeric-literals.ts",source),d=>d.Code=="TSCS1001");

    [Fact]
    public void ScannerKeepsPreciseSpansAndJsonBoundary()
    {
        var tokens=new Scanner("numeric-literals.ts",
            "let a=.5; let b=1.25e-2; let c=3.; let d=1.e+2; let e=12;").Scan();
        Assert.Equal(new[]{".5","1.25e-2","3.","1.e+2","12"},
            tokens.Where(t=>t.Kind==TokenKind.Number).Select(t=>t.Text));
        foreach(var token in tokens)
            Assert.Equal(token.Text.Length,token.Length);
        Assert.Throws<CompilationFailure>(()=>new Scanner("settings.json",
            "{\"value\":1.5}",jsonMode:true).Scan());
        Assert.Equal(TokenKind.Number,Assert.Single(new Scanner("settings.json",
            "12",jsonMode:true).Scan().Where(t=>t.Kind==TokenKind.Number)).Kind);
    }
}

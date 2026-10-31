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
    [InlineData("let n = 1__000;")]
    [InlineData("let n = 1_;")]
    [InlineData("let n = 1_.5;")]
    [InlineData("let n = 1._5;")]
    [InlineData("let n = 1e_2;")]
    [InlineData("let n = 1e2_;")]
    [InlineData("let n = 0_1;")]
    [InlineData("let n = 0x;")]
    [InlineData("let n = 1e9999;")]
    [InlineData("let n = 1.2.3;")]
    public void UnsupportedOrMalformedNumericFormsRemainRejected(string source) =>
        Assert.Contains(Compilation.Check("numeric-literals.ts",source),d=>d.Code=="TSCS1001");

    [Theory]
    [InlineData("let n: number = 1_000;")]
    [InlineData("let n: number = .1_25;")]
    [InlineData("let n: number = 12_34.5_6e-2;")]
    [InlineData("type Thousand = 1_000; let n: Thousand = 1000;")]
    [InlineData("type Negative = -1_000; let n: Negative = -1000;")]
    [InlineData("const slots: { 10: number } = { 1_0: 21 }; let n: number = slots[1_0];")]
    [InlineData("function half(n: number): number { return n / 1_0; } let x: number = half(2_0);")]
    public void DecimalNumericSeparatorsFlowThroughRealCompilation(string source) =>
        Assert.Empty(Compilation.Check("numeric-separators.ts",source));

    [Theory]
    [InlineData("let n: string = 1_000;", "TS2322")]
    [InlineData("type Count = 1_000; let n: Count = 2_000;", "TS2322")]
    [InlineData("function take(n: 2_0): number { return n; } let n: number = take(1_0);", "TS2345")]
    public void DecimalSeparatorsDoNotBypassTypeChecks(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-numeric-separators.ts",source),d=>d.Code==code);

    [Fact]
    public void SeparatorTokensKeepSourceSpanAndDecodedValue()
    {
        var number=Assert.Single(new Scanner("separators.ts","1_000.2_5e1_0").Scan(),
            token=>token.Kind==TokenKind.Number);
        Assert.Equal("1_000.2_5e1_0",number.Text);
        Assert.Equal(13,number.Length);
        Assert.Equal("1000.25e10",number.Value);
        Assert.Throws<CompilationFailure>(()=>new Scanner("strict.json","1_000",jsonMode:true).Scan());
    }

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
            "12",jsonMode:true).Scan(),t=>t.Kind==TokenKind.Number).Kind);
    }
    [Theory]
    [InlineData("let n: number = 0x2a;")]
    [InlineData("let n: number = 0X2_A;")]
    [InlineData("let n: number = 0b10_1010;")]
    [InlineData("let n: number = 0B101010;")]
    [InlineData("let n: number = 0o52;")]
    [InlineData("let n: number = 0O5_2;")]
    [InlineData("type Answer = 0x2A; let n: Answer = 0b101010;")]
    [InlineData("type Answer = 42; let n: Answer = 0o52;")]
    [InlineData("type Negative = -0x2a; let n: Negative = -42;")]
    [InlineData("function scale(n: number): number { return n * 0b10; } let r: number = scale(0o25);")]
    [InlineData("const slots: { 0xA: number } = { 0b1010: 42 }; let n: number = slots[0o12];")]
    [InlineData("let items: number[] = [0x2a, 0o52]; let n: number = items[0b1];")]
    public void CheckedRadixLiteralsUseExistingTypesAndValues(string source) =>
        Assert.Empty(Compilation.Check("radix-literals.ts",source));

    [Theory]
    [InlineData("let n: string = 0x2a;","TS2322")]
    [InlineData("let n: boolean = 0b101010;","TS2322")]
    [InlineData("type Answer = 0x2a; let n: Answer = 0o53;","TS2322")]
    [InlineData("function take(n: 0x2a): number { return n; } let n: number = take(0b101011);","TS2345")]
    public void RadixLiteralsKeepActualSemanticRejections(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-radix-types.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("let n = 0x;")]
    [InlineData("let n = 0b;")]
    [InlineData("let n = 0o;")]
    [InlineData("let n = 0x_FF;")]
    [InlineData("let n = 0b_10;")]
    [InlineData("let n = 0b102;")]
    [InlineData("let n = 0o8;")]
    [InlineData("let n = 0x1_;")]
    [InlineData("let n = 0x1__0;")]
    [InlineData("let n = 0x1g;")]
    [InlineData("let n = 0x1.2;")]
    [InlineData("let n = 0x20000000000000;")]
    public void InvalidRadixSpellingOrUnmodelledUnsafeIntegerFailsClosed(string source) =>
        Assert.Contains(Compilation.Check("invalid-radix-lexing.ts",source),d=>d.Code=="TSCS1001");

    [Fact]
    public void RadixTokenPreservesRawSourceAndExactDecodedNumber()
    {
        var tokens=new Scanner("radix-spans.ts","0X2_A 0b10_1010 0O5_2").Scan()
            .Where(token=>token.Kind==TokenKind.Number).ToArray();
        Assert.Equal(new[]{"0X2_A","0b10_1010","0O5_2"},tokens.Select(t=>t.Text));
        Assert.All(tokens,t=>Assert.Equal(t.Text.Length,t.Length));
        Assert.All(tokens,t=>Assert.Equal("42",t.Value));
    }

}

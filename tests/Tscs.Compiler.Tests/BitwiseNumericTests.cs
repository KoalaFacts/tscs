using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class BitwiseNumericTests
{
    [Theory]
    [InlineData("let n: number = 10 & 15;")]
    [InlineData("let n: number = 1 | 2 ^ 3;")]
    [InlineData("let n: number = 2 ^ 7 & 3;")]
    [InlineData("let n: number = ~4;")]
    [InlineData("let n: number = 1 << 3 + 2;")]
    [InlineData("let n: number = 32 >> 3;")]
    [InlineData("let n: number = -2 >>> 1;")]
    [InlineData("let n: number = (10 & 15) << 2 | 5 ^ 7;")]
    [InlineData("let b: boolean = (2 & 3) === 2;")]
    [InlineData("function bits(a: number, b: number): number { return (a ^ b) << 1; } let n: number = bits(4, 1);")]
    [InlineData("type Packet = { value: number }; function bits(p: Packet): number { return (p.value & 15) | 2; } let n: number = bits({ value: 8 });")]
    [InlineData("let n: number = (1 | 2) & (3 ^ 1);")]
    [InlineData("let n: number = 1 << 2 | 2 & 7;")]
    [InlineData("let n: number = ~(1 << 2) & 255;")]
    public void BitwiseAndShiftExpressionsCompile(string source) =>
        Assert.Empty(Compilation.Check("bitwise.ts",source));

    [Theory]
    [InlineData("let n: number = true & false;", "TSCS1001")]
    [InlineData("let n: number = \"x\" | 1;", "TSCS1001")]
    [InlineData("let n: number = ~false;", "TSCS1001")]
    [InlineData("let n: string = 3 & 2;", "TS2322")]
    [InlineData("let n: boolean = 2 ^ 3;", "TS2322")]
    [InlineData("let n: string = -2 >>> 1;", "TS2322")]
    public void InvalidBitwiseTypesCannotBeAccepted(string source,string code) =>
        Assert.Contains(Compilation.Check("bitwise.ts",source),d=>d.Code==code);

    [Fact]
    public void ScannerGreedilyRecognizesShiftsWithoutBreakingComparisons()
    {
        var kinds=new Scanner("tokens.ts","1 << 2 <= 3; 4 >> 1 >= 0; 5 >>> 1; 1 & 3 | 2 ^ ~1;").Scan()
            .Select(token=>token.Kind).ToArray();
        Assert.Contains(TokenKind.LeftShift,kinds);
        Assert.Contains(TokenKind.RightShift,kinds);
        Assert.Contains(TokenKind.UnsignedRightShift,kinds);
        Assert.Contains(TokenKind.LessEqual,kinds);
        Assert.Contains(TokenKind.GreaterEqual,kinds);
        Assert.Contains(TokenKind.Amp,kinds);
        Assert.Contains(TokenKind.Bar,kinds);
        Assert.Contains(TokenKind.Caret,kinds);
        Assert.Contains(TokenKind.Tilde,kinds);
    }

    [Fact]
    public void BitwisePrecedenceIsPreservedAcrossAllExpressionLevels()
    {
        var root=new Parser("bits.ts",new Scanner("bits.ts","let n: number = 1 | 2 ^ 3 & 4 === 5 << 1 + 2;").Scan()).Parse();
        var value=Assert.IsType<Variable>(Assert.Single(root.Statements)).Initializer;
        var or=Assert.IsType<ValueBinary>(value);
        Assert.Equal(TokenKind.Bar,or.Operator.Kind);
        var xor=Assert.IsType<ValueBinary>(or.Right);
        Assert.Equal(TokenKind.Caret,xor.Operator.Kind);
        var and=Assert.IsType<ValueBinary>(xor.Right);
        Assert.Equal(TokenKind.Amp,and.Operator.Kind);
        var equal=Assert.IsType<ValueBinary>(and.Right);
        Assert.Equal(TokenKind.StrictEqual,equal.Operator.Kind);
        var shift=Assert.IsType<ValueBinary>(equal.Right);
        Assert.Equal(TokenKind.LeftShift,shift.Operator.Kind);
        Assert.Equal(TokenKind.Plus,Assert.IsType<ValueBinary>(shift.Right).Operator.Kind);
    }

    [Fact]
    public void NamedTypeUnionParsingRemainsIndependentOfBitwiseOr()
    {
        Assert.Empty(Compilation.Check("types.ts","type Value = number | string; let n: Value = 2; let bits: number = 1 | 2;"));
    }

    [Theory]
    [InlineData("let n: number = 10; n &= 7;")]
    [InlineData("let n: number = 2; n |= 8;")]
    [InlineData("let n: number = 7; n ^= 2;")]
    [InlineData("let n: number = 1; n <<= 3;")]
    [InlineData("let n: number = 32; n >>= 2;")]
    [InlineData("let n: number = -2; n >>>= 1;")]
    [InlineData("type Packet = { value: number }; let p: Packet = { value: 7 }; p.value &= 3;")]
    [InlineData("let numbers: number[] = [7, 3]; numbers[0] <<= 2;")]
    [InlineData("for (let i: number = 16; i > 1; i >>= 1) {}")]
    [InlineData("let n: number = 7; n &= 3; n |= 8; n ^= 2; n <<= 1; n >>= 2; n >>>= 1;")]
    public void BitwiseCompoundMutationCompiles(string source) =>
        Assert.Empty(Compilation.Check("bitwise-mutation.ts",source));

    [Theory]
    [InlineData("const n: number = 3; n &= 2;", "TS2588")]
    [InlineData("const n: number = 3; n >>>= 1;", "TS2588")]
    [InlineData("let n: string = \"x\"; n &= 1;", "TSCS1001")]
    [InlineData("let n: number = 3; n |= true;", "TSCS1001")]
    public void InvalidBitwiseCompoundMutationFailsClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-bitwise-mutation.ts",source),d=>d.Code==code);

    [Fact]
    public void ScannerRecognizesBitwiseCompoundsWithoutConsumingComparisons()
    {
        var tokens=new Scanner("tokens.ts","a &= 1; b |= 2; c ^= 3; d <<= 4; e >>= 5; f >>>= 6; g <= 7; h >= 8;").Scan();
        var kinds=tokens.Select(x=>x.Kind).ToArray();
        Assert.Contains(TokenKind.AmpEqual,kinds);
        Assert.Contains(TokenKind.BarEqual,kinds);
        Assert.Contains(TokenKind.CaretEqual,kinds);
        Assert.Contains(TokenKind.LeftShiftEqual,kinds);
        Assert.Contains(TokenKind.RightShiftEqual,kinds);
        Assert.Contains(TokenKind.UnsignedRightShiftEqual,kinds);
        Assert.Contains(TokenKind.LessEqual,kinds);
        Assert.Contains(TokenKind.GreaterEqual,kinds);
        Assert.Equal(">>>=",Assert.Single(tokens, x=>x.Kind==TokenKind.UnsignedRightShiftEqual).Text);
    }

    [Fact]
    public void JsonModeStillRejectsBitwiseCompounds()
    {
        Assert.Throws<CompilationFailure>(()=>
            new Scanner("options.json","{\"value\": 1 >>= 2}",jsonMode:true).Scan());
    }
}

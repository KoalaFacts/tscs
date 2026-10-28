using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class DivisionRemainderTests
{
    [Theory]
    [InlineData("let q: number = 12 / 3;")]
    [InlineData("let r: number = 13 % 5;")]
    [InlineData("let n: number = 21 / 3 * 2 % 5;")]
    [InlineData("let fraction: number = 11 / 2;")]
    [InlineData("let signed: number = -8 % 3;")]
    [InlineData("let infinity: number = 1 / 0;")]
    [InlineData("function half(x: number): number { return x / 2; } let n: number = half(42);")]
    [InlineData("let n: number = 1 + 9 / 3;")]
    [InlineData("let n: number = 12 / 3 << 2;")]
    [InlineData("let a: number = 84; a /= 2;")]
    [InlineData("let a: number = 8; a %= 3;")]
    [InlineData("type Data = { value: number }; let data: Data = { value: 81 }; data.value /= 3;")]
    [InlineData("let values: number[] = [40, 6]; values[0] /= 2; values[1] %= 4;")]
    [InlineData("for (let i: number = 16; i > 1; i /= 2) {}")]
    [InlineData("let a: number = 100; a /= 4; a %= 7;")]
    [InlineData("let n: number = (3 + 9) % 5;")]
    [InlineData("let n: number = 9 % 4 * 2;")]
    [InlineData("let n: number = (100 / 2) % 3;")]
    public void DivisionRemainderExpressionsCompile(string source) =>
        Assert.Empty(Compilation.Check("divide.ts",source));

    [Theory]
    [InlineData("let q: string = 12 / 3;", "TS2322")]
    [InlineData("let q: boolean = 13 % 5;", "TS2322")]
    [InlineData("let q: number = true / 2;", "TSCS1001")]
    [InlineData("let q: number = 6 % false;", "TSCS1001")]
    [InlineData("const q: number = 6; q /= 2;", "TS2588")]
    [InlineData("const q: number = 6; q %= 2;", "TS2588")]
    public void InvalidDivisionOrRemainderIsRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("divide.ts",source),d=>d.Code==code);

    [Fact]
    public void SlashAndPercentAreLexedWithoutEatingCommentsOrJson()
    {
        const string source = "let a: number = 8 / 2; // comment\nlet b: number = 7 /* divisor */ % 3; a /= 2; b %= 2;";
        var kinds=new Scanner("tokens.ts",source).Scan().Select(x=>x.Kind).ToArray();
        Assert.Equal(1,kinds.Count(x=>x==TokenKind.Slash));
        Assert.Equal(1,kinds.Count(x=>x==TokenKind.Percent));
        Assert.Equal(1,kinds.Count(x=>x==TokenKind.SlashEqual));
        Assert.Equal(1,kinds.Count(x=>x==TokenKind.PercentEqual));
        Assert.Throws<CompilationFailure>(()=>new Scanner("settings.json","{\"x\": 8 / 2}",jsonMode:true).Scan());
    }

    [Fact]
    public void DivisionAndRemainderHaveLeftAssociativeProductPrecedence()
    {
        var root=new Parser("divide.ts",new Scanner("divide.ts","let n: number = 24 / 3 * 2 % 5;").Scan()).Parse();
        var value=Assert.IsType<Variable>(Assert.Single(root.Statements)).Initializer;
        var mod=Assert.IsType<ValueBinary>(value);
        Assert.Equal(TokenKind.Percent,mod.Operator.Kind);
        var multiply=Assert.IsType<ValueBinary>(mod.Left);
        Assert.Equal(TokenKind.Star,multiply.Operator.Kind);
        Assert.Equal(TokenKind.Slash,Assert.IsType<ValueBinary>(multiply.Left).Operator.Kind);
    }
}

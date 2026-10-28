using Tscs;
using Xunit;
namespace Tscs.Tests;

// Source-bound compiler semantics, not complete upstream TS7 acceptance.
public sealed class StringPrimitiveTests
{
    [Theory]
    [InlineData("let s: string = \"hello\"; let n: number = s.length;")]
    [InlineData("let s: string = \"hello\"; let c: string = s[1];")]
    [InlineData("let s: string = \"hello\"; let c: string = s[0] + s[4];")]
    [InlineData("let s: \"abc\" = \"abc\"; let n: number = s.length;")]
    [InlineData("let b: boolean = \"a\" < \"b\" && \"z\" >= \"y\";")]
    [InlineData("let b: boolean = \"a\" <= \"a\" || \"b\" > \"z\";")]
    [InlineData("type Label = \"a\" | \"b\"; function before(label: Label): boolean { return label < \"z\"; }")]
    [InlineData("function initial(s: string): string { return s[0]; } let c: string = initial(\"test\");")]
    [InlineData("function count(s: string): number { let n: number = 0; for (let i: number = 0; i < s.length; i++) { if (s[i] >= \"a\" && s[i] <= \"z\") { n += 1; } } return n; }")]
    public void StringReadsAndLexicalOrderingAreChecked(string source) =>
        Assert.Empty(Compilation.Check("string-primitives.ts",source));

    [Theory]
    [InlineData("let s: string = \"abc\"; s.length = 2;", "TS2540")]
    [InlineData("let s: string = \"abc\"; s.length += 1;", "TS2540")]
    [InlineData("let s: string = \"abc\"; s[0] = \"z\";", "TS2542")]
    [InlineData("let s: string = \"abc\"; s[0] += \"z\";", "TS2542")]
    [InlineData("let b: boolean = \"a\" < 1;", "TSCS1001")]
    [InlineData("let b: boolean = true > false;", "TSCS1001")]
    [InlineData("let s: string = \"abc\"; let c: string = s[\"0\"];", "TSCS1001")]
    [InlineData("let x: number = \"a\" < \"b\";", "TS2322")]
    public void InvalidOrReadOnlyOperationsAreRejected(string source,string expected) =>
        Assert.Contains(Compilation.Check("string-primitives.ts",source),d=>d.Code==expected);
}

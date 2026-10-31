using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed compiler checking. Not original upstream TS7 acceptance.
public sealed class NoSubstitutionTemplateTests
{
    [Theory]
    [InlineData("let s: string = `hello`;")]
    [InlineData("let literal: \"fixed\" = `fixed`;")]
    [InlineData("type Label = `ready`; let label: Label = `ready`;")]
    [InlineData("function read(s: string): string { return `head` + s; }")]
    [InlineData("let n: number = `hello`.length;")]
    [InlineData("let c: string = `hello`[1];")]
    [InlineData("let b: boolean = `alpha` < `beta`;")]
    [InlineData("let s: string = `escaped \\` tick`;")]
    [InlineData("let s: string = `literal \\${name}`;")]
    public void CheckedTemplateStringExpressions(string source) =>
        Assert.Empty(Compilation.Check("template.ts",source));

    [Theory]
    [InlineData("let n: number = `hello`;", "TS2322")]
    [InlineData("let n: \"right\" = `wrong`;", "TS2322")]
    [InlineData("let s: string = `hello`.missing;", "TS2339")]
    public void TemplateValuesRequireCorrectTypes(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-template.ts",source),d=>d.Code==code);

    [Fact]
    public void PhysicalCrLfIsAValidTemplateLineBreak() =>
        Assert.Empty(Compilation.Check("linebreak-template.ts","let s: string = `one\r\ntwo`;"));

    [Fact]
    public void InterpolationFailsClosed() =>
        Assert.Contains(Compilation.Check("interpolation.ts","let s: string = `hello${21}`;"),
            d=>d.Code=="TSCS1001");

    [Fact]
    public void UnterminatedTemplateReportsTs1160() =>
        Assert.Contains(Compilation.Check("unterminated.ts","let s: string = `hello"),
            d=>d.Code=="TS1160");
}

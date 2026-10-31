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

    [Theory]
    [InlineData("function tag(s: any): number { return 1; } const result = tag\n`hello`;")]
    [InlineData("let label: string = \"value\"\n`hello`;")]
    [InlineData("let data = { `key`: 21 };")]
    [InlineData("type Data = { `key`: number };")]
    [InlineData("import { value } from `./model.js`;")]
    [InlineData("export { value } from `./model.js`;")]
    public void TaggedOrQuotedOnlyTemplateSyntaxCannotBecomeInvalidJavaScript(string source) =>
        Assert.Contains(Compilation.Check("unsafe-template.ts",source),d=>d.Code=="TSCS1001");

    [Fact]
    public void ATemplateAfterBareReturnIsNotMistakenForATag() =>
        Assert.Empty(Compilation.Check("return-template.ts",
            "function f(): void { return\n`standalone`; }"));

    [Fact]
    public void PhysicalCrLfIsAValidTemplateLineBreak() =>
        Assert.Empty(Compilation.Check("linebreak-template.ts","let s: string = `one\r\ntwo`;"));

    [Fact]
    public void InterpolationUsesCheckedStringExpressions() =>
        Assert.Empty(Compilation.Check("interpolation.ts","let s: string = `hello${21}`;"));

    [Fact]
    public void UnterminatedTemplateReportsTs1160() =>
        Assert.Contains(Compilation.Check("unterminated.ts","let s: string = `hello"),
            d=>d.Code=="TS1160");
}

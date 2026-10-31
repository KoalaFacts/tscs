using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed bounded semantics, not original TS7 acceptance.
public sealed class TemplateInterpolationTests
{
    [Theory]
    [InlineData("const n: number = 21; const s: string = `value=${n}`;")]
    [InlineData("const a: number = 1; const b: number = 2; const s: string = `a${a}b${b}`;")]
    [InlineData("function double(n: number): number { return n * 2; } const s: string = `x${double(21)}`;")]
    [InlineData("const item: {value: number} = {value: 21}; const s: string = `x${item.value}`;")]
    [InlineData("const s: string = `${1 + 2}/${3 * 4}`;")]
    [InlineData("const s: string = `${({value: 21}).value}`;")]
    [InlineData("const s: string = `${\"ok\"}-${true}-${null}`;")]
    [InlineData("const s: string = `escaped \\${literal} and ${21}`;")]
    public void EmbeddedExpressionsReuseTheCheckedPipeline(string source) =>
        Assert.Empty(Compilation.Check("interpolated.ts",source));

    [Theory]
    [InlineData("const n: number = `x${21}`;", "TS2322")]
    [InlineData("const s: string = `${missingName}`;", "TS2304")]
    [InlineData("const item = {value: 21}; const s: string = `${item.bad}`;", "TS2339")]
    [InlineData("const s: string = `${}`;", "TSCS1001")]
    [InlineData("const s: string = `${21 +}`;", "TS1109")]
    [InlineData("const s: string = `${21; 22}`;", "TSCS1001")]
    [InlineData("const s: string = `${`nested`}`;", "TSCS1001")]
    public void InvalidOrUnsupportedSubstitutionsCannotCompile(string source,string code) =>
        Assert.Contains(Compilation.Check("bad-template.ts",source),d=>d.Code==code);

    [Fact]
    public void InnerExpressionErrorsRetainOriginalUtf16Offsets()
    {
        var source="const n: number = 1;\nconst s: string = `hello${missingName}`;";
        var diagnostics=Compilation.Check("locations.ts",source);
        Assert.Contains(diagnostics,d=>d.Code=="TS2304" &&
            d.Start==source.IndexOf("missingName",StringComparison.Ordinal));
    }

    [Fact]
    public void TaggedInterpolationAcrossNewlineIsStillRejected() =>
        Assert.Contains(Compilation.Check("tagged.ts",
            "function tag(s: any): number { return 1; }\nconst s = tag\n`${21}`;"),
            d=>d.Code=="TSCS1001");
}

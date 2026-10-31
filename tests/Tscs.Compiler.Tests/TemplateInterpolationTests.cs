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
    [InlineData("const s: string = `${`nested`}`;")]
    [InlineData("const n: number = 21; const s: string = `outer${`inner${n}`}`;")]
    [InlineData("const n: number = 3; const s: string = `${`a${`b${n}`}`}`;")]
    [InlineData("const n: number = 21; const s: string = `x${n > 0 ? `yes${n}` : `no${n}`}`;")]
    [InlineData("const s: string = `${`brace${\"}\"}`}`;")]
    public void EmbeddedExpressionsReuseTheCheckedPipeline(string source) =>
        Assert.Empty(Compilation.Check("interpolated.ts",source));

    [Theory]
    [InlineData("const n: number = `x${21}`;", "TS2322")]
    [InlineData("const s: string = `${missingName}`;", "TS2304")]
    [InlineData("const item = {value: 21}; const s: string = `${item.bad}`;", "TS2339")]
    [InlineData("const s: string = `${}`;", "TSCS1001")]
    [InlineData("const s: string = `${21 +}`;", "TS1109")]
    [InlineData("const s: string = `${21; 22}`;", "TSCS1001")]
    [InlineData("const s: string = `${`nested${}`}`;", "TSCS1001")]
    [InlineData("const s: string = `${`nested${missing}`}`;", "TS2304")]
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
    public void DeepNestedErrorKeepsItsSourceOffset()
    {
        var source="const n: number = 1;\nconst s: string = `a${`b${`c${missingName}`}`}`;";
        Assert.Contains(Compilation.Check("nested-position.ts",source),
            d=>d.Code=="TS2304" && d.Start==source.IndexOf("missingName",StringComparison.Ordinal));
    }

    [Fact]
    public void WholeLineTsIgnoreInsideNestedTemplateKeepsGlobalBoundaries()
    {
        var source="const s: string = `outer${`inner${\n// @ts-ignore\nmissingName}`}`;\nconst n: number = missingOutside;";
        var diagnostics=Compilation.Check("nested-ignore.ts",source);
        Assert.DoesNotContain(diagnostics,d=>d.Code=="TS2304" &&
            d.Start==source.IndexOf("missingName",StringComparison.Ordinal));
        Assert.Contains(diagnostics,d=>d.Code=="TS2304" &&
            d.Start==source.IndexOf("missingOutside",StringComparison.Ordinal));
    }

    [Fact]
    public void UnicodeLineSeparatorEndsASubstitutionLineComment()
    {
        // This is an actual U+2028 character in the parsed TypeScript source.
        var source="const s: string = `${1 // comment\u2028}`;";
        Assert.Empty(Compilation.Check("unicode-lines.ts",source));
    }

    [Fact]
    public void WholeLineTsIgnoreInsideSubstitutionSuppressesTheNextLine()
    {
        var source="const s: string = `${\n// @ts-ignore\nmissingName}`;";
        Assert.Empty(Compilation.Check("ignored-substitution.ts",source));
    }

    [Fact]
    public void TsIgnoreInsideSubstitutionCannotSuppressOutsideItsOwnLine()
    {
        var source="const s: string = `${\n// @ts-ignore\nmissingName}`;\nconst n: number = missingOutside;";
        var diagnostics=Compilation.Check("bounded-ignore.ts",source);
        Assert.Contains(diagnostics,d=>d.Code=="TS2304" &&
            d.Start==source.IndexOf("missingOutside",StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics,d=>d.Code=="TS2304" &&
            d.Start==source.IndexOf("missingName",StringComparison.Ordinal));
    }

    [Fact]
    public void InlineTsIgnoreIsNotPromotedToWholeLineDirective()
    {
        var source="const s: string = `${// @ts-ignore\nmissingName}`;";
        Assert.Contains(Compilation.Check("inline-ignore.ts",source),
            d=>d.Code=="TS2304" && d.Start==source.IndexOf("missingName",StringComparison.Ordinal));
    }

    [Fact]
    public void TaggedInterpolationAcrossNewlineIsStillRejected() =>
        Assert.Contains(Compilation.Check("tagged.ts",
            "function tag(s: any): number { return 1; }\nconst s = tag\n`${21}`;"),
            d=>d.Code=="TSCS1001");
}

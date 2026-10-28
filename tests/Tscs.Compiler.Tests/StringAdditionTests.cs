using Tscs;
using Xunit;
namespace Tscs.Tests;

// Checked source semantics, not original TS7 harness acceptance.
public sealed class StringAdditionTests
{
    [Theory]
    [InlineData("let s: string = \"hello\" + \" world\";")]
    [InlineData("let s: string = \"item-\" + 21;")]
    [InlineData("let s: string = 21 + \" items\";")]
    [InlineData("let s: string = \"ready=\" + true;")]
    [InlineData("let s: string = false + \"-ready\";")]
    [InlineData("let data: { count: number } = { count: 7 }; let s: string = \"n=\" + data.count;")]
    [InlineData("let data: { label: string } = { label: \"x\" }; data.label += 21;")]
    [InlineData("let a: string = \"x\"; a += 2; a += false;")]
    [InlineData("let n: 1 = 1; n += 1;")]
    [InlineData("let s: \"x\" = \"x\"; s += \"y\";")]
    [InlineData("let data: { count: 1 } = { count: 1 }; data.count += 2;")]
    [InlineData("function update(values: 1[]): number { values[0] += 2; return 1; }")]
    [InlineData("let values: number[] = [1, 2]; let s: string = \"size=\" + values.length;")]
    [InlineData("let a: any = 2; let s: string = \"x\" + a;")]
    [InlineData("let a: any = 2; let s: string = a + \"x\";")]
    [InlineData("let a: any = 2; let s: string = a + 1;")]
    [InlineData("let a: any = 2; let n: number = a + 1;")]
    [InlineData("function label(n: number): string { return \"item-\" + n; } let s: string = label(21);")]
    public void StringAndAnyAdditionAreChecked(string source) =>
        Assert.Empty(Compilation.Check("string-addition.ts",source));

    [Theory]
    [InlineData("let n: number = \"x\" + 2;")]
    [InlineData("let n: number = 2 + \"x\";")]
    [InlineData("let n: number = 2; n += \"x\";")]
    [InlineData("let n: 1 = 1; n += \"x\";")]
    [InlineData("let data: { count: number } = { count: 2 }; data.count += \"x\";")]
    [InlineData("let values: number[] = [2]; values[0] += \"x\";")]
    [InlineData("let flag: boolean = \"value=\" + false;")]
    public void ConcatenationMismatchIsTs2322(string source) =>
        Assert.Contains(Compilation.Check("string-addition.ts",source),d=>d.Code=="TS2322");

    [Fact]
    public void UnrelatedNumericOperatorDoesNotGainStringCoercion() =>
        Assert.Contains(Compilation.Check("string-addition.ts",
            "let n = \"5\" - 2;"),d=>d.Code=="TSCS1001");
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class TypeofPrimitiveNarrowingTests
{
    [Theory]
    [InlineData("type Input = number | string; function calculate(value: Input): number { if (typeof value === \"number\") { return value * 2; } return 0; } let result: number = calculate(21);")] // typeof-primitive:positive-01
    [InlineData("function describe(value: string | number): string { if (typeof value === \"string\") { return value; } return \"not-string\"; } let label: string = describe(\"ready\");")] // typeof-primitive:positive-02
    [InlineData("function flag(value: boolean | string): boolean { if (typeof value === \"boolean\") { return value; } return false; } let result: boolean = flag(true);")] // typeof-primitive:positive-03
    [InlineData("type Input = number | string; function score(value: Input): number { if (typeof value !== \"number\") { return 0; } return value * 2; } let result: number = score(21);")] // typeof-primitive:positive-04
    [InlineData("function score(value: number | string): number { if (\"number\" === typeof (value)) { return value * 2; } return 0; }")] // typeof-primitive:positive-05
    [InlineData("function flag(value: string | number | boolean): boolean { if (typeof value === \"string\") { return false; } if (typeof value === \"number\") { return true; } return value; }")] // typeof-primitive:positive-06
    [InlineData("let kind: string = typeof 21; let other: string = typeof \"text\";")] // typeof-primitive:positive-07
    [InlineData("let box: { value: number } = { value: 21 }; let kind: string = typeof box.value;")] // typeof-primitive:positive-08
    [InlineData("type Input = \"ready\" | 21; function describe(value: Input): string { if (typeof value === \"string\") { return value; } return \"other\"; }")] // typeof-primitive:positive-09
    [InlineData("let kind: string = typeof (typeof \"ready\");")] // typeof-primitive:positive-10
    [InlineData("let box: { typeof: number } = { typeof: 21 }; let value: number = box.typeof;")] // typeof-primitive:positive-11
    [InlineData("type Input = number | string; function describe(value: Input): string { if (typeof value === \"string\") { return value; } else { return \"other\"; } }")] // typeof-primitive:positive-12
    public void TypeofGuardsAndRuntimeValuesUseCheckedCompilerPipeline(string source) =>
        Assert.Empty(Compilation.Check("typeof.ts",source));

    [Theory]
    [InlineData("function wrong(value: number | string): number { if (typeof value === \"string\") { let result: number = value; return result; } return 0; }", "TS2322")] // typeof-primitive:negative-01
    [InlineData("function wrong(value: number | string): string { if (typeof value !== \"string\") { let result: string = value; return result; } return \"ok\"; }", "TS2322")] // typeof-primitive:negative-02
    [InlineData("function wrong(value: number | string): number { if (typeof value !== \"number\") { return value; } return 0; }", "TS2322")] // typeof-primitive:negative-03
    [InlineData("function wrong(value: number | string): number { if (typeof value === \"number\") { let result: number = value; } return value; }", "TS2322")] // typeof-primitive:negative-04
    [InlineData("function wrong(value: number | string): number { if (typeof value === \"boolean\") { return 0; } let result: number = value; return result; }", "TS2322")] // typeof-primitive:negative-05
    [InlineData("function wrong(value: number | string): number { if (\"number\" !== typeof value) { return value; } return 0; }", "TS2322")] // typeof-primitive:negative-06
    public void WrongBranchAndUnnarrowedWritesRemainErrors(string source,string code) =>
        Assert.Contains(Compilation.Check("typeof-negative.ts",source),d=>d.Code==code);
}

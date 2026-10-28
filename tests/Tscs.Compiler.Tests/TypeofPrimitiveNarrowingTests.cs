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
    // Regression for review P2: narrowed *reads* may not overwrite the
    // declared assignment target, and a successful rebinding invalidates
    // the previous branch fact even when the if arm completes normally.
    [Theory]
    [InlineData("function reset(v: number | string): string { if (typeof v !== \"number\") { return v; } v = \"reset\"; return v; } let output: string = reset(1);")]
    [InlineData("type State = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function swap(v: State): string { if (v.kind === \"ok\") { v = { kind: \"bad\", message: \"changed\" }; return \"done\"; } return v.message; }")]
    public void RebindingInsideGuardsUsesDeclaredTargetAndUpdatesFlow(string source) =>
        Assert.Empty(Compilation.Check("guard-rebind.ts",source));

    [Theory]
    [InlineData("function wrong(v: number | string): number { if (typeof v === \"number\") { v = \"reset\"; } else { return 0; } return v * 2; }", "TSCS1001")]
    [InlineData("type State = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function wrong(v: State): number { if (v.kind === \"ok\") { v = { kind: \"bad\", message: \"changed\" }; } else { return 0; } return v.value; }", "TS2339")]
    public void RebindingInvalidatesStaleReadNarrowing(string source,string code) =>
        Assert.Contains(Compilation.Check("stale-flow.ts",source),d=>d.Code==code);

    // A write in either surviving arm must invalidate an earlier guard.
    // A loop write cannot establish a post-loop type on zero iterations.
    [Theory]
    [InlineData("function f(v: number | string, change: boolean): number { if (typeof v !== \"number\") { return 0; } if (change) { v = \"reset\"; } return v * 2; }", "TSCS1001")]
    [InlineData("function f(v: number | string): string { if (typeof v !== \"number\") { return v; } while (false) { v = \"reset\"; } return v; }", "TS2322")]
    [InlineData("function f(v: number | string): string { if (typeof v !== \"number\") { return v; } for (let i: number = 0; i < 0; i++) { v = \"reset\"; } return v; }", "TS2322")]
    [InlineData("function f(v: number | string): string { if (typeof v !== \"number\") { return v; } for (const n of [1]) { v = \"reset\"; } return v; }", "TS2322")]
    public void ConditionalAndZeroIterationWritesDoNotPreserveStaleFlow(string source,string code) =>
        Assert.Contains(Compilation.Check("flow-write.ts",source),d=>d.Code==code);

}

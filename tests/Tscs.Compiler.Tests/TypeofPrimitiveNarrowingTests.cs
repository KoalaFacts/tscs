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
    [InlineData("type Handler=(n:number)=>number; function call(v:Handler|undefined):number { if(typeof v===\"function\"){return v(21);} return 0; }")] // typeof-primitive:positive-13
    [InlineData("type Handler=(n:number)=>number; function call(v:Handler|null):number { if(typeof v!==\"function\"){return 0;} return v(21); }")] // typeof-primitive:positive-14
    [InlineData("type Handler=(n:number)=>number; function call(v:Handler|undefined|null):number { if(\"function\"===typeof (v)){return v(21);} return 0; }")] // typeof-primitive:positive-15
    [InlineData("type Handler=(n:number)=>number; function choose(v:Handler|number):number { if(\"function\"!==typeof v){return v;} return v(21); }")] // typeof-primitive:positive-16
    [InlineData("function choose(x:number|undefined):number { if(typeof x===\"undefined\"){return 0;} return x*2; }")] // typeof-primitive:positive-17
    [InlineData("function choose(x:number|null):number { if(typeof x===\"object\"){return 0;} return x*2; }")] // typeof-primitive:positive-18
    [InlineData("function show(x:string|undefined):string { if(\"undefined\"===typeof x){return \"none\";} return x; }")] // typeof-primitive:positive-19
    [InlineData("type Handler=(n:number)=>number; function call(v:Handler|null|undefined):number { if(typeof v===\"undefined\"){return 0;} if(typeof v===\"object\"){return 0;} return v(21); }")] // typeof-primitive:positive-20
    public void TypeofGuardsAndRuntimeValuesUseCheckedCompilerPipeline(string source) =>
        Assert.Empty(Compilation.Check("typeof.ts",source));

    [Theory]
    [InlineData("function wrong(value: number | string): number { if (typeof value === \"string\") { let result: number = value; return result; } return 0; }", "TS2322")] // typeof-primitive:negative-01
    [InlineData("function wrong(value: number | string): string { if (typeof value !== \"string\") { let result: string = value; return result; } return \"ok\"; }", "TS2322")] // typeof-primitive:negative-02
    [InlineData("function wrong(value: number | string): number { if (typeof value !== \"number\") { return value; } return 0; }", "TS2322")] // typeof-primitive:negative-03
    [InlineData("function wrong(value: number | string): number { if (typeof value === \"number\") { let result: number = value; } return value; }", "TS2322")] // typeof-primitive:negative-04
    [InlineData("function wrong(value: number | string): number { if (typeof value === \"boolean\") { return 0; } let result: number = value; return result; }", "TS2322")] // typeof-primitive:negative-05
    [InlineData("function wrong(value: number | string): number { if (\"number\" !== typeof value) { return value; } return 0; }", "TS2322")] // typeof-primitive:negative-06
    [InlineData("type Handler=(n:number)=>number; function bad(v:Handler|undefined):number { if(typeof v===\"undefined\"){let wrong:number=v;return 0;} return 0; }", "TS2322")] // typeof-primitive:negative-07
    [InlineData("type Handler=(n:number)=>number; function bad(v:Handler|null):number { if(typeof v===\"function\"){let wrong:string=v(21);return 0;} return 0; }", "TS2322")] // typeof-primitive:negative-08
    [InlineData("function bad(x:number|null):number { if(typeof x===\"object\"){let wrong:number=x;return 0;} return 0; }", "TS2322")] // typeof-primitive:negative-09
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

    // Review P2s: a possibly repeated write invalidates pre-loop guard facts
    // for body reads, repeated conditions, and for updates. Lexical guard
    // cleanup must not reinstate stale facts after a checked nested write.
    [Theory]
    [InlineData("function f(v: number | string): number { if (typeof v !== \"number\") { return 0; } let result: number = 0; for (const n of [1, 2]) { result = v * 2; v = \"reset\"; } return result; }", "TSCS1001")]
    [InlineData("function f(v: number | string): number { if (typeof v === \"number\") { return 0; } for (let i: number = 0; i < 1; v = 21) { return v * 2; } return 0; }", "TSCS1001")]
    [InlineData("function f(v: number | string | boolean): number | string { if (typeof v === \"boolean\") { return 0; } { if (typeof v === \"string\") { return 0; } v = true; } return v; }", "TS2322")]
    [InlineData("function f(v: number | string): number { if (typeof v !== \"number\") { return 0; } let result: number = 0; while (result < 2) { result = v * 2; v = \"reset\"; } return result; }", "TSCS1001")]
    [InlineData("function f(v: number | string): number { if (typeof v !== \"number\") { return 0; } while (v > 0) { v = \"reset\"; } return 0; }", "TSCS1001")]
    [InlineData("function f(v: number | string): number { if (typeof v !== \"number\") { return 0; } let result: number = 0; do { result = v * 2; v = \"reset\"; } while (result < 2); return result; }", "TSCS1001")]
    public void BackedgesAndLexicalWritesCannotReuseStaleGuardFacts(string source,string code) =>
        Assert.Contains(Compilation.Check("flow-backedge.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("type H=(n:number)=>number; function bad(v:H|undefined):number { if(typeof v==="function"){v=undefined;return v(21);} return 0; }")]
    [InlineData("type H=(n:number)=>number; function bad(v:H|{value:number}):number { if(typeof v==="function"){return v(21);} return 0; }")]
    public void UnsafeRebindingOrUnclassifiedStructuralUnionCannotBecomeCallable(string source) =>
        Assert.Contains(Compilation.Check("typeof-unsafe.ts",source),d=>d.Code=="TSCS1001");

    [Fact]
    public void ShadowedLoopLocalDoesNotInvalidateOuterGuard()
    {
        const string source="function f(v: number | string): number { if (typeof v !== \"number\") { return 0; } while (false) { let v: string = \"local\"; v = \"other\"; } return v * 2; }";
        Assert.Empty(Compilation.Check("shadowed-loop.ts",source));
    }

}

using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class SwitchReturnFlowTests
{
    [Fact]
    public void GroupedSwitchSelectorStillNarrowsDiscriminatedUnion()
    {
        const string source="type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; " +
            "function f(e: E): number { switch (((e.kind))) { " +
            "case \"ok\": { return e.value; } case \"error\": { return 0; } default: { return 0; } } }";
        Assert.Empty(Compilation.Check("grouped-switch.ts",source));
    }

    [Fact]
    public void GroupedSwitchSelectorDoesNotAllowWrongBranchMember()
    {
        const string source="type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; " +
            "function f(e: E): number { switch ((e.kind)) { " +
            "case \"error\": { return e.value; } default: { return 0; } } }";
        Assert.Contains(Compilation.Check("grouped-switch.ts",source),d=>d.Code=="TS2339");
    }
    [Theory]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { switch (e.kind) { case \"ok\": { return e.value; } case \"error\": { return 0; } default: { return 0; } } } const n: number = f({kind:\"ok\",value:42});")] // positive-01
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): string { switch (e.kind) { case \"ok\": { return \"ready\"; } default: { return e.message; } } }")] // positive-02
    [InlineData("type N = { code: 1; value: number } | { code: 2; message: string }; function f(n: N): number { switch (n.code) { case 1: { return n.value; } case 2: { return 0; } default: { return 0; } } }")] // positive-03
    [InlineData("type B = { active: true; value: number } | { active: false; reason: string }; function f(b: B): number { switch (b.active) { case true: { return b.value; } default: { return 0; } } }")] // positive-04
    [InlineData("type T = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string } | { kind: \"idle\"; flag: boolean }; function f(t: T): number { switch (t.kind) { case \"ok\": { return t.value; } case \"error\": { return 0; } case \"idle\": { return 0; } default: { return 0; } } }")] // positive-05
    [InlineData("function f(n: number): number { switch (n) { case 1: { return 11; } default: { return 22; } } }")] // positive-06
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { switch (e.kind) { case \"ok\": { if (e.value > 0) { return e.value; } return 0; } default: { return 0; } } }")] // positive-07
    [InlineData("type N = { code: -1; value: number } | { code: 1; message: string }; function f(n: N): number { switch (n.code) { case -1: { return n.value; } default: { return 0; } } }")] // positive-08
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { switch (e.kind) { case \"ok\": { return e.value; } case \"error\": { return 0; } } return 0; }")] // positive-09
    [InlineData("interface Ok { kind: \"ok\"; value: number } type E = Ok | { kind: \"error\"; message: string }; function f(e: E): number { switch(e.kind) { case \"ok\": { return e.value; } default: { return 0; } } }")] // positive-10
    public void ReturningSwitchArmsUseTheCompilerTypePipeline(string source) =>
        Assert.Empty(Compilation.Check("switch.ts",source));

    [Theory]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { switch(e.kind) { case \"error\": { return e.value; } default: { return 0; } } }", "TS2339")] // negative-01
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { switch(e.kind) { case \"ok\": { return 0; } default: { return e.value; } } }", "TS2339")] // negative-02
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { switch(e.kind) { case \"other\": { return 1; } default: { return 0; } } }", "TS2678")] // negative-03
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function good(e: E): number { switch(e.kind) { case \"ok\": { return e.value; } default: { return 0; } } } function bad(e: E): number { switch(e.kind) { case \"ok\": { return e.message; } default: { return 0; } } }", "TS2339")] // negative-04
    public void InvalidSwitchBranchMembersOrCasesAreRejected(string source,string expectedCode) =>
        Assert.Contains(Compilation.Check("switch.ts",source),d=>d.Code==expectedCode);

    [Theory]
    [InlineData("function f(n: number): number { switch(n) { case 1: let value:number=1; return value; default: { return 0; } } }")]
    [InlineData("function f(n: number): number { switch(n) { case 1: { return 1; } case 1: { return 2; } default: { return 0; } } }")]
    public void UnsupportedUnbracedLexicalDeclarationsAndDuplicateCasesStillFailClosed(string source) =>
        Assert.Contains(Compilation.Check("switch.ts",source),d=>d.Code=="TSCS1001");

    [Theory]
    [InlineData("function f(n: number): number { switch(n) { case 1: return 1; default: { return 0; } } }")]
    [InlineData("function f(n:number):number { let result:number=0; switch(n) { case 1: result=20; case 2: result=result+1; break; default: break; } return result; }")]
    [InlineData("function f(n:number):number { switch(n) { case 1: case 2: return 21; default: return 0; } }")]
    [InlineData("function f(n:number):number { switch(n) { case 1: { return 1; } default: return 0; } }")]
    [InlineData("type E={kind:\"ok\";value:number}|{kind:\"error\";message:string}; function f(e:E):number { switch(e.kind) { case \"ok\": return e.value; default: return 0; } }")]
    [InlineData("function f(n:number):number { switch(n) { case 1: if(n>0) { return n; } return 0; default: return 0; } }")]
    public void UnbracedSwitchFlowUsesTheRealCompilerPipeline(string source) =>
        Assert.Empty(Compilation.Check("unbraced-switch.ts",source));

    [Theory]
    [InlineData("type E={kind:\"ok\";value:number}|{kind:\"error\";message:string}; function f(e:E):number { switch(e.kind) { case \"ok\": return e.message; default: return 0; } }","TS2339")]
    [InlineData("function f(n:number):number { let result:number=0; switch(n) { case 1: result=\"bad\"; break; default: return 0; } return result; }","TS2322")]
    [InlineData("type E={kind:\"ok\";value:number}|{kind:\"error\";message:string}; function f(e:E):number { switch(e.kind) { case \"other\": return 0; default: return 0; } }","TS2678")]
    public void UnbracedSwitchErrorsAreRealTypeErrors(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-unbraced-switch.ts",source),d=>d.Code==code);

    [Fact]
    public void FormerlyUnsupportedBracedFallthroughMustNowCompile()
    {
        // Preserve the exact original negative input now that its behavior is
        // implemented; a real runtime fallthrough is checked separately.
        const string source="function f(n: number): number { switch(n) { case 1: { let x = 1; } default: { return 0; } } }";
        Assert.Empty(Compilation.Check("switch.ts",source));
    }
}

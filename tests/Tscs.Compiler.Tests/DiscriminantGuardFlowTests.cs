using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class DiscriminantGuardFlowTests
{
    [Theory]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { if (e.kind !== \"ok\") { return 0; } return e.value; } let result: number = f({ kind: \"ok\", value: 42 });")]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): string { if (e.kind === \"ok\") { return \"ready\"; } return e.message; } let result: string = f({ kind: \"error\", message: \"bad\" });")]
    [InlineData("type T = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string } | { kind: \"idle\"; idle: boolean }; function f(e: T): number { if (e.kind === \"error\") { return 0; } if (e.kind === \"idle\") { return 0; } return e.value; }")]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { if (\"ok\" !== e.kind) { return 0; } return e.value; }")]
    [InlineData("type B = { active: true; value: number } | { active: false; reason: string }; function f(b: B): number { if (b.active === false) { return 0; } return b.value; }")]
    [InlineData("type N = { code: 1; value: number } | { code: 2; message: string }; function f(n: N): number { if (n.code !== 1) { return 0; } return n.value; }")]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { if (e.kind !== \"ok\") { return 0; } { let n: number = e.value; } return e.value; }")]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): string { if (e.kind === \"ok\") { return \"ready\"; } else { return e.message; } }")]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { if (e.kind === \"ok\") { let n: number = e.value; } else { return 0; } return e.value; }")]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { if (e.kind !== \"ok\") { return 0; } if (e.value > 0) { return e.value; } return 0; }")]
    public void ReturningGuardsNarrowFollowingStatements(string source) =>
        Assert.Empty(Compilation.Check("guard.ts",source));

    [Theory]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { if (e.kind !== \"ok\") { let n: number = 0; } return e.value; }")]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): string { if (e.kind !== \"ok\") { return \"bad\"; } return e.message; }")]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function f(e: E): number { if (e.kind === \"ok\") { return 0; } return e.value; }")]
    [InlineData("type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function good(e: E): number { if (e.kind !== \"ok\") { return 0; } return e.value; } function bad(e: E): number { return e.value; }")]
    public void FallthroughOrWrongBranchCannotReadInaccessibleMember(string source) =>
        Assert.Contains(Compilation.Check("guard.ts",source),d=>d.Code=="TS2339");
}

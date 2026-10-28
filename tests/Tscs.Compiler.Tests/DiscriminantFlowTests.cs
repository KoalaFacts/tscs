using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class DiscriminantFlowTests
{
    // These are real source programs through the existing checker, not placeholders.
    [Theory]
    [InlineData("type Result = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function read(r: Result): number { if (r.kind === \"ok\") { return r.value; } else { return 0; } } let output: number = read({ kind: \"ok\", value: 42 });")]
    [InlineData("type Result = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function read(r: Result): string { if (r.kind !== \"ok\") { return r.message; } else { return \"ready\"; } } let output: string = read({ kind: \"error\", message: \"bad\" });")]
    [InlineData("type Result = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function read(r: Result): string { if (\"error\" === r.kind) { return r.message; } else { return \"ready\"; } }")]
    [InlineData("type Tri = { kind: \"left\"; left: number } | { kind: \"right\"; right: number } | { kind: \"idle\"; other: number }; function read(r: Tri): number { if (r.kind === \"left\") { return r.left; } else if (r.kind === \"right\") { return r.right; } else { return r.other; } }")]
    [InlineData("type Pair = { code: 1; first: number } | { code: 2; second: number }; function pick(p: Pair): number { if (p.code === 1) { return p.first; } else { return p.second; } }")]
    [InlineData("type Flag = { ok: true; value: number } | { ok: false; reason: string }; function read(f: Flag): number { if (f.ok === true) { return f.value; } else { return 0; } }")]
    [InlineData("type Result = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function read(r: Result): number { if (r.kind === \"ok\") { if (r.value > 0) { return r.value; } else { return 0; } } else { return 0; } }")]
    [InlineData("type Result = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function sum(xs: Result[]): number { let total: number = 0; for (const r of xs) { if (r.kind === \"ok\") { total = total + r.value; } } return total; } let data: Result[] = [{ kind: \"ok\", value: 21 }, { kind: \"error\", message: \"bad\" }]; let output: number = sum(data);")]
    public void NarrowedBranchesCompileWithoutInventingAny(string source) =>
        Assert.Empty(Compilation.Check("discriminant.ts",source));

    [Theory]
    [InlineData("type Result = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function wrong(r: Result): number { if (r.kind === \"ok\") { return r.message; } else { return 0; } }", "TS2339")]
    [InlineData("type Result = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function wrong(r: Result): number { if (r.kind !== \"ok\") { return r.value; } else { return 0; } }", "TS2339")]
    [InlineData("type Result = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function wrong(r: Result): number { if (r.kind === \"ok\") { let n: number = r.value; } return r.value; }", "TS2339")]
    [InlineData("type Result = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; function wrong(r: Result): number { if (r.kind === \"ok\") { return r.value; } else { return r.value; } }", "TS2339")]
    public void OtherBranchesAndOutsideTheIfMustRemainSafe(string source,string code) =>
        Assert.Contains(Compilation.Check("discriminant.ts",source),d=>d.Code==code);
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

// The executable fixed-Go counterpart is fixtures/compound-guards/semantic-cases.json.
// These are real compiler checks, not original upstream runner acceptance.
public sealed class CompoundGuardTests
{
    [Theory]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function read(p: Packet): number { if (p.kind === \"ok\" && p.value > 0) { return p.value * 2; } return 0; }")]
    [InlineData("function read(v: number | string): number { if (typeof v === \"number\" && v > 0) { return v * 2; } return 0; }")]
    [InlineData("function read(v: number | string): number { if (!(typeof v !== \"number\")) { return v * 2; } return 0; }")]
    [InlineData("function read(v: number | string | boolean): boolean { if (typeof v === \"number\" || typeof v === \"string\") { return true; } return v; }")]
    [InlineData("function read(v: number | string | boolean): boolean { if (typeof v !== \"number\" && typeof v !== \"string\") { return v; } return false; }")]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function read(p: Packet): string { if (!(p.kind !== \"ok\")) { return \"ready\"; } return p.message; }")]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function read(p: Packet): number { if (p.kind === \"bad\" || p.value > 0) { return 1; } return 0; }")]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function read(p: Packet): number { if (p.kind === \"ok\" || p.message === \"bad\") { return 1; } return 0; }")]
    [InlineData("function read(v: number | string): number { return typeof v === \"number\" && v > 0 ? v * 2 : 0; }")]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function read(p: Packet): number { return p.kind === \"ok\" && p.value > 0 ? p.value : 0; }")]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function read(p: Packet): number { if (((p.kind === \"ok\") && (p.value > 0))) { return p.value; } return 0; }")]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function read(p: Packet): number { if (p.kind !== \"ok\" || p.value <= 0) { return 0; } return p.value; }")]
    public void CompoundConditionsCarrySafeBranchFacts(string source) =>
        Assert.Empty(Compilation.Check("compound.ts",source));

    [Theory]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function wrong(p: Packet): number { if (p.kind === \"ok\" && p.message === \"bad\") { return 1; } return 0; }","TS2339")]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function wrong(p: Packet): number { if (p.kind === \"bad\" || p.message === \"bad\") { return 1; } return 0; }","TS2339")]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function wrong(p: Packet): number { if (!(p.kind === \"ok\")) { return p.value; } return 0; }","TS2339")]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function wrong(p: Packet): number { if (p.kind === \"ok\" || p.message === \"bad\") { return p.value; } return 0; }","TS2339")]
    [InlineData("type Packet = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function wrong(p: Packet): string { if (p.kind !== \"ok\" || p.value > 0) { return p.message; } return \"safe\"; }","TS2339")]
    [InlineData("function wrong(v: number | string): number { if (!(typeof v === \"number\")) { return v; } return 0; }","TS2322")]
    public void UnsafeCompoundPathsStillProduceActualErrors(string source,string code) =>
        Assert.Contains(Compilation.Check("compound-negative.ts",source),d=>d.Code==code);

    [Fact]
    public void ShortCircuitRightHandFactsAreRestored()
    {
        const string source="type P = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; function wrong(p: P): number { const hit: boolean = p.kind === \"ok\" && p.value > 0; return p.value; }";
        Assert.Contains(Compilation.Check("compound-restore.ts",source),d=>d.Code=="TS2339");
    }

    [Fact]
    public void LogicalValueIsNotImplicitlyConvertedToBoolean()
    {
        const string source="function wrong(v: number | string): boolean { return typeof v === \"number\" && v; }";
        Assert.Contains(Compilation.Check("compound-coercion.ts",source),d=>d.Code=="TS2322");
    }
}

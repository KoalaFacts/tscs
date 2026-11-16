using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: ten top-level statements of the pinned lib.es5.d.ts first failed on a
// repeated method signature (`concat`, `indexOf`, `toLocaleString`, ...).
// Repeated method signatures of one name are overloads in the pinned checker:
// every signature is checked like a call signature, in source order, and any
// use of the containing type fails closed. Expected results match the pinned
// Go CLI.
public sealed class MethodOverloadSignatureTests
{
    [Theory]
    [InlineData("interface S { m(): void; m(v: number): void }")]
    [InlineData("interface S { twice(n: number): number; twice(n: number): number }")]
    [InlineData("interface S { m(): void; m<T>(v: T): T }")]
    [InlineData("interface S { m<T>(v: T): T; m(): void; m(a: string, b: string): string }")]
    [InlineData("interface G<T> { concat(...items: T[][]): T[]; concat(...items: (T | T[])[]): T[] }")]
    [InlineData("interface S { m?(): void; m?(v: number): void }")]
    [InlineData("type F = { m(): void, m(v: string): void };")]
    [InlineData("interface S { m(): void; n: number; m(v: number): void }")]
    public void DeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("index.ts",source));

    [Theory]
    // Each overload is checked at its own position, in source order.
    [InlineData("interface S { m(): void; m(v: Missing): void }","TS2304",30)]
    [InlineData("interface S { m(v: Missing): void; m(): Other }","TS2304",19)]
    [InlineData("interface S { m(): void; x: Missing; m(v: Other): void }","TS2304",28)]
    [InlineData("interface S { m(): void; m<T extends string = number>(v: T): T }","TS2344",46)]
    [InlineData("interface S { m(): void; m<T>(v: T): T; y: T }","TS2304",43)]
    // A property and a method of one name stay TS2300.
    [InlineData("interface S { m(): void; m: number }","TS2300",25)]
    public void DiagnosticsMatchThePinnedChecker(string source,string code,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("overload-error.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
    }

    [Theory]
    // Uses, optionality mismatches (TS2386), readonly and recursion are not modelled.
    [InlineData("interface S { m(): void; m(v: number): void }\ndeclare let s: S;")]
    [InlineData("let x: { m(): void; m(v: number): void };")]
    [InlineData("interface S { m(): void; m?(v: number): void }")]
    [InlineData("interface S { m(): void; readonly m(v: number): void }")]
    [InlineData("interface S { m(): void; m(v: number): S }")]
    [InlineData("interface S { m(): void; m(v: number): void }\ninterface S { n(): void }")]
    public void UnmodelledFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("overload-use.ts",source),d=>d.Code=="TSCS1001");
}

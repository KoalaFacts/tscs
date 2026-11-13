using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: the pinned lib.es5.d.ts declares every global constructor interface
// (ObjectConstructor, StringConstructor, ErrorConstructor, ...) with call and
// construct signatures. The declarations check their parameter and return
// types in source order; any use of such a type fails closed until call and
// construct signatures are modelled. Expected results match the pinned Go CLI.
public sealed class CallSignatureDeclarationTests
{
    [Theory]
    [InlineData("interface B { b: string }\ninterface S { new (v?: number): B; (v?: number): string; readonly p: B }")]
    [InlineData("type F = { (a: string): void; new (b: number): string };")]
    [InlineData("interface S {\n    new (...args: string[]): boolean;\n    (...args: string[]): boolean;\n}")]
    [InlineData("interface S { (): void, x: number }")]
    [InlineData("interface S { (): number; (): number }")]
    [InlineData("interface A { new(): void }")]
    [InlineData("interface G<T> { (v: T): T }")]
    public void DeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("index.ts",source));

    [Theory]
    [InlineData("interface S { new (v: Missing): number }",22)]
    [InlineData("interface S { (v: number): Missing }",27)]
    [InlineData("interface G<T> { (v: T): Missing }",25)]
    // Members are checked in source order: the first diagnostic is the reference's.
    [InlineData("interface S { (v: Missing): number; x: Other }",18)]
    [InlineData("interface S { x: Missing; (v: Other): number }",17)]
    public void UnresolvedNamesMatchThePinnedChecker(string source,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("call-error.ts",source));
        Assert.Equal("TS2304",diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
    }

    [Theory]
    // Uses, heritage, reopening and declaration identity are not modelled.
    [InlineData("let x: { (): number };")]
    [InlineData("declare let x: {\n new (): void };")]
    [InlineData("interface A { (): void }\ntype B = A[\"x\"];")]
    [InlineData("interface B { b: string }\ninterface S extends B { (): number }")]
    [InlineData("interface S { new (): number }\ninterface B extends S { b: string }")]
    [InlineData("interface S { new (v: number): number }\ninterface S { (): void }")]
    [InlineData("interface S { x: number }\ninterface S { (): void }")]
    // Recursive references are legal in the reference but not modelled (never TS2456).
    [InlineData("interface C { new (s: string): C }")]
    [InlineData("interface D { c: number }\ninterface C { (): C; d: D }")]
    [InlineData("type F = { (): F };")]
    // Generic signatures and duplicate parameters (TS2300) are not modelled.
    [InlineData("interface S { <T>(v: T): T }")]
    [InlineData("interface S { new <T>(v: T): number }")]
    [InlineData("interface S { (a: number, a: string): number }")]
    [InlineData("interface S { m(a: number, a: string): number }")]
    // `new` is still rejected outside an object type member.
    [InlineData("let a = 1;\nnew (Object)();")]
    [InlineData("let a = 1;\n{\nnew (Object)();\n}")]
    [InlineData("let o = {a: 1,\n new (x: number): number};")]
    public void UnmodelledFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("call-use.ts",source),d=>d.Code=="TSCS1001");
}

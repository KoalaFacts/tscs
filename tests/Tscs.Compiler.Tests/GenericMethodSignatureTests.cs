using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: twelve top-level statements of the pinned lib.es5.d.ts first fail on a
// generic method signature (`map<U>(...)`, `reduce<U>(...)`, `then<T>(...)`).
// Declarations are checked like generic call signatures: type parameters are
// opaque, constraints and defaults are checked in declaration order, and any
// use of the containing type fails closed. Expected results match the pinned
// Go CLI.
public sealed class GenericMethodSignatureTests
{
    [Theory]
    [InlineData("interface S { m<T>(v: T): T }")]
    [InlineData("interface G<T> { map<U>(f: (v: T) => U): U[]; n: number }")]
    [InlineData("interface S { m?<T extends string = \"a\">(v: T): T }")]
    [InlineData("interface G<T> { m<T>(v: T): T }")]
    [InlineData("type F = { m<T>(): T };")]
    [InlineData("interface S { m<A, B extends A = A>(a: A, b: B): B; n(): void }")]
    [InlineData("type T = number;\ninterface S { m<T extends string>(v: T): T }\nlet n: T = 1;")]
    public void DeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("index.ts",source));

    [Theory]
    [InlineData("interface S { m<T>(v: Missing): T }","TS2304",22)]
    [InlineData("interface S { m<T>(v: T): T; x: T }","TS2304",32)]
    [InlineData("interface S { m<T extends Missing>(v: T): T }","TS2304",26)]
    [InlineData("interface S { m<T extends string = number>(v: T): T }","TS2344",35)]
    [InlineData("interface S { m<T = U, U = string>(v: T): T }","TS2744",20)]
    [InlineData("interface S { m<string>(): void }","TS2368",16)]
    // Members are checked in source order.
    [InlineData("interface S { x: Missing; m<T>(v: Other): T }","TS2304",17)]
    // A property and a method of one name stay TS2300; only method overloads fail closed.
    [InlineData("interface S { m: number; m<T>(): T }","TS2300",25)]
    public void DiagnosticsMatchThePinnedChecker(string source,string code,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("generic-method-error.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
    }

    [Theory]
    // Uses, reopening, recursion, duplicate parameters (TS2300), `readonly`
    // and overloads are legal or differently reported in the reference.
    [InlineData("interface S { m<T>(v: T): T }\ndeclare let s: S;")]
    [InlineData("let x: { m<T>(v: T): T };")]
    [InlineData("interface S { m<T>(v: T): T }\ninterface S { n(): void }")]
    [InlineData("interface S { m<T>(): S }")]
    // A recursive sibling member is legal in the reference (never TS2456).
    [InlineData("interface S { m<T>(): T; next: S }")]
    [InlineData("interface C { (): void; next: C }")]
    [InlineData("interface I { [k: string]: I }")]
    [InlineData("interface S { n(): S; m<T>(): T }")]
    [InlineData("interface S { m<T, T>(v: T): T }")]
    [InlineData("interface S { readonly m<T>(): T }")]
    [InlineData("interface S { m(): void; m<T>(v: T): T }")]
    [InlineData("interface S { m(): void; m(v: number): void }")]
    [InlineData("interface S { m<T>(x: T): typeof x }")]
    public void UnmodelledFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("generic-method-use.ts",source),d=>d.Code=="TSCS1001");
}

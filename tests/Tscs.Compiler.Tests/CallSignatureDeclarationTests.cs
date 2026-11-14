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
    // Parameters are in scope for `typeof` in the reference; not bound here (never TS2304).
    [InlineData("interface S { (x: number): typeof x }")]
    [InlineData("interface S { new (x: number, y: typeof x): number }")]
    [InlineData("interface S { m(x: number): typeof x }")]
    [InlineData("let x = \"s\";\nlet f: (x: number) => typeof x = (x: number) => x;")]
    [InlineData("let x = 1;\nlet f: (x: string) => typeof x = (x: string) => 1;")]
    [InlineData("let x = 1;\ninterface I { m(x: string): typeof x }\nlet i: I = { m: (x: string) => 1 };")]
    // Duplicate parameters (TS2300) are not modelled.
    [InlineData("interface S { (a: number, a: string): number }")]
    [InlineData("interface S { m(a: number, a: string): number }")]
    // `new` is still rejected outside an object type member.
    [InlineData("let a = 1;\nnew (Object)();")]
    [InlineData("let a = 1;\n{\nnew (Object)();\n}")]
    [InlineData("let o = {a: 1,\n new (x: number): number};")]
    public void UnmodelledFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("call-use.ts",source),d=>d.Code=="TSCS1001");

    // Generic call and construct signatures (the pinned typed-array, Array and
    // Promise-like constructors): type parameters are opaque while the
    // signature is checked, constraints and defaults are checked in order,
    // and the parameters are not in scope after the signature.
    [Theory]
    [InlineData("interface S { <T>(v: T): T; new <T>(v: T): T[] }")]
    [InlineData("interface S { <T extends string>(v: T): number; new <A, B extends A>(a: A, b: B): A }")]
    [InlineData("interface B { b: string }\ninterface S { new <T extends B = B>(v: T): T }")]
    [InlineData("interface G<T> { <U extends T = T>(v: U): T }")]
    [InlineData("interface S { <T, U = T>(a: T, b: U): U }")]
    [InlineData("interface S { <T>(v: T): T, <T>(v: T, w: T): T }")]
    [InlineData("interface S { new <T>(v: T): T; new (v: string): string }")]
    [InlineData("type T = number;\ninterface S { <T extends string>(v: T): T }\nlet n: T = 1;")]
    public void GenericDeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("generic-call.ts",source));

    [Theory]
    [InlineData("interface S { <T>(v: Missing): T }","TS2304",21)]
    [InlineData("interface S { <T>(v: T): T; x: T }","TS2304",31)]
    [InlineData("interface S { <T extends Missing>(v: T): T }","TS2304",25)]
    [InlineData("interface S { <T = Missing>(v: T): T }","TS2304",19)]
    [InlineData("interface S { <T extends string = number>(v: T): T }","TS2344",34)]
    [InlineData("interface S { <T = U, U = string>(v: T): T }","TS2744",19)]
    [InlineData("interface S { <T>(v: T): T; <U>(v: U): Missing }","TS2304",39)]
    [InlineData("interface S { <string>(v: string): void }","TS2368",15)]
    [InlineData("interface S { new <never>(): void }","TS2368",19)]
    [InlineData("type F = { <T, boolean extends Missing>(v: T): void };","TS2368",15)]
    public void GenericSignatureDiagnosticsMatchThePinnedChecker(string source,string code,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("generic-call-error.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
    }

    [Theory]
    // The reference reports TS2313, TS2300 and TS2706; forward constraints,
    // keyof an opaque parameter, uses and reopening are legal but not modelled.
    [InlineData("interface S { <T extends T>(v: T): T }")]
    [InlineData("interface S { <T extends U, U>(v: T): U }")]
    [InlineData("interface S { <T, T>(v: T): T }")]
    [InlineData("interface S { <T = string, U>(v: T): U }")]
    [InlineData("interface S { <T, K extends keyof T>(o: T, k: K): T[K] }")]
    [InlineData("interface S { <T>(v: T): T }\ndeclare let s: S;")]
    [InlineData("type F = { new <T>(): T };\nlet f: F;")]
    [InlineData("interface S { <T>(v: T): T }\ninterface S { x: number }")]
    [InlineData("interface S { <T>(): S }")]
    [InlineData("interface S { readonly <T>(v: T): T }")]
    public void UnmodelledGenericFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("generic-call-use.ts",source),d=>d.Code=="TSCS1001");
}

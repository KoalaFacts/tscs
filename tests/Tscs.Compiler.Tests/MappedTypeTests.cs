using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: mapped types (`Partial<T> = { [P in keyof T]?: T[P] }`, `Required`,
// `Readonly`, `Pick`, `Record`) failed in the pinned lib.es5.d.ts as
// `Expected a supported named object property`. A generic type alias whose
// whole body is a mapped type is checked: the constraint must be `keyof X` or
// a parameter constrained to `keyof X` (otherwise fail closed), the template
// resolves with the mapped parameter opaque, `T[P]` is valid when the keys
// come from `keyof T`, and only TS2304 is reported as is. Every use, `as`
// clauses, non-generic mapped aliases and mapped types anywhere else fail
// closed. Expected results match the pinned Go CLI.
public sealed class MappedTypeTests
{
    [Theory]
    [InlineData("type Partial<T> = { [P in keyof T]?: T[P]; };")]
    [InlineData("type Required<T> = { [P in keyof T]-?: T[P]; };")]
    [InlineData("type Readonly<T> = { readonly [P in keyof T]: T[P]; };")]
    [InlineData("type Pick<T, K extends keyof T> = { [P in K]: T[P]; };")]
    [InlineData("type Record<K extends keyof any, T> = { [P in K]: T; };")]
    [InlineData("type X<T> = { -readonly [P in keyof T]+?: T[P] };")]
    [InlineData("type X<T> = { [P in keyof T]: P; };")]
    [InlineData("type X<T> = { [P in keyof T]: T[P] }")]
    [InlineData("type X<T, K extends keyof T, V extends keyof T> = { [P in K]: T[P] };")]
    public void DeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("index.ts",source));

    [Theory]
    [InlineData("type X<T> = { [P in keyof T]: Foo; };","TS2304",30)]
    [InlineData("type X<T> = { [P in keyof Foo]: T; };","TS2304",26)]
    [InlineData("type X<T, K extends keyof T> = { [P in K]: T[Q]; };","TS2304",45)]
    [InlineData("type X<T, K extends keyof Bar> = { [P in K]: T };","TS2304",26)]
    [InlineData("type X<T> = { [P in Foo]: T };","TS2304",20)]
    public void DiagnosticsMatchThePinnedChecker(string source,string code,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("mapped-type-error.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
    }

    [Theory]
    // Accepted by the reference, not modelled.
    [InlineData("type X<T> = { [P in string]: T; };")]
    [InlineData("type X<T> = { [P in keyof T as P]: T[P]; };")]
    [InlineData("type X = { [P in \"a\"]: number; };")]
    [InlineData("type X<T, K extends keyof T> = { [P in K]: T[P] };\nlet v: X<{ a: 1 }, \"a\">;")]
    [InlineData("type X<T> = { [P in keyof T]: T[P] extends string ? 1 : 2 };")]
    [InlineData("type X<T> = { [P in keyof T]: T[P][] };")]
    [InlineData("type X<T, K extends keyof T> = { [P in K]: T[K] };")]
    [InlineData("type X<T> = { a: { [P in keyof T]: T[P] } };")]
    [InlineData("type X<T> = T extends string ? { [P in keyof T]: T[P] } : never;")]
    [InlineData("type X<T, K extends keyof T = keyof T> = { [P in K]: T[P] };")]
    // The reference reports TS2322 (constraint), TS2536 (index), TS7061 (members), TS2314 (bare Array).
    [InlineData("type X<T> = { [P in T]: T; };")]
    [InlineData("type X<T, U> = { [P in keyof T]: U[P]; };")]
    [InlineData("type X<T> = { [P in keyof T]: T[P]; x: number };")]
    [InlineData("type X<T> = { [P in keyof T]: Array };")]
    // Shadowing a type parameter is not modelled.
    [InlineData("type X<T> = { [T in keyof T]: T };")]
    public void UnmodelledFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("mapped-type-use.ts",source),d=>d.Code=="TSCS1001");

    // A `keyof` constraint without a mapped body keeps the existing behaviour.
    [Fact]
    public void KeyofConstraintsOutsideMappedAliasesAreUnchanged()=>
        Assert.Contains(Compilation.Check("mapped-type-keyof.ts","type X<T, K extends keyof T> = T[K];"),
            d=>d.Code=="TSCS1001");
}

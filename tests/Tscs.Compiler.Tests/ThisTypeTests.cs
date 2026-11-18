using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: the polymorphic `this` type (typed arrays' `reverse(): this`,
// `(value: number, index: number, array: this) => unknown`) first failed in
// the pinned lib.es5.d.ts as `Unsupported syntax 'this'` (9 statements). It is
// valid only in the members of a class or interface. An interface whose
// members use it is checked like one with signatures, and any use of it, or
// of a `this` type anywhere else, fails closed. Expected results match the
// pinned Go CLI.
public sealed class ThisTypeTests
{
    [Theory]
    [InlineData("interface A { reverse(): this; }")]
    [InlineData("interface A { p: this; }")]
    [InlineData("interface A { (): this; }")]
    [InlineData("interface A { m(): this | undefined; }")]
    [InlineData("interface A { m(cb: (a: this) => void): void; }")]
    [InlineData("interface A<T> { every(p: (v: number, a: this) => unknown): boolean; fill(v: number): this; }")]
    public void DeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("index.ts",source));

    [Theory]
    // Other member types are still checked at their own positions.
    [InlineData("interface A { m(x: Missing): this; }","TS2304",19)]
    public void DiagnosticsMatchThePinnedChecker(string source,string code,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("this-type-error.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
    }

    [Theory]
    // The reference reports TS2526 outside class and interface members,
    // including object type literals of aliases and nested object types.
    [InlineData("type T = { m(): this; };")]
    [InlineData("interface A { p: { q: this; }; }")]
    [InlineData("type X = this;")]
    [InlineData("let x: this;")]
    [InlineData("type F = (a: this) => void;")]
    [InlineData("function f(): this { return 1 as any; }")]
    // Accepted by the reference, not modelled.
    [InlineData("interface A { m(): this; }\ndeclare let a: A;")]
    [InlineData("interface A { [k: string]: this; }")]
    [InlineData("interface Foo<T> { x: T }\ninterface A { m(): Foo<this>; }")]
    [InlineData("interface A { m(): this; }\ninterface A { n(): void; }")]
    [InlineData("interface B { n(): void; }\ninterface A extends B { m(): this; }")]
    // `this` expressions are still unsupported.
    [InlineData("let y = true ? 1 : this;")]
    public void UnmodelledFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("this-type-use.ts",source),d=>d.Code=="TSCS1001");
}

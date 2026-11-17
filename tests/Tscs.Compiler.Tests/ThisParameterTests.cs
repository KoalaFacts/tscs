using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: `this` parameters in signatures (`JSON.parse`'s reviver
// `(this: any, key: string, value: any) => any`, typed-array callbacks,
// `CallableFunction`) first failed in the pinned lib.es5.d.ts as unsupported
// syntax. A `this` parameter is the first parameter of a function type or
// method signature and is not positional. A declaration whose members declare
// one is checked like one with signatures, and any use of it, or of a function
// type with a `this` parameter anywhere else, fails closed. Expected results
// match the pinned Go CLI.
public sealed class ThisParameterTests
{
    [Theory]
    [InlineData("interface J { parse(text: string, reviver?: (this: any, key: string, value: any) => any): any }")]
    [InlineData("interface S { m(this: void, v: number): number }")]
    [InlineData("type T = { f: (this: string) => void };")]
    [InlineData("interface S { f(cb: (this: number, ...rest: string[]) => void): void }")]
    [InlineData("interface S { m(this: number): void; m(v: number): void }")]
    public void DeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("index.ts",source));

    [Theory]
    // The `this` parameter's type is checked at its own position.
    [InlineData("interface S { m(this: Missing): void }","TS2304",22)]
    [InlineData("interface S { f: (this: Missing, v: number) => void }","TS2304",24)]
    public void DiagnosticsMatchThePinnedChecker(string source,string code,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("this-error.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
    }

    [Theory]
    // Uses, value-function `this` parameters, recursive references, and the
    // reference's TS2300/TS2680 for misplaced or repeated `this` parameters
    // are not modelled.
    [InlineData("interface S { m(this: S): void }")]
    [InlineData("interface S { m(this: S): void }\ndeclare let s: S;")]
    [InlineData("declare let f: (this: number) => void;")]
    [InlineData("let x: { m(this: number): void };")]
    [InlineData("type F = (this: number) => void;\ndeclare let f: F;")]
    [InlineData("function f(this: number) {}")]
    [InlineData("interface S { m(this: S, this: S): void }")]
    [InlineData("interface S { m(v: number, this: S): void }")]
    [InlineData("interface S { m(this: S): void }\ninterface S { n(): void }")]
    public void UnmodelledFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("this-use.ts",source),d=>d.Code=="TSCS1001");
}

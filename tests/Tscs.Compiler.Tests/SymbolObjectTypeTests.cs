using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: `symbol` and `object` are keyword types (pinned lib.es5.d.ts:
// `interface Symbol { valueOf(): symbol }`, `interface WeakKeyTypes { object:
// object }`), but tscs resolved them as names and reported a wrong TS2304
// "Cannot find name 'symbol'" for every use. Neither primitive is in the type
// model yet: a declaration whose members mention one is checked like one with
// signatures, and any other use fails closed with TSCS1001 instead of a wrong
// diagnostic. Expected results match the pinned Go CLI.
public sealed class SymbolObjectTypeTests
{
    [Theory]
    [InlineData("interface Symbol { toString(): string; valueOf(): symbol; }")]
    [InlineData("interface WeakKeyTypes { object: object; }")]
    [InlineData("interface S { m(x: symbol | undefined, y: object[]): void }")]
    [InlineData("interface S { f: (k: symbol) => object }")]
    [InlineData("interface G<T> { m(): symbol }")]
    [InlineData("type T = { valueOf(): symbol };")]
    public void DeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("index.ts",source));

    [Theory]
    // Other member types are still checked at their own positions.
    [InlineData("interface S { m(x: Missing): symbol }","TS2304",19)]
    [InlineData("interface S { o: object; p: Missing }","TS2304",28)]
    public void DiagnosticsMatchThePinnedChecker(string source,string code,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("keyword-error.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
    }

    [Theory]
    // The reference accepts all of these (except `symbol<number>`, TS1005);
    // none is modelled, so each fails closed and never reports TS2304.
    [InlineData("let a: symbol;")]
    [InlineData("let a: object = {};")]
    [InlineData("type K = string | symbol;")]
    [InlineData("type F = () => symbol;")]
    [InlineData("function f<T extends object>(x: T) {}")]
    [InlineData("interface S { m<T extends object>(): void }")]
    [InlineData("interface S { [k: string]: object }")]
    [InlineData("interface S { m(): symbol<number> }")]
    [InlineData("interface S { valueOf(): symbol }\ndeclare var s: S;")]
    [InlineData("let x: { o: object };")]
    [InlineData("interface S { valueOf(): symbol }\ninterface S { n(): void }")]
    [InlineData("declare let x: unknown;\nlet y = x as object;")]
    public void UnmodelledUsesFailClosed(string source)
    {
        var diagnostics=Compilation.Check("keyword-use.ts",source);
        Assert.Contains(diagnostics,d=>d.Code=="TSCS1001");
        Assert.DoesNotContain(diagnostics,d=>d.Code=="TS2304");
    }
}

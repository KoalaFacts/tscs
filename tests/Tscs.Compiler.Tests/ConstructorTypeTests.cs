using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: constructor types `new (...) => R` and `abstract new (...) => R`
// (`ConstructorParameters` and `InstanceType` of the pinned lib.es5.d.ts).
// They are checked only inside conditional alias operands and constraints,
// whose result is never used; every other position fails closed until
// construct types are modelled. Expected results match the pinned Go CLI.
public sealed class ConstructorTypeTests
{
    [Theory]
    [InlineData("type ConstructorParameters<T extends abstract new (...args: any) => any> = T extends abstract new (...args: infer P) => any ? P : never;")]
    [InlineData("type InstanceType<T extends abstract new (...args: any) => any> = T extends abstract new (...args: any) => infer R ? R : any;")]
    [InlineData("type X<T> = T extends new (x: number) => infer R ? R : never;")]
    [InlineData("type X<T> = T extends abstract new () => any ? 1 : 2;")]
    [InlineData("type X<T> = T extends [new () => void] ? 1 : 2;")]
    // `abstract` stays a contextual name.
    [InlineData("type abstract = number;\nlet x: abstract = 1;")]
    public void DeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("index.ts",source));

    [Fact]
    public void ParameterNamesResolveLikeThePinnedChecker()
    {
        var diagnostic=Assert.Single(Compilation.Check("constructor-type-error.ts",
            "type X<T> = T extends new (x: Missing) => infer R ? R : never;"));
        Assert.Equal("TS2304",diagnostic.Code);
        Assert.Equal(30,diagnostic.Start);
    }

    [Theory]
    // Accepted by the reference, not modelled.
    [InlineData("type C = new () => number;")]
    [InlineData("type C = abstract new () => number;")]
    [InlineData("let c: new () => number;")]
    [InlineData("interface I { f(c: new () => number): void }")]
    [InlineData("type G<T> = T extends new <U>(x: U) => infer R ? R : never;")]
    [InlineData("type InstanceType<T extends abstract new (...args: any) => any> = T extends abstract new (...args: any) => infer R ? R : any;\nlet x: InstanceType<new () => string>;")]
    [InlineData("type X<T> = T extends abstract\n  new () => infer R ? R : never;")]
    // Reference TS2344: a constructor type has no call signature (Codex on #278).
    [InlineData("type Need<F extends () => number> = F;\ntype X<T> = T extends Need<new () => number> ? 1 : 2;")]
    [InlineData("type Need<F extends () => number> = F;\ntype X<T extends Need<new () => number>> = T extends string ? 1 : 2;")]
    [InlineData("type Need<F> = F;\ntype X<T> = T extends { a: Need<[new () => number]> } ? 1 : 2;")]
    // Reference TS2681: a constructor cannot have a `this` parameter.
    [InlineData("type X<T> = T extends new (this: string) => any ? 1 : 2;")]
    public void UnmodelledFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("constructor-type-use.ts",source),d=>d.Code=="TSCS1001");

    [Theory]
    // `new` expressions stay outside the supported expression subset.
    [InlineData("declare function g(): number;\nlet y = [new (g)()];")]
    [InlineData("declare function g(): number;\nlet y = f(new (g)());")]
    public void NewExpressionsStillFailClosed(string source)=>
        Assert.Contains(Compilation.Check("new-expression.ts",source),d=>d.Code=="TSCS1001");
}

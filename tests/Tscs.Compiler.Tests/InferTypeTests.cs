using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: `infer` declarations in the extends type of a conditional alias body
// (`ReturnType`, `Parameters`, `ThisParameterType` of the pinned
// lib.es5.d.ts). An `infer` name is an opaque parameter of the true branch
// only. In a checked conditional alias (operands and constraints), function
// types may carry a `this` parameter and a rest parameter typed `any`,
// `never`, `infer X`, or a name inferred at a rest position; everywhere else
// those rest forms fail closed (`...args: any` is not `any[]` in the
// reference). Expected results match the pinned Go CLI.
public sealed class InferTypeTests
{
    [Theory]
    [InlineData("type ReturnType<T extends (...args: any) => any> = T extends (...args: any) => infer R ? R : any;")]
    [InlineData("type Parameters<T extends (...args: any) => any> = T extends (...args: infer P) => any ? P : never;")]
    [InlineData("type ThisParameterType<T> = T extends (this: infer U, ...args: never) => any ? U : unknown;")]
    [InlineData("type X<T> = T extends (infer U)[] ? U : never;")]
    [InlineData("type X<T> = T extends readonly (infer U)[] ? U : never;")]
    [InlineData("type X<T> = T extends Array<infer U> ? U : never;")]
    [InlineData("type X<T> = T extends infer T ? T : never;")]
    [InlineData("type X<T> = T extends { a: infer U, b: infer V } ? [U, V] : never;")]
    [InlineData("type X<T> = T extends (...args: infer A) => void ? (...args: A) => A : never;")]
    [InlineData("type X<T> = T extends (...args: infer A) => void ? { a: (...args: A) => void } : never;")]
    [InlineData("type X<T> = T extends (...args: infer A) => void ? T extends (...args: infer B) => void ? (...args: A) => B : 1 : 2;")]
    [InlineData("type X<T> = T extends (...args: any) => infer R ? R extends string ? 1 : R : 0;")]
    [InlineData("type X<T> = T extends (this: infer U) => any ? U : unknown;")]
    [InlineData("type X<T> = T extends [(infer A)?] ? A : 3;")]
    public void DeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("index.ts",source));

    [Theory]
    // Infer names are not in scope in the extends type or the false branch.
    [InlineData("type X<T> = T extends [infer U, U] ? U : never;","TS2304",32)]
    [InlineData("type X<T> = T extends [infer U] ? 1 : U;","TS2304",38)]
    [InlineData("type X<T> = T extends (...args: infer A) => void ? 1 : (...args: A) => void;","TS2304",65)]
    [InlineData("type X<T> = T extends [infer U] ? Gone : never;","TS2304",34)]
    public void DiagnosticsMatchThePinnedChecker(string source,string code,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("infer-type-error.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
    }

    [Theory]
    // Accepted by the reference, not modelled.
    [InlineData("type X<T> = T extends [infer U, infer U] ? U : never;")]
    [InlineData("type X<T> = T extends (x: infer U) => infer U ? U : unknown;")]
    [InlineData("type X<T> = T extends infer U extends string ? U : never;")]
    [InlineData("type X<T> = T extends (...args: string) => void ? 1 : 2;")]
    [InlineData("type F = (...args: any) => void;")]
    [InlineData("type F = (...args: never) => void;")]
    [InlineData("let f: (...args: any) => void = (a: string, b: number) => {};")]
    [InlineData("type X<T> = { a: (...args: any) => void };")]
    [InlineData("type X<T extends (...args: any) => any = (a: string) => void> = T extends string ? 1 : 2;")]
    [InlineData("type X<T> = T extends (...args: any) => infer R ? R : any;\nlet x: X<() => string> = \"a\";")]
    // The rules apply to the conditional's own nodes, not to an alias it names.
    [InlineData("type F = (...args: never) => void;\ntype X<T> = T extends F ? 1 : 2;")]
    [InlineData("type G = (this: string) => void;\ntype X<T> = T extends G ? 1 : 2;\nlet g: G = function() {};")]
    // Reference TS1338 (infer outside an extends type) and TS2370 (rest types).
    [InlineData("type X<T> = T extends string ? infer U : never;")]
    [InlineData("type X<T> = infer U;")]
    [InlineData("type X<T> = T extends [infer U] ? (...args: U) => void : never;")]
    [InlineData("type F = (...args: unknown) => void;")]
    [InlineData("type F<A> = (...args: A) => void;")]
    [InlineData("type X<T> = T extends (...args: unknown) => void ? 1 : 2;")]
    // Reference TS2368 (predefined name), TS1005 (`infer A?` in a tuple) and
    // TS1003 (a bare `infer` type name).
    [InlineData("type X<T> = T extends infer string ? 1 : 2;")]
    [InlineData("type X<T> = T extends infer object ? 1 : 2;")]
    [InlineData("type X<T> = T extends [infer A?] ? A : 3;")]
    [InlineData("type infer = string;\nlet x: infer;")]
    [InlineData("let x: string | infer;")]
    public void UnmodelledFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("infer-type-use.ts",source),d=>d.Code=="TSCS1001");
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: conditional types (`Exclude<T, U> = T extends U ? never : T`) failed in
// the pinned lib.es5.d.ts as `This expression or statement form is not
// supported yet`. A generic type alias whose whole body is a conditional type
// (branches may nest further conditionals) is checked: every operand must
// resolve with opaque parameters, and only TS2304 is reported as is. Any use
// of such an alias, a conditional type anywhere else, and operands the
// reference may accept through true-branch narrowing fail closed. `infer` is
// covered by InferTypeTests.
// Expected results match the pinned Go CLI.
public sealed class ConditionalTypeTests
{
    [Theory]
    [InlineData("type Exclude<T, U> = T extends U ? never : T;")]
    [InlineData("type Extract<T, U> = T extends U ? T : never;")]
    [InlineData("type X<T> = T extends string ? 1 : T extends number ? 2 : 3;")]
    [InlineData("type X<T> = T | undefined extends string ? { a: T } : T[];")]
    [InlineData("type X<T> = T extends (x: string) => void ? true : false;")]
    [InlineData("type X<T> = T extends string ? 1 : 2")]
    public void DeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("index.ts",source));

    [Theory]
    [InlineData("type X<T> = T extends Missing ? 1 : 2;","TS2304",22)]
    [InlineData("type X<T> = T extends string ? Missing : number;","TS2304",31)]
    [InlineData("type X<T> = T extends string ? 1 : T extends number ? 2 : Gone;","TS2304",58)]
    [InlineData("type X<T> = T extends { a: Nope } ? 1 : 2;","TS2304",27)]
    [InlineData("type X<T> = T | U extends string ? 1 : 2;","TS2304",16)]
    public void DiagnosticsMatchThePinnedChecker(string source,string code,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("conditional-type-error.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
    }

    [Theory]
    // Accepted by the reference, not modelled.
    [InlineData("type X = string extends string ? 1 : 2;")]
    [InlineData("type X<T> = T extends string ? 1 : 2;\nlet x: X<string> = 1;")]
    [InlineData("let y: string extends string ? 1 : 2 = 1;")]
    [InlineData("type X<T> = { a: T extends string ? 1 : 2 };")]
    [InlineData("type X<T> = () => T extends string ? 1 : 2;")]
    [InlineData("type X<T> = (() => T) extends string ? 1 : 2;")]
    [InlineData("type X<T> = T extends string ? X<T> : never;")]
    // The reference narrows T to `string & T` in the true branch.
    [InlineData("type B<S extends string> = S;\ntype X<T> = T extends string ? B<T> : never;")]
    // The reference reports TS2344 for `number & T`; tscs cannot tell the two apart.
    [InlineData("type B<S extends string> = S;\ntype X<T> = T extends number ? B<T> : never;")]
    // TS2304 would be wrong here (reference TS2693, TS2314).
    [InlineData("type X<T> = T extends string ? 1 : typeof T;")]
    [InlineData("type X<T> = T extends string ? 1 : Array;")]
    [InlineData("type X<T> = T extends string ? 1 : { a: ReadonlyArray };")]
    // A conditional extends type cannot itself be conditional (reference TS1005).
    [InlineData("type X<T> = T extends string extends T ? 1 : 2 ? 3 : 4;")]
    public void UnmodelledFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("conditional-type-use.ts",source),d=>d.Code=="TSCS1001");

    // `extends` after a line break does not continue the alias (reference TS1128).
    [Fact]
    public void ExtendsAfterALineBreakIsNotAConditional()=>
        Assert.NotEmpty(Compilation.Check("conditional-type-line.ts","type X<T> = T\n  extends string ? 1 : 2;"));
}

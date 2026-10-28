using Tscs;
using Xunit;
namespace Tscs.Tests;

// AST-to-checker type operators; these are not original TS7 acceptance.
public sealed class TypeOperatorTests
{
    [Theory]
    [InlineData("let n: number = 21 as number;")]
    [InlineData("let n: 21 = 21 as 21;")]
    [InlineData("let n: number = (21 as number) + 21;")]
    [InlineData("type Item = { value: number }; let raw: any = { value: 21 }; let p: Item = raw as Item;")]
    [InlineData("let p = { value: 21, extra: 2 } as { value: number }; let n: number = p.value;")]
    [InlineData("let n: number = ({ value: 21 } as { value: number }).value;")]
    [InlineData("let n: number = 21 satisfies number;")]
    [InlineData("let n: number = (21 satisfies number) + 21;")]
    [InlineData("let p = { value: 21 } satisfies { value: number }; let n: number = p.value;")]
    [InlineData("type Item = { value: number }; let p = { value: 21 } satisfies Item;")]
    [InlineData("let raw: any = { value: 21 }; let p = (raw as { value: number }) satisfies { value: number };")]
    [InlineData("let n: number = (21 as number) satisfies number;")]
    [InlineData("let p = { value: 21 } satisfies { value: 21 }; let n: 21 = p.value;")]
    [InlineData("let p = { kind: \"ok\" } satisfies { kind: \"ok\" | \"bad\" }; let s: \"ok\" = p.kind;")]
    [InlineData("let n: 21 = 21 satisfies 21;")]
    public void CheckedAssertionsAndConstraints(string source) =>
        Assert.Empty(Compilation.Check("type-operators.ts",source));

    [Theory]
    [InlineData("let n: number = \"oops\" as number;", "TS2352")]
    [InlineData("let s: string = 21 as string;", "TS2352")]
    [InlineData("let s = \"oops\" satisfies number;", "TS1360")]
    [InlineData("let p = { value: \"oops\" } satisfies { value: number };", "TS2322")]
    [InlineData("let p = { value: 21, extra: 1 } satisfies { value: number };", "TS2353")]
    [InlineData("let s: string = 21 satisfies number;", "TS2322")]
    [InlineData("let x = 21 as MissingType;", "TS2304")]
    [InlineData("let x = 21 satisfies MissingType;", "TS2304")]
    [InlineData("let p = { value: 21 } as { label: string };", "TS2352")]
    [InlineData("let p = { value: 21 } satisfies { value: number }; p.value = \"oops\";", "TS2322")]
    public void InvalidAssertionOrConstraintHasRealDiagnostic(string source,string expectedCode) =>
        Assert.Contains(Compilation.Check("type-operators.ts",source),d=>d.Code==expectedCode);

    [Fact]
    public void SatisfiesRetainsContextualLiteralForWrites()
    {
        var diagnostics=Compilation.Check("type-operators.ts",
            "let p = { kind: \"ok\" } satisfies { kind: \"ok\" | \"bad\" }; p.kind = \"bad\";");
        Assert.Contains(diagnostics,d=>d.Code=="TS2322");
    }

    [Fact]
    public void UnsupportedDiscriminantUnionSatisfiesFailsClosed()
    {
        var source="type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; " +
            "let p = { kind: \"a\", value: 1 } satisfies V;";
        Assert.Contains(Compilation.Check("type-operators.ts",source),d=>d.Code=="TSCS1001");
    }

    [Fact]
    public void TypeOperatorsDoNotReplaceRuntimeValidation()
    {
        // An explicit assertion is a compile-time view, not a runtime cast.
        // A disjoint assertion is still TS2352; a compatible one emits as JS.
        Assert.Contains(Compilation.Check("type-operators.ts","let bad = 21 as string;"),
            d=>d.Code=="TS2352");
    }
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class KeyofObjectTests
{
    [Theory]
    [InlineData("type Keys = keyof { value: number; label: string }; let x: Keys = \"value\";")]
    [InlineData("interface Data { value: number; label: string } type Keys = keyof Data; let x: Keys = \"label\";")]
    [InlineData("type Empty = keyof {}; let x: Empty;")]
    [InlineData("type Keys = keyof {a: number; b: number}; let x: Keys = \"a\"; x = \"b\";")]
    [InlineData("type Field = keyof { readonly value: number; name: string }; let x: Field = \"name\";")]
    [InlineData("type Shape = { a: number; b: boolean }; type K = keyof Shape; let k: K = \"b\";")]
    [InlineData("type Keys = keyof ({ a: number; b: string }); let k: Keys = \"a\";")]
    [InlineData("type Keys = keyof {name: string}; function echo(k: Keys): Keys {return k;} let result: Keys = echo(\"name\");")]
    [InlineData("let keyof: number = 42; let n: number = keyof;")]
    public void CheckedObjectKeyTypesCompile(string source) =>
        Assert.Empty(Compilation.Check("keyof.ts",source));

    [Theory]
    [InlineData("type Keys = keyof {value: number}; let x: Keys = \"missing\";", "TS2322")]
    [InlineData("interface Data {label: string} let x: keyof Data = 42;", "TS2322")]
    [InlineData("type Keys = keyof {a: number}; let x: Keys = true;", "TS2322")]
    [InlineData("type Keys = keyof {}; let x: Keys = \"not-present\";", "TS2322")]
    [InlineData("type Keys = keyof number; let x: Keys = \"toString\";", "TSCS1001")]
    [InlineData("type Keys = keyof (number | string); let x: Keys = \"length\";", "TSCS1001")]
    [InlineData("type Keys = keyof {0: number}; let x: Keys = 0;", "TSCS1001")]
    public void InvalidOrUnimplementedKeyTypesFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("keyof-bad.ts",source),d=>d.Code==code);
}

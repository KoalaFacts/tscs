using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ReadonlyObjectTests
{
    [Theory]
    [InlineData("interface Item { readonly value: number; label: string } let item: Item = { value: 21, label: \"ok\" }; let result: number = item.value;")]
    [InlineData("type Inner = { readonly value: number }; let outer: { inner: Inner } = { inner: { value: 21 } }; let result: number = outer.inner.value;")]
    [InlineData("let item: { readonly: number } = { readonly: 21 }; item.readonly = 42;")]
    [InlineData("interface Item { readonly value: number; label: string } let item: Item = { value: 21, label: \"ok\" }; item.label = \"ready\";")]
    [InlineData("let item: { readonly value: number } = { value: 21 }; let count: number = item.value;")]
    public void ReadonlyMembersCanBeReadAndMutableMembersCanBeWritten(string source) =>
        Assert.Empty(Compilation.Check("readonly.ts",source));

    [Theory]
    [InlineData("interface Item { readonly value: number } let item: Item = { value: 21 }; item.value = 42;", "TS2540")]
    [InlineData("type Item = { readonly value: number }; let item: Item = { value: 21 }; item.value += 1;", "TS2540")]
    [InlineData("interface Inner { readonly value: number } let item: { inner: Inner } = { inner: { value: 21 } }; item.inner.value = 42;", "TS2540")]
    [InlineData("let item: { readonly value: number } = { value: 21 }; item.value /= 2;", "TS2540")]
    [InlineData("type Item = { readonly value: number }; let item: Item = { value: 21 }; item.value <<= 1;", "TS2540")]
    public void WritesThroughReadonlyPropertyAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("readonly.ts",source),diagnostic=>diagnostic.Code==code);
}

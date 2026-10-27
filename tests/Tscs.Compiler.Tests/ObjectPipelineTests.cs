using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ObjectPipelineTests
{
    [Theory]
    [InlineData("const item = { quantity: 21, owner: \"Ada\" }; let answer: number = item.quantity * 2;")]
    [InlineData("let item: { quantity: number; owner: string } = { quantity: 21, owner: \"Ada\" }; item.quantity = item.quantity * 2;")]
    [InlineData("function double(item: { quantity: number }): { quantity: number } { return { quantity: item.quantity * 2 }; } let answer: number = double({ quantity: 21 }).quantity;")]
    [InlineData("const full: { quantity: number } = { quantity: 21, label: \"extra\" };")]
    [InlineData("let nested: { details: { quantity: number } } = { details: { quantity: 42 } }; let total: number = nested.details.quantity;")]
    public void RequiredNamedObjectShapesWorkAcrossTheExistingPipeline(string source) =>
        Assert.Empty(Compilation.Check("source.ts",source));

    [Theory]
    [InlineData("const item = { quantity: 2 }; let n: number = item.missing;","TS2339")]
    [InlineData("const item: { quantity: number } = { quantity: \"wrong\" };","TS2322")]
    [InlineData("const item: { quantity: number } = { };","TS2322")]
    [InlineData("let item: { quantity: number } = { quantity: 1 }; item.quantity = \"wrong\";","TS2322")]
    [InlineData("function read(item: { quantity: number }): number { return item.quantity; } let n: number = read({ name: 21 });","TS2345")]
    [InlineData("const item = { quantity: 1, quantity: 2 };","TS1117")]
    [InlineData("let item: { quantity: number; quantity: string } = { quantity: 1 };","TSCS1001")]
    [InlineData("const item = { quantity: 2 }; let n: number = item.quantity.extra;","TS2339")]
    public void IncorrectObjectShapesAreRejected(string source,string expected) =>
        Assert.Contains(Compilation.Check("source.ts",source),d=>d.Code==expected);
}

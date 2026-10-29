using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class IndexedAccessTypeTests
{
    [Theory]
    [InlineData("type Bag={value:number;label:string};type V=Bag[\"value\"];let x:V=42;")]
    [InlineData("interface Bag{value:number;label:string} type V=Bag[\"label\"];let x:V=\"ok\";")]
    [InlineData("type Bag={nested:{value:number}};type V=Bag[\"nested\"][\"value\"];let x:V=42;")]
    [InlineData("type V={a:number;b:string}[\"a\"|\"b\"];let n:V=42;let s:V=\"hi\";")]
    [InlineData("type Bag={value:number;label:string};type V=Bag[keyof Bag];let x:V=\"hi\";")]
    [InlineData("type A={shared:number;left:string};type B={shared:string;right:number};type V=(A|B)[\"shared\"];let x:V=42;")]
    [InlineData("type V={0:number;\"1\":string}[0|\"1\"];let x:V=42;")]
    [InlineData("type V={value:number}[\"value\"][];let x:V=[42];")]
    [InlineData("type V={readonly value:number}[\"value\"];let x:V=42;")]
    [InlineData("type Bag={value:number};type K=\"value\";type V=Bag[K];let x:V=42;")]
    public void ExactIndexedTypesCompile(string source) =>
        Assert.Empty(Compilation.Check("indexed-types.ts",source));

    [Theory]
    [InlineData("type V={value:number}[\"value\"];let x:V=\"bad\";", "TS2322")]
    [InlineData("type V={value:number}[\"missing\"];let x:V=42;", "TS2339")]
    [InlineData("type V={value:number}[\"value\"|\"missing\"];let x:V=42;", "TS2339")]
    [InlineData("type V={value:number}[string];let x:V=42;", "TSCS1001")]
    [InlineData("type V=number[\"value\"];let x:V=42;", "TSCS1001")]
    [InlineData("type V={value:number}[Missing];let x:V=42;", "TS2304")]
    [InlineData("type V={value:number}[true];let x:V=42;", "TSCS1001")]
    [InlineData("type V=({a:number}|{b:string})[\"a\"];let x:V=42;", "TS2339")]
    public void MissingOrUnimplementedIndexedTypesFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("indexed-types-bad.ts",source),d=>d.Code==code);
}

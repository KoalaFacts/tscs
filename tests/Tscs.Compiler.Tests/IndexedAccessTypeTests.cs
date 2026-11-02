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
    [InlineData("type A={nested:{0:number}};type B={nested:{\"0\":number}};type V=(A|B)[\"nested\"];let key:keyof V;")]
    [InlineData("type A={nested:{0:number}};type B={nested:{\"0\":number}};type V=(B|A)[\"nested\"];let key:keyof V;")]
    public void ExactIndexedTypesCompile(string source) =>
        Assert.Empty(Compilation.Check("indexed-types.ts",source));

    [Theory]
    [InlineData("type A=number[];type V=A[number];let x:V=42;")]
    [InlineData("type A=number[];type V=A[0];let x:V=42;")]
    [InlineData("type A=(readonly string[]);type V=A[number];let x:V=\"ok\";")]
    [InlineData("type A=ReadonlyArray<boolean>;type V=A[1];let x:V=true;")]
    [InlineData("type A=(number[]|string[]);type V=A[number];let x:V=\"hi\";")]
    [InlineData("type A=number[];type V=A[\"length\"];let x:V=2;")]
    [InlineData("type A=string[];type V=A[number|\"length\"];let x:V=2;let y:V=\"hi\";")]
    [InlineData("type A={items:{id:number}[]};type V=A[\"items\"][number];let x:V={id:7};")]
    [InlineData("const values:number[]=[1,2];type V=typeof values[number];let x:V=42;")]
    public void BoundedArrayElementTypeSelectionsCompile(string source) =>
        Assert.Empty(Compilation.Check("array-indexed-types.ts",source));

    [Theory]
    [InlineData("type V=number[][number];let x:V=\"bad\";", "TS2322")]
    [InlineData("type V=(readonly string[])[number];let x:V=42;", "TS2322")]
    [InlineData("type V=(number[]|string[])[number];let x:V=true;", "TS2322")]
    [InlineData("type V=number[][\"length\"];let x:V=\"bad\";", "TS2322")]
    [InlineData("type V={id:number}[][number];let x:V=\"bad\";", "TS2322")]
    [InlineData("type V=number[][boolean];let x:V=42;", "TSCS1001")]
    [InlineData("type V=number[][\"unknown\"];let x:V=42;", "TSCS1001")]
    [InlineData("type V=(number[]|{id:number})[number];let x:V=42;", "TSCS1001")]
    public void BoundedArrayIndexErrorsAreNotSilenced(string source,string code) =>
        Assert.Contains(Compilation.Check("array-indexed-types-bad.ts",source),diagnostic=>diagnostic.Code==code);

    [Theory]
    [InlineData("type V={value:number}[\"value\"];let x:V=\"bad\";", "TS2322")]
    [InlineData("type V={value:number}[\"missing\"];let x:V=42;", "TS2339")]
    [InlineData("type V={value:number}[\"value\"|\"missing\"];let x:V=42;", "TS2339")]
    [InlineData("type V={value:number}[string];let x:V=42;", "TSCS1001")]
    [InlineData("type V=number[\"value\"];let x:V=42;", "TSCS1001")]
    [InlineData("type V={value:number}[Missing];let x:V=42;", "TS2304")]
    [InlineData("type V={value:number}[true];let x:V=42;", "TSCS1001")]
    [InlineData("type V=({a:number}|{b:string})[\"a\"];let x:V=42;", "TS2339")]
    [InlineData("type A={nested:{0:number}};type B={nested:{\"0\":number}};type V=(A|B)[\"nested\"];let key:keyof V=0;", "TS2322")]
    [InlineData("type A={nested:{0:number}};type B={nested:{\"0\":number}};type V=(A|B)[\"nested\"];let key:keyof V=\"0\";", "TS2322")]
    [InlineData("type A={nested:{0:number}};type B={nested:{\"0\":number}};type V=(B|A)[\"nested\"];let key:keyof V=0;", "TS2322")]
    [InlineData("type A={nested:{0:number}};type B={nested:{\"0\":number}};type V=(B|A)[\"nested\"];let key:keyof V=\"0\";", "TS2322")]
    [InlineData("type A={0:number};type B={\"0\":number};let key:keyof (A|B)=0;", "TS2322")]
    [InlineData("type A={0:number};type B={\"0\":number};let key:keyof (B|A)=\"0\";", "TS2322")]
    public void MissingOrUnimplementedIndexedTypesFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("indexed-types-bad.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("type C=string[number];let x:C=\"a\";")]
    [InlineData("type C=string[0];let x:C=\"a\";")]
    [InlineData("type C=\"hello\"[1];let x:C=\"a\";")]
    [InlineData("type C=string[\"length\"];let x:C=5;")]
    [InlineData("type C=\"world\"[\"length\"];let x:C=5;")]
    [InlineData("type C=string[number|\"length\"];let a:C=\"a\";let b:C=1;")]
    [InlineData("type C=(\"abc\"|\"xyz\")[number];let a:C=\"q\";")]
    [InlineData("type C=(\"abc\"|\"xyz\")[\"length\"];let a:C=1;")]
    [InlineData("type K=number;type C=string[K];let a:C=\"a\";")]
    [InlineData("type K=0|\"length\";type C=string[K];let a:C=1;let b:C=\"a\";")]
    public void StringIndexedAccessTypesUseCodeUnitsAndLength(string source) =>
        Assert.Empty(Compilation.Check("string-indexed-types.ts",source));

    [Theory]
    [InlineData("type C=string[number];let x:C=1;", "TS2322")]
    [InlineData("type C=string[1];let x:C=true;", "TS2322")]
    [InlineData("type C=\"hi\"[0];let x:C=42;", "TS2322")]
    [InlineData("type C=string[\"length\"];let x:C=\"bad\";", "TS2322")]
    [InlineData("type C=(\"a\"|\"b\")[\"length\"];let x:C=true;", "TS2322")]
    [InlineData("type C=string[number|\"length\"];let x:C=false;", "TS2322")]
    [InlineData("type K=\"length\";type C=string[K];let x:C=\"bad\";", "TS2322")]
    [InlineData("const word:string=\"hi\";type C=typeof word[number];let x:C=false;", "TS2322")]
    [InlineData("type C=string[\"missing\"];let x:C=1;", "TSCS1001")]
    [InlineData("type C=string[boolean];let x:C=\"a\";", "TSCS1001")]
    public void InvalidStringIndexedAccessTypesFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("string-indexed-types-bad.ts",source),
            diagnostic=>diagnostic.Code==code);
}

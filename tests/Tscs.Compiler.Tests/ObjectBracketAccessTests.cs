using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ObjectBracketAccessTests
{
    [Theory]
    [InlineData("let obj={value:21};let value:number=obj[\"value\"];")]
    [InlineData("let obj={\"data-key\":42};let value:number=obj[\"data-key\"];")]
    [InlineData("let obj={inner:{value:21}};let value:number=obj[\"inner\"][\"value\"];")]
    [InlineData("let obj:{value:number}={value:20};obj[\"value\"]=42;")]
    [InlineData("let obj:{value:number}={value:20};obj[\"value\"]+=1;")]
    [InlineData("let obj:{value:number}={value:20};obj[\"value\"]++;")]
    [InlineData("let obj:{value:number}={value:20};++obj[\"value\"];")]
    [InlineData("interface Item { readonly value: number } let obj:Item={value:42};let value:number=obj[\"value\"];")]
    [InlineData("let obj={value:21};let value:number=obj[(\"value\")];")]
    [InlineData("let obj:any={value:21};let value:number=obj[\"value\"];")]
    [InlineData("let obj={value:21};let value:number=obj[\"va\\u006cue\"];")]
    public void LiteralStringKeysReadAndWriteCheckedObjectProperties(string source) =>
        Assert.Empty(Compilation.Check("indexed-object.ts",source));

    [Theory]
    [InlineData("let obj:{value:number}={value:21};obj[\"value\"]=\"bad\";","TS2322")]
    [InlineData("let obj:{readonly value:number}={value:21};obj[\"value\"]=42;","TS2540")]
    [InlineData("let obj:{readonly value:number}={value:21};obj[\"value\"]+=1;","TS2540")]
    [InlineData("let obj:{readonly value:number}={value:21};obj[\"value\"]++;","TS2540")]
    [InlineData("let obj:{readonly value:number}={value:21};++obj[\"value\"];","TS2540")]
    [InlineData("let obj={value:21};let result:string=obj[\"value\"];","TS2322")]
    [InlineData("let obj={value:21};obj[\"missing\"];","TS2339")]
    [InlineData("let obj={value:21};let key:string=\"value\";obj[key];","TSCS1001")]
    [InlineData("let obj={value:21};obj[0];","TSCS1001")]
    [InlineData("let xs=[1,2];xs[\"0\"];","TSCS1001")]
    public void InvalidOrUnsupportedIndexedOperationsDoNotSucceed(string source,string diagnostic) =>
        Assert.Contains(Compilation.Check("indexed-object.ts",source),d=>d.Code==diagnostic);

    [Fact]
    public void MissingNamedKeyDiagnosticPointsToOpeningBracket()
    {
        const string source="let obj={value:21};obj[\"missing\"];";
        var diagnostic=Assert.Single(Compilation.Check("indexed-object.ts",source));
        Assert.Equal("TS2339",diagnostic.Code);
        Assert.Equal(source.LastIndexOf('['),diagnostic.Start);
    }
}

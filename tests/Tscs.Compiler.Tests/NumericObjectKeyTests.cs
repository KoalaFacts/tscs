using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class NumericObjectKeyTests
{
    [Theory]
    [InlineData("const slots={0:20,1:22};let total:number=slots[0]+slots[1];")]
    [InlineData("let slots:{0:number;1:number}={0:20,1:22};let total:number=slots[0]+slots[\"1\"];")]
    [InlineData("type Slots={0:number;1:number};const slots:Slots={0:20,1:22};let total:number=slots[0]+slots[1];")]
    [InlineData("interface Slots { readonly 0:number; 1:number } const slots:Slots={0:20,1:22};let n:number=slots[0];")]
    [InlineData("function sum(s:{0:number;1:number}):number{return s[0]+s[1];}let n:number=sum({0:20,1:22});")]
    [InlineData("let x={nested:{0:42}};let result:number=x.nested[0];")]
    [InlineData("let x={\"0\":20,1:22};let n:number=x[0]+x[1];")]
    [InlineData("let x:{0:number}={0:20};x[0]+=1;x[0]++;")]
    [InlineData("const x={0:42};let n:number=x[(0)];")]
    public void StaticallyNamedIntegerPropertiesAreChecked(string source) =>
        Assert.Empty(Compilation.Check("numeric-keys.ts",source));

    [Theory]
    [InlineData("let x:{0:number}={0:20};x[0]=\"bad\";","TS2322")]
    [InlineData("let x:{readonly 0:number}={0:20};x[0]=21;","TS2540")]
    [InlineData("let x:{readonly 0:number}={0:20};x[0]++;","TS2540")]
    [InlineData("let x:{0:number}={0:\"bad\"};","TS2322")]
    [InlineData("const x={0:20,\"0\":22};","TS1117")]
    [InlineData("const x={0:20};x[1];","TSCS1001")]
    [InlineData("const x={1.5:21};","TSCS1001")]
    [InlineData("const x={0:21};let key:number=0;x[key];","TSCS1001")]
    public void NumericKeysDoNotBecomeUncheckedIndexSignatures(string source,string code) =>
        Assert.Contains(Compilation.Check("numeric-keys-invalid.ts",source),
            diagnostic=>diagnostic.Code==code);
}

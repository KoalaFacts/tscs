using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded checked property existence, separate from original TS7 acceptance.
public sealed class InOperatorTests
{
    [Theory]
    [InlineData("let obj={value:42};let present:boolean=\"value\" in obj;")]
    [InlineData("let obj={value:42};let missing:boolean=\"missing\" in obj;")]
    [InlineData("let obj={value:42};let key:string=\"value\";let result:boolean=key in obj;")]
    [InlineData("let obj={value:42};let result:number=(\"value\" in obj)?42:0;")]
    [InlineData("let obj={value:42};let result:boolean=(\"value\" in obj) && (\"missing\" in obj);")]
    [InlineData("let obj:any={value:42};let result:boolean=\"value\" in obj;")]
    [InlineData("interface Item {value:number} let obj:Item={value:42};let result:boolean=\"value\" in obj;")]
    [InlineData("let obj={in:42};let result:boolean=\"in\" in obj;")]
    [InlineData("let xs:number[]=[20,22];let present:boolean=1 in xs;")]
    public void PropertyExistenceProducesBoolean(string source) =>
        Assert.Empty(Compilation.Check("in-operator.ts",source));

    [Theory]
    [InlineData("let obj={value:42};let result:boolean=true in obj;", "TS2322")]
    [InlineData("let x:number=42;let result:boolean=\"value\" in x;", "TS2322")]
    [InlineData("let obj={value:42};let result:number=\"value\" in obj;", "TS2322")]
    public void InvalidOperandOrResultIsRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("in-operator.ts",source),d=>d.Code==code);
}

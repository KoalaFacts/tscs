using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ObjectIntersectionTests
{
    [Theory]
    [InlineData("type Left={amount:number};type Right={label:string};type Both=Left&Right;let x:Both={amount:42,label:\"ok\"};")]
    [InlineData("type Both={a:number}&{b:string}&{c:boolean};let x:Both={a:1,b:\"ready\",c:true};")]
    [InlineData("interface Left{amount:number}interface Right{label:string}type Both=Left&Right;let x:Both={amount:42,label:\"ok\"};")]
    [InlineData("type Both={a:number}&{b:string};let k:keyof Both=\"a\";k=\"b\";")]
    [InlineData("type Both={a:number}&{b:string};type Value=Both[\"a\"];let x:Value=42;")]
    [InlineData("type Both={a:number}&{a:number;b:string};let x:Both={a:42,b:\"ok\"};")]
    [InlineData("type Both={0:number}&{name:string};let k:keyof Both=0;let x:Both={0:42,name:\"ok\"};")]
    [InlineData("type Both={readonly amount:number}&{label:string};let x:Both={amount:42,label:\"ok\"};let n:number=x.amount;")]
    public void CheckedFiniteObjectIntersectionsCompile(string source) =>
        Assert.Empty(Compilation.Check("intersection.ts",source));

    [Theory]
    [InlineData("type Both={a:number}&{b:string};let x:Both={a:1,b:\"ok\"};let n:number=x.b;", "TS2322")]
    [InlineData("type Both={a:number}&{b:string};let x:Both={a:1,b:\"ok\"};let n:number=x.missing;", "TS2339")]
    [InlineData("type Both={readonly a:number}&{b:string};let x:Both={a:1,b:\"ok\"};x.a=2;", "TS2540")]
    [InlineData("type Both={a:number}&{b:string};let k:keyof Both=\"missing\";", "TS2322")]
    [InlineData("type Both={a:number}&number;let x:Both;", "TSCS1001")]
    [InlineData("type Both={a:number}&{a:string};let x:Both;", "TSCS1001")]
    [InlineData("type Both={0:number}&{\"0\":number};let x:Both;", "TSCS1001")]
    [InlineData("type Both=({a:number}|{b:string})&{c:boolean};let x:Both;", "TSCS1001")]
    public void IncompatibleOrUnimplementedIntersectionsFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("intersection-bad.ts",source),d=>d.Code==code);
}

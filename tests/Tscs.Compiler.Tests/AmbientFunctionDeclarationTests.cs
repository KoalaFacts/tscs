using Tscs;
using Xunit;
namespace Tscs.Tests;
public sealed class AmbientFunctionDeclarationTests
{
    [Theory]
    [InlineData("declare function twice(n:number):number; let answer:number=twice(21);")]
    [InlineData("/// <style requireSemi=\"on\" />\ndeclare function first():void; declare function second():void;")]
    [InlineData("declare function greet(s:string, loud?:boolean):string; let a:string=greet(\"hi\");")]
    public void AmbientSignaturesBindAndCheck(string source) =>
        Assert.Empty(Compilation.Check("ambient.ts",source));

    [Fact]
    public void GlobalAmbientDeclarationsBindAcrossFiles() =>
        Assert.Empty(Compilation.CheckFiles([
            new SourceInput("ambient.ts","declare function twice(n:number):number;"),
            new SourceInput("entry.ts","let answer:number=twice(21);")
        ]));

    [Theory]
    [InlineData("declare function f(n:number):number; let n:number=f(\"bad\");","TS2345")]
    [InlineData("declare function f(n:number):number; let n:number=f();","TS2554")]
    [InlineData("declare function f():string; let n:number=f();","TS2322")]
    [InlineData("declare function f():void {}","TSCS1001")]
    [InlineData("declare function f() {}","TSCS1001")]
    [InlineData("declare const value:number;","TSCS1001")]
    [InlineData("declare function f(n:number=1):number;","TSCS1001")]
    [InlineData("declare function f({n}:{n:number}):number;","TSCS1001")]
    [InlineData("/// <reference types=\"node\" />\ndeclare function f():void;","TSCS1001")]
    [InlineData("/// <style unsupported=\"on\" />\ndeclare function f():void;","TSCS1001")]
    public void InvalidAmbientSignaturesAndCallsFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("ambient.ts",source),d=>d.Code==code);

    [Fact]
    public void UnsupportedOverloadMergingCannotPretendToSucceed() =>
        Assert.Contains(Compilation.Check("ambient.ts",
            "declare function f(n:number):number; declare function f(s:string):string;"),
            d=>d.Code=="TSCS1001");
}

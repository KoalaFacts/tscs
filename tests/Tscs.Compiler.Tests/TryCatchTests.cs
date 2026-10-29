using Tscs;
using Xunit;

namespace Tscs.Tests;

// Product source semantics; not original TS7 acceptance.
public sealed class TryCatchTests
{
    [Theory]
    [InlineData("function f(n:number):number {try{if(n<0){throw n;}return n+1;}catch(error){return 42;}}")]
    [InlineData("function f():number {try{throw 21;}catch{return 42;}}")]
    [InlineData("function f():number {try{throw 21;}catch(error){return error;}finally{let clean:number=1;}}")]
    [InlineData("function f():number {try{return 21;}catch{return 42;}finally{return 42;}}")]
    [InlineData("function f():number {try{throw 21;}catch(error){throw error;}finally{return 42;}}")]
    [InlineData("function f(n:number):number {try{try{throw n;}catch(inner){throw inner;}}catch(outer){return outer;}}")]
    [InlineData("let value:number=0;try{throw {code:21};}catch(error){value=error.code;}finally{value+=21;}")]
    [InlineData("let outer:number=21;try{throw 1;}catch(outer){outer=42;}let result:number=outer;")]
    [InlineData("const item={catch:42,finally:21,try:21};let total:number=item.catch;")]
    [InlineData("let total:number=0;for(let i:number=0;i<3;i++){try{if(i===1){throw i;}}catch(error){total+=1;}}")]
    [InlineData("try{throw 21;}catch{let result:number=42;}")]
    [InlineData("let x:number=21;try{x+=1;}catch(error){x+=1;}finally{x+=20;}")]
    public void SupportedCatchControlFlowAndBindingsCompile(string source) =>
        Assert.Empty(Compilation.Check("try-catch.ts",source));

    [Theory]
    [InlineData("try{throw 21;}", "TSCS1001")]
    [InlineData("try{throw 21;}catch(error){let n:number=missing;}", "TS2304")]
    [InlineData("try{throw 21;}catch(error){let error:number=42;}", "TSCS1001")]
    [InlineData("try{throw 21;}catch(error){let n:number=error;}let leaked:number=error;", "TS2304")]
    [InlineData("try{throw 21;}catch{throw missing;}", "TS2304")]
    [InlineData("try{throw 21;}catch(error){let n:number=\"wrong\";}", "TS2322")]
    [InlineData("try{throw 21;}finally{let n:number=\"wrong\";}", "TS2322")]
    [InlineData("try{throw 21;}catch(error:number){let n:number=21;}", "TSCS1001")]
    [InlineData("try{throw 21;}catch({code}){let n:number=21;}", "TSCS1001")]
    [InlineData("function f():number {try{return 42;}catch(error){let n:number=1;}}", "TSCS1001")]
    [InlineData("try{throw 21;}catch(error){return error;}", "TSCS1001")]
    public void InvalidCatchProgramsAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("try-catch-invalid.ts",source),d=>d.Code==code);
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

// Product source semantics; not original TS7 harness credit.
public sealed class ThrowFlowTests
{
    [Theory]
    [InlineData("function f(n:number):number { if(n<0){throw \"negative\";} return n+1; }")]
    [InlineData("function f(n:number):number { if(n>0){return n;} else {throw n;} }")]
    [InlineData("function f(n:number):number { switch(n){case 1:{return 42;} default:{throw \"invalid\";}} }")]
    [InlineData("function f():number { throw \"always\"; }")]
    [InlineData("function f(n:number):number { while(n<0){throw n;} return n; }")]
    [InlineData("function f(n:number):number { do { if(n<0){throw n;} return n; } while(false); }")]
    [InlineData("function f(n:number):number { if(n<0){throw {code:n};} return n; }")]
    [InlineData("function f(n:number):number { for(let i:number=0;i<n;i++){if(i===2){throw i;}} return n; }")]
    [InlineData("function fail(n:number):number { throw n; } function f(n:number):number { return fail(n); }")]
    [InlineData("throw \"root\";")]
    [InlineData("const item={throw:42}; let n:number=item.throw;")]
    public void ThrowExpressionsAndAbruptExitPathsAreChecked(string source) =>
        Assert.Empty(Compilation.Check("throw-ok.ts",source));

    [Theory]
    [InlineData("function f():number { throw missing; }", "TS2304")]
    [InlineData("function f():number { throw; }", "TS1109")]
    [InlineData("function f():number { throw\n 21; }", "TS1142")]
    [InlineData("function f():number { throw (21 as string); }", "TS2352")]
    [InlineData("function f(n:number):number { if(n>0){throw n;} }", "TSCS1001")]
    [InlineData("function f():number { throw new Error(); }", "TSCS1001")]
    public void InvalidThrownExpressionsOrMissingTerminationAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("throw-invalid.ts",source),d=>d.Code==code);

    [Fact]
    public void ThrowDoesNotHideSemanticErrorsInTheOtherBranch()
    {
        const string source="function f(n:number):number { if(n<0){throw n;} else {return \"bad\";} }";
        Assert.Contains(Compilation.Check("throw-type.ts",source),d=>d.Code=="TS2322");
    }
}

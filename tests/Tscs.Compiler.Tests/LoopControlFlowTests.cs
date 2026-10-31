using Tscs;
using Xunit;

namespace Tscs.Tests;

// Local executable and negative product regressions. These are NOT original TS7
// harness cases and cannot be credited to the upstream SKIP ledger.
public sealed class LoopControlFlowTests
{
    [Theory]
    [InlineData("function sum(values:number[]):number { let i:number=0; let total:number=0; while(i<values.length) { total=total+values[i]; i++; if(total>20){break;} } return total; }")]
    [InlineData("function sum(values:number[]):number { let i:number=0; let total:number=0; while(i<values.length) { const x:number=values[i]; i++; if(x<0){continue;} total=total+x; } return total; }")]
    [InlineData("function sum(values:number[]):number { let total:number=0; for(const x of values) { if(x<0){continue;} total=total+x; } return total; }")]
    [InlineData("function sum(values:number[]):number { let i:number=0; let total:number=0; while(i<values.length) { const x:number=values[i]; i++; switch(x) { case 0: { continue; } default: { break; } } total=total+x; } return total; }")]
    [InlineData("function f(n:number):number { let i:number=0; while(i<n) { i++; if(i===2){continue;} if(i>4){break;} } return i; }")]
    [InlineData("function sum(values:number[]):number { let total:number=0; for(const x of values) { if(x>10){break;} if(x<0){continue;} total=total+x; } return total; }")]
    [InlineData("function f(n:number):number { let i:number=0; while(i<n) { i++; for(const x of [1,2]) { if(x===1){continue;} if(x===2){break;} } if(i===2){break;} } return i; }")]
    [InlineData("function f(n:number):number { let i:number=0; while(i<n) { if(i>5){break;} i++; } return i; }")]
    [InlineData("const data={ while:1, continue:2 }; let n:number=data.while+data.continue;")]
    [InlineData("function f(n:number):number { let i:number=0; while(i<n) { i++; if(i<2){continue;} return i; } return 0; }")]
    [InlineData("function f(n:number):number { while(1) {break;} return n; }")]
    [InlineData("function f(n:number):number { while(n>0) n++; return n; }")]
    public void BracedWhileContinueAndGeneralBreakAreChecked(string source) =>
        Assert.Empty(Compilation.Check("loop-control.ts",source));

    [Theory]
    [InlineData("function f(n:number):number { let i:number=0; while(i<n) { let s:string=i; i++; } return i; }")]
    [InlineData("function f(values:number[]):number { let n:number=0; for(const x of values) { let s:string=x; continue; } return n; }")]
    [InlineData("function f(n:number):number { while(n>0) { n=\"bad\"; break; } return n; }")]
    [InlineData("function f(values:number[]):number { let i:number=0; while(i<values.length) { i++; let s:string=values[i]; continue; } return i; }")]
    public void WrongAssignmentsInsideRealLoopBodiesStillFail(string source) =>
        Assert.Contains(Compilation.Check("loop-invalid.ts",source), d=>d.Code=="TS2322");

    [Theory]
    [InlineData("continue;")]
    [InlineData("function f(n:number):number { continue; return n; }")]
    [InlineData("function f(n:number):number { switch(n) { case 1: { continue; } default: { return n; } } }")]
    [InlineData("break;")]
    [InlineData("function f(n:number):number { if(n>0){break;} return n; }")]
    [InlineData("function f(n:number):number { while(n>0) const x=1; return n; }")]
    public void UnimplementedOrOutOfLoopControlFlowIsRejected(string source) =>
        Assert.Contains(Compilation.Check("loop-boundary.ts",source), d=>d.Code=="TSCS1001");

    [Fact]
    public void ContinueInSwitchInsideLoopDoesNotExitLoopOrFunction()
    {
        const string source="function f(values:number[]):number { let i:number=0; let sum:number=0; while(i<values.length) { const n:number=values[i]; i++; switch(n){case 0:{continue;}default:{break;}} sum=sum+n; if(sum>20){break;} } return sum; }";
        Assert.Empty(Compilation.Check("nearest-target.ts",source));
    }

    [Fact]
    public void LoopBodyCannotFabricateDefiniteInitializationAfterZeroIterations()
    {
        const string source="function f(flag:boolean):number { let value:number; while(flag){value=1;break;} return value; }";
        Assert.Contains(Compilation.Check("definite-init.ts",source), d=>d.Code=="TSCS1001");
    }

    [Fact]
    public void ConservativeWhileReturnAnalysisRequiresARealFollowingReturn()
    {
        const string source="function f():number { while(true){return 42;} }";
        Assert.Contains(Compilation.Check("bounded-return.ts",source), d=>d.Code=="TSCS1001");
    }

    [Fact]
    public void LoopDepthLimitReportsACompilerErrorInsteadOfCrashing()
    {
        string source="function f():number { "+string.Concat(Enumerable.Repeat("while(true){",140))+
            "return 1;"+new string('}',140)+"return 0;}";
        Assert.Contains(Compilation.Check("deep-loop.ts",source), d=>d.Code=="TSCS1001");
    }
}

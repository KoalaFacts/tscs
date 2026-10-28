using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class SwitchCompletionTests
{
    private const string EventType =
        "type E = { kind: \"ok\"; value: number } | { kind: \"error\"; message: string }; ";

    [Theory]
    [InlineData("function f(n:number):number { let result:number=0; switch(n) { case 1: { result=21; break; } default: { result=0; break; } } return result; }")]
    [InlineData("function f(n:number):number { let result:number=0; switch(n) { case 1: { result=20; } case 2: { result=result+1; break; } default: { break; } } return result; }")]
    [InlineData("function f(n:number):number { let result:number=0; switch(n) { default: { result=7; break; } case 1: { result=21; break; } } return result; }")]
    [InlineData("function f(n:number):number { switch(n) { case 1: { break; } default: { return 0; } } return 21; }")]
    [InlineData("function f(n:number):number { let sum:number=0; for (const value of [21,22]) { sum=sum+value; break; } return sum+sum; }")]
    [InlineData("function f(n:number):number { let sum:number=0; for (const value of [21,22]) { if(value>20) { break; } sum=sum+value; } return sum; }")]
    [InlineData("function f(n:number):number { switch(n) { case 1: { if(n>0) { break; } return 0; } default: { return 0; } } return 42; }")]
    [InlineData("function f(n:number):number { switch(n) { case 1: { break; n=2; } default: { return 0; } } return n; }")]
    [InlineData("function f(n:number):number { for (const x of [1,2]) { break; n=3; } return n; }")]
    [InlineData("function f(n:number):number { let result:number=0; switch(n) { case 1: { if(n>0) { result=20; } else { result=10; } } case 2: { result=result+1; break; } default: { break; } } return result; }")]
    [InlineData("function f(n:number):number { switch(n) { case 1: { return 1; } default: { return 2; } } }")]
    public void NormalBreakAndFallthroughShareTheCheckedCompilerPipeline(string source)=>
        Assert.Empty(Compilation.Check("switch-completion.ts",source));

    [Fact]
    public void BracedSwitchArmRetainsDiscriminantForDirectEntry()
    {
        var source=EventType+
            "function f(e:E):number { let n:number=0; switch(e.kind) { "+
            "case \"ok\": { n=e.value; break; } "+
            "case \"error\": { n=0; break; } default: { break; } } return n; }";
        Assert.Empty(Compilation.Check("switch-completion.ts",source));
    }

    [Fact]
    public void FallthroughCannotPretendToBeOnlyTheNextCaseVariant()
    {
        var source=EventType+
            "function f(e:E):number { switch(e.kind) { "+
            "case \"ok\": { } case \"error\": { return e.value; } default: { return 0; } } }";
        Assert.Contains(Compilation.Check("switch-completion.ts",source),d=>d.Code=="TS2339");
    }

    [Fact]
    public void ReturningPreviousArmMustNotWidenFollowingCase()
    {
        var source=EventType+
            "function f(e:E):number { switch(e.kind) { "+
            "case \"ok\": { return e.value; } case \"error\": { return 0; } default: { return 0; } } }";
        Assert.Empty(Compilation.Check("switch-completion.ts",source));
    }

    [Theory]
    [InlineData("break;")]
    [InlineData("function f(n:number):number { break; return n; }")]
    [InlineData("function f(n:number):number { if(n>0) { break; } return n; }")]
    public void BreakOutsideItsSupportedControlFlowBoundaryIsRejected(string source)=>
        Assert.Contains(Compilation.Check("break-boundary.ts",source),d=>d.Code=="TSCS1001");

    [Fact]
    public void WrongBranchMemberAfterFallthroughBlocksJsOutput()
    {
        var source=EventType+
            "function f(e:E):number { switch(e.kind) { case \"ok\": { } "+
            "case \"error\": { return e.value; } default: { return 0; } } }";
        var analysis=Compilation.Check("no-unsafe-fallthrough.ts",source);
        Assert.Contains(analysis,d=>d.Code=="TS2339");
    }

    [Fact]
    public void SwitchWithBreakMustHaveARealFollowingFunctionReturn()
    {
        const string source="function f(n:number):number { switch(n) { case 1: { break; } default: { return 0; } } }";
        Assert.Contains(Compilation.Check("definite-return.ts",source),d=>d.Code=="TSCS1001");
    }
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

// Checked runtime conditions; not original TS7 acceptance.
public sealed class ConditionTruthinessTests
{
    [Theory]
    [InlineData("function read(box:{value:number}):number{if(box.value){return box.value;}return 0;}")] // condition-truthiness:positive-01
    [InlineData("function choose(cb:()=>number):number{if(cb()){return 21;}return 0;}")] // condition-truthiness:positive-02
    [InlineData("function choose(n:number):number{return n+1?21:0;}")] // condition-truthiness:positive-03
    [InlineData("function label(box:{name:string}):string{return box.name?\"yes\":\"no\";}")] // condition-truthiness:positive-04
    [InlineData("function loop(n:number):number{while(n){n=n-1;}return n;}")] // condition-truthiness:positive-05
    [InlineData("function loop(n:number):number{do{n=n-1;}while(n);return n;}")] // condition-truthiness:positive-06
    [InlineData("function loop(n:number):number{for(;n;){n=n-1;}return n;}")] // condition-truthiness:positive-07
    [InlineData("function choose(box:{value?:number}):number{return box.value?21:0;}")] // condition-truthiness:positive-08
    [InlineData("function choose(box:{value:number}|null):number{if(box?.value){return 21;}return 0;}")] // condition-truthiness:positive-09
    [InlineData("function choose(box:{value:number}):number{if(box.value+1){return 21;}return 0;}")] // condition-truthiness:positive-10
    public void CheckedExpressionsCanBeTestedForTruthiness(string source) =>
        Assert.Empty(Compilation.Check("condition-truthiness.ts",source));

    [Theory]
    [InlineData("function wrong():number{if(void 0){return 21;}return 0;}")] // condition-truthiness:negative-01
    [InlineData("function wrong():number{return (void 0)?21:0;}")] // condition-truthiness:negative-02
    [InlineData("function wrong(n:number):number{while(void n){n=n-1;}return n;}")] // condition-truthiness:negative-03
    [InlineData("function wrong(n:number):number{do{n=n-1;}while(void n);return n;}")] // condition-truthiness:negative-04
    [InlineData("function wrong(n:number):number{for(;void n;){n=n-1;}return n;}")] // condition-truthiness:negative-05
    public void VoidExpressionConditionsAreRejected(string source) =>
        Assert.Contains(Compilation.Check("condition-void.ts",source),d=>d.Code=="TS1345");
}

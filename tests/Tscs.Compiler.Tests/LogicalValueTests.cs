using Tscs;
using Xunit;

namespace Tscs.Tests;

// Executable compiler regression, distinct from original TS7 acceptance.
public sealed class LogicalValueTests
{
    [Theory]
    [InlineData("function choose(n:number|null):number|null{return n && n+1;}")] // logical-values:positive-01
    [InlineData("function choose(n:number|null):number{return n || 21;}")] // logical-values:positive-02
    [InlineData("function choose(s:string|null):string{return s || \"fallback\";}")] // logical-values:positive-03
    [InlineData("function choose(flag:boolean,n:number):number|false{return flag && n;}")] // logical-values:positive-04
    [InlineData("function choose(flag:boolean,n:number):number|true{return flag || n;}")] // logical-values:positive-05
    [InlineData("function read(box:{value:number}|null):number|null{return box && box.value;}")] // logical-values:positive-06
    [InlineData("function get(box:{value:number}|null):{value:number}{return box || {value:21};}")] // logical-values:positive-07
    [InlineData("function choose(n:number):number{return n && 21;}")] // logical-values:positive-08
    [InlineData("function choose(n:number):number{return n || 21;}")] // logical-values:positive-09
    [InlineData("function choose(s:string):string{return s && \"ready\";}")] // logical-values:positive-10
    public void LogicalOperatorsPreserveOperandValues(string source) =>
        Assert.Empty(Compilation.Check("logical-values.ts",source));

    [Theory]
    [InlineData("function wrong(n:number|null):number{return n && n+1;}","TS2322")] // logical-values:negative-01
    [InlineData("function wrong(n:number|null):string{return n || 21;}","TS2322")] // logical-values:negative-02
    [InlineData("function wrong(box:{value:number}|null):number{return box && box.missing;}","TS2339")] // logical-values:negative-03
    [InlineData("function wrong(v:number|string):boolean{return typeof v === \"number\" && v;}","TS2322")] // logical-values:negative-04
    public void InvalidLogicalResultAndRhsAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("logical-values-bad.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("function correct():number{return true && 1;}")]
    [InlineData("function correct():number{return (true) && 1;}")]
    [InlineData("function correct():number{return 1 || 2;}")]
    [InlineData("function correct():string{return \"ready\" || \"fallback\";}")]
    [InlineData("function correct():number{return false || 21;}")]
    public void KnownTruthyAndFalsyLiteralShortCircuitsDoNotInventBranches(string source) =>
        Assert.Empty(Compilation.Check("logical-literal.ts",source));

    [Theory]
    [InlineData("function bad(n:null,value:any):number{return n && value;}","TS2322")]
    [InlineData("function bad(box:{value:number}|null):{value:number}{return box || {value:1,typo:2};}","TS2353")]
    [InlineData("function bad(box:{value:number}|null):{value:number}{return box && {value:1,typo:2};}","TS2353")]
    public void UnreachableAnyAndFreshObjectTyposRemainRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("logical-fresh.ts",source),d=>d.Code==code);

    [Fact]
    public void VoidLogicalValuesRemainExplicitlyUnsupported()
    {
        const string source="function noop():void{return;}function wrong():void{return noop() && noop();}";
        Assert.Contains(Compilation.Check("logical-void.ts",source),d=>d.Code=="TSCS1001");
    }
}

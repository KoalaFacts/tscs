using Tscs;
using Xunit;

namespace Tscs.Tests;

// Checked finite JavaScript truthiness, separate from original upstream TS7 acceptance.
public sealed class TruthinessGuardTests
{
    [Theory]
    [InlineData("function read(v:{value:number}|null):number{if(v){return v.value;}return 0;}")] // truthy-guard:positive-01
    [InlineData("function read(v:{value:number}|null):number{if(!v){return 0;}return v.value;}")] // truthy-guard:positive-02
    [InlineData("function read(v:{value:number}|undefined|null):number{if(v){return v.value;}return 0;}")] // truthy-guard:positive-03
    [InlineData("function twice(v:number|null):number{if(v){return v*2;}return 0;}")] // truthy-guard:positive-04
    [InlineData("function label(v:string|null):string{return v?v:\"missing\";}")] // truthy-guard:positive-05
    [InlineData("type Handler=(n:number)=>number;function invoke(v:Handler|undefined):number{if(v){return v(21);}return 0;}")] // truthy-guard:positive-06
    [InlineData("type Handler=(n:number)=>number;function invoke(v:Handler|null):number{if(!v){return 0;}return v(21);}")] // truthy-guard:positive-07
    [InlineData("function choose(v:0|21):number{if(v){return v;}return 0;}")] // truthy-guard:positive-08
    [InlineData("function score(v:number|string):number{if(v){return 21;}return 0;}")] // truthy-guard:positive-09
    [InlineData("interface Box{value:number}type Maybe=Box|undefined;function read(v:Maybe):number{if(!v){return 0;}return v.value;}")] // truthy-guard:positive-10
    [InlineData("function read(v:{value:number}|null):number{return v?v.value:0;}")] // truthy-guard:positive-11
    [InlineData("function read(v:{value:number}|null):number{if(((v))){return v.value;}return 0;}")] // truthy-guard:positive-12
    public void FiniteTruthyIdentifierGuardsKeepSoundFlowFacts(string source) =>
        Assert.Empty(Compilation.Check("truthy-guard.ts",source));

    [Theory]
    [InlineData("function wrong(v:number|string):number{if(v){return v;}return 0;}", "TS2322")] // truthy-guard:negative-01
    [InlineData("function wrong(v:number|string):number{if(!v){return v;}return 0;}", "TS2322")] // truthy-guard:negative-02
    [InlineData("function wrong(v:number|string):number{return v?v:0;}", "TS2322")] // truthy-guard:negative-03
    [InlineData("function wrong(v:string|null):number{if(v){return v;}return 0;}", "TS2322")] // truthy-guard:negative-04
    [InlineData("function wrong(v:{value:number}|undefined):number{if(v){v=undefined;return v.value;}return 0;}", "TS18048")]
    [InlineData("function bad(v:{}|null):null{if(v){return null;}return v;}", "TS2322")]
    [InlineData("function bad(v:void|{value:number}):number{if(v){return v.value;}return 0;}", "TS2339")]
    public void FalsyAlternativesAndReassignmentsAreNotNarrowedUnsafely(string source,string code) =>
        Assert.Contains(Compilation.Check("truthy-guard-invalid.ts",source),d=>d.Code==code);
}

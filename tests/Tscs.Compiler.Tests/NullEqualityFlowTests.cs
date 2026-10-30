using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class NullEqualityFlowTests
{
    // These are source-backed semantic checks, not original TS7 acceptance.
    [Theory]
    [InlineData("function read(v:{value:number}|null):number{if(v===null){return 0;}return v.value;}")]
    [InlineData("function read(v:{value:number}|null):number{if(v!==null){return v.value;}return 0;}")]
    [InlineData("function read(v:{value:number}|null):number{if(null===v){return 0;}return v.value;}")]
    [InlineData("function read(v:{value:number}|null):number{if(null!==v){return v.value;}return 0;}")]
    [InlineData("function read(v:number|null|undefined):number{if(v==null){return 0;}return v*2;}")]
    [InlineData("function read(v:number|null|undefined):number{if(v!=null){return v*2;}return 0;}")]
    [InlineData("function read(v:{value:number}|null):number{return v===null?0:v.value;}")]
    [InlineData("function read(v:{value:number}|null):number{if(!(v===null)){return v.value;}return 0;}")]
    [InlineData("function read(v:{value:number}|null):number{if(v!==null && v.value>0){return v.value;}return 0;}")]
    [InlineData("function read(v:{value:number}|null):number{if(v===null || v.value>0){return 0;}return v.value;}")]
    [InlineData("type Maybe={value:number}|null;function read(v:Maybe):number{if(v===null){return 0;}return v.value;}")]
    public void NullSentinelGuardsNarrowOnlyTheirReachableBranch(string source) =>
        Assert.Empty(Compilation.Check("null-flow.ts",source));

    [Theory]
    [InlineData("function bad(v:{value:number}|null):number{if(v===null){return v.value;}return 0;}", "TS18047")]
    [InlineData("function bad(v:{value:number}|null):number{if(v!==null){return 0;}return v.value;}", "TS18047")]
    [InlineData("function bad(v:{value:number}|null):number{if(v===null){let n:number=0;}return v.value;}", "TS18047")]
    [InlineData("function bad(v:{value:number}|null|undefined):number{if(v!==null){return v.value;}return 0;}", "TS18048")]
    [InlineData("function bad(v:{value:number}|null):number{if(v===null){return 0;}v=null;return v.value;}", "TS18047")]
    [InlineData("function bad(v:{value:number}|null):number{if(v!==null){v=null;return v.value;}return 0;}", "TS18047")]
    [InlineData("function bad(v:{value:number}|null):number{if(v===null){return 0;}while(false){v=null;}return v.value;}", "TS18047")]
    public void WrongArmsUndefinedAndRebindingsRemainErrors(string source,string code) =>
        Assert.Contains(Compilation.Check("null-flow-invalid.ts",source),d=>d.Code==code);
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class UndefinedEqualityFlowTests
{
    // Source-backed, bounded semantics; not original upstream TS7 acceptance.
    [Theory]
    [InlineData("function read(v:{value:number}|undefined):number{if(v===undefined){return 0;}return v.value;}")]
    [InlineData("function read(v:{value:number}|undefined):number{if(v!==undefined){return v.value;}return 0;}")]
    [InlineData("function read(v:{value:number}|undefined):number{if(undefined===v){return 0;}return v.value;}")]
    [InlineData("function read(v:{value:number}|undefined):number{if(undefined!==v){return v.value;}return 0;}")]
    [InlineData("function read(v:{value:number}|null|undefined):number{if(v==undefined){return 0;}return v.value;}")]
    [InlineData("function read(v:{value:number}|null|undefined):number{if(v!=undefined){return v.value;}return 0;}")]
    [InlineData("function read(v:{value:number}|undefined):number{return v===undefined?0:v.value;}")]
    [InlineData("function read(v:{value:number}|undefined):number{if(!(v===undefined)){return v.value;}return 0;}")]
    [InlineData("function read(v:{value:number}|undefined):number{if(v!==void 0){return v.value;}return 0;}")]
    [InlineData("function read(v:{value:number}|undefined):number{if(void 0===v){return 0;}return v.value;}")]
    [InlineData("function read(v:{value:number}|undefined):number{if(v!==undefined && v.value>0){return v.value;}return 0;}")]
    [InlineData("function read(v:{value:number}|undefined):number{if(v===undefined || v.value>0){return 0;}return v.value;}")]
    [InlineData("type Maybe={value:number}|undefined;function read(v:Maybe):number{if(v===undefined){return 0;}return v.value;}")]
    public void IntrinsicUndefinedGuardsNarrowOnlyMatchingBranches(string source) =>
        Assert.Empty(Compilation.Check("undefined-flow.ts",source));

    [Theory]
    [InlineData("function bad(v:{value:number}|undefined):number{if(v===undefined){return v.value;}return 0;}", "TS2339")]
    [InlineData("function bad(v:{value:number}|undefined):number{if(v!==undefined){return 0;}return v.value;}", "TS2339")]
    [InlineData("function bad(v:{value:number}|null|undefined):number{if(v!==undefined){return v.value;}return 0;}", "TS2339")]
    [InlineData("function bad(v:{value:number}|null|undefined):number{if(v!=undefined){return 0;}return v.value;}", "TS2339")]
    [InlineData("function bad(v:{value:number}|undefined):number{if(v===undefined){return 0;}v=undefined;return v.value;}", "TS2339")]
    [InlineData("function bad(v:{value:number}|undefined):number{if(v===undefined){return 0;}while(false){v=undefined;}return v.value;}", "TS2339")]
    [InlineData("function bad(v:{value:number}|undefined):number{if(v===void 0){return v.value;}return 0;}", "TS2339")]
    [InlineData("function bad(v:{value:number}|undefined):number{let undefined:number=0;if(v!==undefined){return v.value;}return 0;}", "TS2339")]
    public void WrongArmsNullAndShadowedSentinelsNeverGetUnsafeReads(string source,string code) =>
        Assert.Contains(Compilation.Check("undefined-flow-invalid.ts",source),d=>d.Code==code);
}

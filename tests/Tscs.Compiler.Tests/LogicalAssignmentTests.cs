using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-level real compiler checks, separate from original TS7 acceptance.
public sealed class LogicalAssignmentTests
{
    [Theory]
    [InlineData("let n:number|null=null;n??=21;")]
    [InlineData("let n:number|null=21;n??=42;")]
    [InlineData("let s:string|undefined=undefined;s??=\"ready\";")]
    [InlineData("let n:number|null|undefined=null;n??=42;")]
    [InlineData("function choose(n:number|null):number{n??=42;return n!;}")]
    [InlineData("let flag:boolean=true;flag&&=false;")]
    [InlineData("let flag:boolean=false;flag||=true;")]
    [InlineData("let b:{value?:number}={};b.value??=42;")]
    [InlineData("let b:{value?:number}={};b[\"value\"]??=42;")]
    [InlineData("let b:{flag:boolean}={flag:true};b.flag&&=false;")]
    [InlineData("let b:{flag:boolean}={flag:false};b[\"flag\"]||=true;")]
    [InlineData("const b:{value?:number}={};b.value??=21;")]
    [InlineData("let b:{inner:{value?:number}}={inner:{}};b.inner.value??=42;")]
    [InlineData("let a:number[]=[21];a[0]??=42;")]
    [InlineData("function create():{value?:number}{return {}}create().value??=42;")]
    [InlineData("function choose(v:boolean):boolean{v&&=false;v||=true;return v;}")]
    [InlineData("function run(v:number|null):number{for(let i:number=0;i<1;v??=42){i++;}return v!;}")]
    public void CheckedIdentifierLogicalAssignmentsCompile(string source) =>
        Assert.Empty(Compilation.Check("logical-assignment.ts",source));

    [Theory]
    [InlineData("let n:number|null=null;n??=\"bad\";","TS2322")]
    [InlineData("const n:number|null=null;n??=42;","TS2588")]
    [InlineData("let flag:boolean=true;flag&&=21;","TS2322")]
    [InlineData("let flag:boolean=false;flag||=\"bad\";","TS2322")]
    [InlineData("let n:number=42;n&&=21;","TSCS1001")]
    [InlineData("let n:number;n??=42;","TSCS1001")]
    [InlineData("let b:{value?:number}={};b[\"value\"]||=42;","TSCS1001")]
    [InlineData("let b:{value?:number}={};b.value??=\"bad\";","TS2322")]
    [InlineData("let b:{readonly value?:number}={};b.value??=42;","TS2540")]
    [InlineData("let b:{flag:boolean}={flag:true};b.flag&&=42;","TS2322")]
    [InlineData("let b:{flag:boolean}={flag:false};b[\"flag\"]||=\"bad\";","TS2322")]
    [InlineData("let b:{value?:number}={};b?.value??=42;","TSCS1001")]
    public void InvalidLogicalAssignmentsHaveDiagnostics(string source,string code) =>
        Assert.Contains(Compilation.Check("logical-assignment-invalid.ts",source),d=>d.Code==code);
}

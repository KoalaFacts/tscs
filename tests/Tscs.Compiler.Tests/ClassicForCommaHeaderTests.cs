using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded native ES2015 classic for comma headers, not original TS7 acceptance.
public sealed class ClassicForCommaHeaderTests
{
    [Theory]
    [InlineData("""function f(n:number):number { let i:number=0;let j:number=0;for(i=0,j=0;i<n;i++,j+=2){}return j; }""")]
    [InlineData("""function f(n:number):number { let i:number=0;let j:number=0;for(;i<n;i++,j++){if(i===1){continue;}}return j; }""")]
    [InlineData("""function f(n:number):number { let i:number=0;let j:number=0;for(;i<n;j+=2,i++){}return j; }""")]
    [InlineData("""function f(n:number):number { let i:number=0;let j:number=0;for(i=0,j=0;i<n;i=i+1,j=j+2){}return j; }""")]
    [InlineData("""function f(n:number):number { let i:number=0;let j:number=0;for(;i<n;i++,j++){for(let k=0;k<2;k++){j+=1;}}return j; }""")]
    [InlineData("""function tick():number{return 1;}function f(n:number):number { let i:number=0;for(let j=0;tick(),j<n;j++,i++){}return i; }""")]
    [InlineData("""function tick():number{return 1;}function f(n:number):number { let i:number=0;for(;tick(),i<n;i++){}return i; }""")]
    public void FiniteCommasKeepCheckedMutationAndConditionTypes(string source)=>
        Assert.Empty(Compilation.Check("classic-for-comma.ts",source));

    [Theory]
    [InlineData("""function f(n:number):number { let i:number=0;let j:number=0;for(i=0,j="bad";i<n;i++,j++){}return j; }""","TS2322")]
    [InlineData("""function f(n:number):number { let i:number=0;let j:number=0;for(;i<n;i++,j="bad"){}return j; }""","TS2322")]
    [InlineData("""function f(n:number):number { let i:number=0;for(;i<n;i++,missing++){}return i; }""","TS2304")]
    [InlineData("""function f(n:number):number { let i:number=0;for(;21,i<n;i++){}return i; }""","TS2695")]
    [InlineData("""function bad(x:number):number{return x;}function f(n:number):number { let i:number=0;for(;bad("wrong"),i<n;i++){}return i; }""","TS2345")]
    [InlineData("""function f(n:number):number { let i:number=0;for(;i<n;i++,i="wrong"){}return i; }""","TS2322")]
    public void EachHeaderPartIsTypeChecked(string source,string code)=>
        Assert.Contains(Compilation.Check("classic-for-comma-invalid.ts",source),d=>d.Code==code);

    // This exact case was a rejection before multi-declaration support; it
    // must now type-check instead of disappearing from regression coverage.
    [Fact]
    public void MultiDeclaratorCaseFormerlyUnsupportedNowCompiles()
    {
        const string source="""function f():number {for(let i=0,j=0;i<2;i++){}return 1;}""";
        Assert.Empty(Compilation.Check("classic-for-multidecl-promoted.ts",source));
    }

    [Theory]
    [InlineData("""function f():number {let i=0;for(;i<2;i++,){}return i;}""")]
    [InlineData("""function f():number {let i=0;for(;i<2;21,i++){}return i;}""")]
    public void OutsideTheFiniteHeaderContractStillRejects(string source)=>
        Assert.NotEmpty(Compilation.Check("classic-for-comma-unsupported.ts",source));

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void AllMutationsAndConditionEffectsExecuteAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-classic-for-comma-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const state:{left:number,right:number,check:number}={left:0,right:0,check:0};""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {state} from "./model.js";""" +
                """function tick():number {state.check=state.check+1;return state.check;}""" +
                """export function run(n:number):number {""" +
                """let i:number=0;let j:number=0;""" +
                """for(i=0,j=0;tick(),i<n;i++,j+=2){""" +
                """state.left=state.left+j;if(i===1){continue;}state.right=state.right+1;""" +
                """}return j;}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {state} from "./model.js";export {run} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {state,run} from "./bridge.js";""" +
                """export const result:number=run(3);""" +
                """export const checks:number=state.check;""" +
                """export const left:number=state.left;""" +
                """export const right:number=state.right;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,item=>item.Text.Contains("i++, j += 2",StringComparison.Ordinal));
            foreach(var file in emitted)
                File.WriteAllText(Path.ChangeExtension(file.SourceFile,".js"),file.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node") {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add(
                """import {result,checks,left,right} from './entry.js';console.log(JSON.stringify([result,checks,left,right]));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Classic for comma runtime exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("[6,4,6,2]",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidLaterHeaderMutationPreventsCrossModuleOutput()
    {
        const string root="/virtual/classic-for-comma-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]="""export const item:{count:number}={count:0};""";
        host.Files[root+"/entry.ts"]=
            """import {item} from "./model.js";""" +
            """export function bad(n:number):number {let i:number=0;let j:number=0;""" +
            """for(i=0,j=0;i<n;i++,j="bad"){item.count=j;}return j;}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

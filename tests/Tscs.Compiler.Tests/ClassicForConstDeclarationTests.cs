using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Finite checked const declarations in classic for headers; not original TS7 acceptance.
public sealed class ClassicForConstDeclarationTests
{
    [Theory]
    [InlineData("""function f():number {let total=0;for(const box:{n:number}={n:0};box.n<3;box.n++){total+=box.n;}return total;}""")]
    [InlineData("""function f():number {let total=0;for(const box:{n:number}={n:0},step:number=2;box.n<5;box.n+=step){total+=box.n;}return total;}""")]
    [InlineData("""function f():number {for(const left=1,right=left+2;false;){let n:number=right;}return 1;}""")]
    [InlineData("""function f():number {for(const x:number=1;false;){let n:number=x;}return 1;}""")]
    [InlineData("""function f():number {let result=0;for(const box={n:0},step=1;box.n<3;box.n+=step){if(box.n===1){continue;}result+=box.n;}return result;}""")]
    [InlineData("""function f():number {for(const get:()=>number=():number=>next,next:number=2;false;){let result:number=get();}return 1;}""")]
    [InlineData("""function f():number {let result=0;for(const box={n:0},step=2;box.n<5;box.n+=step){for(const nested={m:0};nested.m<1;nested.m++){result+=box.n;}}return result;}""")]
    public void CheckedConstBindingsKeepScopeAndMutableReceivers(string source)=>
        Assert.Empty(Compilation.Check("classic-for-const.ts",source));

    [Theory]
    [InlineData("""function f():number {for(const i=0;i<2;i++){}return 1;}""","TS2588")]
    [InlineData("""function f():number {for(const i=0;i<2;i+=1){}return 1;}""","TS2588")]
    [InlineData("""function f():number {for(const i=0;i<2;i=1){}return 1;}""","TS2588")]
    [InlineData("""function f():number {for(const box={n:0};box.n<2;box={n:2}){}return 1;}""","TS2588")]
    [InlineData("""function f():number {for(const box={n:0};box.n<2;box.n++){box={n:2};}return 1;}""","TS2588")]
    [InlineData("""function f():number {for(const i:number;false;){}return 1;}""","TSCS1001")]
    [InlineData("""function f():number {for(const i=0,j:number;false;){}return 1;}""","TSCS1001")]
    [InlineData("""function f():number {for(const i=j,j=1;false;){}return 1;}""","TSCS1001")]
    [InlineData("""function f():number {for(const i=0,i=1;false;){}return 1;}""","TSCS1001")]
    [InlineData("""function f():number {for(const i:number="bad";false;){}return 1;}""","TS2322")]
    [InlineData("""function f():number {for(const i=0;false;){}return i;}""","TS2304")]
    public void RebindingInvalidDeclarationsAndEscapingScopesAreRejected(string source,string code)=>
        Assert.Contains(Compilation.Check("classic-for-const-invalid.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void ConstHeaderObjectMutationAndContinueExecuteAcrossModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-classic-for-const-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const state:{sum:number,checks:number}={sum:0,checks:0};""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {state} from "./model.js";""" +
                """export function run(max:number):number {let last:number=0;""" +
                """for(const box:{n:number}={n:0},step:number=2;box.n<max;box.n+=step){""" +
                """state.sum+=box.n;last=box.n;if(box.n===2){continue;}state.checks+=1;}""" +
                """return last;}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {state} from "./model.js";export {run} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {state,run} from "./bridge.js";""" +
                """export const result:number=run(6);""" +
                """export const sum:number=state.sum;export const checks:number=state.checks;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,item=>item.Text.Contains(
                "const box = { n: 0 }, step = 2",StringComparison.Ordinal));
            foreach(var file in emitted)
                File.WriteAllText(Path.ChangeExtension(file.SourceFile,".js"),file.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node"){
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add(
                """import {result,sum,checks} from './entry.js';console.log(JSON.stringify([result,sum,checks]));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Const classic-for runtime exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("[4,6,2]",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedConstHeaderRebindingForbidsJavaScriptOutput()
    {
        const string root="/virtual/classic-for-const-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]="""export const state:{n:number}={n:0};""";
        host.Files[root+"/entry.ts"]=
            """import {state} from "./model.js";""" +
            """export function bad():number {for(const box:{n:number}={n:0};box.n<2;box={n:2}){state.n=box.n;}return state.n;}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2588");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

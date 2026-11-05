using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded checked classic-for calls, not original TS7 acceptance.
public sealed class ClassicForCallHeaderTests
{
    [Theory]
    [InlineData("""function tick(value:number):number{return value;}function run(limit:number):number{let i=0;for(tick(1);i<limit;tick(i),i++){}return i;}""")]
    [InlineData("""function run(record:(value:number)=>number,limit:number):number{let i=0;for(record(0);i<limit;record(i),i++){if(i===1){continue;}}return i;}""")]
    [InlineData("""function tick(value:number):void{}function run(limit:number):number{let i=0;for(;i<limit;tick(i),i++){}return i;}""")]
    [InlineData("""function tick(value:number):number{return value;}function run(limit:number):number{let i=0;for(tick(3),i=0;i<limit;tick(4),i++){}return i;}""")]
    public void CheckedCallsCanRunInInitializerAndUpdate(string source)=>
        Assert.Empty(Compilation.Check("classic-for-call-header.ts",source));

    [Theory]
    [InlineData("""function tick(value:number):number{return value;}function run(limit:number):number{let i=0;for(tick("bad");i<limit;i++){}return i;}""","TS2345")]
    [InlineData("""function tick(value:number):number{return value;}function run(limit:number):number{let i=0;for(;i<limit;i++,tick("bad")){}return i;}""","TS2345")]
    [InlineData("""function run(limit:number):number{let i=0;for(;i<limit;missing(i),i++){}return i;}""","TS2304")]
    public void CallArgumentsAndCalleesRemainTypeChecked(string source,string code)=>
        Assert.Contains(Compilation.Check("classic-for-call-header-invalid.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("""function run(limit:number):number{let i=0;for(;i<limit;21,i++){}return i;}""")]
    [InlineData("""function run(limit:number):number{let i=0;for(;i<limit;i++,){}return i;}""")]
    public void NonCallPureHeaderExpressionsAndTrailingCommasStillReject(string source)=>
        Assert.NotEmpty(Compilation.Check("classic-for-call-header-unsupported.ts",source));

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void CheckedCallsExecuteInNativeOrderAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-classic-for-calls-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const state:{history:number}={history:0};""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {state} from "./model.js";""" +
                """function mark(value:number):number{state.history=state.history*10+value;return value;}""" +
                """export function run(limit:number):number{let index:number=0;let sum:number=0;""" +
                """for(mark(1);index<limit;mark(2),index++){""" +
                """if(index===1){continue;}sum+=index;}return sum;}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {state} from "./model.js";export {run} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {state,run} from "./bridge.js";""" +
                """export const total:number=run(3);export const history:number=state.history;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,item=>item.Text.Contains("for (mark(1);",StringComparison.Ordinal));
            Assert.Contains(emitted,item=>item.Text.Contains("mark(2), index++",StringComparison.Ordinal));
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
                """import {total,history} from './entry.js';console.log(JSON.stringify([total,history]));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Classic-for call header runtime exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("[2,1222]",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedCallArgumentPreventsAnyJavaScriptOutput()
    {
        const string root="/virtual/classic-for-call-header-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]="""export function mark(value:number):number{return value;}""";
        host.Files[root+"/entry.ts"]=
            """import {mark} from "./model.js";""" +
            """export function run(limit:number):number{let i=0;""" +
            """for(mark("bad");i<limit;i++){ }return i;}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2345");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

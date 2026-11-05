using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Real source-level product checks; not full original TS7 acceptance.
public sealed class ClassicForEffectHeaderTests
{
    [Theory]
    [InlineData("""function tick(n:number):number{return n;}function run(end:number):number{let i=0;for((tick(1));i<end;(tick(i)),i++){}return i;}""")]
    [InlineData("""type Callback=(n:number)=>number;function run(f:Callback|undefined,end:number):number{let i=0;for(f?.(1);i<end;f?.(i),i++){}return i;}""")]
    [InlineData("""interface Box{tick?(n:number):number;}function run(b:Box,end:number):number{let i=0;for(b.tick?.(1);i<end;b.tick?.(i),i++){}return i;}""")]
    [InlineData("""function tick(n:number):number{return n;}function run(flag:boolean,end:number):number{let i=0;for(flag ? tick(1) : tick(2);i<end;flag ? tick(3) : tick(4),i++){}return i;}""")]
    [InlineData("""function tick(n:number):number{return n;}function run(flag:boolean,end:number):number{let i=0;for(flag && tick(1);i<end;flag || tick(i),i++){}return i;}""")]
    [InlineData("""function tick(n:number):number{return n;}function run(flag:number|undefined,end:number):number{let i=0;for(flag ?? tick(1);i<end;flag ?? tick(i),i++){}return i;}""")]
    [InlineData("""function tick(n:number):number{return n;}function run(end:number):number{let i=0;for((tick(1),tick(2));i<end;(tick(i),tick(3)),i++){}return i;}""")]
    [InlineData("""function run(b:{value?:number},end:number):number{let i=0;for(delete b.value;i<end;delete b.value,i++){}return i;}""")]
    public void CheckedSideEffectsRunInClassicForHeaders(string source) =>
        Assert.Empty(Compilation.Check("classic-for-effect.ts",source));

    [Theory]
    [InlineData("""type F=(n:number)=>number;function run(f:F|undefined):number{let i=0;for(;i<2;f?.("bad"),i++){}return i;}""","TS2345")]
    [InlineData("""function tick(n:number):number{return n;}function run(flag:boolean):number{let i=0;for(;i<2;flag ? tick("bad") : tick(1),i++){}return i;}""","TS2345")]
    [InlineData("""function tick(n:number):number{return n;}function run(flag:boolean):number{let i=0;for(;i<2;flag && missing(i),i++){}return i;}""","TS2304")]
    [InlineData("""function run(b:{required:number}):number{let i=0;for(;i<2;delete b.required,i++){}return i;}""","TS2790")]
    public void EffectfulBranchesStillCheckTypes(string source,string code) =>
        Assert.Contains(Compilation.Check("classic-for-effect-invalid.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("""function run(flag:boolean):number{let i=0;for(;i<2;flag ? 1 : 2,i++){}return i;}""")]
    [InlineData("""function tick(n:number):number{return n;}function run(flag:boolean):number{let i=0;for(;i<2;flag ? tick(i) : 2,i++){}return i;}""")]
    [InlineData("""function run(flag:boolean):number{let i=0;for(;i<2;flag && 21,i++){}return i;}""")]
    [InlineData("""function run():number{let i=0;for(;i<2;(21),i++){}return i;}""")]
    [InlineData("""function run():number{let i=0;for(;i<2;i++,){}return i;}""")]
    public void UnprovenEffectsAndMalformedListsRemainUnsupported(string source) =>
        Assert.NotEmpty(Compilation.Check("classic-for-effect-unsupported.ts",source));

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void OptionalCallsConditionalEffectsAndContinueExecuteAcrossModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-for-effects-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const state:{history:number}={history:0};""" +
                """export function mark(n:number):number{state.history=state.history*10+n;return n;}""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {mark} from "./model.js";""" +
                """type Callback=(n:number)=>number;""" +
                """export function run(flag:boolean,maybe:Callback|undefined):number{let i:number=0;""" +
                """for((maybe?.(mark(9))),mark(1);i<3;flag ? mark(2) : mark(3),maybe?.(mark(8)),i++){""" +
                """if(i===1){continue;}}return i;}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {state} from "./model.js";export {run} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {state,run} from "./bridge.js";""" +
                """export const total:number=run(true,undefined);export const history:number=state.history;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,item=>item.Text.Contains("for (",StringComparison.Ordinal));
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
                throw new TimeoutException("Classic-for effect runtime exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("[3,1222]",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedConditionalCallPreventsAllJavaScriptOutput()
    {
        const string root="/virtual/classic-for-effects-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]="""export function mark(n:number):number{return n;}""";
        host.Files[root+"/entry.ts"]=
            """import {mark} from "./model.js";""" +
            """export function run(flag:boolean):number{let i=0;""" +
            """for(;i<2;flag ? mark("bad") : mark(1),i++){}return i;}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2345");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

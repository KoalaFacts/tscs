using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Checked finite classic-for declaration lists; not original TS7 acceptance.
public sealed class ClassicForMultiDeclarationTests
{
    [Theory]
    [InlineData("""function f(n:number):number {let sum=0;for(let i=0,j=1;i<n;i++,j+=2){sum+=j;}return sum;}""")]
    [InlineData("""function f(n:number):number {let sum=0;for(let i:number=0,j:number=i+2;i<n;i++,j++){sum+=j;}return sum;}""")]
    [InlineData("""function f(n:number):number {let sum=0;for(let i=0,j=2,k=j+1;i<n;i++,j++,k++){sum+=i+j+k;}return sum;}""")]
    [InlineData("""function f(n:number):number {let sum=0;for(let i=0,j:number=1;i<n;i++,j++){if(i===1){continue;}sum+=j;}return sum;}""")]
    [InlineData("""function f(n:number):number {let i=10;for(let i=0,j=i+1;i<n;i++,j++){i++;}return i;}""")]
    [InlineData("""function f(n:number):number {let sum=0;for(let i=0,j=1;i<n;i++){for(let j=2,k=j+1;k<5;j++,k++){sum+=j;}sum+=j;}return sum;}""")]
    [InlineData("""function f(n:number):number {let sum=0;for(let i=0,j:number;i<n;i++,j=2){sum+=i;}return sum;}""")]
    public void MultipleHeaderBindingsAreChecked(string source)=>
        Assert.Empty(Compilation.Check("classic-for-multibind.ts",source));

    [Theory]
    [InlineData("""function f(n:number):number {for(let i=0,i=1;i<n;i++){}return n;}""","TSCS1001")]
    [InlineData("""function f(n:number):number {for(let i=0,j:number="bad";i<n;i++,j++){}return n;}""","TS2322")]
    [InlineData("""function f(n:number):number {for(let i=0,j=i+1;i<n;i++,j="bad"){}return n;}""","TS2322")]
    [InlineData("""function f(n:number):number {for(let i=j,j=1;i<n;i++){}return n;}""","TSCS1001")]
    [InlineData("""function f(n:number):number {for(let i=0,j:number=i;i<n;i++){}return j;}""","TS2304")]
    [InlineData("""function f(n:number):number {for(let i=0,j=1;i<n;i++,j++){}return j;}""","TS2304")]
    [InlineData("""function f(n:number):number {for(let i=0,j=1;i<n;i++,missing++){}return n;}""","TS2304")]
    public void InvalidBindingsAndWritesAreRejected(string source,string code)=>
        Assert.Contains(Compilation.Check("classic-for-multibind-invalid.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("""function f():number {for(let i=0,;i<2;i++){}return 1;}""")]
    [InlineData("""function f():number {for(let i=0,j;i<2;i++){}return 1;}""")]
    [InlineData("""function f():number {for(const i=0,j=1;i<2;i++){}return 1;}""")]
    public void UnsupportedOrMalformedHeadersFailClosed(string source)=>
        Assert.NotEmpty(Compilation.Check("classic-for-multibind-unsupported.ts",source));

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void OrderedInitializersAndLoopUpdatesExecuteAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-classic-for-multibind-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const state:{sum:number,checks:number,seen:number}={sum:0,checks:0,seen:0};""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {state} from "./model.js";""" +
                """function record(n:number):number {state.checks=state.checks+1;return n;}""" +
                """export function run(limit:number):number {let last:number=0;""" +
                """for(let i=0,j=record(2),k:number=j+1;i<limit;i++,j+=2,k++){""" +
                """state.sum=state.sum+j+k;last=j+k;""" +
                """if(i===1){continue;}state.seen=state.seen+1;""" +
                """}return last;}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {state} from "./model.js";export {run} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {state,run} from "./bridge.js";""" +
                """export const result:number=run(3);""" +
                """export const checks:number=state.checks;""" +
                """export const sum:number=state.sum;""" +
                """export const seen:number=state.seen;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,item=>item.Text.Contains("let i = 0, j = record(2), k = j + 1",StringComparison.Ordinal));
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
                """import {result,checks,sum,seen} from './entry.js';console.log(JSON.stringify([result,checks,sum,seen]));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Classic for declaration runtime exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("[11,1,24,2]",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedHeaderBindingPreventsJavaScriptOutput()
    {
        const string root="/virtual/classic-for-multibind-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]="""export const item:{count:number}={count:0};""";
        host.Files[root+"/entry.ts"]=
            """import {item} from "./model.js";""" +
            """export function bad(n:number):number {""" +
            """for(let i=0,j:number="bad";i<n;i++,j++){item.count=i;}return n;}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

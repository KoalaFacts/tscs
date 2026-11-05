using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded do/while definite-assignment product acceptance, not an original
// pinned TypeScript 7 compatibility PASS.
public sealed class DoWhileDefiniteAssignmentTests
{
    [Theory]
    [InlineData("""function run():number{let result:number;do{result=3;}while(result<3);return result;}""")]
    [InlineData("""function run():number{let a:number;let b:number;do{a=2;b=a+4;}while(b<6);return b;}""")]
    [InlineData("""function run():number{let count:number;let previous:number=0;do{count=previous+1;previous=count;}while(count<3);return count;}""")]
    [InlineData("""function run():number{let count:number;do count=2;while(count<2);return count;}""")]
    public void StraightLineAssignmentsInitializeBeforeTheFirstConditionAndAfterLoop(string source) =>
        Assert.Empty(Compilation.Check("do-while-assign.ts",source));

    [Theory]
    [InlineData("""function run():number{let value:number;do{if(true){continue;}value=1;}while(value<2);return value;}""","TSCS1001")]
    [InlineData("""function run(flag:boolean):number{let value:number;do{if(flag){break;}value=1;}while(value<2);return value;}""","TSCS1001")]
    [InlineData("""function run():number{let a:number;let b:number;do{b=a;a=1;}while(a<2);return b;}""","TSCS1001")]
    [InlineData("""function run():number{let value:number;let cb:()=>void;do{cb=():void=>{value=1;};}while(value<1);return 0;}""","TSCS1001")]
    [InlineData("""function run():number{let value:number;do{value="bad";}while(value<2);return value;}""","TS2322")]
    public void BypassedOutOfOrderOrWronglyTypedWritesCannotProveInitialization(string source,string code) =>
        Assert.Contains(Compilation.Check("do-while-assign-invalid.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void DefiniteDoWhileAssignmentsExecuteThroughModuleGraph()
    {
        var root=Directory.CreateTempSubdirectory("tscs-do-while-init-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export function next(n:number):number{return n+1;}""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {next} from "./model.js";""" +
                """export function run():number{let value:number;let previous:number=0;""" +
                """do{value=next(previous);previous=value;}while(value<3);return value;}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {run} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {run} from "./bridge.js";export const result:number=run();""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,file=>file.Text.Contains("do {",StringComparison.Ordinal));
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
                """import {result} from './entry.js';console.log(JSON.stringify(result));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Do/while runtime exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("3",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedValuePreventsAllJavaScriptOutput()
    {
        const string root="/virtual/do-while-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]="""export function next(n:number):number{return n+1;}""";
        host.Files[root+"/entry.ts"]=
            """import {next} from "./model.js";""" +
            """export function run():number{let count:number;do{count=next("bad");}""" +
            """while(count<2);return count;}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2345");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

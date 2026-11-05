using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded product behavior; not an original pinned TS7 compatibility PASS.
public sealed class SwitchOptionalCaseCallTests
{
    [Theory]
    [InlineData("""type F=(n:number)=>number;function choose(f:F|undefined,n:number):number{switch(n){case f?.(1):{return 1;}default:{return 0;}}}""")]
    [InlineData("""type F=(n:number)=>number;function choose(f:F|null,n:number):number{switch(n){case (f)?.(1):{return 1;}default:{return 0;}}}""")]
    [InlineData("""function inc(n:number):number{return n+1;}switch(2){case inc?.(1):{break;}default:{break;}}""")]
    [InlineData("""interface Box{run?(n:number):number;}function choose(box:Box,n:number):number{switch(n){case box.run?.(1):{return 1;}default:{return 0;}}}""")]
    [InlineData("""interface Box{run?(n:number):number;}function choose(box:Box,n:number):number{switch(n){case box["run"]?.(1):{return 1;}default:{return 0;}}}""")]
    [InlineData("""interface Box{run?(n:number):number;}function choose(box:Box,n:number):number{switch(n){case (box.run)?.(1):{return 1;}default:{return 0;}}}""")]
    public void CheckedOptionalCallsAreValidRuntimeCaseLabels(string source) =>
        Assert.Empty(Compilation.Check("switch-optional-call.ts",source));

    [Theory]
    [InlineData("""type F=(n:number)=>number;function bad(f:F|undefined):number{switch(1){case f?.("wrong"):{return 1;}default:{return 0;}}}""","TS2345")]
    [InlineData("""type F=(n:number)=>number;function bad(f:F|undefined):number{switch(1){case f?.():{return 1;}default:{return 0;}}}""","TS2554")]
    [InlineData("""type F=(n:number)=>number;function bad(f:F|undefined):number{switch("wrong"){case f?.(1):{return 1;}default:{return 0;}}}""","TS2678")]
    [InlineData("""function bad(f:any):number{switch(1){case f?.(1):{return 1;}default:{return 0;}}}""","TSCS1001")]
    [InlineData("""type F=(n:number)=>number;function bad(f:F|undefined):number{let n:number=1;switch(1){case f?.(n++):{return 1;}default:{return 0;}}}""","TSCS1001")]
    [InlineData("""type F=(n:number)=>number;function extra():number{return 1;}function bad(f:F|undefined):number{switch(1){case f?.(extra()):{return 1;}default:{return 0;}}}""","TSCS1001")]
    [InlineData("""type F=(n:number)=>{value:number};function bad(f:F|undefined):number{switch(1){case f?.(1):{return 1;}default:{return 0;}}}""","TSCS1001")]
    [InlineData("""interface Box{run?(n:number):number;}function bad(box:Box,n:number):number{switch(n){case box.run?.("bad"):{return 1;}default:{return 0;}}}""","TS2345")]
    [InlineData("""interface Box{run?(n:number):number;}function bad(box:Box,n:number):number{let key:string="run";switch(n){case box[key]?.(1):{return 1;}default:{return 0;}}}""","TSCS1001")]
    public void OptionalCaseLabelsStillRejectUnsafeInputs(string source,string expectedCode) =>
        Assert.Contains(Compilation.Check("switch-optional-invalid.ts",source),
            diagnostic=>diagnostic.Code==expectedCode);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void OptionalDirectAndMemberCallsExecuteAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-switch-optional-calls-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export let hits:number=0;export function next(n:number):number{hits++;return n;}export function count():number{return hits;}""" +
                """export const handler:{run?:(n:number)=>number}={run(n:number):number{hits+=10;return n;}};""" +
                """export const empty:{run?:(n:number)=>number}={};""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {next,count,handler,empty} from "./model.js";type F=(n:number)=>number;""" +
                """export function direct(n:number,enabled:boolean):number{let fn:F|undefined=enabled?next:undefined;""" +
                """switch(n){case fn?.(1):{return count()*10+1;}case undefined:{return count()*10+2;}default:{return count()*10+9;}}}""" +
                """export function member(n:number):number{switch(n){case handler.run?.(2):{return count()*10+3;}""" +
                """case empty["run"]?.(4):{return count()*10+4;}default:{return count()*10+9;}}}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {direct,member} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {direct,member} from "./bridge.js";""" +
                """export const answer:number=direct(1,true)*1000000+direct(2,false)*10000+member(2)*100+member(4);""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,file=>file.Text.Contains("case ",StringComparison.Ordinal));
            foreach(var file in emitted)
                File.WriteAllText(Path.ChangeExtension(file.SourceFile,".js"),file.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node")
            {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add("""import {answer} from './entry.js';console.log(JSON.stringify(answer));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Switch optional calls exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("11201519",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedOptionalCallBlocksAllEmission()
    {
        const string root="/virtual/tscs-switch-optional-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export function next(n:number):number{return n;}""";
        host.Files[root+"/entry.ts"]=
            """import {next} from "./model.js";let fn:((n:number)=>number)|undefined=next;""" +
            """switch(1){case fn?.("wrong"):{break;}default:{break;}}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2345");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

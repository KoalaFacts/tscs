using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded product regressions, not original frozen TS7 acceptance cases.
public sealed class SwitchPureShortCircuitCaseTests
{
    [Theory]
    [InlineData("""let ready:boolean=true;let base:number=2;switch(3){case (ready ? base+1 : 5):{break;}}""")]
    [InlineData("""let candidate:number|undefined=undefined;switch(4){case (candidate ?? 4):{break;}}""")]
    [InlineData("""let active:boolean=true;switch(true){case (active && true):{break;}}""")]
    [InlineData("""let active:boolean=false;switch(true){case (active || true):{break;}}""")]
    [InlineData("""let active:boolean=true;switch(false){case !active:{break;}}""")]
    [InlineData("""let n:number=2;switch(true){case (n===2):{break;}}""")]
    [InlineData("""let n:number=2;switch(3){case (n>1 ? 3 : 4):{break;}}""")]
    [InlineData("""let ready:boolean=true;let n:number|undefined=undefined;switch(3){case (ready && !false ? (n ?? 3) : 1):{break;}}""")]
    public void PureConditionalAndShortCircuitCaseLabelsUseOrdinaryChecking(string source) =>
        Assert.Empty(Compilation.Check("pure-switch-case.ts",source));

    [Theory]
    [InlineData("""let ready:boolean=true;function effect():number{return 1;}switch(1){case (ready ? effect() : 1):{break;}}""","TSCS1001")]
    [InlineData("""let ready:boolean=true;let obj={value:1};switch(1){case (ready ? 1 : obj):{break;}}""","TSCS1001")]
    [InlineData("""let count:number=1;switch(1){case (true && count++):{break;}}""","TSCS1001")]
    [InlineData("""let value:any=1;switch(1){case (value ?? 1):{break;}}""","TSCS1001")]
    [InlineData("""let ready:boolean=true;switch(1){case (ready ? "bad" : "worse"):{break;}}""","TS2678")]
    [InlineData("""let ready:boolean=true;switch(3){case (ready ? missing : 3):{break;}}""","TS2304")]
    public void InvalidPureCaseExpressionsRemainErrors(string source,string code) =>
        Assert.Contains(Compilation.Check("pure-switch-invalid.ts",source),d=>d.Code==code);

    [Fact]
    public void IgnoredMissingNameCannotConcealEffectfulLazyBranch()
    {
        const string source="""
            let ready:boolean=true;
            function effect():number{return 1;}
            switch(3){
              // @ts-ignore
              case (ready ? missing : effect()):{break;}
            }
            """;
        Assert.Contains(Compilation.Check("pure-switch-ignored.ts",source),
            d=>d.Code=="TSCS1001");
    }

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void FourLinkedModulesExecuteLazyPrimitiveCaseLabels()
    {
        var root=Directory.CreateTempSubdirectory("tscs-pure-switch-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const ready:boolean=true;export const skip:boolean=false;export let candidate:number|undefined=undefined;export const base:number=4;""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {ready,skip,candidate,base} from "./model.js";""" +
                """export function choose(n:number):number{switch(n){case (ready ? base+1 : 2):{return 10;}case (candidate ?? 6):{return 20;}case ((ready && true) ? 7 : 9):{return 30;}default:{return -1;}}}""" +
                """export function flags(n:boolean):number{switch(n){case (ready && true):{return 3;}case (skip || false):{return 4;}default:{return -2;}}}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {choose,flags} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {choose,flags} from "./bridge.js";export const answer:number=choose(5)*10000+choose(6)*1000+choose(7)*100+flags(true)*10+flags(false);""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,file=>file.Text.Contains("case ",StringComparison.Ordinal) &&
                file.Text.Contains("?",StringComparison.Ordinal));
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
                throw new TimeoutException("Pure switch case module run exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("123034",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedObjectInLazyBranchBlocksAllJavaScriptEmission()
    {
        const string root="/virtual/pure-switch-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const ready:boolean=true;export const bad:{value:number}={value:1};""";
        host.Files[root+"/logic.ts"]=
            """import {ready,bad} from "./model.js";export function choose(n:number):number{switch(n){case (ready ? bad : 1):{return 1;}default:{return 0;}}}""";
        host.Files[root+"/bridge.ts"]=
            """export {choose} from "./logic.js";""";
        host.Files[root+"/entry.ts"]=
            """import {choose} from "./bridge.js";export const answer:number=choose(1);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TSCS1001");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

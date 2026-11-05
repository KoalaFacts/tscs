using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed bounded compiler behavior, not an original TS7 suite PASS.
public sealed class SwitchConstantCaseTests
{
    [Theory]
    [InlineData("""const READY="ready";function choose():string{return "ready";}let value:number=0;switch(choose()){case READY:{value=21;break;}default:{value=0;}}""")]
    [InlineData("""const ZERO:0=0;let value:number=0;function choose():number{return 0;}switch(choose()){case (ZERO):{value=21;break;}default:{value=0;}}""")]
    [InlineData("""const YES=true;function choose():boolean{return true;}let value:number=0;switch(choose()){case YES:{value=21;break;}case false:{value=0;break;}}""")]
    [InlineData("""type Item={kind:"ready";value:number}|{kind:"pending";message:string};function read(item:Item):number{const READY="ready";switch(item.kind){case READY:{return item.value;}case "pending":{return 0;}default:{return 0;}}}""")]
    public void ExactConstLabelsUseCheckedTypesAndDiscriminantFlow(string source) =>
        Assert.Empty(Compilation.Check("switch-const-positive.ts",source));

    [Theory]
    [InlineData("""const READY="ready";switch("ready"){case READY:{break;}case "ready":{break;}}""","TSCS1001")]
    [InlineData("""const CODE=42;switch("ready"){case CODE:{break;}}""","TS2678")]
    [InlineData("""switch("ready"){case READY:{break;}}const READY="ready";""","TSCS1001")]
    [InlineData("""switch("ready"){case UNKNOWN:{break;}}""","TS2304")]
    public void MutableBroadDuplicateAndUnreadyLabelsFailClosed(string source,string expectedCode) =>
        Assert.Contains(Compilation.Check("switch-const-negative.ts",source),
            diagnostic=>diagnostic.Code==expectedCode);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void ImportedConstLabelsEmitAndExecuteAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-switch-const-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"labels.ts")]=
                """export const READY="ready";export const PENDING="pending";""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {READY,PENDING} from "./labels.js";""" +
                """export function value(flag:boolean):number{""" +
                """let answer:number=0;const state:string=flag?READY:PENDING;""" +
                """switch(state){case READY:{answer=41;break;}case PENDING:{answer=2;break;}default:{answer=-1;}}return answer;}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {value} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {value} from "./bridge.js";export const result:number=value(true);""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,file=>file.Text.Contains("case READY:",StringComparison.Ordinal));
            foreach(var file in emitted)
                File.WriteAllText(Path.ChangeExtension(file.SourceFile,".js"),file.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node"){
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add("""import {result} from './entry.js';console.log(JSON.stringify(result));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Switch const module execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("41",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void ImportedWidenedConstLabelPreventsAllEmission()
    {
        const string root="/virtual/switch-const-invalid";
        var host=new Host();
        host.Files[root+"/labels.ts"]="""export const READY:number=21;""";
        host.Files[root+"/entry.ts"]=
            """import {READY} from "./labels.js";switch("ready"){case READY:{break;}}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2678");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

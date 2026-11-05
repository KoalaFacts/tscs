using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded source-backed behavior; not an original TS7 compatibility PASS.
public sealed class SwitchNullishAndEmptyTests
{
    [Theory]
    [InlineData("""function f(value:number|null|undefined):number {switch(value){case null:{return 7;}case undefined:{return 11;}case 3:{return 13;}default:{return 0;}}}""")]
    [InlineData("""const NIL:null=null;function f(value:number|null):number {switch(value){case NIL:{return 7;}default:{return 0;}}}""")]
    [InlineData("""const MISSING:undefined=undefined;function f(value:string|undefined):number {switch(value){case MISSING:{return 11;}default:{return 0;}}}""")]
    [InlineData("""function f(n:number):number {switch(n){}return n;}""")]
    [InlineData("""function f(n:number):number {switch(n){case null:{return 7;}case undefined:{return 11;}default:{return n;}}}""")]
    [InlineData("""let x:number=0;for(let i:number=0;i<3;i++){switch(i){}x+=1;}""")]
    public void NullishLabelsAndEmptySwitchUseNormalTypeChecking(string source) =>
        Assert.Empty(Compilation.Check("switch-nullish.ts",source));

    [Theory]
    [InlineData("""switch(3){case null:{break;}case null:{break;}}""","TSCS1001")]
    [InlineData("""switch(3){case undefined:{break;}case undefined:{break;}}""","TSCS1001")]
    [InlineData("""let value:number=1;switch(value){case "wrong":{break;}}""","TS2678")]
    [InlineData("""function f(n:number):number {switch(n){}}""","TSCS1001")]
    [InlineData("""switch(3){case null:{let n:number="wrong";}}""","TS2322")]
    [InlineData("""switch(missing()){}""","TS2304")]
    public void EmptyAndNullishSwitchPreserveActualErrors(string source,string code) =>
        Assert.Contains(Compilation.Check("switch-nullish-negative.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void NullishCasesAndEmptySelectorExecuteAcrossFourEsModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-nullish-switch-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export function normalize(value:number|null|undefined):number|null|undefined{return value;}""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {normalize} from "./model.js";let calls:number=0;""" +
                """function choose(value:number|null|undefined):number|null|undefined{calls+=1;return normalize(value);}""" +
                """function classify(value:number|null|undefined):number{let result:number=0;""" +
                """switch(choose(value)){case null:{result=7;break;}case undefined:{result=11;break;}case 3:{result=13;break;}default:{result=-1;break;}}""" +
                """switch(choose(99)){}return result;}""" +
                """export const result:number=classify(null)+classify(undefined)+classify(3)+calls;""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {result} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {result} from "./bridge.js";export const answer:number=result;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,file=>file.Text.Contains("case null:",StringComparison.Ordinal));
            Assert.Contains(emitted,file=>file.Text.Contains("case undefined:",StringComparison.Ordinal));
            Assert.Contains(emitted,file=>file.Text.Contains("switch (",StringComparison.Ordinal));
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
                """import {answer} from './entry.js';console.log(JSON.stringify(answer));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Nullish switch module execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("37",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleSelectorMustPreventAnyEmission()
    {
        const string root="/virtual/switch-nullish-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export function normalize(value:number|null):number|null{return value;}""";
        host.Files[root+"/entry.ts"]=
            """import {normalize} from "./model.js";switch(normalize("wrong")){}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2345");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded compiler behavior, not original pinned TS7 acceptance.
public sealed class SwitchStaticPropertyCaseTests
{
    [Theory]
    [InlineData("""const codes:{ready:number}={ready:2};switch(2){case codes.ready:{break;}}""")]
    [InlineData("""const codes:{word:string}={word:"ready"};switch("ready"){case codes["word"]:{break;}}""")]
    [InlineData("""const codes:{nested:{value:number}}={nested:{value:3}};switch(4){case codes.nested.value+1:{break;}}""")]
    [InlineData("""const codes:{value:number}={value:2};function normal(n:number):number{return n;}switch(2){case normal(codes.value):{break;}}""")]
    [InlineData("""const codes:{value:number}={value:2};switch(3){case codes.value+1:{break;}default:{break;}}""")]
    [InlineData("""const codes:{value:number}={value:2};switch(2){case true?codes.value:3:{break;}}""")]
    public void TypedStaticDataReadsCanFormComputedCaseLabels(string source) =>
        Assert.Empty(Compilation.Check("switch-data-read.ts",source));

    [Theory]
    [InlineData("""const codes:{value:number}={value:2};switch("wrong"){case codes.value:{break;}}""","TS2678")]
    [InlineData("""const codes:{value:number}={value:2};switch(2){case codes.missing:{break;}}""","TS2339")]
    [InlineData("""const codes:{nested:{value:number}}={nested:{value:2}};switch(2){case codes.nested:{break;}}""","TSCS1001")]
    [InlineData("""const codes:{value:any}={value:2};switch(2){case codes.value:{break;}}""","TSCS1001")]
    [InlineData("""function load():{value:number}{return {value:2};}switch(2){case load().value:{break;}}""","TSCS1001")]
    [InlineData("""const codes:{value:number}={value:2};let key:string="value";switch(2){case codes[key]:{break;}}""","TSCS1001")]
    [InlineData("""const codes:{value:number}={value:2};switch(2){case codes?.value:{break;}}""","TSCS1001")]
    [InlineData("""let n:number=2;const codes:{value:number}={value:2};switch(n){case codes.value++:{break;}}""","TSCS1001")]
    public void UnsupportedOrIncompatibleReadsStayRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("switch-data-read-invalid.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void FourLinkedModulesCompileAndExecuteStaticPropertyCases()
    {
        var root=Directory.CreateTempSubdirectory("tscs-switch-data-read-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const codes:{hit:number;other:number;nested:{bonus:number}}={hit:2,other:4,nested:{bonus:3}};""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {codes} from "./model.js";""" +
                """export function choose(n:number):number{switch(n){case codes.hit:{return 11;}case codes["other"]:{return 22;}case codes.nested.bonus+2:{return 33;}default:{return 0;}}}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {choose} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {choose} from "./bridge.js";export const answer:number=choose(2)*10000+choose(4)*100+choose(5);""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,file=>file.Text.Contains("case codes.hit:",StringComparison.Ordinal));
            Assert.Contains(emitted,file=>file.Text.Contains("codes[\"other\"]",StringComparison.Ordinal));
            foreach(var file in emitted)
                File.WriteAllText(Path.ChangeExtension(file.SourceFile,".js"),file.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{ "type": "module" }""");
            var info=new ProcessStartInfo("node") {
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
                throw new TimeoutException("Static property case module run exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("112233",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedDataReadBlocksAllJavaScriptOutput()
    {
        const string root="/virtual/switch-data-read-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const codes:{value:number}={value:2};""";
        host.Files[root+"/entry.ts"]=
            """import {codes} from "./model.js";switch("wrong"){case codes.value:{break;}default:{break;}}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2678");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

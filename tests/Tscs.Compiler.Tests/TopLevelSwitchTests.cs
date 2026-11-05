using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed product acceptance for module-level switch statements.
// These checks are not original pinned TS7 compatibility PASS records.
public sealed class TopLevelSwitchTests
{
    [Theory]
    [InlineData("""let result:number=0; switch(2){case 1:{result=10;break;}case 2:{result=21;break;}default:{result=-1;}}""")]
    [InlineData("""const kind:"ok"|"error"="ok";let result:number=0;switch(kind){case "ok":{result=21;break;}case "error":{result=0;break;}}""")]
    [InlineData("""let total:number=0;for(let n:number=0;n<3;n++){switch(n){case 1:{total+=10;break;}default:{total+=1;break;}}}""")]
    [InlineData("""let seen:number=0;switch(1){case 0:{seen=10;}case 1:{seen=seen+1;break;}default:{seen=0;}}""")]
    public void ModuleLevelSwitchUsesTheExistingCheckedPipeline(string source) =>
        Assert.Empty(Compilation.Check("top-level-switch.ts",source));

    [Theory]
    [InlineData("""let result:number=0;switch(1){case 1:{result="bad";break;}default:{break;}}""","TS2322")]
    [InlineData("""switch(1){case "wrong":{break;}default:{break;}}""","TS2678")]
    [InlineData("""switch(1){case 1:{break;}case 1:{break;}default:{break;}}""","TSCS1001")]
    [InlineData("""switch(1){case 1:{continue;}default:{break;}}""","TSCS1001")]
    [InlineData("""switch(1){case 1:{break;}}break;""","TSCS1001")]
    public void ModuleLevelSwitchRejectsInvalidTypesAndControlFlow(string source,string code) =>
        Assert.Contains(Compilation.Check("top-level-switch-bad.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void ModuleLevelSwitchEmitsAndRunsAcrossFourEsModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-top-level-switch-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export function score(n:number):number{return n*10+1;}""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {score} from "./model.js";""" +
                """export let result:number=0;""" +
                """switch(score(2)){case 21:{result=score(4);break;}""" +
                """default:{result=-1;break;}}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {result} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {result} from "./bridge.js";export const answer:number=result;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
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
                throw new TimeoutException("Top-level switch module execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("41",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedCallInsideModuleSwitchPreventsAllOutput()
    {
        const string root="/virtual/top-level-switch-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]="""export function score(n:number):number{return n+1;}""";
        host.Files[root+"/entry.ts"]=
            """import {score} from "./model.js";let out:number=0;""" +
            """switch(1){case 1:{out=score("bad");break;}default:{break;}}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2345");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

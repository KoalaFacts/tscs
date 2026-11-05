using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded compiler behavior: not an original pinned TS7 compatibility PASS.
public sealed class SwitchComputedCaseTests
{
    [Theory]
    [InlineData("""let READY="ready";switch("ready"){case READY:{break;}}""")]
    [InlineData("""const READY:string="ready";switch("ready"){case READY:{break;}}""")]
    [InlineData("""function choose():string{return "ready";}const READY=choose();switch("ready"){case READY:{break;}}""")]
    [InlineData("""let step:number=2;switch(3){case step+1:{break;}default:{break;}}""")]
    [InlineData("""let prefix:string="re";switch("ready"){case prefix+"ady":{break;}default:{break;}}""")]
    [InlineData("""let delta:number=1;switch(-1){case (-delta):{break;}}""")]
    [InlineData("""let code:number=2;switch(code){case code:{break;}case code:{break;}default:{break;}}""")]
    [InlineData("""let amount:number=8;switch(amount){case (amount/2):{break;}case amount+1:{break;}}""")]
    [InlineData("""function twice(n:number):number{return n*2;}switch(2){case twice(1):{break;}}""")]
    [InlineData("""function compute(n:number):number{return n+1;}switch(3){case (compute(1+1)):{break;}default:{break;}}""")]
    [InlineData("""const handler={code(n:number):number{return n+1;}};switch(2){case handler.code(1):{break;}default:{break;}}""")]
    [InlineData("""const handler={code(n:number):number{return n+1;}};switch(2){case (handler.code)(1):{break;}}""")]
    [InlineData("""const handler={code(n:number):number{return n+1;}};switch(2){case handler["code"](1):{break;}}""")]
    [InlineData("""function compare(x:number|boolean,y:number|string):number{switch(x){case y:{return 1;}default:{return 0;}}}""")]
    public void MutableWidenedAndArithmeticCaseValuesAreCheckedAndEmitted(string source) =>
        Assert.Empty(Compilation.Check("computed-case.ts",source));

    [Theory]
    [InlineData("""let key:string="a";switch(3){case key:{break;}}""","TS2678")]
    [InlineData("""let count:number=2;switch(count){case count+"x":{break;}}""","TS2678")]
    [InlineData("""let base:number=2;switch(base){case base+missing:{break;}}""","TS2304")]
    [InlineData("""let data:{value:number}={value:2};switch(2){case data:{break;}}""","TSCS1001")]
    [InlineData("""let data:{value:number}={value:2};switch("x"){case ""+data:{break;}}""","TSCS1001")]
    [InlineData("""let loose:any=2;switch(2){case loose/2:{break;}}""","TSCS1001")]
    [InlineData("""let value:number=2;switch(value){case value++:{break;}}""","TSCS1001")]
    [InlineData("""function code(n:number):number{return n;}switch("ready"){case code(1):{break;}}""","TS2678")]
    [InlineData("""function code(n:number):number{return n;}switch(2){case code("bad"):{break;}}""","TS2345")]
    [InlineData("""function box():{value:number}{return {value:2};}switch(2){case box():{break;}}""","TSCS1001")]
    [InlineData("""let notCallable:number=1;switch(1){case notCallable(1):{break;}}""","TSCS1001")]
    [InlineData("""const handler={code(n:number):number{return n;}};switch(2){case handler.code("wrong"):{break;}}""","TS2345")]
    [InlineData("""const handler={code(n:number):number{return n;}};switch("ok"){case handler.code(1):{break;}}""","TS2678")]
    [InlineData("""const handler={code:1};switch(1){case handler.code(1):{break;}}""","TSCS1001")]
    [InlineData("""const handler={code(n:number):number{return n;}};switch(1){case handler?.code(1):{break;}}""","TSCS1001")]
    [InlineData("""const handler={code(n:number):number{return n;}};switch(1){case handler.code(1).toString():{break;}}""","TSCS1001")]
    [InlineData("""let READY:number;switch(1){case READY:{break;}}""","TSCS1001")]
    [InlineData("""let v:number=2;switch(v){case v:{let invalid:number="bad";}}""","TS2322")]
    [InlineData("""type E={kind:"ok";value:number}|{kind:"bad";reason:string};function read(e:E,k:string):number{switch(e.kind){case k:{return e.value;}default:{return 0;}}}""","TS2339")]
    public void InvalidCasesAndUnprovenDiscriminantNarrowingAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("computed-case-bad.ts",source),d=>d.Code==code);

    [Fact]
    public void IgnoredUnknownCaseCannotHideIndependentBodyErrors()
    {
        const string source = """
            let base:number=2;
            switch(base){
              // @ts-ignore
              case base+missing:{
                let invalid:number="wrong";
                break;
              }
              case 3:{
                let alsoInvalid:number="wrong";
                break;
              }
            }
            """;
        var diagnostics=Compilation.Check("computed-case-ignored.ts",source);
        Assert.DoesNotContain(diagnostics,d=>d.Code=="TS2304");
        Assert.Equal(2,diagnostics.Count(d=>d.Code=="TS2322"));
    }

    [Theory]
    [InlineData("""
        let calls:number=0;
        function mutate():number{calls++;return calls;}
        switch(2){
          // @ts-ignore
          case missing+mutate():{break;}
        }
        """)]
    [InlineData("""
        let data:{value:number}={value:2};
        switch(2){
          // @ts-ignore
          case missing+data:{break;}
        }
        """)]
    public void IgnoredUnknownNameCannotMaskEffectfulOrNonprimitiveSibling(string source)
    {
        var diagnostics=Compilation.Check("computed-case-ignored-unsafe.ts",source);
        Assert.Contains(diagnostics,d=>d.Code=="TSCS1001");
    }

    [Fact]
    public void UnknownComputedLabelRetainsOriginalNameErrorAndChecksBody()
    {
        const string source = """
            let base:number=2;
            switch(base){
              case base+missing:{
                let invalid:number="wrong";
                break;
              }
            }
            """;
        var diagnostics=Compilation.Check("computed-case-unknown.ts",source);
        Assert.Contains(diagnostics,d=>d.Code=="TS2304");
        Assert.Contains(diagnostics,d=>d.Code=="TS2322");
    }

    [Fact]
    public void ExactLiteralDuplicateChecksRemainBlocking()
    {
        const string source="""let candidate:number=2;switch(candidate){case 1:{break;}case 1:{break;}case candidate:{break;}}""";
        Assert.Contains(Compilation.Check("computed-duplicate.ts",source),d=>d.Code=="TSCS1001");
    }

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void ComputedCaseValuesRunAcrossFourLinkedModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-computed-switch-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export let offset:number=2;export const threshold:number=4;""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {offset,threshold} from "./model.js";""" +
                """export function resolve(n:number):number{switch(n){case (offset+1):{return 21;}case threshold:{return 7;}default:{return -9;}}}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {resolve} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {resolve} from "./bridge.js";export const answer:number=resolve(3)+resolve(4);""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,file=>file.Text.Contains("case ",StringComparison.Ordinal));
            foreach(var file in emitted)
                File.WriteAllText(Path.ChangeExtension(file.SourceFile,".js"),file.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node"){
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
                throw new TimeoutException("Computed case module run exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("28",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void DirectCheckedCaseCallsExecuteOnlyUntilTheFirstMatchAcrossModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-call-switch-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export let ticks:number=0;export function next(n:number):number{ticks++;return n;}export function count():number{return ticks;}""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {next,count} from "./model.js";""" +
                """export function choose(n:number):number{switch(n){case next(1):{return count()*10+1;}case next(2):{return count()*10+2;}default:{return count()*10+9;}}}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {choose} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {choose} from "./bridge.js";export const answer:number=choose(1)*100+choose(2);""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,file=>file.Text.Contains("case next(",StringComparison.Ordinal));
            foreach(var file in emitted)
                File.WriteAllText(Path.ChangeExtension(file.SourceFile,".js"),file.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node"){
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
                throw new TimeoutException("Direct-call switch module run exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("1132",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void CheckedMemberCaseCallsExecuteInOrderAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-member-switch-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export let hits:number=0;export const handler:{first:(n:number)=>number;second:(n:number)=>number}={first(n:number):number{hits++;return n;},second(n:number):number{hits+=10;return n;}};export function count():number{return hits;}""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {handler,count} from "./model.js";""" +
                """export function choose(n:number):number{switch(n){case handler.first(1):{return count()*10+1;}case handler["second"](2):{return count()*10+2;}default:{return count()*10+9;}}}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {choose} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {choose} from "./bridge.js";export const answer:number=choose(1)*100+choose(2);""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,file=>file.Text.Contains("case handler.first(",StringComparison.Ordinal));
            Assert.Contains(emitted,file=>file.Text.Contains("handler[",StringComparison.Ordinal));
            foreach(var file in emitted)
                File.WriteAllText(Path.ChangeExtension(file.SourceFile,".js"),file.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node"){
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
                throw new TimeoutException("Member-call switch module run exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("1222",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedMemberCaseArgumentPreventsJavaScriptOutput()
    {
        const string root="/virtual/member-switch-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const handler:{code:(n:number)=>number}={code(n:number):number{return n;}};""";
        host.Files[root+"/entry.ts"]=
            """import {handler} from "./model.js";switch(2){case handler.code("bad"):{break;}default:{break;}}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2345");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }

    [Fact]
    public void InvalidImportedDirectCaseCallPreventsAllJavaScriptOutput()
    {
        const string root="/virtual/call-switch-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]="""export function code(n:number):number{return n;}""";
        host.Files[root+"/entry.ts"]=
            """import {code} from "./model.js";switch(2){case code("wrong"):{break;}default:{break;}}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2345");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }

    [Fact]
    public void BadImportedCallInComputedCaseProgramPreventsAllJavaScriptOutput()
    {
        const string root="/virtual/computed-switch-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export function normalize(n:number):number{return n;}""";
        host.Files[root+"/entry.ts"]=
            """import {normalize} from "./model.js";let value:number=2;""" +
            """switch(value){case value+1:{let bad:number=normalize("bad");break;}default:{break;}}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2345");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

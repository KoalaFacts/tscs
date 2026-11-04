using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed and executed bounded feature tests; not original TS7 acceptance.
public sealed class NumberFormattingTests
{
    [Theory]
    [InlineData("""let n:number=255;let text:string=n.toString();""")]
    [InlineData("""let n:number=255;let text:string=n.toString(16);""")]
    [InlineData("""let n:number=255;let text:string=n.toString(undefined);""")]
    [InlineData("""let n:255=255;let text:string=n.toString(2);""")]
    [InlineData("""let n:number=12.5;let text:string=n.toFixed();""")]
    [InlineData("""let n:number=12.5;let text:string=n.toFixed(2);""")]
    [InlineData("""let n:number=12.5;let text:string=n.toExponential(1);""")]
    [InlineData("""let n:number=12.5;let text:string=n.toPrecision(3);""")]
    [InlineData("""let n:number=12.5;let text:string=n.toPrecision(undefined);""")]
    [InlineData("""let n:number=12.5;let result:number=n.valueOf();""")]
    [InlineData("""function display(n:number):string{return n.toFixed(2)+":"+n.toString(16);}let s:string=display(255);""")]
    [InlineData("""let n:1|2=1;let s:string=n.toString(10);""")]
    public void KnownNumberMethodsHaveCheckedTypes(string source) =>
        Assert.Empty(Compilation.Check("number-methods.ts",source));

    [Theory]
    [InlineData("""let n:number=42;let wrong:number=n.toString();""","TS2322")]
    [InlineData("""let n:number=42;n.toString("16");""","TS2345")]
    [InlineData("""let n:number=42;n.toString(2,10);""","TS2554")]
    [InlineData("""let n:number=42;n.toFixed("2");""","TS2345")]
    [InlineData("""let n:number=42;n.toExponential(true);""","TS2345")]
    [InlineData("""let n:number=42;n.toPrecision(1,2);""","TS2554")]
    [InlineData("""let n:number=42;n.valueOf(2);""","TS2554")]
    [InlineData("""let n:number=42;let wrong:string=n.valueOf();""","TS2322")]
    [InlineData("""let flag:boolean=true;flag.toFixed(2);""","TS2339")]
    public void InvalidNumberCallsAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-number-methods.ts",source),
            diagnostic=>diagnostic.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void NumberFormattingExecutesAcrossFourLinkedModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-number-format-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export type Score=number;export const score:Score=255;""";
            host.Files[Path.Combine(root,"format.ts")]=
                """import type {Score} from "./model.js";""" +
                """export function format(value:Score):string{return value.toString(16).concat("|",value.toFixed(1),"|",value.toExponential(1),"|",value.toPrecision(4));}""" +
                """export function identity(value:Score):number{return value.valueOf();}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {score} from "./model.js";export {format,identity} from "./format.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {score,format,identity} from "./bridge.js";""" +
                """export const output:string=format(score);export const original:number=identity(score);""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {
                    NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
                });
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,js=>js.Text.Contains(".toFixed(",StringComparison.Ordinal));
            Assert.Contains(emitted,js=>js.Text.Contains(".toString(",StringComparison.Ordinal));
            foreach(var item in emitted)
                File.WriteAllText(Path.ChangeExtension(item.SourceFile,".js"),item.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node") {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add(
                """import {output,original} from './entry.js';console.log(JSON.stringify({output,original}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Number formatting execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"output":"ff|255.0|2.6e+2|255.0","original":255}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleNumberResultCannotEmitJavaScript()
    {
        const string root="/virtual/invalid-number-format";
        var host=new Host();
        host.Files[root+"/model.ts"]="export const value:number=42;";
        host.Files[root+"/format.ts"]=
            """export function format(n:number):string{return n.toFixed(2);}""";
        host.Files[root+"/entry.ts"]=
            """import {value} from "./model.js";import {format} from "./format.js";export const wrong:number=format(value);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {
                NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
            });
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

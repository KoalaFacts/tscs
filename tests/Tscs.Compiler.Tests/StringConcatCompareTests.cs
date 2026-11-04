using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded source-backed compiler behavior, not original TS7 acceptance.
public sealed class StringConcatCompareTests
{
    [Theory]
    [InlineData("""let s:string="koala";let result:string=s.concat();""")]
    [InlineData("""let s:string="koala";let result:string=s.concat("-","facts");""")]
    [InlineData("""let s:"koala"="koala";let result:string=s.concat("-facts");""")]
    [InlineData("""let items:string[]=["-","facts"];let result:string="koala".concat(...items);""")]
    [InlineData("""let items:readonly string[]=["-","facts"];let result:string="koala".concat(...items);""")]
    [InlineData("""function append(prefix:string,rest:readonly string[]):string{return prefix.concat(...rest);}let result:string=append("a",["b","c"]);""")]
    [InlineData("""let result:number="same".localeCompare("same");""")]
    [InlineData("""let result:number="same".localeCompare("same",undefined);""")]
    [InlineData("""let result:number="same".localeCompare("same","en");""")]
    [InlineData("""let isEqual:boolean="a".concat("b").localeCompare("ab")===0;""")]
    public void NativeStringConcatAndComparisonAreChecked(string source) =>
        Assert.Empty(Compilation.Check("string-concat-compare.ts",source));

    [Theory]
    [InlineData("""let result:string="a".concat(42);""","TS2345")]
    [InlineData("""let result:string="a".concat("b",false);""","TS2345")]
    [InlineData("""let xs:number[]=[1];let result:string="a".concat(...xs);""","TS2345")]
    [InlineData("""let result:number="a".concat("b");""","TS2322")]
    [InlineData("""let result:number="a".localeCompare();""","TS2554")]
    [InlineData("""let result:number="a".localeCompare(123);""","TS2345")]
    [InlineData("""let result:number="a".localeCompare("b",true);""","TS2345")]
    [InlineData("""let result:number="a".localeCompare("b","en","extra");""","TS2554")]
    [InlineData("""let result:string="a".localeCompare("b");""","TS2322")]
    public void InvalidStringArgumentsAndReturnTypesAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-string-concat-compare.ts",source),
            diagnostic=>diagnostic.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void NativeStringCallsExecuteAcrossFourLinkedModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-string-concat-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export type Label=string;export const prefix:Label="koala";export const pieces:readonly string[]=["-","facts"];""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import type {Label} from "./model.js";""" +
                """export function combine(s:Label,items:readonly string[]):string{return s.concat(...items);}""" +
                """export function same(a:string,b:string):boolean{return a.localeCompare(b)===0;}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {prefix,pieces} from "./model.js";export {combine,same} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {prefix,pieces,combine,same} from "./bridge.js";""" +
                """export const label:string=combine(prefix,pieces);export const equal:boolean=same(label,"koala-facts");""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {
                    NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
                });
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,js=>js.Text.Contains(".concat(",StringComparison.Ordinal));
            Assert.Contains(emitted,js=>js.Text.Contains(".localeCompare(",StringComparison.Ordinal));
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
                """import {label,equal} from './entry.js';console.log(JSON.stringify({label,equal}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Native string calls exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"label":"koala-facts","equal":true}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleResultMustNotEmitAnyJavaScript()
    {
        const string root="/virtual/invalid-string-concat";
        var host=new Host();
        host.Files[root+"/model.ts"]="export const prefix:string=\"a\";";
        host.Files[root+"/logic.ts"]=
            """export function combine(s:string):string{return s.concat("b");}""";
        host.Files[root+"/entry.ts"]=
            """import {prefix} from "./model.js";import {combine} from "./logic.js";export const wrong:number=combine(prefix);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {
                NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
            });
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

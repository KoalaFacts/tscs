using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Native ES2015 Object.is / ES2022 Object.hasOwn (bounded, not TS7 acceptance).
public sealed class NativeObjectIdentityTests
{
    [Theory]
    [InlineData("""let result:boolean=Object.is(1,1);""")]
    [InlineData("""let result:boolean=Object.is(0,-0);""")]
    [InlineData("""let result:boolean=Object.is(0/0,0/0);""")]
    [InlineData("""let result:boolean=Object.is(null,undefined);""")]
    [InlineData("""let result:boolean=Object.is({id:1},{id:1});""")]
    [InlineData("""let result:boolean=Object.hasOwn({id:1},"id");""")]
    [InlineData("""let result:boolean=Object.hasOwn({id:1},"missing");""")]
    [InlineData("""let result:boolean=Object.hasOwn({id:1},0);""")]
    [InlineData("""let a:number[]=[1,2];let result:boolean=Object.hasOwn(a,"length");""")]
    [InlineData("""let a:readonly number[]=[1,2];let result:boolean=Object.hasOwn(a,0);""")]
    [InlineData("""function f(Object:{is:(a:number,b:number)=>number}):number{return Object.is(2,3); }""")]
    [InlineData("""function f(Object:{hasOwn:(a:number,k:string)=>number}):number{return Object.hasOwn(3,"x");}""")]
    public void AcceptsNativeIdentityAndOwnKeyCalls(string source)=>
        Assert.Empty(Compilation.Check("object-identity.ts",source));

    [Theory]
    [InlineData("""let result=Object.is();""","TS2554")]
    [InlineData("""let result=Object.is(1);""","TS2554")]
    [InlineData("""let result=Object.is(1,2,3);""","TS2554")]
    [InlineData("""let result=Object.is(...[1,2]);""","TSCS1001")]
    [InlineData("""let result=Object.hasOwn({id:1});""","TS2554")]
    [InlineData("""let result=Object.hasOwn({id:1},"id","extra");""","TS2554")]
    [InlineData("""let result=Object.hasOwn(...[{id:1},"id"]);""","TSCS1001")]
    [InlineData("""let result=Object.hasOwn(null,"id");""","TSCS1001")]
    [InlineData("""let result=Object.hasOwn(21,"id");""","TSCS1001")]
    [InlineData("""let result=Object.hasOwn({id:1},true);""","TSCS1001")]
    [InlineData("""let result=Object.hasOwn({id:1},undefined);""","TSCS1001")]
    [InlineData("""let result:number=Object.hasOwn({id:1},"id");""","TS2322")]
    [InlineData("""function f(Object:{is:(a:number,b:number)=>number}):number{return Object.is("bad",3);}""","TS2345")]
    [InlineData("""function f(Object:{hasOwn:(n:number,k:string)=>number}):number{return Object.hasOwn("bad","id");}""","TS2345")]
    public void RejectsInvalidNativeIdentityAndOwnKeyCalls(string source,string code)=>
        Assert.Contains(Compilation.Check("object-identity-invalid.ts",source),
            diagnostic=>diagnostic.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void SameValueAndOwnKeySemanticsExecuteAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-object-identity-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const item:{id:number,label:string}={id:21,label:"koala"};""" +
                """export const other:{code:number}={code:7};""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """export function inspect(a:{id:number,label:string},b:{code:number}):boolean[]{return [Object.is(a,a),Object.is(a,b),Object.hasOwn(a,"id"),Object.hasOwn(a,"toString"),Object.hasOwn(a,7),Object.is(0,-0),Object.is(0/0,0/0)];}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {item,other} from "./model.js";export {inspect} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {item,other,inspect} from "./bridge.js";export const result:boolean[]=inspect(item,other);""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,item=>item.Text.Contains("Object.is(",StringComparison.Ordinal));
            Assert.Contains(emitted,item=>item.Text.Contains("Object.hasOwn(",StringComparison.Ordinal));
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
                """import {result} from './entry.js';console.log(JSON.stringify(result));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Object identity runtime exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("[true,false,true,false,false,false,true]",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedPropertyKeyPreventsJavaScriptOutput()
    {
        const string root="/virtual/object-identity-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const item:{id:number}={id:21};""";
        host.Files[root+"/entry.ts"]=
            """import {item} from "./model.js";export const invalid:boolean=Object.hasOwn(item,true);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TSCS1001");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

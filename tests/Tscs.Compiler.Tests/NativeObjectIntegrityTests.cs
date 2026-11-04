using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed bounded ES2015 checks; not original TS7 compatibility credit.
public sealed class NativeObjectIntegrityTests
{
    [Theory]
    [InlineData("""let frozen=Object.freeze({id:1,label:"ok"});let n:number=frozen.id;""")]
    [InlineData("""let frozen=Object.freeze({inner:{value:1}});frozen.inner.value=2;""")]
    [InlineData("""let frozen:readonly number[]=Object.freeze([2,4]);""")]
    [InlineData("""let frozen=Object.freeze([2,4] as const);let first:number=frozen[0];""")]
    [InlineData("""let item:{value?:number}={value:1};let frozen=Object.freeze(item);let n:number|undefined=frozen.value;""")]
    [InlineData("""let item=Object.seal({count:1});item.count=2;""")]
    [InlineData("""let item:number[]=Object.seal([2,4]);item[0]=5;""")]
    [InlineData("""let item=Object.preventExtensions({count:1});item.count=2;""")]
    [InlineData("""let item:number[]=Object.preventExtensions([2,4]);item[0]=5;""")]
    [InlineData("""let yes:boolean=Object.isFrozen(Object.freeze({count:1}));""")]
    [InlineData("""let yes:boolean=Object.isSealed(Object.seal({count:1}));""")]
    [InlineData("""let no:boolean=Object.isExtensible(Object.preventExtensions({count:1}));""")]
    [InlineData("""function f(Object:{freeze:(n:number)=>number}):number{return Object.freeze(7);}""")]
    public void AcceptsIntegrityOperations(string source) =>
        Assert.Empty(Compilation.Check("object-integrity.ts",source));

    [Theory]
    [InlineData("""let f=Object.freeze();""","TS2554")]
    [InlineData("""let f=Object.freeze({},{});""","TS2554")]
    [InlineData("""let f=Object.freeze(...[{}]);""","TSCS1001")]
    [InlineData("""let f=Object.seal(42);""","TSCS1001")]
    [InlineData("""let f=Object.preventExtensions(null);""","TSCS1001")]
    [InlineData("""let f=Object.isFrozen(undefined);""","TSCS1001")]
    [InlineData("""let f=Object.isSealed({a:1},{});""","TS2554")]
    [InlineData("""let f=Object.isExtensible(...[{}]);""","TSCS1001")]
    [InlineData("""let f=Object.freeze({value:1});f.value=2;""","TS2540")]
    [InlineData("""let f=Object.freeze({value:1});f.value+=2;""","TS2540")]
    [InlineData("""let f=Object.freeze([2,4]);f[0]=5;""","TS2542")]
    [InlineData("""let values:number[]=Object.freeze([2,4]);""","TS4104")]
    [InlineData("""let f:string=Object.isFrozen({value:1});""","TS2322")]
    [InlineData("""function f(Object:{freeze:(n:number)=>number}):number{return Object.freeze("bad");}""","TS2345")]
    public void RejectsUnsafeIntegrityOperations(string source,string code) =>
        Assert.Contains(Compilation.Check("object-integrity-invalid.ts",source),
            diagnostic=>diagnostic.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void NativeIntegrityRunsAcrossFourLinkedModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-object-integrity-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const record:{count:number,nested:{value:number}}={count:21,nested:{value:1}};""" +
                """export const list:number[]=[2,4];""" +
                """export const sealTarget:{name:string}={name:"seal"};""" +
                """export const extTarget:{name:string}={name:"ext"};""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """export function lock(item:{count:number,nested:{value:number}}):{readonly count:number;readonly nested:{value:number}}{return Object.freeze(item);}""" +
                """export function lockList(items:number[]):readonly number[]{return Object.freeze(items);}""" +
                """export function seal(item:{name:string}):{name:string}{return Object.seal(item);}""" +
                """export function prevent(item:{name:string}):{name:string}{return Object.preventExtensions(item);}""" +
                """export function checks(a:{readonly count:number;readonly nested:{value:number}},b:readonly number[],c:{name:string},d:{name:string}):boolean[]{return [Object.isFrozen(a),Object.isFrozen(b),Object.isSealed(c),Object.isExtensible(d)];}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {record,list,sealTarget,extTarget} from "./model.js";""" +
                """export {lock,lockList,seal,prevent,checks} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {record,list,sealTarget,extTarget,lock,lockList,seal,prevent,checks} from "./bridge.js";""" +
                """export const locked=lock(record);export const numbers:readonly number[]=lockList(list);""" +
                """export const sealed=seal(sealTarget);export const blocked=prevent(extTarget);""" +
                """locked.nested.value=9;export const flags:boolean[]=checks(locked,numbers,sealed,blocked);""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,item=>item.Text.Contains("Object.freeze(",StringComparison.Ordinal));
            Assert.Contains(emitted,item=>item.Text.Contains("Object.seal(",StringComparison.Ordinal));
            Assert.Contains(emitted,item=>item.Text.Contains("Object.preventExtensions(",StringComparison.Ordinal));
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
                """import {locked,numbers,flags} from './entry.js';import {record,list} from './model.js';console.log(JSON.stringify({flags,nested:locked.nested.value,alias:locked===record&&numbers===list,count:locked.count}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Object integrity runtime exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"flags":[true,true,true,false],"nested":9,"alias":true,"count":21}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleFrozenWritePreventsJavaScriptOutput()
    {
        const string root="/virtual/object-integrity-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const record:{count:number}={count:21};""";
        host.Files[root+"/entry.ts"]=
            """import {record} from "./model.js";export const frozen=Object.freeze(record);frozen.count=3;""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2540");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

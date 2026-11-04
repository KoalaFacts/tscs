using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Finite native ES2015 Object.assign subset; not full original TS7 acceptance.
public sealed class NativeObjectAssignTests
{
    [Theory]
    [InlineData("""let result=Object.assign({id:21},{name:"koala"});let id:number=result.id;let name:string=result.name;""")]
    [InlineData("""let result=Object.assign({},{first:1},{second:"ok"},{ready:true});let first:number=result.first;let second:string=result.second;let ready:boolean=result.ready;""")]
    [InlineData("""let result=Object.assign({point:{value:1}},{label:"a"});result.point.value=2;""")]
    [InlineData("""let result=Object.assign({n:1},{n:2},{tag:"ok"});result.n=4;""")]
    [InlineData("""let result:{id:number,label:string}=Object.assign({id:21},{label:"ready"});""")]
    [InlineData("""let unchanged:{id:number}=Object.assign({id:21});unchanged.id=22;""")]
    [InlineData("""function f(Object:{assign:(n:number)=>number}):number{return Object.assign(7);}""")]
    public void AcceptsFiniteCheckedStructuralMerges(string source) =>
        Assert.Empty(Compilation.Check("object-assign.ts",source));

    [Theory]
    [InlineData("""let value=Object.assign();""","TS2554")]
    [InlineData("""let value=Object.assign(...[{}]);""","TSCS1001")]
    [InlineData("""let value=Object.assign(21,{id:1});""","TSCS1001")]
    [InlineData("""let value=Object.assign({id:1},null);""","TSCS1001")]
    [InlineData("""let value=Object.assign({id:1},[2,4]);""","TSCS1001")]
    [InlineData("""let value=Object.assign({id:1},{id:"wrong"});""","TSCS1001")]
    [InlineData("""let source:{name?:string}={};let value=Object.assign({id:1},source);""","TSCS1001")]
    [InlineData("""let source:{readonly name:string}={name:"a"};let value=Object.assign({},source);""","TSCS1001")]
    [InlineData("""let value=Object.assign({id:1},{name:"ok"});let wrong:string=value.id;""","TS2322")]
    [InlineData("""let value=Object.assign({id:1},{name:"ok"});value.missing=2;""","TS2339")]
    [InlineData("""function f(Object:{assign:(n:number)=>number}):number{return Object.assign("bad");}""","TS2345")]
    public void RejectsUnsafeStructuralMerges(string source,string code) =>
        Assert.Contains(Compilation.Check("object-assign-invalid.ts",source),
            diagnostic=>diagnostic.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void ObjectAssignPreservesTargetIdentityAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-object-assign-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const base:{id:number,nested:{count:number}}={id:21,nested:{count:1}};""" +
                """export const extra:{name:string}={name:"koala"};""" +
                """export const flags:{ready:boolean}={ready:true};""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """export function merge(a:{id:number,nested:{count:number}},b:{name:string},c:{ready:boolean}):{id:number,nested:{count:number},name:string,ready:boolean}{return Object.assign(a,b,c);}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {base,extra,flags} from "./model.js";""" +
                """export {merge} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {base,extra,flags,merge} from "./bridge.js";""" +
                """export const result=merge(base,extra,flags);""" +
                """result.nested.count=9;export const original=base;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,item=>item.Text.Contains("Object.assign(",StringComparison.Ordinal));
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
                """import {result,original} from './entry.js';console.log(JSON.stringify({same:result===original,id:result.id,name:result.name,ready:result.ready,count:result.nested.count}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Native Object.assign runtime exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"same":true,"id":21,"name":"koala","ready":true,"count":9}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedObjectAssignResultPreventsJavaScriptOutput()
    {
        const string root="/virtual/object-assign-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const base:{id:number}={id:21};""";
        host.Files[root+"/entry.ts"]=
            """import {base} from "./model.js";export const result=Object.assign(base,{name:"koala"});export const wrong:string=result.id;""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

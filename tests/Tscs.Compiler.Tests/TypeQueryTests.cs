using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class TypeQueryTests
{
    [Theory]
    [InlineData("const item:{value:number}={value:21};type Item=typeof item;let next:Item={value:42};")]
    [InlineData("type T=typeof later;const later:{value:number}={value:21};let x:T={value:42};")]
    [InlineData("const item:{nested:{amount:number}}={nested:{amount:21}};type N=typeof item.nested.amount;let n:N=42;")]
    [InlineData("const item:{label:string;value:number}={label:\"ok\",value:21};let key:keyof typeof item=\"label\";")]
    [InlineData("const item:{value:number}={value:21};let n:(typeof item)[\"value\"]=42;")]
    [InlineData("function twice(n:number):number{return n*2;}type Callback=typeof twice;function apply(fn:Callback):number{return fn(21);}let n:number=apply(twice);")]
    [InlineData("const item:{readonly value:number}={value:21};type T=typeof item;let x:T={value:42};")]
    public void ExplicitValueAndFunctionQueriesCompile(string source)=>
        Assert.Empty(Compilation.Check("type-query.ts",source));

    [Theory]
    [InlineData("type T=typeof missing;", "TS2304")]
    [InlineData("const obj:{value:number}={value:21};type V=typeof obj.absent;", "TS2339")]
    [InlineData("const obj:{value:number}={value:21};type V=typeof obj.value;let bad:V=\"wrong\";", "TS2322")]
    [InlineData("let raw=21;type T=typeof raw;", "TSCS1001")]
    [InlineData("type T=typeof raw;let raw:T=21;", "TS2456")]
    [InlineData("type T=typeof 21;", "TSCS1001")]
    public void InvalidOrUnimplementedQueriesFailClosed(string source,string code)=>
        Assert.Contains(Compilation.Check("type-query-bad.ts",source),d=>d.Code==code); 
    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>
            Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine("/virtual",path));
        public string ReadFile(string path)=>
            Files.TryGetValue(path,out var text)?text:throw new FileNotFoundException(path);
    }

    [Fact]
    public void ImportedTypeofQueriesUseExportersOwnTypeNamespaceThroughReexports()
    {
        var host=new Host();
        host.Files["/virtual/types.ts"]="export type Shape={value:number};";
        host.Files["/virtual/model.ts"]=
            "import type {Shape} from \"./types.js\";export const item:Shape={value:21};";
        host.Files["/virtual/logic.ts"]=
            "import type {Shape} from \"./types.js\";export function twice(s:Shape):number{return s.value*2;}";
        host.Files["/virtual/bridge.ts"]=
            "export {item as payload} from \"./model.js\";export {twice as transform} from \"./logic.js\";";
        host.Files["/virtual/entry.ts"]=
            "import {payload,transform} from \"./bridge.js\";"+
            "type P=typeof payload;type K=keyof typeof payload;type N=(typeof payload)[\"value\"];"+
            "type Fn=typeof transform;function apply(fn:Fn,s:P):number{return fn(s);}"+
            "const key:K=\"value\";export const result:N=apply(transform,payload);";
        var program=CompilerProgram.Load(host,["entry.ts"]);
        Assert.Equal(5,program.Sources.Count);
        Assert.Empty(program.Analyze().Diagnostics);
    }

    [Theory]
    [InlineData("type V=typeof payload.nope;","TS2339")]
    [InlineData("type V=(typeof payload)[\"value\"];let wrong:V=\"bad\";","TS2322")]
    [InlineData("let bad:typeof transform=42;","TS2322")]
    [InlineData("import {hidden} from \"./bridge.js\";type T=typeof hidden;","TS2305")]
    [InlineData("import {untyped} from \"./untyped.js\";type T=typeof untyped;","TSCS1001")]
    public void ImportedTypeofQueriesRejectGenuineErrorsAndUnsupportedInference(string source,string code)
    {
        var host=new Host();
        host.Files["/virtual/model.ts"]="export const item:{value:number}={value:21};";
        host.Files["/virtual/logic.ts"]=
            "export function twice(n:number):number{return n*2;}";
        host.Files["/virtual/bridge.ts"]=
            "export {item as payload} from \"./model.js\";export {twice as transform} from \"./logic.js\";";
        host.Files["/virtual/untyped.ts"]="export const untyped=21;";
        host.Files["/virtual/entry.ts"]=
            "import {payload,transform} from \"./bridge.js\";"+source;
        var diagnostic=Assert.Single(CompilerProgram.Load(host,["entry.ts"]).Analyze().Diagnostics);
        Assert.Equal(code,diagnostic.Code);
    }

    [Fact]
    public void CyclicImportedTypeQueriesFailClosed()
    {
        var host=new Host();
        host.Files["/virtual/a.ts"]=
            "import {b} from \"./b.js\";export const a:typeof b={value:21};";
        host.Files["/virtual/b.ts"]=
            "import {a} from \"./a.js\";export const b:typeof a={value:21};";
        Assert.Equal("TSCS1001",
            Assert.Single(CompilerProgram.Load(host,["a.ts"]).Analyze().Diagnostics).Code);
    }

}

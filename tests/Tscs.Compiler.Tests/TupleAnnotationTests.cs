using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed finite tuple checks, separate from original TS7 acceptance.
public sealed class TupleAnnotationTests
{
    [Theory]
    [InlineData("type Pair=[number,string];let pair:Pair=[21,\"go\"];let n:number=pair[0];let s:string=pair[1];")]
    [InlineData("type Pair=readonly [number,string];const pair:Pair=[21,\"go\"];let n:2=pair.length;")]
    [InlineData("type Pair=[number,{label:string}];const pair:Pair=[21,{label:\"go\"}];let s:string=pair[1].label;")]
    [InlineData("type Pair=[number,string];type A=Pair[0];type B=Pair[1];let n:A=21;let s:B=\"go\";")]
    [InlineData("type Pair=[number,string];type Both=Pair[number];let n:Both=21;let s:Both=\"go\";")]
    [InlineData("type Pair=[[number,string],boolean];const p:Pair=[[21,\"go\"],true];let s:string=p[0][1];let b:boolean=p[1];")]
    [InlineData("let pair:[number,string]=[21,\"go\"];pair[0]=42;pair=[42,\"ready\"];let n:number=pair[0];")]
    [InlineData("let empty:[]=[];let n:0=empty.length;")]
    [InlineData("type P=[number,string];let p:P=[21,\"go\"];let part:P[\"length\"]=2;")]
    [InlineData("type P=[readonly [21,\"go\"],boolean];let p:P=[[21,\"go\"],true];let a:21=p[0][0];")]
    public void FixedTupleAnnotationsCheckAndRetainIndividualSlots(string source) =>
        Assert.Empty(Compilation.Check("tuple-annotation.ts",source));

    [Theory]
    [InlineData("let pair:[number,string]=[21,22];","TS2322")]
    [InlineData("let pair:[number,string]=[21];","TS2322")]
    [InlineData("let pair:[number,string]=[21,\"go\",true];","TS2322")]
    [InlineData("let pair:[number,string]=[21,\"go\"];pair[0]=\"bad\";","TS2322")]
    [InlineData("let pair:[number,string]=[21,\"go\"];let n=pair[2];","TS2493")]
    [InlineData("let pair:readonly [number,string]=[21,\"go\"];pair[0]=42;","TS2540")]
    [InlineData("let pair:readonly [number,string]=[21,\"go\"];let mutable:[number,string]=pair;","TS4104")]
    [InlineData("type P=[number,string];let pair:P=[21,\"go\"];let n:number=pair[1];","TS2322")]
    public void InvalidFixedTuplesHaveConcreteTypeErrors(string source,string code) =>
        Assert.Contains(Compilation.Check("tuple-annotation-invalid.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("let p:[number,string]=[21,\"go\"];p[0]++;let n:number=p[0];")]
    [InlineData("let p:[number,string]=[21,\"go\"];++p[0];let n:number=p[0];")]
    [InlineData("let p:[number,string]=[21,\"go\"];const i=0;p[i]++;")]
    public void FiniteTupleNumericUpdatesRespectTheSelectedNumericSlot(string source) =>
        Assert.Empty(Compilation.Check("tuple-update.ts",source));

    [Theory]
    [InlineData("let p:[number,string]=[21,\"go\"];p[1]++;","TSCS1001")]
    [InlineData("let p:[number,string]=[21,\"go\"];++p[1];","TSCS1001")]
    [InlineData("let p:[number]=[21];p[1]++;","TS2493")]
    [InlineData("let p:[number]=[21];++p[1];","TS2493")]
    [InlineData("let p:[number]=[21];const i=2;p[i]++;","TS2493")]
    [InlineData("let p:readonly [number,string]=[21,\"go\"];p[0]++;","TS2540")]
    public void FiniteTupleNumericUpdatesRejectOutOfBoundsReadonlyAndNonNumericSlots(string source,string code) =>
        Assert.Contains(Compilation.Check("tuple-update-invalid.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("type Bad=[number?];")]
    [InlineData("type Bad=[...number[]];")]
    [InlineData("type Bad=[number,,string];")]
    [InlineData("type Bad=[item:number];")]
    public void OptionalRestHolesAndNamedTuplePartsFailClosed(string source) =>
        Assert.Contains(Compilation.Check("tuple-annotation-unsupported.ts",source),
            d=>d.Code=="TSCS1001");

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path) =>
            Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine("/virtual",path));
        public string ReadFile(string path) =>
            Files.TryGetValue(path,out var content)?content:throw new FileNotFoundException(path);
    }

    [Fact]
    public void NamedTupleAliasesAndFunctionsCrossFourLinkedModules()
    {
        var host=new Host();
        host.Files["/virtual/model.ts"]=
            "export type Pair=[number,string];"+
            "export type Nested=[readonly [number,string],{valid:boolean}];";
        host.Files["/virtual/factory.ts"]=
            "import type {Pair,Nested} from \"./model.js\";"+
            "export function make(n:number,s:string):Pair {"+
            "let p:Pair=[n,s];p[0]=p[0]+1;return p;}"+
            "export function wrap(n:number):Nested {return [[n,\"ready\"],{valid:true}];}";
        host.Files["/virtual/bridge.ts"]="export {make,wrap} from \"./factory.js\";";
        host.Files["/virtual/entry.ts"]=
            "import {make,wrap} from \"./bridge.js\";"+
            "const pair=make(20,\"ok\");const nested=wrap(21);"+
            "export const result:number=pair[0]+nested[0][0];"+
            "export const label:string=pair[1];"+
            "export const flag:boolean=nested[1].valid;";
        var program=CompilerProgram.Load(host,["entry.ts"]);
        Assert.Equal(4,program.Sources.Count);
        Assert.Empty(program.Analyze().Diagnostics);
    }
}

using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ArrayRestBindingTests
{
    [Theory]
    [InlineData("const xs:number[]=[20,21,22];const [first,...tail]=xs;let n:number=first+tail[0];")]
    [InlineData("const xs:number[]=[20,21,22];const [,,...tail]=xs;let n:number=tail[0];")]
    [InlineData("const xs:readonly number[]=[20,22];let [...copy]=xs;copy[0]=42;")]
    [InlineData("const xs:number[][]=[[20],[22]];const [[first],...remaining]=xs;let n:number=first+remaining[0][0];")]
    [InlineData("function sum([head,...tail]:number[]):number{return head+tail[0];}let n:number=sum([20,22]);")]
    [InlineData("const f=([head,...tail]:number[]):number=>head+tail[0];let n:number=f([20,22]);")]
    [InlineData("const {values:[first,...tail]}={values:[20,22]};let n:number=first+tail[0];")]
    [InlineData("const xs:number[]=[21,22];const [...copy]=xs;let n:number=copy[1];")]
    [InlineData("const [head,...tail]:number[]=[21,22];let n:number=head+tail[0];")]
    [InlineData("const pair:[number,string]=[21,\"x\"];const [head,...tail]=pair;let label:string=tail[0];let length:1=tail.length;")]
    [InlineData("const pair:readonly [number,string]=[21,\"x\"];let [...copy]=pair;copy[0]=42;let label:string=copy[1];")]
    [InlineData("const pair:[number,string,boolean]=[21,\"x\",true];const [head,,...tail]=pair;let flag:boolean=tail[0];let length:1=tail.length;")]
    [InlineData("const pair:[number,string]=[21,\"x\"];const [head,label,...empty]=pair;let length:0=empty.length;")]
    [InlineData("function label([head,...tail]:[number,string]):string{return tail[0];}let text:string=label([21,\"x\"]);")]
    [InlineData("const label=([head,...tail]:[number,string]):string=>tail[0];let text:string=label([21,\"x\"]);")]
    [InlineData("const pair:[number,string]=[21,\"x\"];const [head,...tail]:[number,string]=pair;let text:string=tail[0];")]
    [InlineData("const pair:[number,string?]=[21];const [head,...tail]=pair;let size:0|1=tail.length;let item:string|undefined=tail[0];")]
    [InlineData("const pair:readonly [number,string?]=[21,\"ready\"];const [head,...tail]=pair;let size:0|1=tail.length;let item:string|undefined=tail[0];")]
    [InlineData("const pair:[number,string?]=[21];let [...copy]=pair;copy[0]=42;let size:1|2=copy.length;")]
    [InlineData("const pair:[number,string?]=[21];let [head,...tail]=pair;tail[0]=\"ready\";")]
    [InlineData("const triple:readonly [number,string?,boolean?]=[21,\"ready\",true];const [head,...tail]=triple;let size:0|1|2=tail.length;let item:string|undefined=tail[0];let flag:boolean|undefined=tail[1];")]
    [InlineData("const triple:readonly [number,string?,boolean?]=[21,\"ready\"];const [head,label,...tail]=triple;let size:0|1=tail.length;let flag:boolean|undefined=tail[0];")]
    [InlineData("function label([head,...tail]:readonly [number,string?]):string|undefined{return tail[0];}let text:string|undefined=label([21]);")]
    [InlineData("const label=([head,...tail]:[number,string?]):string|undefined=>tail[0];let text:string|undefined=label([21,\"ready\"]);")]
    [InlineData("function take(xs:number[]|string[]):number|string{const [head,...tail]=xs;return tail[0];}let s:number|string=take([1,2]);")]
    [InlineData("function take(xs:readonly number[]|readonly string[]):number|string{const [...copy]=xs;return copy[0];}let s:number|string=take([\"a\",\"b\"]);")]
    [InlineData("type Blend=number[]|string[];function copy(v:Blend):(number|string)[]{const [head,...tail]=v;return tail;}const out:(number|string)[]=copy([1,2]);")]
    [InlineData("function accept(x:readonly number[]|string[]):void{const [first,...rest]=x;let n:number|string=first;let second:number|string=rest[0];}")]
    [InlineData("function choose(x:number[]|readonly string[]):number|string{const [,,...tail]=x;return tail[0];}let value:number|string=choose([1,2,3]);")]
    [InlineData("function combine(x:number[]|string[]):number|string{let [...tail]=x;let first:number|string=tail[0];return first;}")]
    public void NativeArrayRestBindingsUseExistingCheckedTypePaths(string source) =>
        Assert.Empty(Compilation.Check("array-rest.ts",source));

    [Theory]
    [InlineData("const [...tail, last]=[20,22];","TSCS1001")]
    [InlineData("const [...tail,]=[20,22];","TSCS1001")]
    [InlineData("const [...tail=[1]]=[20,22];","TSCS1001")]
    [InlineData("const [...[tail]]=[20,22];","TSCS1001")]
    [InlineData("const [...tail]=\"hi\";","TSCS1001")]
    [InlineData("const [...tail]={0:21};","TSCS1001")]
    [InlineData("const [head,...tail]=[21,22];let wrong:string=tail[0];","TS2322")]
    [InlineData("const [head,...tail]=[21,22];tail=[1];","TS2588")]
    [InlineData("const pair:[number,string]=[21,\"x\"];const [head,...tail]=pair;let wrong:number=tail[0];","TS2322")]
    [InlineData("const pair:[number,string]=[21,\"x\"];const [head,...tail]=pair;tail[0]=42;","TS2322")]
    [InlineData("const pair:[number,string]=[21,\"x\"];const [head,...tail]=pair;let wrong:2=tail.length;","TS2322")]
    [InlineData("const pair:[number,string]=[21,\"x\"];const [head,...tail]=pair;tail[1];","TS2493")]
    [InlineData("const pair:[number,string?]=[21];const [head,...tail]=pair;let unsafe:string=tail[0];","TS2322")]
    [InlineData("const pair:[number,string?]=[21];const [head,...tail]=pair;tail[0]=42;","TS2322")]
    [InlineData("const pair:[number,string?]=[21];const [head,...tail]=pair;let wrong:2=tail.length;","TS2322")]
    [InlineData("const pair:[number,string?]=[21];const [head,...tail]=pair;tail[1];","TS2493")]
    [InlineData("const triple:readonly [number,string?,boolean?]=[21];const [head,...tail]=triple;let wrong:boolean=tail[1];","TS2322")]
    [InlineData("function bad(xs:number[]|string[]):void{const [head,...tail]=xs;let n:number=tail[0];}","TS2322")]
    [InlineData("function bad(xs:readonly number[]|readonly string[]):void{const [...copy]=xs;let s:string=copy[0];}","TS2322")]
    [InlineData("function bad(xs:number[]|string[]):void{const [head,...tail]=xs;let flag:boolean=head;}","TS2322")]
    [InlineData("function bad(xs:number[]|string[]):void{const [head,...tail]=xs;tail[0]=true;}","TS2322")]
    [InlineData("function bad(xs:number[]|string[]):void{const [, ...tail]=xs;let n:number=tail[0];}","TS2322")]
    [InlineData("function bad(xs:number[]|string[]):void{const [head,...tail]=xs;let n:undefined=tail[0];}","TS2322")]
    public void UnsafeOrInvalidArrayRestBindingsFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("array-rest-invalid.ts",source),
            diagnostic=>diagnostic.Code==code);

    [Fact]
    public void LinkedArrayRestBindingsEmitAndExecuteRealEs2015()
    {
        var root=Directory.CreateTempSubdirectory("tscs-array-rest-").FullName;
        try
        {
            var sources=new Dictionary<string,string>(StringComparer.Ordinal)
            {
                [Path.Combine(root,"values.ts")]="export const nums:number[]=[20,21,22];export const sparse:number[]=[7];sparse.length=2;export const pair:readonly [number,string,boolean]=[20,\"pair\",true];export const optEmpty:readonly [number,string?]=[13];export const optFull:readonly [number,string?]=[13,'ready'];export const optTriple:readonly [number,string?,boolean?]=[13,'ready',true];export const mixed:number[]|string[]=[20,22];export const words:readonly number[]|readonly string[]=['a','b'];",
                [Path.Combine(root,"sum.ts")]="export function sum(a:number,b:number):number{return a+b;}export function optionalCount(pair:readonly [number,string?]):0|1{const [head,...tail]=pair;return tail.length;}",
                [Path.Combine(root,"bridge.ts")]="export {sum,optionalCount} from \"./sum.js\";",
                [Path.Combine(root,"entry.ts")]=
                    "import {nums,sparse,pair,optEmpty,optFull,optTriple,mixed,words} from \"./values.js\";import {sum,optionalCount} from \"./bridge.js\";"+
                    "const [first,...tail]=nums;const [...holeTail]=sparse;"+
                    "export const result:number=sum(first,tail[1]);export const materialized:number[]=holeTail;"+
                    "const [tupleFirst,...tupleTail]=pair;export const tupleLabel:string=tupleTail[0];"+
                    "export const tupleFlag:boolean=tupleTail[1];export const tupleCount:2=tupleTail.length;"+
                    "const [emptyFirst,...emptyTail]=optEmpty;const [fullFirst,...fullTail]=optFull;const [tripleFirst,...tripleTail]=optTriple;"+
                    "export const emptyCount:0|1=optionalCount(optEmpty);export const fullValue:string|undefined=fullTail[0];"+
                    "export const fullCount:0|1=fullTail.length;export const tripleFlag:boolean|undefined=tripleTail[1];export const tripleSize:0|1|2=tripleTail.length;"+
                    "const [eitherHead,...eitherTail]=mixed;const [...wordsCopy]=words;"+
                    "export const unionTail:number|string=eitherTail[0];export const unionWord:number|string=wordsCopy[0];"
            };
            var program=CompilerProgram.Load(new SourceHost(sources),
                [Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var outputs=program.EmitJavaScript();
            Assert.Equal(4,outputs.Count);
            foreach(var output in outputs)
                File.WriteAllText(Path.ChangeExtension(output.SourceFile,".js"),output.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"{\"type\":\"module\"}");
            var info=new ProcessStartInfo("node")
            {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            // Dynamic import after overriding the source array's own slice
            // guards against accidentally lowering rest to nums.slice(...).
            info.ArgumentList.Add("import {nums,mixed} from './values.js';nums.slice=()=>{throw new Error('own slice invoked')};mixed.slice=()=>{throw new Error('union own slice invoked')};const {result,materialized,tupleLabel,tupleFlag,tupleCount,emptyCount,fullValue,fullCount,tripleFlag,tripleSize,unionTail,unionWord}=await import('./entry.js');console.log(result+':'+(0 in materialized)+':'+(1 in materialized)+':'+materialized.length+':'+tupleLabel+':'+tupleFlag+':'+tupleCount+':'+emptyCount+':'+fullValue+':'+fullCount+':'+tripleFlag+':'+tripleSize+':'+unionTail+':'+unionWord)");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Node ES2015 array-rest execution exceeded 10 seconds.");
            }
            var stdout=process.StandardOutput.ReadToEnd();
            var stderr=process.StandardError.ReadToEnd();
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",stderr);
            Assert.Equal("42:true:true:2:pair:true:2:0:ready:1:true:2:22:a",stdout.Trim());
        }
        finally { Directory.Delete(root,true); }
    }

    [Fact]
    public void InvalidImportedRestTypesPreventAnyModuleEmissions()
    {
        var root=Path.Combine(Path.GetTempPath(),"tscs-array-rest-rejected");
        var sources=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [Path.Combine(root,"values.ts")]="export const nums:string[]=[\"bad\"];",
            [Path.Combine(root,"entry.ts")]=
                "import {nums} from \"./values.js\";const [...rest]=nums;let wrong:number=rest[0];"
        };
        var program=CompilerProgram.Load(new SourceHost(sources),
            [Path.Combine(root,"entry.ts")],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }

    [Fact]
    public void InvalidImportedTupleRestTypesPreventAnyModuleEmissions()
    {
        var root=Path.Combine(Path.GetTempPath(),"tscs-tuple-rest-rejected");
        var sources=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [Path.Combine(root,"values.ts")]="export const pair:readonly [number,string]=[21,\"bad\"];",
            [Path.Combine(root,"entry.ts")]=
                "import {pair} from \"./values.js\";const [head,...tail]=pair;let wrong:number=tail[0];"
        };
        var program=CompilerProgram.Load(new SourceHost(sources),
            [Path.Combine(root,"entry.ts")],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }

    [Fact]
    public void InvalidImportedOptionalTupleTailPreventsAnyModuleEmissions()
    {
        var root=Path.Combine(Path.GetTempPath(),"tscs-optional-tuple-rest-rejected");
        var sources=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [Path.Combine(root,"values.ts")]="export const pair:readonly [number,string?]=[21];",
            [Path.Combine(root,"entry.ts")]=
                "import {pair} from \"./values.js\";const [head,...tail]=pair;let unsafe:string=tail[0];"
        };
        var program=CompilerProgram.Load(new SourceHost(sources),
            [Path.Combine(root,"entry.ts")],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }

    [Fact]
    public void InvalidImportedNativeArrayUnionRestPreventsAllModuleEmissions()
    {
        var root=Path.Combine(Path.GetTempPath(),"tscs-native-array-union-rest-rejected");
        var sources=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [Path.Combine(root,"values.ts")]="export const mixed:number[]|string[]=[21,22];",
            [Path.Combine(root,"entry.ts")]=
                "import {mixed} from \"./values.js\";const [first,...tail]=mixed;let wrong:boolean=tail[0];"
        };
        var program=CompilerProgram.Load(new SourceHost(sources),
            [Path.Combine(root,"entry.ts")],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }

    private sealed class SourceHost(IReadOnlyDictionary<string,string> sources):ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string canonicalFileName)=>sources[canonicalFileName];
    }
}

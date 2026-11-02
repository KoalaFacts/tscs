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
    [InlineData("const raw=21;type T=typeof raw;let same:T=21;")]
    [InlineData("type T=typeof later;const later=\"ready\";let same:T=\"ready\";")]
    [InlineData("let raw=21;type T=typeof raw;let next:T=42;")]
    [InlineData("const flag=true;type T=typeof flag;let same:T=true;")]
    [InlineData("const box={nested:{value:21},label:\"ok\"};type B=typeof box;let x:B={nested:{value:42},label:\"new\"};")]
    [InlineData("const box={nested:{value:21}};type V=typeof box.nested.value;let n:V=42;")]
    [InlineData("const box={value:21};type K=keyof typeof box;let key:K=\"value\";")]
    [InlineData("const nums=[1,2];type N=(typeof nums)[number];let n:N=42;")]
    public void FiniteInferredTypeQueriesCompile(string source) =>
        Assert.Empty(Compilation.Check("inferred-query.ts",source));

    [Theory]
    [InlineData("const seed=21;let copy=seed;copy=42;")]
    [InlineData("const seed=\"ready\";let copy=seed;copy=\"later\";")]
    [InlineData("const seed=true;let copy=seed;copy=false;")]
    [InlineData("const seed=21;const box={value:seed};box.value=42;")]
    [InlineData("const seed=21;const items=[seed];items[0]=42;")]
    [InlineData("const seed=21;const box={value:seed as 21};type V=typeof box.value;const copy:V=21;")]
    [InlineData("const seed=21;const items=[seed as 21];type N=(typeof items)[number];const n:N=21;")]
    [InlineData("const n=(21);type N=typeof n;const copy:N=n;")]
    [InlineData("const n=((21));type N=typeof n;const copy:N=n;")]
    [InlineData("const s=(\"ready\");type S=typeof s;const copy:S=s;")]
    [InlineData("const n=21;const m=n as 22;")]
    [InlineData("const s=\"ready\";const t=s as \"later\";")]
    public void LiteralInferenceReviewRegressionsCompile(string source) =>
        Assert.Empty(Compilation.Check("literal-inference-review.ts",source));

    [Theory]
    [InlineData("const n=21;const m=n as 22|23;")]
    [InlineData("const s=\"ready\";const t=s as \"later\"|\"next\";")]
    [InlineData("const box={nested:({value:21} satisfies {value:21})};type V=typeof box.nested.value;let same:V=21;")]
    public void LiteralInferenceSecondReviewRegressionsCompile(string source) =>
        Assert.Empty(Compilation.Check("literal-second-review.ts",source));

    [Theory]
    [InlineData("const seed=21 as 21;let copy=seed;copy=42;","TS2322")]
    [InlineData("const seed:21=21;const box={value:seed};box.value=42;","TS2322")]
    [InlineData("const seed:21=21;const items=[seed];items[0]=42;","TS2322")]
    [InlineData("const seed:21=21;const alias=seed;let copy=alias;copy=42;","TS2322")]
    [InlineData("const n=21;const m=n as \"a\"|\"b\";","TS2352")]
    [InlineData("const box={nested:({value:21} satisfies {value:21})};type V=typeof box.nested.value;let bad:V=22;","TS2322")]
    public void LiteralInferenceSecondReviewRegressionsRejectInvalid(string source,string code) =>
        Assert.Contains(Compilation.Check("literal-second-review-bad.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("const seed=21;const box={value:seed as 21};box.value=42;","TS2322")]
    [InlineData("const seed=21;const items=[seed as 21];items[0]=42;","TS2322")]
    [InlineData("const seed=21;const t=seed as \"other\";","TS2352")]
    [InlineData("const seed=\"ready\";const t=seed as 22;","TS2352")]
    [InlineData("const n=(21);type N=typeof n;let wrong:N=22;","TS2322")]
    public void LiteralInferenceReviewRegressionsRejectInvalid(string source,string code) =>
        Assert.Contains(Compilation.Check("literal-inference-review-bad.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("const n=21;type T=typeof n;let bad:T=22;", "TS2322")]
    [InlineData("const s=\"ready\";type T=typeof s;let bad:T=\"later\";", "TS2322")]
    [InlineData("const box={value:21};type V=typeof box.value;let bad:V=\"no\";", "TS2322")]
    [InlineData("const box={nested:{value:21}};type V=typeof box.nested.missing;", "TS2339")]
    public void InferredQueryErrorsAreNotSwallowed(string source,string code) =>
        Assert.Contains(Compilation.Check("inferred-query-bad.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("type T=typeof missing;", "TS2304")]
    [InlineData("const obj:{value:number}={value:21};type V=typeof obj.absent;", "TS2339")]
    [InlineData("const obj:{value:number}={value:21};type V=typeof obj.value;let bad:V=\"wrong\";", "TS2322")]
    [InlineData("function read():number{return 21;}let raw=read();type T=typeof raw;", "TSCS1001")]
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

    [Fact]
    public void InferredTypeQueriesPreserveOriginAcrossReexports()
    {
        var host=new Host();
        host.Files["/virtual/data.ts"]=
            "export const item={value:21,nested:{label:\"ok\"}};export const status=\"ready\";";
        host.Files["/virtual/bridge.ts"]=
            "export {item as payload,status} from \"./data.js\";";
        host.Files["/virtual/entry.ts"]=
            "import {payload,status} from \"./bridge.js\";"+
            "type P=typeof payload;type V=typeof payload.value;"+
            "type S=typeof status;type K=keyof typeof payload;"+
            "const copy:P={value:42,nested:{label:\"new\"}};"+
            "const amount:V=copy.value;const state:S=\"ready\";"+
            "const key:K=\"nested\";export const result:number=amount;";
        Assert.Empty(CompilerProgram.Load(host,["entry.ts"]).Analyze().Diagnostics);
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
        host.Files["/virtual/untyped.ts"]="function read():number{return 21;}export const untyped=read();";
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


    [Fact]
    public void NamedConstMembersCrossModulesAndReexports()
    {
        var host=new Host();
        host.Files["/virtual/model.ts"]="export const seed=21;export const inner={count:21};";
        host.Files["/virtual/factory.ts"]=
            "import {seed,inner} from \"./model.js\";export const boxed={seed,nested:{inner}} as const;";
        host.Files["/virtual/bridge.ts"]="export {boxed as state} from \"./factory.js\";";
        host.Files["/virtual/entry.ts"]=
            "import {state} from \"./bridge.js\";"+
            "type Snapshot=typeof state;let copy:Snapshot={seed:21,nested:{inner:{count:23}}};"+
            "let exact:21=state.seed;state.nested.inner.count=42;"+
            "export const result:number=state.nested.inner.count+exact;";
        Assert.Empty(CompilerProgram.Load(host,["entry.ts"]).Analyze().Diagnostics);
    }

    [Fact]
    public void ForwardFunctionValueInExportedConstLiteralKeepsHoistedSignature()
    {
        var host=new Host();
        host.Files["/virtual/model.ts"]=
            "export const box={read} as const;export function read():number{return 21;}";
        host.Files["/virtual/entry.ts"]=
            "import {box} from \"./model.js\";export const result:number=box.read();";
        Assert.Empty(CompilerProgram.Load(host,["entry.ts"]).Analyze().Diagnostics);
    }
}

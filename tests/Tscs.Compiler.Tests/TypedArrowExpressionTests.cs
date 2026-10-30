using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class TypedArrowExpressionTests
{
    [Theory]
    [InlineData("""const inc=(n:number)=>n+1;const result:number=inc(41);""")]
    [InlineData("""const twice=(n:number):number=>n+n;const result:number=twice(21);""")]
    [InlineData("""const answer=():number=>42;const result:number=answer();""")]
    [InlineData("""const seed:number=1;const inc=(n:number)=>n+seed;const result:number=inc(41);""")]
    [InlineData("""type Fn=(n:number)=>number;const cb:Fn=(n:number)=>n+1;const result:number=cb(41);""")]
    [InlineData("""function apply(cb:(n:number)=>number,n:number):number{return cb(n);}const result:number=apply((n:number)=>n+n,21);""")]
    [InlineData("""const make=(n:number)=>(m:number)=>n+m;const fn=make(20);const result:number=fn(22);""")]
    [InlineData("""type Fn=(n:number)=>number;const box:{cb:Fn}={cb:(n:number)=>n+n};const fn:Fn=box.cb;const result:number=fn(21);""")]
    [InlineData("""const make=(n:number)=>({value:n});const result:number=make(42).value;""")]
    [InlineData("""const inc=(n:number):number=>{return n+1;};const result:number=inc(41);""")]
    [InlineData("""const inc=(n:number):number=>{const one:number=1;return n+one;};const result:number=inc(41);""")]
    [InlineData("""const answer=():number=>{return 42;};const result:number=answer();""")]
    [InlineData("""const f=(n:number):number=>{if(n>0){return n;}else{return -n;}};const result:number=f(42);""")]
    [InlineData("""const noop=():void=>{const x:number=42;};noop();""")]
    [InlineData("""type Fn=(n:number)=>number;const cb:Fn=(n:number):number=>{return n+1;};const result:number=cb(41);""")]
    [InlineData("""const wrap=(n:number):{value:number}=>{return {value:n};};const result:number=wrap(42).value;""")]
    public void TypedExpressionArrowsFlowThroughRealChecker(string source)=>
        Assert.Empty(Compilation.Check("arrow.ts",source));

    [Theory]
    [InlineData("""const bad=(n:number):number=>"no";""","TS2322")]
    [InlineData("""const bad=(n:number):string=>n;""","TS2322")]
    [InlineData("""const inc=(n:number)=>n+1;const result:number=inc("bad");""","TS2345")]
    [InlineData("""const inc=(n:number)=>n+1;const result:number=inc();""","TS2554")]
    [InlineData("""const inc=(n:number)=>n+1;const result:number=inc(1,2);""","TS2554")]
    [InlineData("""const inc=(n:number)=>n+1;const result:string=inc(1);""","TS2322")]
    [InlineData("""const inc=(n:number)=>missing;""","TS2304")]
    [InlineData("""const inc=(n:number)=>n;let bad:(n:string)=>number=inc;""","TS2322")]
    [InlineData("""const noType=(n)=>n;""","TSCS1001")]
    [InlineData("""const bad=(n:number):number=>{return "no";};""","TS2322")]
    [InlineData("""const bad=(n:number):string=>{return n;};""","TS2322")]
    [InlineData("""const bad=(n:number):number=>{return missing;};""","TS2304")]
    [InlineData("""const missing=(n:number):number=>{if(n>0){return n;}};""","TSCS1001")]
    [InlineData("""const inferred=(n:number)=>{return n;};""","TSCS1001")]
    [InlineData("""const bare=(n:number):number=>{return;};""","TSCS1001")]
    public void InvalidOrUnimplementedArrowsAreRejected(string source,string code)=>
        Assert.Contains(Compilation.Check("arrow-invalid.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>
            Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine("/virtual",path));
        public string ReadFile(string path)=>
            Files.TryGetValue(path,out var text)?text:throw new FileNotFoundException(path);
    }

    [Fact]
    public void TypedArrowCapturesAndCallbacksCrossFourESModules()
    {
        var host=new Host();
        host.Files["/virtual/model.ts"]="export type Callback=(n:number)=>number;";
        host.Files["/virtual/logic.ts"]=
            "import type {Callback} from \"./model.js\";"+
            "const extra:number=1;export const inc:Callback=(n:number):number=>{return n+extra;};"+
            "export function apply(fn:Callback,n:number):number{return fn(n);}";
        host.Files["/virtual/bridge.ts"]=
            "export {inc,apply} from \"./logic.js\";";
        host.Files["/virtual/entry.ts"]=
            "import type {Callback} from \"./model.js\";"+
            "import {inc,apply} from \"./bridge.js\";"+
            "const twice:Callback=(n:number):number=>n+n;"+
            "export const result:number=apply(twice,21);"+
            "export const captured:number=inc(41);";
        var program=CompilerProgram.Load(host,["entry.ts"]);
        Assert.Equal(4,program.Sources.Count);
        Assert.Empty(program.Analyze().Diagnostics);
    }
}

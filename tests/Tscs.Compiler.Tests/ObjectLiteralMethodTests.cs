using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ObjectLiteralMethodTests
{
    [Theory]
    [InlineData("""const op={twice(n:number):number{return n+n;}};const result:number=op.twice(21);""")]
    [InlineData("""const op={twice(n:number){return n+n;}};const result:number=op.twice(21);""")]
    [InlineData("""interface Op{twice(n:number):number}const op:Op={twice(n:number):number{return n+n;}};const result:number=op.twice(21);""")]
    [InlineData("""const op={twice(n:number=21):number{return n+n;}};const result:number=op.twice();""")]
    [InlineData("""const op={add(base:number,...items:number[]):number{let total:number=base;for(const item of items){total+=item;}return total;}};const result:number=op.add(20,21,1);""")]
    [InlineData("""const box={nested:{twice(n:number){return n+n;}}};const result:number=box.nested.twice(21);""")]
    [InlineData("""const op={"twice"(n:number){return n+n;}};const result:number=op.twice(21);""")]
    [InlineData("""const offset:number=1;const op={plus(n:number){return n+offset;}};const result:number=op.plus(41);""")]
    [InlineData("""const op={twice(n:number){return n+n;}};const cb:(n:number)=>number=op.twice;const result:number=cb(21);""")]
    [InlineData("""const op={twice(n:number):number{return n+n;},name:"test"};const result:number=op.twice(21);""")]
    [InlineData("""const op={run(n:number):void{const value:number=n;}};op.run(42);""")]
    public void TypedObjectMethodsCompileThroughTheRealChecker(string source) =>
        Assert.Empty(Compilation.Check("object-method.ts",source));

    [Theory]
    [InlineData("""const op={run(n:number):number{return "bad";}};""","TS2322")]
    [InlineData("""const op={run(n:number):number{return n;}};op.run("bad");""","TS2345")]
    [InlineData("""const op={run(n:number):number{return n;}};op.run();""","TS2554")]
    [InlineData("""const op={run(n:number):number{return missingName;}};""","TS2304")]
    [InlineData("""const op={run(n:number):number{if(n>0){return n;}}};""","TSCS1001")]
    [InlineData("""const op={run(n){return n;}};""","TSCS1001")]
    public void InvalidObjectMethodsAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("object-method-invalid.ts",source),d=>d.Code==code);

    private sealed class Host:ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files {get;}=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void TypedObjectMethodContractsFlowAcrossFourModules()
    {
        const string root="/virtual/object-method";
        var host=new Host();
        host.Files[root+"/model.ts"]="export interface Op{twice(n:number):number}";
        host.Files[root+"/logic.ts"]=String.Join("\n",[
            """import type {Op} from "./model.js";""",
            "export const op:Op={twice(n:number):number{return n+n;}};"
        ]);
        host.Files[root+"/bridge.ts"]="""export {op} from "./logic.js";""";
        host.Files[root+"/entry.ts"]=
            """import {op} from "./bridge.js";export const result:number=op.twice(21);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],new CompilerOptions {
            NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        Assert.Equal(4,program.EmitJavaScript().Count);
    }
}

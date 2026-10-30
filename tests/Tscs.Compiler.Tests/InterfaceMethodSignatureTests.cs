using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class InterfaceMethodSignatureTests
{
    [Theory]
    [InlineData("""interface Op{twice(n:number):number}const op:Op={twice:(n:number)=>n+n};const cb:(n:number)=>number=op.twice;let result:number=cb(21);""")]
    [InlineData("""type Op={sum(a:number,b:number):number};function add(a:number,b:number):number{return a+b;}const op:Op={sum:add};const cb:(a:number,b:number)=>number=op.sum;let result:number=cb(20,22);""")]
    [InlineData("""interface Op{twice(n:number):number;label:string}const op:Op={twice:(n:number)=>n+n,label:"ok"};const cb:(n:number)=>number=op.twice;let result:number=cb(21);""")]
    [InlineData("""interface Base{run(n:number):number}interface Child extends Base{label:string}const child:Child={run:(n:number)=>n+n,label:"ok"};const cb:(n:number)=>number=child.run;let result:number=cb(21);""")]
    [InlineData("""interface Step{step(n:number,extra?:number):number}const op:Step={step:(n:number,extra?:number):number=>n};const fn:(n:number,extra?:number)=>number=op.step;let result:number=fn(42);""")]
    [InlineData("""interface Step{run(base:number,...items:number[]):number}const op:Step={run:(base:number,...items:number[]):number=>base};const cb:(base:number,...items:number[])=>number=op.run;let result:number=cb(42,1,2);""")]
    [InlineData("""interface Op{readonly(n:number):number}const op:Op={readonly:(n:number)=>n+n};const cb:(n:number)=>number=op.readonly;let result:number=cb(21);""")]
    [InlineData("""interface Op{"double"(n:number):number}const op:Op={double:(n:number)=>n+n};const cb:(n:number)=>number=op.double;let result:number=cb(21);""")]
    public void MethodMembersBindAsCheckedFunctionContracts(string source)=>
        Assert.Empty(Compilation.Check("method-types.ts",source));

    [Theory]
    [InlineData("""interface Op{twice(n:number):number}const op:Op={twice:(n:number):string=>"bad"};""","TS2322")]
    [InlineData("""interface Op{twice(n:number):number}const op:Op={twice:(n:number)=>n+n};const cb:(n:number)=>number=op.twice;cb("bad");""","TS2345")]
    [InlineData("""interface Op{twice(n:number):number}const op:Op={twice:(n:number)=>n+n};const cb:(n:number)=>number=op.twice;cb();""","TS2554")]
    [InlineData("""interface Op{twice(n:number):number;twice(n:number):number}""","TS2300")]
    [InlineData("""interface Op{twice(n?:number,required:number):number}""","TSCS1001")]
    [InlineData("""interface Op{twice(...numbers:number):number}""","TSCS1001")]
    [InlineData("""interface Op{readonly twice(n:number):number}""","TSCS1001")]
    [InlineData("""interface Op{twice(n:number)}""","TSCS1001")]
    public void InvalidMethodShapesFailClosed(string source,string expected)=>
        Assert.Contains(Compilation.Check("invalid-method-types.ts",source),d=>d.Code==expected);

    private sealed class Host:ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void ImportedMethodContractsCrossFourModules()
    {
        const string root="/virtual/method-types";
        var host=new Host();
        host.Files[root+"/model.ts"]="export interface Op{twice(n:number):number}";
        host.Files[root+"/logic.ts"]=
            "import type {Op} from \"./model.js\";"+
            "export function twice(n:number):number{return n+n;}"+
            "export const op:Op={twice:twice};";
        host.Files[root+"/bridge.ts"]="export {op} from \"./logic.js\";";
        host.Files[root+"/entry.ts"]=
            "import type {Op} from \"./model.js\";"+
            "import {op} from \"./bridge.js\";"+
            "const selected:Op=op;"+
            "const cb:(n:number)=>number=selected.twice;"+
            "export const result:number=cb(21);";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],new CompilerOptions {
            NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        Assert.Equal(4,program.EmitJavaScript().Count);
    }
}

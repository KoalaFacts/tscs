using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class MemberCallExpressionTests
{
    [Theory]
    [InlineData("""interface Op{twice(n:number):number}const op:Op={twice:(n:number)=>n+n};const result:number=op.twice(21);""")]
    [InlineData("""const box={twice:(n:number)=>n+n};const result:number=box.twice(21);""")]
    [InlineData("""const box={twice:(n:number)=>n+n};const result:number=box["twice"](21);""")]
    [InlineData("""const box={inner:{twice:(n:number)=>n+n}};const result:number=box.inner.twice(21);""")]
    [InlineData("""const box={twice:function(n:number){return n+n;}};const result:number=box.twice(21);""")]
    [InlineData("""const box={run:(n:number=21)=>n+n};const result:number=box.run();""")]
    [InlineData("""const box={run:(base:number,...xs:number[]):number=>base};const result:number=box.run(42,1,2);""")]
    [InlineData("""const result:number=(function(n:number){return n+n;})(21);""")]
    [InlineData("""const box={twice:(n:number)=>n+n};function invoke(cb:(n:number)=>number,n:number):number{return cb(n);}const result:number=invoke(box.twice,21);""")]
    [InlineData("""function make():{run:(n:number)=>number}{return {run:(n:number)=>n+n};}const result:number=make().run(21);""")]
    public void CheckedCallableExpressionsAcceptRealSource(string source) =>
        Assert.Empty(Compilation.Check("member-invoke.ts",source));

    [Theory]
    [InlineData("""const box={run:(n:number)=>n+n};box.run("bad");""","TS2345")]
    [InlineData("""const box={run:(n:number)=>n+n};box.run();""","TS2554")]
    [InlineData("""const box={run:(n:number)=>n+n};box.run(21,22);""","TS2554")]
    [InlineData("""const box={run:(n:number)=>n+n};box.missing(21);""","TS2339")]
    [InlineData("""const box={value:42};box.value(21);""","TSCS1001")]
    [InlineData("""const box={run:(n:number)=>n+n};box["missing"](21);""","TS2339")]
    [InlineData("""const box={run:(n:number)=>n+n};const key:string="run";box[key](21);""","TSCS1001")]
    public void InvalidMemberInvocationsAreDiagnosed(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-member-invoke.ts",source),d=>d.Code==code);

    private sealed class Host:ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files {get;}=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void CheckedDirectMethodCallsCrossFourModules()
    {
        const string root="/virtual/member-invoke";
        var host=new Host();
        host.Files[root+"/model.ts"]="export interface Op{run(n:number):number}";
        host.Files[root+"/logic.ts"]=
            """import type {Op} from "./model.js";export const op:Op={run:(n:number)=>n+n};""";
        host.Files[root+"/bridge.ts"]=
            """export {op} from "./logic.js";""";
        host.Files[root+"/entry.ts"]=
            """import {op} from "./bridge.js";export const result:number=op.run(21);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],new CompilerOptions {
            NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        Assert.Equal(4,program.EmitJavaScript().Count);
    }
}

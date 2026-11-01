using Tscs.Compiler;
using Xunit;

namespace Tscs.Tests;

// Executable bounded source regression, not original TS7 harness acceptance.
public sealed class ArrayAccumulatorTests
{
    [Theory]
    [InlineData("let xs:number[]=[2,3,4];let result:string=xs.reduce((a:string,n:number):string=>a+n,\"\");")]
    [InlineData("let xs:readonly number[]=[2,3,4];let result:string=xs.reduceRight((a:string,n:number):string=>a+n,\"\");")]
    [InlineData("let xs:ReadonlyArray<number>=[10,20];let result:string=xs.reduce((a:string,n:number):string=>a+n,\"#\");")]
    [InlineData("let xs:number[]=[2,3];let f=(a:string,n:number):string=>a+n;let result:string=xs.reduce(f,\"\");")]
    [InlineData("let xs:number[]=[2,3,4];let result:{total:number}=xs.reduce((a:{total:number},n:number):{total:number}=>({total:a.total+n}),{total:0});")]
    [InlineData("let xs:string[]=[\"a\",\"b\"];let result:number=xs.reduce((a:number,s:string):number=>a+(s===\"a\"?1:2),0);")]
    [InlineData("function append(a:string,n:number):string{return a+n;}let xs:readonly number[]=[2,3];let result:string=xs.reduce(append,\"\");")]
    [InlineData("type Items=ReadonlyArray<number>;function label(xs:Items):string{return xs.reduceRight((a:string,n:number):string=>a+n,\"start\");}let result:string=label([1,2]);")]
    public void CompilesDifferentAccumulatorTypes(string source) =>
        Assert.Empty(Compilation.Check("array-accumulator.ts",source));

    [Theory]
    [InlineData("let xs:number[]=[1];xs.reduce((a:string,n:number):string=>a+n,0);","TS2345")]
    [InlineData("let xs:number[]=[1];xs.reduce((a:string,n:string):string=>a+n,\"\");","TS2345")]
    [InlineData("let xs:number[]=[1];xs.reduce((a:string,n:number):number=>n,\"\");","TS2345")]
    [InlineData("let xs:number[]=[1];let wrong:number=xs.reduce((a:string,n:number):string=>a+n,\"\");","TS2322")]
    [InlineData("let xs:readonly number[]=[1];xs.reduceRight((a:string,n:number):string=>a+n,21);","TS2345")]
    [InlineData("let xs:number[]=[1];xs.reduce(21,\"\");","TS2345")]
    [InlineData("let xs:number[]=[1];xs.reduce((a:string,n:number):string=>a+n,\"\",0);","TS2554")]
    [InlineData("let xs:ReadonlyArray<number>=[1];xs.reduceRight((a:string,n:boolean):string=>a+\"x\",\"\");","TS2345")]
    public void RejectsInvalidAccumulators(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-accumulator.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void EmitsDifferentAccumulatorTypesAcrossFourModules()
    {
        const string root="/virtual/array-accumulator";
        var host=new Host();
        host.Files[root+"/model.ts"]="export type Values=ReadonlyArray<number>;export const values:number[]=[2,3,4];";
        host.Files[root+"/logic.ts"]="import type {Values} from \"./model.js\";export function join(xs:Values):string{return xs.reduce((a:string,n:number):string=>a+n,\"\");}export function backwards(xs:Values):string{return xs.reduceRight((a:string,n:number):string=>a+n,\"\");}export function total(xs:Values):{value:number}{return xs.reduce((a:{value:number},n:number):{value:number}=>({value:a.value+n}),{value:0});}";
        host.Files[root+"/bridge.ts"]="export {values} from \"./model.js\";export {join,backwards,total} from \"./logic.js\";";
        host.Files[root+"/entry.ts"]="import {values,join,backwards,total} from \"./bridge.js\";export const label:string=join(values);export const reverseLabel:string=backwards(values);export const summary:{value:number}=total(values);";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],new CompilerOptions {
            NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        var output=program.EmitJavaScript();
        Assert.Equal(4,output.Count);
        Assert.Contains(output,js=>js.Text.Contains(".reduce(",StringComparison.Ordinal));
        Assert.Contains(output,js=>js.Text.Contains(".reduceRight(",StringComparison.Ordinal));
    }
}

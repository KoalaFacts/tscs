using Tscs.Compiler;
using Xunit;

namespace Tscs.Tests;

// Executable source-backed regression checks, not original TS7 acceptance.
public sealed class ArrayFilterReduceTests
{
    [Theory]
    [InlineData("let xs:number[]=[19,20,21];let result:number[]=xs.filter((n:number):boolean=>n>19);")]
    [InlineData("let xs:readonly number[]=[1,2,3];let selected:number[]=xs.filter((n:number):boolean=>n>1);selected[0]=7;")]
    [InlineData("let xs:ReadonlyArray<string>=[\"a\",\"b\"];let selected:string[]=xs.filter((s:string):boolean=>s===\"b\");")]
    [InlineData("let xs:number[]=[1,2,3];let total:number=xs.reduce((acc:number,n:number):number=>acc+n,0);")]
    [InlineData("let xs:number[]=[1,2,3];let total:number=xs.reduce((acc:number,n:number):number=>acc+n);")]
    [InlineData("let xs:number[]=[1,2,3];let folded:number=xs.reduceRight((acc:number,n:number):number=>acc*10+n,0);")]
    [InlineData("let xs:readonly number[]=[2,3,4];let total:number=xs.reduce((acc:number,n:number):number=>acc+n,0);")]
    [InlineData("let xs:ReadonlyArray<number>=[2,3,4];let folded:number=xs.reduceRight((acc:number,n:number):number=>acc+n,0);")]
    [InlineData("let xs:string[]=[\"a\",\"b\"];let folded:string=xs.reduce((acc:string,s:string):string=>acc+s,\"\");")]
    [InlineData("type Readings=readonly number[];function even(xs:Readings):number[]{return xs.filter((n:number):boolean=>n%2===0);}let values:number[]=even([1,2,3,4]);")]
    public void CompilesFilterAndFolds(string source) =>
        Assert.Empty(Compilation.Check("array-filter-reduce.ts",source));

    [Theory]
    [InlineData("let xs:number[]=[21];xs.filter();","TS2554")]
    [InlineData("let xs:number[]=[21];xs.filter(42);","TS2345")]
    [InlineData("let xs:readonly number[]=[21];xs.filter((n:string):boolean=>true);","TS2345")]
    [InlineData("let xs:number[]=[21];xs.filter((n:number):string=>\"bad\");","TS2345")]
    [InlineData("let xs:number[]=[21];let value:boolean=xs.filter((n:number):boolean=>true);","TS2322")]
    [InlineData("let xs:number[]=[21];xs.reduce((a:number,n:number):string=>\"bad\",0);","TS2345")]
    [InlineData("let xs:number[]=[21];xs.reduce((a:number,n:number):number=>a+n,\"bad\");","TS2345")]
    [InlineData("let xs:readonly number[]=[21];xs.reduceRight((a:string,n:string):string=>a+n,\"\");","TS2345")]
    [InlineData("let xs:number[]=[21];let value:string=xs.reduceRight((a:number,n:number):number=>a+n,0);","TS2322")]
    [InlineData("let xs:number[]=[21];xs.filter((n:number):boolean=>true,42);","TS2554")]
    public void RejectsInvalidFilterAndFolds(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-filter-fold.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void CompilesCheckedFilterAndFoldsAcrossFourModules()
    {
        const string root="/virtual/array-filter-reduce";
        var host=new Host();
        host.Files[root+"/model.ts"]="export type Values=ReadonlyArray<number>;export const values:number[]=[2,3,4,5];";
        host.Files[root+"/logic.ts"]="import type {Values} from \"./model.js\";export function evens(xs:Values):number[]{return xs.filter((n:number):boolean=>n%2===0);}export function sum(xs:Values):number{return xs.reduce((acc:number,n:number):number=>acc+n,0);}export function backwards(xs:Values):number{return xs.reduceRight((acc:number,n:number):number=>acc*10+n,0);}";
        host.Files[root+"/bridge.ts"]="export {values} from \"./model.js\";export {evens,sum,backwards} from \"./logic.js\";";
        host.Files[root+"/entry.ts"]="import {values,evens,sum,backwards} from \"./bridge.js\";export const selected:number[]=evens(values);export const total:number=sum(values);export const backwardsValue:number=backwards(values);selected[0]=100;";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],new CompilerOptions {
            NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        var output=program.EmitJavaScript();
        Assert.Equal(4,output.Count);
        foreach(var method in new[]{".filter(",".reduce(",".reduceRight("})
            Assert.Contains(output,js=>js.Text.Contains(method,StringComparison.Ordinal));
    }
}

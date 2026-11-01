using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed product checks, not original TS7 upstream acceptance.
public sealed class ArrayQueryTests
{
    [Theory]
    [InlineData("let xs:number[]=[20,21];let found:boolean=xs.includes(21);")]
    [InlineData("let xs:readonly number[]=[20,21];let found:boolean=xs.includes(21,1);")]
    [InlineData("let xs:ReadonlyArray<number>=[20,21];let p:number=xs.indexOf(20);")]
    [InlineData("let xs:number[]=[20,21,20];let p:number=xs.lastIndexOf(20,1);")]
    [InlineData("let xs:readonly string[]=[\"a\",\"b\"];let p:number=xs.indexOf(\"a\");")]
    [InlineData("let xs:number[]=[20,21];let s:string=xs.join(\"/\");")]
    [InlineData("let xs:ReadonlyArray<number>=[20,21];let s:string=xs.join();")]
    [InlineData("let xs:number[]=[20,21];let s:string=xs.join(undefined);")]
    [InlineData("let xs:readonly number[]=[20,21];let b:boolean=xs.includes(21,undefined);")]
    [InlineData("function find(xs:readonly number[],n:number):number{return xs.indexOf(n);}let p:number=find([20,21],21);")]
    [InlineData("let xs:number[]=[20,21];let copy:number[]=xs.slice();")]
    [InlineData("let xs:readonly number[]=[20,21];let copy:number[]=xs.slice(1);")]
    [InlineData("let xs:ReadonlyArray<number>=[20,21];let copy:number[]=xs.slice(0,1);")]
    [InlineData("let xs:Array<number>=[20,21];let copy:number[]=xs.slice(undefined,1);")]
    [InlineData("let xs:readonly string[]=[\"a\",\"b\"];let copy:string[]=xs.slice(0,undefined);")]
    [InlineData("let xs:readonly number[]=[20,21];let copy:number[]=xs.slice();copy[0]=42;")]
    [InlineData("function copy(xs:readonly number[]):number[]{return xs.slice();}let out:number[]=copy([20,21]);")]
    [InlineData("let xs:number[]=[1,2];let result:number[]=xs.concat();")]
    [InlineData("let xs:readonly number[]=[1,2];let result:number[]=xs.concat(3);result[0]=9;")]
    [InlineData("let xs:ReadonlyArray<number>=[1,2];let result:number[]=xs.concat([3,4]);")]
    [InlineData("let xs:readonly number[]=[1,2];let extra:readonly number[]=[3,4];let result:number[]=xs.concat(extra,5);")]
    [InlineData("let xs:string[]=[\"a\"];let result:string[]=xs.concat(\"b\",[\"c\"]);")]
    [InlineData("let xs:number[]=[1];let next:number[]=[2,3];let result:number[]=xs.concat(...next);")]
    [InlineData("function extend(xs:readonly number[],rest:readonly number[]):number[]{return xs.concat(rest,7);}let result:number[]=extend([1],[2,3]);")]
    [InlineData("type Samples=ReadonlyArray<number>;let xs:Samples=[1,2];let result:number[]=xs.concat([3]);")]
    [InlineData("let xs:number[]=[1,2];let result:number[]=xs.concat(3).slice(1);")]
    [InlineData("let xs:number[]=[10,20];let n:number|undefined=xs.at(0);")]
    [InlineData("let xs:readonly number[]=[10,20];let n:number|undefined=xs.at(-1);")]
    [InlineData("let xs:ReadonlyArray<string>=[\"a\",\"b\"];let n:string|undefined=xs.at(-2);")]
    [InlineData("let xs:number[]=[];let n:number|undefined=xs.at(0);")]
    [InlineData("type Items=readonly number[];let xs:Items=[2,4];let n:number|undefined=xs.at(-3);")]
    [InlineData("function last(xs:readonly number[]):number|undefined{return xs.at(-1);}let n:number|undefined=last([9]);")]
    [InlineData("let xs:readonly number[]=[2,4];let n:number|undefined=xs.slice(0,1).at(0);")]
    [InlineData("let xs:ReadonlyArray<number>=[1,2,3];let i:number=1;let n:number|undefined=xs.at(i-3);")]
    [InlineData("let xs:number[]=[3,1,2];let out:number[]=xs.toReversed();")]
    [InlineData("let xs:readonly number[]=[3,1,2];let out:number[]=xs.toReversed();out[0]=9;")]
    [InlineData("let xs:ReadonlyArray<string>=[\"a\",\"b\"];let out:string[]=xs.toReversed();")]
    [InlineData("let xs:readonly number[]=[3,1,2];let out:number[]=xs.toSorted((a:number,b:number):number=>a-b);")]
    [InlineData("let xs:number[]=[3,1,2];let out:number[]=xs.toSorted();")]
    [InlineData("let xs:number[]=[3,1,2];let out:number[]=xs.toSorted(undefined);")]
    [InlineData("function reverseCopy(xs:readonly number[]):number[]{return xs.toReversed();}let out:number[]=reverseCopy([1,2]);")]
    [InlineData("let xs:readonly string[]=[\"b\",\"a\"];let out:string[]=xs.toSorted();")]
    [InlineData("let xs:number[]=[1,2,3];let out:number[]=xs.with(1,9);")]
    [InlineData("let xs:readonly number[]=[1,2,3];let out:number[]=xs.with(-1,7);out[0]=9;")]
    [InlineData("let xs:ReadonlyArray<string>=[\"a\",\"b\"];let out:string[]=xs.with(0,\"z\");")]
    [InlineData("function replace(xs:readonly number[]):number[]{return xs.with(0,5);}let out:number[]=replace([1,2]);")]
    [InlineData("let xs:number[]=[1,2,3];let out:number[]=xs.toSpliced(1,1,9);")]
    [InlineData("let xs:readonly number[]=[1,2,3];let out:number[]=xs.toSpliced(1);")]
    [InlineData("let xs:ReadonlyArray<string>=[\"a\",\"b\"];let out:string[]=xs.toSpliced(0,0,\"x\",\"y\");")]
    [InlineData("function insert(xs:readonly number[]):number[]{return xs.toSpliced(1,0,4,5);}let out:number[]=insert([1,2]);")]
    public void ReadonlyAndMutableArraysCanPerformCheckedQueries(string source) =>
        Assert.Empty(Compilation.Check("array-queries.ts",source));

    [Theory]
    [InlineData("let xs:number[]=[21];xs.includes(\"bad\");","TS2345")]
    [InlineData("let xs:number[]=[21];xs.includes();","TS2554")]
    [InlineData("let xs:number[]=[21];xs.includes(21,\"bad\");","TS2345")]
    [InlineData("let xs:number[]=[21];xs.indexOf();","TS2554")]
    [InlineData("let xs:number[]=[21];xs.lastIndexOf(\"bad\");","TS2345")]
    [InlineData("let xs:readonly number[]=[21];xs.join(42);","TS2345")]
    [InlineData("let xs:number[]=[21];xs.join(\",\",\",\");","TS2554")]
    [InlineData("let xs:number[]=[21];let b:boolean=xs.indexOf(21);","TS2322")]
    [InlineData("let xs:readonly number[]=[21];xs.push(22);","TS2339")]
    [InlineData("let xs:number[]=[21];xs.slice(\"bad\");","TS2345")]
    [InlineData("let xs:readonly number[]=[21];xs.slice(0,\"bad\");","TS2345")]
    [InlineData("let xs:number[]=[21];xs.slice(0,1,2);","TS2554")]
    [InlineData("let xs:ReadonlyArray<number>=[21];let wrong:number=xs.slice(0);","TS2322")]
    [InlineData("let xs:readonly string[]=[\"a\"];let wrong:number[]=xs.slice();","TS2322")]
    [InlineData("let xs:number[]=[21];xs.slice(true);","TS2345")]
    [InlineData("let xs:number[]=[1];xs.concat(\"bad\");","TS2345")]
    [InlineData("let xs:readonly number[]=[1];let wrong:string[]=[\"bad\"];xs.concat(wrong);","TS2345")]
    [InlineData("let xs:number[]=[1];let wrong:string[]=xs.concat(2);","TS2322")]
    [InlineData("let xs:readonly string[]=[\"a\"];xs.concat(42);","TS2345")]
    [InlineData("let xs:number[]=[1];let extra:string[]=[\"bad\"];xs.concat(...extra);","TS2345")]
    [InlineData("let xs:ReadonlyArray<number>=[1];let wrong:readonly string[]=xs.concat([2]);","TS2322")]
    [InlineData("let xs:number[]=[1];xs.concat(true);","TS2345")]
    [InlineData("let xs:number[]=[1];xs.at();","TS2554")]
    [InlineData("let xs:number[]=[1];xs.at(\"0\");","TS2345")]
    [InlineData("let xs:readonly number[]=[1];xs.at(true);","TS2345")]
    [InlineData("let xs:readonly number[]=[1];xs.at(0,1);","TS2554")]
    [InlineData("let xs:number[]=[1];let wrong:string=xs.at(0);","TS2322")]
    [InlineData("let xs:ReadonlyArray<number>=[1];let wrong:boolean=xs.at(-1);","TS2322")]
    [InlineData("let xs:number[]=[1];xs.at((n:number):number=>n);","TS2345")]
    [InlineData("let xs:readonly string[]=[\"a\"];let wrong:number|undefined=xs.at(0);","TS2322")]
    [InlineData("let xs:number[]=[1];xs.toReversed(0);","TS2554")]
    [InlineData("let xs:ReadonlyArray<number>=[1];xs.toSorted(1);","TS2345")]
    [InlineData("let xs:number[]=[1];xs.toSorted((a:number,b:number):boolean=>true);","TS2345")]
    [InlineData("let xs:readonly string[]=[\"a\"];let out:number[]=xs.toReversed();","TS2322")]
    [InlineData("let xs:number[]=[1];let out:string[]=xs.toSorted();","TS2322")]
    [InlineData("let xs:ReadonlyArray<number>=[1];xs.toSorted((a:string,b:string):number=>0);","TS2345")]
    [InlineData("let xs:number[]=[1];xs.toSorted(undefined,undefined);","TS2554")]
    [InlineData("let xs:number[]=[1];xs.toReversed(\"bad\");","TS2554")]
    [InlineData("let xs:number[]=[1];xs.with();","TS2554")]
    [InlineData("let xs:number[]=[1];xs.with(0);","TS2554")]
    [InlineData("let xs:readonly number[]=[1];xs.with(\"0\",2);","TS2345")]
    [InlineData("let xs:number[]=[1];xs.with(0,\"x\");","TS2345")]
    [InlineData("let xs:readonly string[]=[\"a\"];let out:number[]=xs.with(0,\"b\");","TS2322")]
    [InlineData("let xs:number[]=[1];xs.toSpliced();","TS2554")]
    [InlineData("let xs:number[]=[1,2];let out:string[]=xs.toSpliced(0,1);","TS2322")]
    [InlineData("let xs:readonly string[]=[\"a\"];let out:number[]=xs.toSpliced(0,0,\"b\");","TS2322")]
    [InlineData("let xs:number[]=[1,2];xs.toSpliced(0,1,\"bad\");","TS2345")]
    [InlineData("let xs:number[]=[1,2];xs.toSpliced(0,undefined,3);","TS2345")]
    public void InvalidQueriesAndReadonlyMutationDoNotSucceed(string source,string expectedCode) =>
        Assert.Contains(Compilation.Check("array-queries-invalid.ts",source),d=>d.Code==expectedCode);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; } = new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path) => Path.GetFullPath(path);
        public string ReadFile(string path) => Files[path];
    }

    [Fact]
    public void QueryMethodsCrossModuleLinkingAndOutput()
    {
        const string root="/virtual/array-queries";
        var host=new Host();
        host.Files[root+"/model.ts"]="export type Readings=ReadonlyArray<number>;export const values:number[]=[19,20,21,19];";
        host.Files[root+"/logic.ts"]="import type {Readings} from \"./model.js\";export function contains(xs:Readings,n:number):boolean{return xs.includes(n);}export function first(xs:Readings,n:number):number{return xs.indexOf(n);}export function last(xs:Readings,n:number):number{return xs.lastIndexOf(n);}export function label(xs:Readings):string{return xs.join(\"/\");}export function middle(xs:Readings):number[]{return xs.slice(1,-1);}export function copy(xs:Readings):number[]{return xs.slice();}export function append(xs:Readings,rest:Readings):number[]{return xs.concat(rest,22);}export function atEnd(xs:Readings):number|undefined{return xs.at(-1);}";
        host.Files[root+"/bridge.ts"]="export {values} from \"./model.js\";export {contains,first,last,label,middle,copy,append,atEnd} from \"./logic.js\";";
        host.Files[root+"/entry.ts"]="import {values,contains,first,last,label,middle,copy,append,atEnd} from \"./bridge.js\";export const present:boolean=contains(values,21);export const firstIndex:number=first(values,19);export const lastIndex:number=last(values,19);export const joined:string=label(values);export const sliced:number[]=middle(values);export const writable:number[]=copy(values);writable[0]=99;export const appended:number[]=append(values,[23,24]);appended[0]=42;export const end:number|undefined=atEnd(values);";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],new CompilerOptions {
            NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        var output=program.EmitJavaScript();
        Assert.Equal(4,output.Count);
        Assert.Contains(output,js=>js.Text.Contains(".includes(",StringComparison.Ordinal));
        Assert.Contains(output,js=>js.Text.Contains(".indexOf(",StringComparison.Ordinal));
        Assert.Contains(output,js=>js.Text.Contains(".lastIndexOf(",StringComparison.Ordinal));
        Assert.Contains(output,js=>js.Text.Contains(".join(",StringComparison.Ordinal));
        Assert.Contains(output,js=>js.Text.Contains(".slice(",StringComparison.Ordinal));
        Assert.Contains(output,js=>js.Text.Contains(".at(",StringComparison.Ordinal));
        Assert.Contains(output,js=>js.Text.Contains(".concat(",StringComparison.Ordinal));
    }
}

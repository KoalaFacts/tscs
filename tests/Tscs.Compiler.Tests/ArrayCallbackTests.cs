using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed product tests; never count as original TS7 acceptance.
public sealed class ArrayCallbackTests
{
    [Theory]
    [InlineData("let xs:number[]=[19,21];let result:boolean=xs.some((n:number):boolean=>n>20);")]
    [InlineData("let xs:readonly number[]=[19,21];let result:boolean=xs.every((n:number):boolean=>n>0);")]
    [InlineData("let xs:ReadonlyArray<number>=[19,21];let result:number|undefined=xs.find((n:number):boolean=>n>19);")]
    [InlineData("let xs:Array<number>=[19,21];let result:number=xs.findIndex((n:number):boolean=>n===21);")]
    [InlineData("let xs:readonly number[]=[19,21];let sum:number=0;xs.forEach((n:number):void=>{sum=sum+n;});")]
    [InlineData("let xs:string[]=[\"a\",\"b\"];let hit:string|undefined=xs.find((s:string):boolean=>s===\"b\");")]
    [InlineData("type Readings=readonly number[];let xs:Readings=[1,2];let result:boolean=xs.some((n:number)=>n>1);")]
    [InlineData("function count(xs:ReadonlyArray<number>):number{let sum:number=0;xs.forEach((n:number):void=>{sum=sum+n;});return sum;}let result:number=count([2,3]);")]
    [InlineData("let xs:number[]=[1,2];let result:boolean=xs.every((n:number):boolean=>{return n>0;});")]
    public void TypeChecksNativeArrayCallbacks(string source) =>
        Assert.Empty(Compilation.Check("array-callbacks.ts",source));

    [Theory]
    [InlineData("let xs:number[]=[21];xs.every();","TS2554")]
    [InlineData("let xs:number[]=[21];xs.some(42);","TS2345")]
    [InlineData("let xs:readonly number[]=[21];xs.find((n:string):boolean=>true);","TS2345")]
    [InlineData("let xs:number[]=[21];xs.findIndex((n:number):string=>\"bad\");","TS2345")]
    [InlineData("let xs:ReadonlyArray<number>=[21];xs.forEach();","TS2554")]
    [InlineData("let xs:number[]=[21];xs.forEach((n:string):void=>{});","TS2345")]
    [InlineData("let xs:number[]=[21];let wrong:number=xs.every((n:number):boolean=>true);","TS2322")]
    [InlineData("let xs:number[]=[21];let wrong:boolean=xs.findIndex((n:number):boolean=>true);","TS2322")]
    [InlineData("let xs:number[]=[21];let wrong:number=xs.forEach((n:number):void=>{});","TS2322")]
    [InlineData("let xs:number[]=[21];xs.some((n:number):boolean=>true,(n:number):boolean=>true);","TS2554")]
    public void RejectsInvalidArrayCallbacks(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-callback.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void CheckedCallbacksCrossFourES2015Modules()
    {
        const string root="/virtual/array-callbacks";
        var host=new Host();
        host.Files[root+"/model.ts"]="export type Readings=ReadonlyArray<number>;export const values:number[]=[19,20,21,22];";
        host.Files[root+"/logic.ts"]="import type {Readings} from \"./model.js\";"+
            "export function any(xs:Readings):boolean{return xs.some((n:number):boolean=>n>21);}"+
            "export function all(xs:Readings):boolean{return xs.every((n:number):boolean=>n>0);}"+
            "export function first(xs:Readings):number|undefined{return xs.find((n:number):boolean=>n>20);}"+
            "export function index(xs:Readings):number{return xs.findIndex((n:number):boolean=>n>20);}"+
            "export function total(xs:Readings):number{let sum:number=0;xs.forEach((n:number):void=>{sum=sum+n;});return sum;}";
        host.Files[root+"/bridge.ts"]="export {values} from \"./model.js\";export {any,all,first,index,total} from \"./logic.js\";";
        host.Files[root+"/entry.ts"]="import {values,any,all,first,index,total} from \"./bridge.js\";"+
            "export const some:boolean=any(values);export const every:boolean=all(values);"+
            "export const found:number|undefined=first(values);export const position:number=index(values);"+
            "export const sum:number=total(values);";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],new CompilerOptions {
            NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        var output=program.EmitJavaScript();
        Assert.Equal(4,output.Count);
        foreach(var method in new[]{".some(",".every(",".find(",".findIndex(",".forEach("})
            Assert.Contains(output,js=>js.Text.Contains(method,StringComparison.Ordinal));
    }
}

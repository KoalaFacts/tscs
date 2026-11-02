using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed bounded product verification; not original TS7 acceptance.
public sealed class ArrayInPlaceMethodTests
{
    [Theory]
    [InlineData("let xs:number[]=[3,1,2];let result:number[]=xs.reverse();")]
    [InlineData("let xs:Array<number>=[3,1,2];let result:number[]=xs.sort();")]
    [InlineData("let xs:number[]=[3,1,2];let result:number[]=xs.sort((a:number,b:number):number=>a-b);")]
    [InlineData("let xs:number[]=[3,1,2];let result:number[]=xs.sort(undefined);")]
    [InlineData("let xs:number[]=[1,2];let result:number[]=xs.fill(4);")]
    [InlineData("let xs:number[]=[1,2];let result:number[]=xs.fill(4,1);")]
    [InlineData("let xs:number[]=[1,2];let result:number[]=xs.fill(4,0,1);")]
    [InlineData("let xs:number[]=[1,2];let result:number[]=xs.fill(4,undefined,undefined);")]
    [InlineData("let xs:number[]=[1,2,3];let result:number[]=xs.copyWithin(1,0);")]
    [InlineData("let xs:number[]=[1,2,3];let result:number[]=xs.copyWithin(1,0,2);")]
    [InlineData("let xs:number[]=[1,2,3];let result:number[]=xs.copyWithin(1,0,undefined);")]
    [InlineData("type Numbers=number[];let xs:Numbers=[2,1];let result:Numbers=xs.reverse();")]
    [InlineData("function order(xs:number[]):number[]{return xs.sort();}let ordered:number[]=order([2,1]);")]
    [InlineData("let xs:string[]=[\"b\",\"a\"];let result:string[]=xs.sort();")]
    [InlineData("let xs:number[]=[1,2,3];let removed:number[]=xs.splice(1,1);")]
    [InlineData("let xs:number[]=[1,2,3];let removed:number[]=xs.splice(1);")]
    [InlineData("let xs:string[]=[\"a\",\"b\"];let removed:string[]=xs.splice(1,0,\"x\");")]
    [InlineData("let xs:Array<number>=[1,2];let removed:number[]=xs.splice(1,0,3,4);")]
    [InlineData("type Samples=number[];let xs:Samples=[2,4];let removed:number[]=xs.splice(-1,1);")]
    [InlineData("function take(xs:number[]):number[]{return xs.splice(0,1);}let removed:number[]=take([9,8]);")]
    [InlineData("let xs:number[]=[1,2];let removed:number[]=xs.splice(0,undefined);")]
    [InlineData("let xs:number[]=[1,2];let items:number[]=[3,4];let removed:number[]=xs.splice(1,0,...items);")]
    public void CheckedMutableArrayMethodsReturnTheReceiverType(string source) =>
        Assert.Empty(Compilation.Check("in-place.ts",source));

    [Theory]
    [InlineData("let xs:readonly number[]=[2,1];xs.reverse();","TS2339")]
    [InlineData("let xs:ReadonlyArray<number>=[2,1];xs.sort();","TS2339")]
    [InlineData("let xs:readonly number[]=[2,1];xs.fill(0);","TS2339")]
    [InlineData("let xs:ReadonlyArray<number>=[2,1];xs.copyWithin(0,1);","TS2339")]
    [InlineData("let xs:number[]=[2,1];xs.reverse(1);","TS2554")]
    [InlineData("let xs:number[]=[2,1];xs.sort(42);","TS2345")]
    [InlineData("let xs:number[]=[2,1];xs.fill(\"bad\");","TS2345")]
    [InlineData("let xs:number[]=[2,1];xs.fill();","TS2554")]
    [InlineData("let xs:number[]=[2,1];xs.fill(0,\"bad\");","TS2345")]
    [InlineData("let xs:number[]=[2,1];xs.copyWithin(0);","TS2554")]
    [InlineData("let xs:number[]=[2,1];xs.copyWithin(\"bad\",1);","TS2345")]
    [InlineData("let xs:number[]=[2,1];xs.copyWithin(0,1,2,3);","TS2554")]
    [InlineData("let xs:string[]=[\"a\"];let result:number[]=xs.reverse();","TS2322")]
    [InlineData("let xs:readonly number[]=[2,1];xs.splice(0,1);","TS2339")]
    [InlineData("let xs:ReadonlyArray<number>=[2,1];xs.splice(0,1);","TS2339")]
    [InlineData("let xs:number[]=[2,1];xs.splice(\"bad\",1);","TS2345")]
    [InlineData("let xs:number[]=[2,1];xs.splice(0,\"bad\");","TS2345")]
    [InlineData("let xs:number[]=[2,1];xs.splice(0,1,\"bad\");","TS2345")]
    [InlineData("let xs:number[]=[2,1];xs.splice(0,undefined,3);","TS2345")]
    [InlineData("let xs:number[]=[2,1];let wrong:string[]=xs.splice(0,1);","TS2322")]
    [InlineData("let xs:string[]=[\"a\"];xs.splice(0,0,42);","TS2345")]
    public void RejectsReadonlyReceiversBadArgumentsAndWrongResults(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-in-place.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void InPlaceCallsCrossTheExistingFourModuleCompilerGraph()
    {
        const string root="/virtual/array-in-place";
        var host=new Host();
        host.Files[root+"/model.ts"]="export type Samples=number[];export const values:Samples=[3,1,2,4];";
        host.Files[root+"/logic.ts"]="import type {Samples} from \"./model.js\";export function order(xs:Samples):Samples{return xs.sort((a:number,b:number):number=>a-b);}export function reverse(xs:Samples):Samples{return xs.reverse();}export function fill(xs:Samples):Samples{return xs.fill(9,1,3);}export function move(xs:Samples):Samples{return xs.copyWithin(0,2,4);}export function cut(xs:Samples):Samples{return xs.splice(1,1,7,8);}";
        host.Files[root+"/bridge.ts"]="export {values} from \"./model.js\";export {order,reverse,fill,move,cut} from \"./logic.js\";";
        host.Files[root+"/entry.ts"]="import {values,order,reverse,fill,move,cut} from \"./bridge.js\";export const ordered:number[]=order(values);export const reversed:number[]=reverse(values);export const filled:number[]=fill(values);export const moved:number[]=move(values);export const removed:number[]=cut(values);export const first:number=values[0];";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],new CompilerOptions {
            NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        var output=program.EmitJavaScript();
        Assert.Equal(4,output.Count);
        foreach(var method in new[]{".sort(",".reverse(",".fill(",".copyWithin(",".splice("})
            Assert.Contains(output,js=>js.Text.Contains(method,StringComparison.Ordinal));
    }
}

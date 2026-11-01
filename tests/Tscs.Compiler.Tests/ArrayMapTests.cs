using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed bounded regressions, not original TS7 acceptance.
public sealed class ArrayMapTests
{
    [Theory]
    [InlineData("let xs:number[]=[19,21];let labels:string[]=xs.map((n:number):string=>`n:${n}`);")]
    [InlineData("let xs:readonly number[]=[1,2];let labels:string[]=xs.map((n:number):string=>\"ok\");")]
    [InlineData("let xs:ReadonlyArray<string>=[\"a\",\"bb\"];let sizes:number[]=xs.map((s:string):number=>s.length);")]
    [InlineData("let xs:string[]=[\"a\",\"b\"];let ys:string[]=xs.map((s:string):string=>s+\"!\");")]
    [InlineData("let xs:number[]=[1,2];let flags:boolean[]=xs.map((n:number):boolean=>n>1);")]
    [InlineData("let xs:number[]=[1,2];let ys:number[][]=xs.map((n:number):number[]=>[n,n+1]);")]
    [InlineData("let xs:number[]=[1,2];function label(n:number):string{return \"n\"+n;}let labels:string[]=xs.map(label);")]
    [InlineData("type Items=readonly number[];function convert(xs:Items):string[]{return xs.map((n:number):string=>`v${n}`);}let names:string[]=convert([1,2]);")]
    [InlineData("let xs:number[]=[1,2];let lengths:number[]=xs.map((n:number):string=>\"x\").map((s:string):number=>s.length);")]
    [InlineData("let xs:number[]=[3,5];let boxes:{value:number}[]=xs.map((n:number):{value:number}=>{return {value:n};});")]
    [InlineData("let xs:number[]=[1,2];let ys:readonly string[]=xs.map((n:number):string=>\"x\");")]
    [InlineData("let xs:number[]=[1,2];let mapped:string[]=xs.map(function(n:number):string{return `x${n}`;});")]
    public void MapsAcrossExplicitCallbackOutputTypes(string source) =>
        Assert.Empty(Compilation.Check("array-map.ts", source));

    [Theory]
    [InlineData("let xs:number[]=[21];xs.map();", "TS2554")]
    [InlineData("let xs:number[]=[21];xs.map(42);", "TS2345")]
    [InlineData("let xs:number[]=[21];xs.map((s:string):string=>s);", "TS2345")]
    [InlineData("let xs:number[]=[21];let wrong:number[]=xs.map((n:number):string=>\"x\");", "TS2322")]
    [InlineData("let xs:readonly number[]=[21];xs.map((s:string):string=>s);", "TS2345")]
    [InlineData("let xs:number[]=[21];xs.map((n:number):number=>n,42);", "TS2554")]
    [InlineData("let xs:number[]=[21];let strings:string[]=xs.map((n:number):string=>\"x\");strings[0]=3;", "TS2322")]
    [InlineData("let xs:number[]=[21];let wrong:boolean[]=xs.map((n:number):number=>n*2);", "TS2322")]
    public void RejectsMapErrors(string source, string code) =>
        Assert.Contains(Compilation.Check("invalid-array-map.ts", source), d => d.Code == code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; } = new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path) => Path.GetFullPath(path);
        public string ReadFile(string path) => Files[path];
    }

    [Fact]
    public void MapsValuesToDifferentTypesAcrossFourModules()
    {
        const string root = "/virtual/array-map";
        var host = new Host();
        host.Files[root+"/model.ts"] = "export type Readings=ReadonlyArray<number>;export const values:number[]=[2,3,4];";
        host.Files[root+"/logic.ts"] = "import type {Readings} from \"./model.js\";"+
            "export function labels(xs:Readings):string[]{return xs.map((n:number):string=>`#${n}`);}"+
            "export function doubled(xs:Readings):number[]{return xs.map((n:number):number=>n*2);}"+
            "export function boxed(xs:Readings):{value:number}[]{return xs.map((n:number):{value:number}=>{return {value:n};});}";
        host.Files[root+"/bridge.ts"] = "export {values} from \"./model.js\";export {labels,doubled,boxed} from \"./logic.js\";";
        host.Files[root+"/entry.ts"] = "import {values,labels,doubled,boxed} from \"./bridge.js\";"+
            "export const strings:string[]=labels(values);export const twice:number[]=doubled(values);"+
            "export const objects:{value:number}[]=boxed(values);strings[0]=\"changed\";";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],new CompilerOptions {
            NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        var output=program.EmitJavaScript();
        Assert.Equal(4,output.Count);
        Assert.Contains(output,js=>js.Text.Contains(".map(",StringComparison.Ordinal));
    }
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class InterfaceInheritanceTests
{
    [Theory]
    [InlineData("interface A{value:number}interface B extends A{extra:number}let b:B={value:20,extra:22};let n:number=b.value+b.extra;")]
    [InlineData("interface A{value:number}interface B{label:string}interface C extends A,B{}let c:C={value:42,label:\"ok\"};")]
    [InlineData("interface A{value:number}interface B extends A{}type Key=keyof B;let k:Key=\"value\";")]
    [InlineData("interface A{value:number}interface B extends A{}type Value=B[\"value\"];let n:Value=42;")]
    [InlineData("interface A{readonly value:number}interface B extends A{extra:number}let b:B={value:20,extra:22};let n:number=b.value;")]
    [InlineData("interface Child extends Parent{extra:number}interface Parent{value:number}let c:Child={value:20,extra:22};")]
    [InlineData("interface Parent{0:number}interface Child extends Parent{label:string}let c:Child={0:42,label:\"ok\"};let n:number=c[0];")]
    [InlineData("type Shape={value:number};interface Child extends Shape{extra:number}let c:Child={value:20,extra:22};")]
    public void NamedStructuralInheritanceCompiles(string source)=>
        Assert.Empty(Compilation.Check("heritage.ts",source));

    // Explicit narrower members satisfy all parents regardless of extends order.
    [Theory]
    [InlineData("interface A{value:number}interface B{value:42}interface C extends A,B{value:42}let c:C={value:42};")]
    [InlineData("interface A{value:number}interface B{value:42}interface C extends B,A{value:42}let c:C={value:42};")]
    public void ExplicitMemberSatisfiesEveryBaseInEitherOrder(string source)=>
        Assert.Empty(Compilation.Check("heritage-compatible.ts",source));

    [Theory]
    [InlineData("interface A{value:number}interface B extends A{}let b:B={value:\"wrong\"};","TS2322")]
    [InlineData("interface A{readonly value:number}interface B extends A{}let b:B={value:1};b.value=2;","TS2540")]
    [InlineData("interface A{value:number}interface B extends A{}let k:keyof B=\"missing\";","TS2322")]
    [InlineData("interface A{value:number}interface B extends A{value:string}","TS2430")]
    [InlineData("interface B extends Missing{value:number}","TS2304")]
    [InlineData("type Scalar=number;interface B extends Scalar{value:number}","TSCS1001")]
    [InlineData("interface A{value:number}interface B{value:string}interface C extends A,B{}","TSCS1001")]
    [InlineData("interface A extends B{}interface B extends A{}","TS2456")]
    [InlineData("interface A<T>{value:T}","TSCS1001")]
    [InlineData("interface A{value:number}interface B{value:string}interface C extends A,B{value:42}","TS2430")]
    [InlineData("interface A{value:number}interface B{value:42}interface C extends A,B{value:string}","TS2430")]
    public void InvalidHeritageFailsClosed(string source,string code)=>
        Assert.Contains(Compilation.Check("heritage-bad.ts",source),d=>d.Code==code);

    private sealed class Host:ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void HeritageFromImportedBaseChecksAndErasesAcrossFourModules()
    {
        const string root="/virtual/heritage";
        var host=new Host();
        host.Files[root+"/base.ts"]="export interface Base{readonly value:number;0:number}export const seed={value:20,0:20};";
        host.Files[root+"/model.ts"]="import type {Base} from \"./base.js\";export interface Child extends Base{extra:number}export const item:Child={value:20,0:20,extra:22};";
        host.Files[root+"/logic.ts"]="import type {Child} from \"./model.js\";export function total(x:Child):number{return x.value+x.extra;}";
        host.Files[root+"/entry.ts"]="import {item} from \"./model.js\";import {total} from \"./logic.js\";export const result:number=total(item);";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],new CompilerOptions{
            NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        var emitted=program.EmitJavaScript();
        Assert.Equal(4,emitted.Count);
        Assert.All(emitted,output=>Assert.DoesNotContain("interface Child",output.Text,StringComparison.Ordinal));
    }
}

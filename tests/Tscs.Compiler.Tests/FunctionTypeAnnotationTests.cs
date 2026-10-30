using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class FunctionTypeAnnotationTests
{
    [Theory]
    [InlineData("type Fn=(n:number)=>number;function twice(n:number):number{return n+n;}let cb:Fn=twice;let result:number=cb(21);")]
    [InlineData("function twice(n:number):number{return n+n;}function apply(fn:(n:number)=>number):number{return fn(21);}let result:number=apply(twice);")]
    [InlineData("type Fn=(left:number,right:number)=>number;function sum(a:number,b:number):number{return a+b;}let cb:Fn=sum;let result:number=cb(20,22);")]
    [InlineData("type Fn=()=>number;function answer():number{return 42;}let cb:Fn=answer;let result:number=cb();")]
    [InlineData("type Fn=(data:{value:number})=>number;function get(data:{value:number}):number{return data.value;}let cb:Fn=get;let result:number=cb({value:42});")]
    [InlineData("type Fn=(n:number)=>number;type Alias=Fn;function twice(n:number):number{return n+n;}let cb:Alias=twice;")]
    [InlineData("type Higher=(fn:(n:number)=>number,n:number)=>number;function twice(n:number):number{return n+n;}function apply(fn:(n:number)=>number,n:number):number{return fn(n);}let cb:Higher=apply;let result:number=cb(twice,21);")]
    [InlineData("type Fn=(n?:number)=>number;function answer(n?:number):number{return 42;}let cb:Fn=answer;let result:number=cb();")]
    [InlineData("type Fn=(...n:number[])=>number;function answer(...n:number[]):number{return 42;}let cb:Fn=answer;let result:number=cb(1,2);")]
    [InlineData("type Fn=(n:number)=>number;function twice(n:number):number{return n+n;}let callbacks:Fn[]=[twice];let cb:Fn=callbacks[0];let result:number=cb(21);")]
    [InlineData("type Fn=(n:number)=>number;type Box={handler:Fn};function twice(n:number):number{return n+n;}let box:Box={handler:twice};let cb:Fn=box.handler;let result:number=cb(21);")]
    public void NamedCallSignaturesFlowThroughRealCompiler(string source) =>
        Assert.Empty(Compilation.Check("function-types.ts",source));

    [Theory]
    [InlineData("type Fn=(n:number)=>number;let cb:Fn=42;","TS2322")]
    [InlineData("type Fn=(n:number)=>number;function wrong(n:string):number{return 42;}let cb:Fn=wrong;","TS2322")]
    [InlineData("type Fn=(n:number)=>number;function wrong(n:number):string{return \"bad\";}let cb:Fn=wrong;","TS2322")]
    [InlineData("type Fn=(n:number)=>number;function twice(n:number):number{return n+n;}let cb:Fn=twice;let x:number=cb(\"bad\");","TS2345")]
    [InlineData("type Fn=(n:number)=>number;function twice(n:number):number{return n+n;}let cb:Fn=twice;let x:number=cb();","TS2554")]
    [InlineData("type Fn=(n:number)=>number;function twice(n:number):number{return n+n;}let cb:Fn=twice;let x:number=cb(1,2);","TS2554")]
    [InlineData("type Fn=(n:number)=>number;function twice(n:number):number{return n+n;}let cb:Fn=twice;let x:string=cb(21);","TS2322")]
    [InlineData("type Bad=(n?:number,required:number)=>number;","TSCS1001")]
    [InlineData("type Bad=(...values:number)=>number;","TSCS1001")]
    [InlineData("const lambda=(n)=>n;","TSCS1001")]
    public void InvalidContractsAndUnsupportedSyntaxFailClosed(string source,string code)=>
        Assert.Contains(Compilation.Check("function-types-invalid.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>
            Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine("/virtual",path));
        public string ReadFile(string path)=>
            Files.TryGetValue(path,out var text)?text:throw new FileNotFoundException(path);
    }

    [Fact]
    public void ImportedFunctionContractsBindAndCallAcrossFourModules()
    {
        var host=new Host();
        host.Files["/virtual/model.ts"]=
            "export type Callback=(value:number)=>number;"+
            "export type Invoker=(cb:Callback,value:number)=>number;"+
            "export interface Box { handler:Callback; }";
        host.Files["/virtual/logic.ts"]=
            "import type {Callback,Invoker} from \"./model.js\";"+
            "export function double(value:number):number{return value+value;}"+
            "export function apply(cb:Callback,value:number):number{return cb(value);}"+
            "export const executor:Invoker=apply;";
        host.Files["/virtual/bridge.ts"]=
            "export {double as operation,executor} from \"./logic.js\";";
        host.Files["/virtual/entry.ts"]=
            "import type {Callback,Box} from \"./model.js\";"+
            "import {operation,executor} from \"./bridge.js\";"+
            "const selected:Callback=operation;"+
            "const box:Box={handler:selected};"+
            "const extracted:Callback=box.handler;"+
            "export const result:number=executor(extracted,21);";
        var program=CompilerProgram.Load(host,["entry.ts"]);
        Assert.Equal(4,program.Sources.Count);
        Assert.Empty(program.Analyze().Diagnostics);
    }
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class AmbientArrayGlobalsTests : IDisposable
{
    private readonly string directory=Directory.CreateTempSubdirectory("tscs-arrays-global-").FullName;
    public void Dispose()=>Directory.Delete(directory,true);

    private (int Exit,string Output) Compile(string arrayDeclaration,string source)
    {
        var file=Path.Combine(directory,"input.ts");
        var globals=Path.Combine(directory,"minimal.d.ts");
        File.WriteAllText(file,source);
        File.WriteAllText(globals,arrayDeclaration+"\n"+
            "interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} "+
            "interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");
        using var stdout=new StringWriter();
        using var stderr=new StringWriter();
        var result=CommandLine.Run(["--noEmit","--noLib","--target","es2015",file,globals],stdout,stderr);
        return(result,stdout.ToString()+stderr.ToString());
    }

    [Theory]
    [InlineData("interface Array<T> { length: number; [index: number]: T; }")]
    [InlineData("interface Array < T > {[n:number]:T;length:number;}")]
    [InlineData("interface Array<T> {\n length:number;\n[position:number]:T;\n }")]
    public void ArrayIndexAndLengthContractSupportsTypedPrograms(string declaration)
    {
        var (code,output)=Compile(declaration,"const items: number[] = [20,22]; let n: number = items[0] + items.length;");
        Assert.Equal(0,code);
        Assert.Equal("",output);
    }

    [Theory]
    [InlineData("interface Array<T> { length: string; [index:number]:T; }")]
    [InlineData("interface Array<T> { length: number; }")]
    [InlineData("interface Array<T> { [index:number]:T; }")]
    [InlineData("interface Array<T> { length: number; length: number; [index:number]:T; }")]
    [InlineData("interface Array<T> { length: number; [index:string]:T; }")]
    [InlineData("interface Array<T> { length: number; [index:number]:any; }")]
    [InlineData("interface Array<T> { length: number; [index:number]:T; pop():T; }")]
    [InlineData("interface Array<T> { length: number; [index:number]:T; } interface Array<T> {}")]
    [InlineData("interface Array<T> { length: number; [index:number]:T; } interface Object { x:number; }")]
    public void UnexpectedAmbientSemanticsNeverSilentlySucceed(string declaration)
    {
        var (code,output)=Compile(declaration,"const xs: number[] = [1,2]; let n: number = xs[0];");
        Assert.Equal(1,code);
        Assert.Contains("TSCS1002",output);
    }

    [Theory]
    [InlineData("interface Array<T> { length:number; [index:number]:T; includes(value:T,fromIndex?:number):boolean; indexOf(value:T,fromIndex?:number):number; lastIndexOf(value:T,fromIndex?:number):number; join(separator?:string):string; }")]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; readonly [index:number]:T; includes(value:T,fromIndex?:number):boolean; indexOf(value:T,fromIndex?:number):number; lastIndexOf(value:T,fromIndex?:number):number; join(separator?:string):string; }")]
    public void ExactQueryMethodAmbientContractsAreAccepted(string declaration)
    {
        var source=Path.Combine(directory,"query-globals.d.ts");
        var program=Path.Combine(directory,"query-input.ts");
        File.WriteAllText(source,(declaration.StartsWith("interface Array<T>",StringComparison.Ordinal)
            ?"interface ReadonlyArray<T> { readonly length:number; readonly [index:number]:T; }\n"
            :"interface Array<T> { length:number; [index:number]:T; }\n")+
            declaration+"\n"+
            "interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} "+
            "interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");
        File.WriteAllText(program,"let source: number[] = [21]; let view: readonly number[] = source; let ok:boolean = view.includes(21);");
        using var stdout=new StringWriter();
        using var stderr=new StringWriter();
        var result=CommandLine.Run(["--noEmit","--noLib","--target","es2015",program,source],stdout,stderr);
        Assert.Equal(0,result);
        Assert.Equal("",stdout.ToString()+stderr.ToString());
    }

    [Theory]
    [InlineData("interface Array<T> { length:number; [index:number]:T; includes(value:T):number; }")]
    [InlineData("interface Array<T> { length:number; [index:number]:T; includes(value:T,fromIndex?:string):boolean; }")]
    [InlineData("interface Array<T> { length:number; [index:number]:T; includes(value:T,fromIndex?:number):boolean; includes(value:T):boolean; }")]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; readonly [index:number]:T; indexOf(value:T):boolean; }")]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; readonly [index:number]:T; join(separator?:number):string; }")]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; readonly [index:number]:T; push(value:T):number; }")]
    public void IncorrectQueryAmbientContractsRemainRejected(string declaration)
    {
        var source=Path.Combine(directory,"bad-query-globals.d.ts");
        var program=Path.Combine(directory,"bad-query-input.ts");
        File.WriteAllText(source,"interface Array<T> {}\n"+declaration+"\n"+
            "interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} "+
            "interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");
        File.WriteAllText(program,"let xs: number[] = [21]; let n: number = xs[0];");
        using var stdout=new StringWriter();
        using var stderr=new StringWriter();
        var result=CommandLine.Run(["--noEmit","--noLib","--target","es2015",program,source],stdout,stderr);
        Assert.Equal(1,result);
        Assert.Contains("TSCS1002",stdout.ToString()+stderr.ToString());
    }

    [Theory]
    [InlineData("interface String { readonly length: number; readonly [index: number]: string; }")]
    [InlineData("interface String { readonly [position:number]:string;readonly length:number; }")]
    public void ExactReadonlyStringContractIsAccepted(string declaration)
    {
        var source=Path.Combine(directory,"string-global.d.ts");
        var program=Path.Combine(directory,"index.ts");
        File.WriteAllText(source,"interface Array<T> {}\n"+declaration+"\n"+
            "interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} "+
            "interface IArguments {} interface Number {} interface Object {} interface RegExp {}");
        File.WriteAllText(program,"let s: string = \"abc\"; let c: string = s[0]; let n: number = s.length;");
        using var stdout=new StringWriter();
        using var stderr=new StringWriter();
        var result=CommandLine.Run(["--noEmit","--noLib","--target","es2015",program,source],stdout,stderr);
        Assert.Equal(0,result);
        Assert.Equal("",stdout.ToString()+stderr.ToString());
    }

    [Theory]
    [InlineData("interface String { length: number; readonly [index:number]:string; }")]
    [InlineData("interface String { readonly length:number; [index:number]:string; }")]
    [InlineData("interface String { readonly length:string; readonly [index:number]:string; }")]
    [InlineData("interface String { readonly length:number; readonly [index:string]:string; }")]
    [InlineData("interface String { readonly length:number; readonly [index:number]:any; }")]
    [InlineData("interface String { readonly length:number; readonly [index:number]:string; pop():string; }")]
    [InlineData("interface String { readonly length:number; readonly length:number; readonly [index:number]:string; }")]
    public void MutatingOrUnexpectedStringAmbientContractsAreRejected(string declaration)
    {
        var source=Path.Combine(directory,"string-global.d.ts");
        var program=Path.Combine(directory,"index.ts");
        File.WriteAllText(source,"interface Array<T> {}\n"+declaration+"\n"+
            "interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} "+
            "interface IArguments {} interface Number {} interface Object {} interface RegExp {}");
        File.WriteAllText(program,"let s: string = \"abc\"; let c: string = s[0];");
        using var stdout=new StringWriter();
        using var stderr=new StringWriter();
        var result=CommandLine.Run(["--noEmit","--noLib","--target","es2015",program,source],stdout,stderr);
        Assert.Equal(1,result);
        Assert.Contains("TSCS1002",stdout.ToString()+stderr.ToString());
    }


    private (int Exit,string Output) CompileReadonlyArrayAmbient(string declaration)
    {
        var source=Path.Combine(directory,"readonly-globals.d.ts");
        var program=Path.Combine(directory,"readonly-input.ts");
        File.WriteAllText(source,
            "interface Array<T> { length:number; [index:number]:T; }\n"+
            declaration+"\n"+
            "interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} "+
            "interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");
        File.WriteAllText(program,"let source: number[] = [21]; let view: readonly number[] = source; let n: number = view[0];");
        using var stdout=new StringWriter();
        using var stderr=new StringWriter();
        var exit=CommandLine.Run(["--noEmit","--noLib","--target","es2015",program,source],stdout,stderr);
        return(exit,stdout.ToString()+stderr.ToString());
    }

    [Theory]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; readonly [index:number]:T; }")]
    [InlineData("interface ReadonlyArray < T > { readonly [i: number]:T; readonly length: number; }")]
    public void ExactReadonlyArrayAmbientContractIsAccepted(string declaration)
    {
        var (exit,output)=CompileReadonlyArrayAmbient(declaration);
        Assert.Equal(0,exit);
        Assert.Equal("",output);
    }

    [Theory]
    [InlineData("interface ReadonlyArray<T> { length:number; readonly [index:number]:T; }")]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; [index:number]:T; }")]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; }")]
    [InlineData("interface ReadonlyArray<T> { readonly [index:number]:T; }")]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; readonly [index:string]:T; }")]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; readonly [index:number]:any; }")]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; readonly length:number; readonly [index:number]:T; }")]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; readonly [index:number]:T; push(x:T):number; }")]
    [InlineData("interface ReadonlyArray<T> { readonly length:number; readonly [index:number]:T; } interface ReadonlyArray<T> {}")]
    [InlineData("interface ReadonlyArray<T,K> { readonly length:number; readonly [index:number]:T; }")]
    public void NonreadonlyOrMalformedArrayAmbientContractsFailClosed(string declaration)
    {
        var (exit,output)=CompileReadonlyArrayAmbient(declaration);
        Assert.Equal(1,exit);
        Assert.Contains("TSCS1002",output);
    }

    [Fact]
    public void DuplicateReadonlyArrayContractsAcrossAmbientFilesFailClosed()
    {
        const string readonlyContract="interface ReadonlyArray<T> { readonly length: number; readonly [index: number]: T; }";
        var first=Path.Combine(directory,"one.d.ts");
        var second=Path.Combine(directory,"two.d.ts");
        var source=Path.Combine(directory,"two-ambient.ts");
        File.WriteAllText(first,
            "interface Array<T> { length:number; [index:number]:T; }\n"+
            readonlyContract+"\n"+
            "interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} "+
            "interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");
        File.WriteAllText(second,readonlyContract);
        File.WriteAllText(source,"let values: readonly number[] = [21]; let value: number = values[0];");
        using var stdout=new StringWriter();
        using var stderr=new StringWriter();
        var exit=CommandLine.Run(["--noEmit","--noLib","--target","es2015",source,first,second],stdout,stderr);
        Assert.Equal(1,exit);
        Assert.Contains("TSCS1002",stdout.ToString()+stderr.ToString());
    }


    [Fact]
    public void ExactArrayPushAmbientModelsMutableArrayReferenceSurfaceAndCheckedCalls()
    {
        const string ambient =
            "interface Array<T> { length:number; [index:number]:T; push(...items:T[]):number; }";
        var (exit,output)=Compile(ambient,
            "const values: number[] = [21]; let n: number = values[0];");
        Assert.Equal(0,exit);
        Assert.Equal("",output);

        // Push now has a checked product implementation; the exact ambient
        // contract is still validated rather than treated as a generic method map.
        var (supported,diagnostics)=Compile(ambient,
            "const values: number[] = [21]; let size: number = values.push(42);");
        Assert.Equal(0,supported);
        Assert.Equal("",diagnostics);

        // Unimplemented methods do not become available by virtue of push.
        var (unsupported,missing)=Compile(ambient,
            "const values: number[] = [21]; values.splice(0,1);");
        Assert.NotEqual(0,unsupported);
        Assert.Contains("TS2339",missing);
    }

    [Fact]
    public void ExactMutableArrayMethodAmbientsValidateRuntimeContracts()
    {
        const string array =
            "interface Array<T> { length:number; [index:number]:T; "+
            "push(...items:T[]):number; pop():T|undefined; "+
            "unshift(...items:T[]):number; shift():T|undefined; }";
        var (exit,output)=Compile(array,
            "let values:number[]=[21]; let n:number=values.push(22); "+
            "let first:number|undefined=values.shift();");
        Assert.Equal(0,exit);
        Assert.Equal("",output);
    }

    [Theory]
    [InlineData("interface Array<T> {length:number;[index:number]:T;pop():T;}")]
    [InlineData("interface Array<T> {length:number;[index:number]:T;shift():T;}")]
    [InlineData("interface Array<T> {length:number;[index:number]:T;unshift(...items:T[]):string;}")]
    [InlineData("interface Array<T> {length:number;[index:number]:T;pop():T|undefined;pop():T|undefined;}")]
    [InlineData("interface Array<T> {length:number;[index:number]:T;unshift(...items:T[]):number;unshift(...items:T[]):number;}")]
    public void MalformedOrDuplicateArrayMutatorAmbientContractsStillFailClosed(string declaration)
    {
        var (exit,output)=Compile(declaration,"let values:number[]=[21];");
        Assert.Equal(1,exit);
        Assert.Contains("TSCS1002",output);
    }

    [Theory]
    [InlineData("interface Array<T> { length:number; [index:number]:T; push(...items:T[]):string; }")]
    [InlineData("interface Array<T> { length:number; [index:number]:T; push(item:T):number; }")]
    [InlineData("interface Array<T> { length:number; [index:number]:T; push(...items:T[]):number; push(...items:T[]):number; }")]
    public void IncorrectArrayPushAmbientShapesFailClosed(string declaration)
    {
        var (exit,output)=Compile(declaration,"let values: number[] = [21];");
        Assert.Equal(1,exit);
        Assert.Contains("TSCS1002",output);
    }
    [Fact]
    public void OriginalEmptyAmbientArrayContractRemainsAccepted()
    {
        var (code,output)=Compile("interface Array<T> {}","let n: number = 1;");
        Assert.Equal(0,code);
        Assert.Equal("",output);
    }
}

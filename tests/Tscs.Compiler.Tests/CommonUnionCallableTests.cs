using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed compiler checks, not original TS7 compatibility credit.
public sealed class CommonUnionCallableTests
{
    [Theory]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';run(n:number):string};function f(x:C):number|string{return x.run(21);}""")]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';run(n:number):string};function f(x:C):number|string{return x['run'](21);}""")]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';run(n:number):number};function f(x:C):number{return x.run(21);}""")]
    [InlineData("""type C={tag:'a';run:(n:number)=>number}|{tag:'b';run:(n:number)=>string};function f(x:C):number|string{return x.run(21);}""")]
    [InlineData("""interface A{tag:'a';run(n:number):number}interface B{tag:'b';run(n:number):string}type C=A|B;function f(x:C):number|string{return x.run(21);}""")]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';run(n:number):string}|{tag:'c';run(n:number):boolean};function f(x:C):number|string|boolean{return x.run(21);}""")]
    [InlineData("""type C={tag:'a';inside:{run(n:number):number}}|{tag:'b';inside:{run(n:number):string}};function f(x:C):number|string{return x.inside.run(21);}""")]
    [InlineData("""type C={tag:'a';run(n:number,extra?:number):number}|{tag:'b';run(n:number,extra?:number):string};function f(x:C):number|string{return x.run(21);}""")]
    public void IdenticalInputSignaturesJoinReturnTypes(string source) =>
        Assert.Empty(Compilation.Check("common-union-call.ts",source));

    [Theory]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';run(n:number):string};function f(x:C):number|string{return x.run('bad');}""","TS2345")]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';run(n:number):string};function f(x:C):number|string{return x.run();}""","TS2554")]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';run(n:number):string};function f(x:C):number|string{return x.run(1,2);}""","TS2554")]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';other(n:number):string};function f(x:C):number|string{return x.run(1);}""","TS2339")]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';run(n:number):string};function f(x:C):number{return x.run(1);}""","TS2322")]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';run(n:number):string};function f(x:C):string{return x.run(1);}""","TS2322")]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';run(n:number):string};function f(x:C):number|string{return x['run']('bad');}""","TS2345")]
    [InlineData("""type C={tag:'a';run(n:number):number}|{tag:'b';run(n:number):string};function f(x:C):number|string{return x['run']();}""","TS2554")]
    public void CommonCallsDoNotEraseArgumentAndResultErrors(string source,string code) =>
        Assert.Contains(Compilation.Check("bad-union-call.ts",source),diagnostic=>diagnostic.Code==code);

    [Theory]
    [InlineData("""type C={run(n:any):string}|{run(n:number):number};function f(x:C):number|string{return x.run('unsafe');}""")]
    [InlineData("""type C={run(n:number):number}|{run(n:string):string};function f(x:C):number|string{return x.run(21);}""")]
    [InlineData("""type C={run(n:number):number}|{run(n:number,other:number):string};function f(x:C):number|string{return x.run(21);}""")]
    [InlineData("""type C={run(n:number):number}|{run?(n:number):string};function f(x:C):number|string{return x.run(21);}""")]
    public void DifferentOrOptionalSignaturesFailClosed(string source) =>
        Assert.Contains(Compilation.Check("unsafe-union-call.ts",source),
            diagnostic=>diagnostic.Code is "TSCS1001" or "TS2722" or "TS18048");

    private sealed class Host:ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files {get;}=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void UnionCallsTraverseFourLinkedModulesWithoutErasingTypes()
    {
        const string root="/virtual/common-union-call";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export type Choice={tag:'number';run(n:number):number}|{tag:'string';run(n:number):string};export const numeric:Choice={tag:'number',run:(n:number):number=>n+1};export const textual:Choice={tag:'string',run:(n:number):string=>'value'+n};""";
        host.Files[root+"/logic.ts"]=
            """import type {Choice} from './model.js';export function apply(x:Choice):number|string{return x.run(41);}""";
        host.Files[root+"/bridge.ts"]="export {apply} from './logic.js';";
        host.Files[root+"/entry.ts"]=
            """import {numeric,textual} from './model.js';import {apply} from './bridge.js';export const a:number|string=apply(numeric);export const b:number|string=apply(textual);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],new CompilerOptions {
            NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        Assert.Equal(4,program.EmitJavaScript().Count);
    }

    [Fact]
    public void IndexedAccessTypePreservesTheOriginalFunctionUnion()
    {
        const string source=
            """type A=(n:number)=>number;type B=(n:number)=>string;type C={run:A}|{run:B};function f(g:C['run']):A|B{return g;}""";
        Assert.Empty(Compilation.Check("union-callable-type-query.ts",source));
    }

    [Fact]
    public void FirstClassFunctionUnionIsCallableWithoutWideningItsType()
    {
        const string source=
            """type A=(n:number)=>number;type B=(n:number)=>string;function invoke(f:A|B):number|string{return f(21);}""";
        Assert.Empty(Compilation.Check("union-function-value.ts",source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedReturnJoinsDiagnoseInsteadOfCrashing(bool bracket)
    {
        var a=string.Join("|",Enumerable.Range(0,33).Select(i=>$"'a{i}'"));
        var b=string.Join("|",Enumerable.Range(0,33).Select(i=>$"'b{i}'"));
        var access=bracket?"x['run'](1)":"x.run(1)";
        var source=$"type A={a};type B={b};type C={{run:(n:number)=>A}}|{{run:(n:number)=>B}};function invoke(x:C):any{{return {access};}}";
        Assert.Contains(Compilation.Check("union-call-overflow.ts",source),
            diagnostic=>diagnostic.Code=="TSCS1001");
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptionalCallReturnOverflowIsDiagnosedInsteadOfCrashing(bool chainedMember)
    {
        var a=string.Join("|",Enumerable.Range(0,32).Select(i=>$"'a{i}'"));
        var b=string.Join("|",Enumerable.Range(0,32).Select(i=>$"'b{i}'"));
        var source=chainedMember
            ?$"type A={a};type B={b};type C={{run:(n:number)=>A}}|{{run:(n:number)=>B}};function f(g:C|undefined):any{{return g?.run(1);}}"
            :$"type A={a};type B={b};type F=((n:number)=>A)|((n:number)=>B)|undefined;function f(g:F):any{{return g?.(1);}}";
        Assert.Contains(Compilation.Check("union-optional-overflow.ts",source),
            diagnostic=>diagnostic.Code=="TSCS1001" &&
                diagnostic.Message.Contains("Too many union variants",StringComparison.Ordinal));
    }

}

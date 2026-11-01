using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class VariableBindingPatternTests
{
    // Checked, executable expression-bound parameters use the same AST
    // and diagnostics as ordinary named-function pattern parameters.
    [Theory]
    [InlineData("const f=({value}:{value:number}):number=>value;let n:number=f({value:21});")]
    [InlineData("const f=([first,,third]:number[]):number=>first+third;let n:number=f([20,1,22]);")]
    [InlineData("const f=({inner:{value}}:{inner:{value:number}}):number=>value;let n:number=f({inner:{value:42}});")]
    [InlineData("const f=({value=42}:{value?:number}):number=>value;let n:number=f({});")]
    [InlineData("const f=function({value}:{value:number}):number{return value*2;};let n:number=f({value:21});")]
    [InlineData("const f=function([a,b]:number[]):number{return a+b;};let n:number=f([20,22]);")]
    [InlineData("const f=({value}:{value:number}):number=>{value=value+1;return value;};let n:number=f({value:41});")]
    [InlineData("const f=({left}:{left:number},[right]:number[]):number=>left+right;let n:number=f({left:20},[22]);")]
    public void TypedArrowAndFunctionExpressionPatternsCompile(string source) =>
        Assert.Empty(Compilation.Check("binding-expressions.ts",source));

    [Theory]
    [InlineData("const f=({missing}:{value:number}):number=>missing;","TS2339")]
    [InlineData("const f=({value}:{value:number}):string=>value;","TS2322")]
    [InlineData("const f=({value}:{value:number}):number=>value;let n:number=f({value:\"no\"});","TS2345")]
    [InlineData("const f=({value=\"no\"}:{value?:number}):number=>value;","TS2322")]
    [InlineData("const f=([value]:number[]):string=>value;","TS2322")]
    [InlineData("const f=function({value}:{value:number}):string{return value;};","TS2322")]
    [InlineData("const f=({inner:{missing}}:{inner:{value:number}}):number=>missing;","TS2339")]
    [InlineData("const f=({value}:{value:number}):number=>{value=\"no\";return value;};","TS2322")]
    public void InvalidTypedExpressionPatternSemanticsAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("binding-expressions.ts",source),
            diagnostic=>diagnostic.Code==code);

    [Theory]
    [InlineData("const f=({...rest}:{value:number}):number=>1;")]
    [InlineData("const f=({value}:{value:number},later:number=value):number=>later;")]
    [InlineData("const seed:number=1;const f=({x=seed}:{x?:number}):number=>{const seed:number=2;return x;};")]
    public void UnsupportedExpressionPatternFormsFailClosed(string source) =>
        Assert.NotEmpty(Compilation.Check("binding-expressions.ts",source));

    [Theory]
    [InlineData("const {x,y:label}={x:21,y:\"yes\"};let n:number=x;let s:string=label;")]
    [InlineData("const {outer:{value}}={outer:{value:42}};let answer:number=value;")]
    [InlineData("const [first,,third]=[10,20,30];let answer:number=first+third;")]
    [InlineData("const [[first],[second]]=[[20],[22]];let answer:number=first+second;")]
    [InlineData("let {value}:{value:number}={value:21};value=42;let answer:number=value;")]
    [InlineData("const {left:a,right:b}={left:20,right:22};let answer:number=a+b;")]
    [InlineData("const [value]=[21];const {answer}={answer:value*2};")]
    [InlineData("const {value=21}:{value?:number}={};let n:number=value;")]
    [InlineData("const {value=42}={value:undefined};let n:number=value;")]
    [InlineData("const {value=undefined}:{value?:number}={};let n:number|undefined=value;")]
    [InlineData("const {value:alias=42}:{value?:number}={};let n:number=alias;")]
    [InlineData("const [value=21]:(number|undefined)[]=[undefined];let n:number=value;")]
    [InlineData("const {nested:{value=42}}:{nested:{value?:number}}={nested:{}};let n:number=value;")]
    [InlineData("const {value=21}={value:21};let n:number=value;")]
    [InlineData("const {value=42}={};let n:number=value;")]
    [InlineData("const {present,missing=42}={present:1};let n:number=present+missing;")]
    [InlineData("const {inner:{value=42}}={inner:{}};let n:number=value;")]
    [InlineData("const {value:alias=42}={};let n:number=alias;")]
    [InlineData("const {value=\"fallback\"}={value:21};let result:number|string=value;")]
    [InlineData("const {value={nested:{n:42}}}:{value?:{nested:{n:number}}}={};let n:number=value.nested.n;")]
    [InlineData("const [value=21]=[21];let n:number=value;")]
    [InlineData("function calculate(x:{value:number}):number{const {value}=x;return value*2;}const n:number=calculate({value:21});")]
    [InlineData("function pick({value}:{value:number}):number{return value;}const n:number=pick({value:21});")]
    [InlineData("function pick([first,,third]:number[]):number{return first+third;}const n:number=pick([20,1,22]);")]
    [InlineData("function pick({inner:{value}}:{inner:{value:number}}):number{return value;}const n:number=pick({inner:{value:42}});")]
    [InlineData("function pick({value=21}:{value?:number}):number{return value;}const n:number=pick({});")]
    [InlineData("function update({value}:{value:number}):number{value=value+1;return value;}const n:number=update({value:20});")]
    [InlineData("function combine({left}:{left:number},[right]:number[]):number{return left+right;}const n:number=combine({left:20},[22]);")]
    [InlineData("function take({value}:{value:number}={value:42}):number{return value;}const n:number=take();")]
    [InlineData("function seed():number{return 42;}function f({x=seed()}:{x?:number}):number{return x;}let n:number=f({});")]
    [InlineData("const seed:number=42;function f({x}:{x:number}={x:seed}):number{return x;}let n:number=f();")]
    [InlineData("const obj={x:42};function f({x=obj?.x??0}:{x?:number}):number{return x;}let n:number=f({});")]
    [InlineData("function f({x=((x:number):number=>x)(42)}:{x?:number}):number{return x;}let n:number=f({});")]
    [InlineData("function f({x}:{x:number}={x:((x:number):number=>x)(42)}):number{return x;}let n:number=f();")]
    [InlineData("const seed={value:21};const {value}=seed, after=value+1;let n:number=after;")]
    [InlineData("const list:number[]=[10,20];const [first,second]=list;let n:number=first+second;")]
    public void FinitePatternsFlowThroughNormalVariableTyping(string source) =>
        Assert.Empty(Compilation.Check("binding.ts",source));

    [Theory]
    [InlineData("const {missing}={value:1};","TS2339")]
    [InlineData("const {value}={value:1};value=2;","TS2588")]
    [InlineData("const [first]=[21];let bad:string=first;","TS2322")]
    [InlineData("const {value:renamed}={value:\"text\"};let bad:number=renamed;","TS2322")]
    [InlineData("const {nested:{value}}={nested:{value:21}};let bad:boolean=value;","TS2322")]
    [InlineData("const [value]=[21];value=22;","TS2588")]
    [InlineData("let {value}:{value:number}={value:\"bad\"};","TS2322")]
    [InlineData("const {missing=21}:{value:number}={value:21};","TS2339")]
    [InlineData("const {value=\"wrong\"}:{value?:number}={};","TS2322")]
    [InlineData("const {value=42}:{value:undefined}={value:undefined};","TS2322")]
    [InlineData("const source={};const {missing=42}=source;","TS2339")]
    [InlineData("const {value={nested:{n:1,extra:2}}}:{value?:{nested:{n:number}}}={};","TS2353")]
    [InlineData("const {value=21}:{value?:number}={};value=22;","TS2588")]
    [InlineData("const {value=21}:{value?:number}={};let wrong:string=value;","TS2322")]
    [InlineData("function fail({missing}:{value:number}):number{return missing;}","TS2339")]
    [InlineData("function fail({value}:{value:number}):string{return value;}","TS2322")]
    [InlineData("function fail({value=\"no\"}:{value?:number}):number{return value;}","TS2322")]
    [InlineData("function fail([value]:number[]):number{return value;}let wrong:string=fail([21]);","TS2322")]
    [InlineData("function fail({value}:{value:number}):number{return value;}const input={value:\"no\"};let wrong:number=fail(input);","TS2345")]
    [InlineData("function fail({x}:{x:number},x:number):number{return x;}","TS2300")]
    [InlineData("function fail({x}:{x:number},y:number=x):number{return y;}","TSCS1001")]
    [InlineData("function fail({x=1}:{x?:number},y:number):number{return x+y;}","TSCS1001")]
    [InlineData("function fail(first:number=21,{value}:{value:number}={value:42}):number{return value;}","TSCS1001")]
    [InlineData("function fail({value}:{value:number}):number{return value;}value=21;","TS2304")]
    [InlineData("function seed():number{return 1;}function f({x=seed()}:{x?:number}):number{function seed():number{return 2;}return x;}let n:number=f({});","TSCS1001")]
    [InlineData("let x:number=42;function f({x}:{x:number}={x:x}):number{return x;}let n:number=f();","TSCS1001")]
    [InlineData("let x:number=42;function f({x=x}:{x?:number}):number{return x;}let n:number=f({});","TSCS1001")]
    [InlineData("let x:string=\"outer\";function f({x}:{x:string}={x:`\u0024{x}`}):string{return x;}let n:string=f();","TSCS1001")]
    [InlineData("let x:string=\"outer\";function f({x=`\u0024{x}`}:{x?:string}):string{return x;}let n:string=f({});","TSCS1001")]
    [InlineData("const [value=21]:(number|undefined)[]=[undefined];let wrong:string=value;","TS2322")]
    public void BadBindingsOrAssignmentsProduceRealDiagnostics(string source,string code) =>
        Assert.Contains(Compilation.Check("binding.ts",source),diagnostic=>diagnostic.Code==code);

    [Theory]
    [InlineData("let x:string=\"outer\";function f({x}:{x:string}={x:(():string=>{for(let x in {read:():string=>x}){}return \"ok\";})()}):string{return x;}let n:string=f();")]
    [InlineData("let x:string=\"outer\";function f({x}:{x:string}={x:(():string=>{for(let x of [\"item\"]){}return \"ok\";})()}):string{return x;}let n:string=f();")]
    public void NestedLoopSourcesRespectTheirOwnBindingScope(string source) =>
        Assert.Empty(Compilation.Check("binding.ts",source));

    [Fact]
    public void LongParameterDefaultExpressionsUseBoundedNativeStack()
    {
        var chain=string.Join("+",Enumerable.Repeat("1",30000));
        var diagnostics=Compilation.Check("long-binding.ts",
            "function f({x="+chain+"}:{x?:number}):number{return x;}");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void InvalidTypedParameterDefaultReportsThePrimaryErrorOnly()
    {
        var diagnostics=Compilation.Check("binding.ts",
            "function fail({value=\"no\"}:{value?:number}):number{return value;}");
        Assert.Equal(new[] {"TS2322"},diagnostics.Select(d=>d.Code).ToArray());
    }

    [Theory]
    [InlineData("const {...rest}={value:21};")]
    [InlineData("const []=[21];")]
    [InlineData("const {}={value:21};")]
    [InlineData("const {value}={value:21};const {value}={value:22};")]
    [InlineData("const [value]={0:42};")]
    [InlineData("const [value]=\"😀\";")]
    [InlineData("const [[value]]=[{0:42}];")]
    public void UnsupportedOrDuplicateBindingsCannotSilentlyCompile(string source) =>
        Assert.NotEmpty(Compilation.Check("binding.ts",source));

    [Fact]
    public void ExcessiveNestedObjectShorthandFailsWithDiagnosticRatherThanCrash()
    {
        var pattern=string.Concat(Enumerable.Repeat("{a:",32))+"{value}"+new string('}',32);
        var result=Compilation.Check("nested.ts","const "+pattern+"={a:1};");
        Assert.Contains(result,d=>d.Code=="TSCS1001");
    }

    [Fact]
    public void NestedBindingsFromImportedObjectsFeedLinkedFunctionsAndEmit()
    {
        var root=Path.Combine(Path.GetTempPath(),"tscs-bounded-bindings");
        var inputs=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [Path.Combine(root,"model.ts")]="export const box={value:21,nested:{tag:\"ok\"}};export const readings:number[]=[20,22,24];",
            [Path.Combine(root,"logic.ts")]="export function add(a:number,b:number):number{return a+b;}",
            [Path.Combine(root,"bridge.ts")]="export {add} from \"./logic.js\";",
            [Path.Combine(root,"entry.ts")]=
                "import {box,readings} from \"./model.js\";import {add} from \"./bridge.js\";"+
                "const {value,nested:{tag}}=box;const [first,second]=readings;"+
                "export const result:number=add(value,first)+second;export const label:string=tag;"
        };
        var program=CompilerProgram.Load(new Host(inputs),[Path.Combine(root,"entry.ts")],
            new CompilerOptions { NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015 });
        Assert.Empty(program.Analyze().Diagnostics);
        var js=program.EmitJavaScript();
        Assert.Equal(4,js.Count);
        Assert.Contains(js,x=>x.Text.Contains("__tscs_binding_",StringComparison.Ordinal));
        Assert.All(js,x=>Assert.DoesNotContain(":number",x.Text));
    }

    [Fact]
    public void ImportedBindingTypeMismatchPreventsTheEntireModuleGraphFromEmitting()
    {
        var root=Path.Combine(Path.GetTempPath(),"tscs-bounded-bindings-invalid");
        var inputs=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [Path.Combine(root,"model.ts")]="export const data={label:\"hello\"};",
            [Path.Combine(root,"entry.ts")]="import {data} from \"./model.js\";const {label}=data;let wrong:number=label;"
        };
        var program=CompilerProgram.Load(new Host(inputs),[Path.Combine(root,"entry.ts")],
            new CompilerOptions { NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015 });
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }

    private sealed class Host(IReadOnlyDictionary<string,string> sources) : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string canonicalFileName)=>sources[canonicalFileName];
    }
}

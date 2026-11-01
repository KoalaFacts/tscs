using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class VariableBindingPatternTests
{
    [Theory]
    [InlineData("const {x,y:label}={x:21,y:\"yes\"};let n:number=x;let s:string=label;")]
    [InlineData("const {outer:{value}}={outer:{value:42}};let answer:number=value;")]
    [InlineData("const [first,,third]=[10,20,30];let answer:number=first+third;")]
    [InlineData("const [[first],[second]]=[[20],[22]];let answer:number=first+second;")]
    [InlineData("let {value}:{value:number}={value:21};value=42;let answer:number=value;")]
    [InlineData("const {left:a,right:b}={left:20,right:22};let answer:number=a+b;")]
    [InlineData("const [value]=[21];const {answer}={answer:value*2};")]
    [InlineData("function calculate(x:{value:number}):number{const {value}=x;return value*2;}const n:number=calculate({value:21});")]
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
    public void BadBindingsOrAssignmentsProduceRealDiagnostics(string source,string code) =>
        Assert.Contains(Compilation.Check("binding.ts",source),diagnostic=>diagnostic.Code==code);

    [Theory]
    [InlineData("const {...rest}={value:21};")]
    [InlineData("const [value=21]=[21];")]
    [InlineData("const {value=21}={value:21};")]
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
        var pattern=string.Concat(Enumerable.Repeat("{a:",33))+"value"+new string('}',33);
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

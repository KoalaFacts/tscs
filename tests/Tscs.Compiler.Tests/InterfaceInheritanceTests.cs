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
    // Shared inherited members with intersection provenance (Go: TS2320).
    [InlineData("interface A<T>{x:T}interface B<T>{x:T}interface N extends A<{a:1}&{b:2}>,B<{a:1;b:2}>{}","TSCS1001")]
    [InlineData("type I={a:1}&{b:2};interface A{x:I}interface B{x:{a:1;b:2}}interface N extends A,B{}","TSCS1001")]
    // No override: the inherited intersection still conflicts (Go TS2320).
    [InlineData("type I={a:number}&{b:string};interface C{x:I}interface A extends C{}interface B{x:{a:number;b:string}}interface N extends A,B{}","TSCS1001")]
    [InlineData("interface A{value:number}interface B{value:string}interface C extends A,B{value:42}","TS2430")]
    [InlineData("interface A{value:number}interface B{value:42}interface C extends A,B{value:string}","TS2430")]
    public void InvalidHeritageFailsClosed(string source,string code)=>
        Assert.Contains(Compilation.Check("heritage-bad.ts",source),d=>d.Code==code);


    [Theory]
    [InlineData("interface Record { value:number } interface Record { label:string } let data:Record={value:21,label:\"ok\"}; let answer:number=data.value;")]
    [InlineData("interface Record { readonly value:number } interface Record { readonly value:number; label:string } let data:Record={value:21,label:\"ok\"};")]
    [InlineData("interface Record { value:number } interface Record { extra:number } type Keys=keyof Record; let k:Keys=\"extra\";")]
    public void ReopenedFiniteInterfacesContributeAllProperties(string source)=>
        Assert.Empty(Compilation.Check("merged.ts",source));

    [Fact]
    public void ReopenedInterfacesBindAcrossDeclarationFilesBeforeConsumer()
    {
        var diagnostics=Compilation.CheckFiles([
            new SourceInput("first.d.ts","interface Reading { value:number; }"),
            new SourceInput("second.d.ts","interface Reading { label:string; }"),
            new SourceInput("entry.ts","let r:Reading={value:21,label:\"yes\"}; let total:number=r.value;")
        ]);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("interface R { value:number } interface R { value:string }","TS2717")]
    [InlineData("interface R { readonly value:number } interface R { value:number }","TS2687")]
    [InlineData("interface R { value?:number } interface R { value:number }","TS2687")]
    [InlineData("type R={value:number};interface R{extra:number}","TS2300")]
    [InlineData("interface R{value:number} type R={extra:number}","TS2300")]
    [InlineData("interface R { value:number } interface R { extra:number } let r:R={value:21};","TS2741")]
    [InlineData("interface R { value:number } interface R { extra:number } let r:R={value:21,extra:\"bad\"};","TS2322")]
    public void IncompatibleInterfaceMergesAndConsumersFailClosed(string source,string code)=>
        Assert.Contains(Compilation.Check("merged-bad.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("interface R{value:{a:number;b:string}}interface R{value:{b:string;a:number}}let r:R={value:{a:1,b:\"x\"}};")]
    [InlineData("interface R{value:number|string}interface R{value:string|number}let r:R={value:1};")]
    [InlineData("type Shape={a:number}&{b:string};interface R{value:Shape}interface R{value:Shape}")]
    public void DeclarationIdentityAcceptsOrderIndependentAndSameIntersectionTypes(string source)=>
        Assert.Empty(Compilation.Check("identity-ok.ts",source));

    [Theory]
    [InlineData("interface R{value:{a:number}&{b:string}}interface R{value:{a:number;b:string}}")]
    [InlineData("interface R{value:{a:number;b:string}}interface R{value:{a:number}&{b:string}}")]
    [InlineData("interface R{value:({a:number}&{b:string})[]}interface R{value:{a:number;b:string}[]}")]
    [InlineData("interface R{value:(x:{a:number}&{b:string})=>number}interface R{value:(x:{a:number;b:string})=>number}")]
    public void DeclarationIdentityRejectsFlattenedIntersectionProvenance(string source)=>
        Assert.Contains(Compilation.Check("identity-bad.ts",source),d=>d.Code=="TS2717");

    [Fact]
    public void AmbientVarMergingUsesIdentityRatherThanPrintedOrder()
    {
        var compatible=Compilation.CheckFiles([
            new SourceInput("a.d.ts","declare var total:{a:number;b:string};"),
            new SourceInput("b.d.ts","declare var total:{b:string;a:number};"),
            new SourceInput("entry.ts","let result:number=total.a;")
        ]);
        Assert.Empty(compatible);
        var conflict=Compilation.CheckFiles([
            new SourceInput("a.d.ts","declare var total:{a:number}&{b:string};"),
            new SourceInput("b.d.ts","declare var total:{a:number;b:string};"),
            new SourceInput("entry.ts","let result:number=total.a;")
        ]);
        Assert.Contains(conflict,d=>d.Code=="TS2403" && d.File=="b.d.ts");
    }

    private const string DerivedPrelude=
        "type A={a:number};type B={b:string};type P=A&B;declare const first:P;declare const second:P;";

    [Theory]
    [InlineData("typeof first","typeof second")]
    [InlineData("keyof P","\"a\"|\"b\"")]
    [InlineData("P[\"a\"]","number")]
    public void DeclarationIdentityComparesWhatDerivedSyntaxDenotes(string one,string two)
    {
        foreach(var (left,right) in new[]{(one,two),(two,one)})
        {
            Assert.Empty(Compilation.Check("derived-ok.ts",
                DerivedPrelude+$"interface R{{value:{left}}}interface R{{value:{right}}}"));
            Assert.Empty(Compilation.CheckFiles([
                new SourceInput("types.d.ts",DerivedPrelude),
                new SourceInput("a.d.ts",$"declare var item:{left};"),
                new SourceInput("b.d.ts",$"declare var item:{right};"),
                new SourceInput("entry.ts","let result=7;")
            ]));
        }
    }

    [Theory]
    [InlineData("P[\"b\"]","number")]
    [InlineData("typeof first","{a:number;b:string}")]
    public void DerivedDeclarationIdentityStillRejectsDifferentTypes(string one,string two)=>
        Assert.Contains(Compilation.Check("derived-bad.ts",
            DerivedPrelude+$"interface R{{value:{one}}}interface R{{value:{two}}}"),d=>d.Code=="TS2717");

    [Theory]
    [InlineData("function f(x:A&B):number{return 1}","typeof f","(x:{a:number;b:string})=>number")]
    [InlineData("const v={a:1,b:\"x\"} as A&B;","typeof v","{a:number;b:string}")]
    public void TypeQueryWithoutDeclaredIntersectionShapeFailsClosed(string value,string one,string two)=>
        Assert.Contains(Compilation.Check("derived-query.ts",
            DerivedPrelude+value+$"interface R{{value:{one}}}interface R{{value:{two}}}"),d=>d.Code=="TS2717");

    private const string QueryPrelude="type A={a:number};type B={b:string};type P=A&B;";

    // Review regression on 2d3bb1d: inferred and function-declaration roots
    // without intersection provenance compare by the type they denote.
    [Theory]
    [InlineData("let first=7;let second=8;")]
    [InlineData("const first={a:7,b:\"ok\"};const second={a:8,b:\"yes\"};")]
    [InlineData("function first(v:P):number{return 1}function second(v:P):number{return 2}")]
    public void TypeQueriesOfEqualDeclaredOrInferredTypesMerge(string values)
    {
        foreach(var (left,right) in new[]{("first","second"),("second","first")})
        {
            Assert.Empty(Compilation.Check("query-ok.ts",
                QueryPrelude+values+$"interface R{{value:typeof {left}}}interface R{{value:typeof {right}}}"));
            Assert.Empty(Compilation.CheckFiles([
                new SourceInput("types.d.ts",QueryPrelude),
                new SourceInput("shared.ts",values),
                new SourceInput("a.d.ts",$"declare var item:typeof {left};"),
                new SourceInput("b.d.ts",$"declare var item:typeof {right};"),
                new SourceInput("entry.ts","let result=7;")
            ]));
        }
    }

    // Review regressions on merged #238 (main 8a9bd2b): main aa3782e and the
    // pinned reference accept these repeated ambient var declarations.
    [Theory]
    [InlineData("const first=null;const second=null;","typeof second")]
    [InlineData("const first={x:null};const second={x:null};","typeof second")]
    [InlineData("const first=[null];const second=[null];","typeof second")]
    [InlineData("const first=null;","null")]
    [InlineData("const first={x:null};","{x:null}")]
    [InlineData("const first={a:7,b:\"ok\"} as P;","P")]
    [InlineData("function first(...v:P[]):number{return 7;}","(...v:P[])=>number")]
    public void RepeatedAmbientVarKeepsMainNullAssertedAndRestIdentity(string values,string other)
    {
        foreach(var (left,right) in new[]{("typeof first",other),(other,"typeof first")})
            Assert.Empty(Compilation.CheckFiles([
                new SourceInput("types.d.ts",QueryPrelude),
                new SourceInput("shared.ts",values),
                new SourceInput("a.d.ts",$"declare var item:{left};"),
                new SourceInput("b.d.ts",$"declare var item:{right};"),
                new SourceInput("entry.ts","let result=7;")
            ]));
    }

    [Theory]
    [InlineData("let first=null;","null")]
    [InlineData("const first={a:7,b:\"ok\"} as P;","{a:number;b:string}")]
    [InlineData("const first={a:7,b:\"ok\"} as const;","{a:number;b:string}")]
    [InlineData("function first(...v:P[]):number{return 7;}","(v?:P)=>number")]
    public void RepeatedAmbientVarStillRejectsDifferentNullAssertedAndRestTypes(string values,string other)=>
        Assert.Contains(Compilation.CheckFiles([
            new SourceInput("types.d.ts",QueryPrelude),
            new SourceInput("shared.ts",values),
            new SourceInput("a.d.ts","declare var item:typeof first;"),
            new SourceInput("b.d.ts",$"declare var item:{other};"),
            new SourceInput("entry.ts","let result=7;")
        ]),d=>d.Code=="TS2403");

    [Theory]
    [InlineData("let first=7;let second=\"x\";","typeof second")]
    [InlineData("function first(v:P):number{return 1}","(v:{a:number;b:string})=>number")]
    [InlineData("function first(v:P):number{return 1}function second(v:{a:number;b:string}):number{return 2}","typeof second")]
    [InlineData("const first={a:7,b:\"ok\"};const v={a:1,b:\"x\"} as P;","typeof v")]
    [InlineData("let first=null;","null")]
    [InlineData("let first=(null);let second=null;","typeof second")]
    public void TypeQueryIdentityStillRejectsDifferentOrUnprovenTypes(string values,string other)=>
        Assert.Contains(Compilation.Check("query-bad.ts",
            QueryPrelude+values+$"interface R{{value:typeof first}}interface R{{value:{other}}}"),d=>d.Code=="TS2717");

    [Fact]
    public void ConflictingDeclarationFilesReportTheLaterFile()
    {
        var errors=Compilation.CheckFiles([
            new SourceInput("first.d.ts","interface R { value:number; }"),
            new SourceInput("second.d.ts","interface R { value:string; }"),
            new SourceInput("entry.ts","let x:number=1;")
        ]);
        Assert.Contains(errors,e=>e.Code=="TS2717" && e.File=="second.d.ts");
    }

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

    // A merged interface's heritage is checked once, against the merged
    // type, at the first declaration's name (pinned Go, TS2430).
    [Theory]
    [InlineData("interface Base<T>{a:T}interface N{x:number}interface N extends Base<string>{a:number}",32)]
    [InlineData("interface Base{a:string}interface N{b:number}interface N extends Base{a:number}",34)]
    public void MergedInterfaceHeritageReportsAtFirstDeclaration(string source,int start)
    {
        var diagnostic=Assert.Single(Compilation.Check("merged-heritage-bad.ts",source));
        Assert.Equal("TS2430",diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
    }

    // A member one declaration inherits and another declares or inherits
    // differently is TS2430 / TS2320 against the merged type in the
    // reference, not a TS2717 redeclaration: fail closed.
    [Theory]
    [InlineData("interface Base<T>{a:T}interface N extends Base<string>{x:number}interface N extends Base<number>{y:number}")]
    [InlineData("interface Base{a:string}interface N{a:number}interface N extends Base{}")]
    [InlineData("interface Base{a:string}interface N extends Base{}interface N{a:number}")]
    public void MergedInheritedMemberConflictsFailClosed(string source)
    {
        var diagnostics=Compilation.Check("merged-heritage-unsupported.ts",source);
        Assert.Contains(diagnostics,d=>d.Code=="TSCS1001");
        Assert.DoesNotContain(diagnostics,d=>d.Code=="TS2717");
    }

    [Theory]
    [InlineData("interface Base{a:string}interface N extends Base{x:number}interface N{y:number}let n:N={a:\"\",x:1,y:2};")]
    [InlineData("interface Base{a:string}interface N extends Base{}interface N{a:string}")]
    [InlineData("interface Base{a:string}interface N extends Base{a:\"k\"}interface N{b:number}")]
    [InlineData("interface Base{a:string}interface Other{a:string}interface N extends Base{}interface N extends Other{}")]
    public void MergedInterfaceHeritageAgreesWithReference(string source)=>
        Assert.Empty(Compilation.Check("merged-heritage.ts",source));

    // An intersection in an unshared member does not affect a shared one
    // (pinned Go accepts all; regression from the base-wide check in #245).
    [Theory]
    [InlineData("I")]
    [InlineData("I[]")]
    [InlineData("{inner:I}")]
    [InlineData("(v:I)=>number")]
    public void IntersectionInUnsharedMemberKeepsSharedMemberValid(string extra)
    {
        foreach(var order in new[]{"A,B","B,A"})
            Assert.Empty(Compilation.Check("unshared-intersection.ts",
                $"type I={{a:number}}&{{b:string}};interface A{{x:number;extra:{extra}}}interface B{{x:number}}interface N extends {order}{{}}let result=7;"));
    }

    // Shared members of optional, reopened, diamond and unrelated bases
    // without any intersection stay valid (pinned Go accepts all).
    [Theory]
    [InlineData("interface A{x?:number}interface B{x?:number}","A,B")]
    [InlineData("interface A{x:number}interface A{extra:string}interface B{x:number}","A,B")]
    [InlineData("interface C{x:number}interface E{x:number}interface A extends C,E{}interface B{x:number}","A,B")]
    [InlineData("interface A{x:number}interface B{x:number}interface C{other:number}interface C{extra:string}","A,B,C")]
    [InlineData("interface A{x:number}interface B{x:number}interface C{other:number}interface C{extra:string}","C,B,A")]
    // An own member overrides (shadows) an inherited intersection member.
    [InlineData("type I={a:number}&{b:string};interface C{x:I}interface A extends C{x:{a:number;b:string}}interface B{x:{a:number;b:string}}","A,B")]
    [InlineData("type I={a:number}&{b:string};interface C{x:I}interface X extends C{}interface A extends X{x:{a:number;b:string}}interface B{x:{a:number;b:string}}","A,B")]
    [InlineData("type I={a:number}&{b:string};type F={a:number;b:string};interface C{x:I[]}interface A extends C{x:F[]}interface B{x:F[]}","A,B")]
    // Optional shared member next to an unrelated intersection member.
    [InlineData("type I={a:number}&{b:string};interface A{x?:number;extra:I}interface B{x?:number}","A,B")]
    [InlineData("type I={a:number}&{b:string};interface A{x:number}interface A{extra:I}interface B{x:number}","A,B")]
    [InlineData("type I={a:number}&{b:string};interface C{x:number;extra:I}interface E{x:number}interface A extends C,E{}interface B{x:number}","A,B")]
    public void SharedMembersWithoutIntersectionsStayValid(string prelude,string order)
    {
        Assert.Empty(Compilation.Check("shared-no-intersection.ts",$"{prelude}interface N extends {order}{{}}"));
        var reversed=string.Join(",",order.Split(',').Reverse());
        Assert.Empty(Compilation.Check("shared-no-intersection.ts",$"{prelude}interface N extends {reversed}{{}}"));
    }

    // Mutual assignability is not identity: `any` against a concrete shared
    // member type is TS2320 in the reference; fail closed (review on #246).
    [Theory]
    [InlineData("type I={a:number}&{b:string};type Q={a:number;b:string};interface C{x:I}interface A extends C{x:any}interface B{x:Q}")]
    [InlineData("type Q={a:number;b:string};interface A{x:any[]}interface B{x:Q[]}")]
    [InlineData("type Q={a:number;b:string};interface A{x:{v:any}}interface B{x:{v:Q}}")]
    [InlineData("type Q={a:number;b:string};interface A{x:(v:any)=>number}interface B{x:(v:Q)=>number}")]
    [InlineData("interface A{x:any}interface B{x:number}")]
    [InlineData("interface A{x:{readonly a:number}}interface B{x:{a:number}}")]
    [InlineData("interface A{x:{a:any}|string}interface B{x:{a:number}|string}")]
    public void SharedMemberAnyAgainstConcreteFailsClosed(string prelude)
    {
        foreach(var order in new[]{"A,B","B,A"})
            Assert.Contains(Compilation.Check("shared-any.ts",$"{prelude}interface N extends {order}{{}}let result=7;"),
                d=>d.Code=="TSCS1001");
    }

    [Theory]
    [InlineData("interface A{x:any}interface B{x:any}")]
    [InlineData("interface A{x:any[]}interface B{x:any[]}")]
    [InlineData("interface A{x:(v:any)=>number}interface B{x:(v:any)=>number}")]
    [InlineData("interface A{x:{a:any}|string}interface B{x:{a:any}|string}")]
    [InlineData("interface A{x:any[]|string}interface B{x:any[]|string}")]
    [InlineData("interface A{x:{readonly a:any}}interface B{x:{readonly a:any}}")]
    public void SharedMemberAnyAtSamePositionsStaysValid(string prelude)
    {
        foreach(var order in new[]{"A,B","B,A"})
            Assert.Empty(Compilation.Check("shared-any-ok.ts",$"{prelude}interface N extends {order}{{}}let result=7;"));
    }
}

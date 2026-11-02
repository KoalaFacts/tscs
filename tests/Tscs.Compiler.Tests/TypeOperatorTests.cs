using Tscs;
using Xunit;
namespace Tscs.Tests;

// AST-to-checker type operators; these are not original TS7 acceptance.
public sealed class TypeOperatorTests
{
    [Theory]
    [InlineData("let n: number = 21 as number;")]
    [InlineData("let n: 21 = 21 as 21;")]
    [InlineData("let n: number = (21 as number) + 21;")]
    [InlineData("type Item = { value: number }; let raw: any = { value: 21 }; let p: Item = raw as Item;")]
    [InlineData("let p = { value: 21, extra: 2 } as { value: number }; let n: number = p.value;")]
    [InlineData("let n: number = ({ value: 21 } as { value: number }).value;")]
    [InlineData("let n: number = 21 satisfies number;")]
    [InlineData("let n: number = (21 satisfies number) + 21;")]
    [InlineData("let p = { value: 21 } satisfies { value: number }; let n: number = p.value;")]
    [InlineData("type Item = { value: number }; let p = { value: 21 } satisfies Item;")]
    [InlineData("let raw: any = { value: 21 }; let p = (raw as { value: number }) satisfies { value: number };")]
    [InlineData("let n: number = (21 as number) satisfies number;")]
    [InlineData("let p = { value: 21 } satisfies { value: 21 }; let n: 21 = p.value;")]
    [InlineData("let p = { kind: \"ok\" } satisfies { kind: \"ok\" | \"bad\" }; let s: \"ok\" = p.kind;")]
    [InlineData("let n: 21 = 21 satisfies 21;")]
    public void CheckedAssertionsAndConstraints(string source) =>
        Assert.Empty(Compilation.Check("type-operators.ts",source));

    [Theory]
    [InlineData("let n: number = \"oops\" as number;", "TS2352")]
    [InlineData("let s: string = 21 as string;", "TS2352")]
    [InlineData("let s = \"oops\" satisfies number;", "TS1360")]
    [InlineData("let p = { value: \"oops\" } satisfies { value: number };", "TS2322")]
    [InlineData("let p = { value: 21, extra: 1 } satisfies { value: number };", "TS2353")]
    [InlineData("let s: string = 21 satisfies number;", "TS2322")]
    [InlineData("let x = 21 as MissingType;", "TS2304")]
    [InlineData("let x = 21 satisfies MissingType;", "TS2304")]
    [InlineData("let p = { value: 21 } as { label: string };", "TS2352")]
    [InlineData("let p = { value: 21 } satisfies { value: number }; p.value = \"oops\";", "TS2322")]
    public void InvalidAssertionOrConstraintHasRealDiagnostic(string source,string expectedCode) =>
        Assert.Contains(Compilation.Check("type-operators.ts",source),d=>d.Code==expectedCode);

    [Fact]
    public void SatisfiesRetainsContextualLiteralForWrites()
    {
        var diagnostics=Compilation.Check("type-operators.ts",
            "let p = { kind: \"ok\" } satisfies { kind: \"ok\" | \"bad\" }; p.kind = \"bad\";");
        Assert.Contains(diagnostics,d=>d.Code=="TS2322");
    }

    [Theory]
    [InlineData("type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; let p = { kind: \"a\", value: 21 } satisfies V; let tag: \"a\" = p.kind; let n: number = p.value;")]
    [InlineData("type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; let p = { kind: \"b\", label: \"ok\" } satisfies V; let tag: \"b\" = p.kind; let s: string = p.label;")]
    [InlineData("type V = { kind: \"a\" | \"aa\"; value: number } | { kind: \"b\"; label: string }; let p = { kind: \"aa\", value: 21 } satisfies V; let tag: \"aa\" = p.kind;")]
    [InlineData("type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; let p = ({ kind: \"a\", value: 5 } satisfies V); p.value = 6;")]
    [InlineData("type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; function make(n:number):{kind:\"a\";value:number}{return {kind:\"a\",value:n} satisfies V;} let x:number=make(21).value;")]
    [InlineData("type V={kind:\"a\"; data:{score:number}}|{kind:\"b\"; label:string}; let p={kind:\"a\",data:{score:21}} satisfies V;let n:number=p.data.score;")]
    public void FreshDiscriminatedSatisfiesChecksSelectedArm(string source) =>
        Assert.Empty(Compilation.Check("discriminated-satisfies.ts",source));

    [Theory]
    [InlineData("type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; let p = { kind: \"a\", value: 21 } satisfies V; p.kind = \"b\";", "TS2322")]
    [InlineData("type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; let p = { kind: \"a\", value: 21 } satisfies V; let bad: \"b\" = p.kind;", "TS2322")]
    [InlineData("type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; let p = { kind: \"a\", value: \"bad\" } satisfies V;", "TS2322")]
    [InlineData("type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; let p = { kind: \"a\", value: 21, label: \"wrong\" } satisfies V;", "TS2353")]
    public void DiscriminatedSatisfiesRejectsUnsoundWritesAndShapes(string source,string code) =>
        Assert.Contains(Compilation.Check("discriminated-satisfies.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("type V={kind:\"a\";value:number}|{kind:\"b\";label:string};let p={kind:\"c\",value:1} satisfies V;")]
    [InlineData("type V={kind:\"a\";value:number}|{kind:\"a\";label:string};let p={kind:\"a\",value:1} satisfies V;")]
    [InlineData("type V={kind:\"a\";value:number}|{kind:\"b\";label:string};let kind:string=\"a\";let p={kind:kind,value:1} satisfies V;")]
    public void AmbiguousOrUnresolvedDiscriminantsFailClosed(string source) =>
        Assert.Contains(Compilation.Check("discriminated-satisfies.ts",source),d=>d.Code=="TSCS1001");

    [Fact]
    public void TypeOperatorsDoNotReplaceRuntimeValidation()
    {
        // An explicit assertion is a compile-time view, not a runtime cast.
        // A disjoint assertion is still TS2352; a compatible one emits as JS.
        Assert.Contains(Compilation.Check("type-operators.ts","let bad = 21 as string;"),
            d=>d.Code=="TS2352");
    }

    [Theory]
    [InlineData("const state={tag:\"ready\", count:21} as const; let name:\"ready\"=state.tag; let n:21=state.count;")]
    [InlineData("const state={nested:{flag:true, count:-3}} as const; let flag:true=state.nested.flag; let n:-3=state.nested.count;")]
    [InlineData("let state={ok:false} as const; let f:false=state.ok;")]
    [InlineData("const state={0:\"zero\"} as const; let zero:\"zero\"=state[0];")]
    [InlineData("const state={} as const; type Shape=typeof state; let empty:Shape={};")]
    [InlineData("type Shape={readonly tag:\"ready\"}; const state={tag:\"ready\"} as const; let shape:Shape=state;")]
    public void FiniteConstAssertionsKeepReadonlyExactTypes(string source) =>
        Assert.Empty(Compilation.Check("const-assertions.ts",source));

    [Theory]
    [InlineData("const tuple=[21,\"go\"] as const;let n:21=tuple[0];let s:\"go\"=tuple[1];")]
    [InlineData("const tuple=[] as const;let n:0=tuple.length;")]
    [InlineData("const tuple=[21,\"go\"] as const;const i=0;let n:21=tuple[i];")]
    [InlineData("const tuple=[21,\"go\"] as const;let copy:typeof tuple=[21,\"go\"];let n:21=copy[0];")]
    [InlineData("const tuple=[21,\"go\"] as const;let copy:typeof tuple=[tuple[0],tuple[1]];let n:21=copy[0];let s:\"go\"=copy[1];")]
    [InlineData("const tuple=[21,\"go\"] as const;const first=tuple[0];let copy:typeof tuple=[first,tuple[1]];")]
    [InlineData("const tuple=[[21],[42]] as const;let copy:typeof tuple=[[tuple[0][0]],[tuple[1][0]]];let n:21=copy[0][0];")]
    [InlineData("const tuple=[[21],[42]] as const;let n:21=tuple[0][0];let m:42=tuple[1][0];")]
    [InlineData("const data={tuple:[true,21]} as const;let b:true=data.tuple[0];let n:21=data.tuple[1];")]
    [InlineData("const tuple=[21,\"go\"] as const;type First=typeof tuple[0];type Last=typeof tuple[1];let n:First=21;let s:Last=\"go\";")]
    [InlineData("const tuple=[21,\"go\"] as const;type Values=typeof tuple[number];type Length=typeof tuple[\"length\"];let n:Values=21;let s:Values=\"go\";let l:Length=2;")]
    public void FiniteConstArraysKeepPerIndexReadonlyTupleTypes(string source) =>
        Assert.Empty(Compilation.Check("const-tuples.ts",source));

    [Theory]
    [InlineData("const tuple=[21,\"go\"] as const;tuple[0]=21;", "TS2540")]
    [InlineData("const tuple=[[21]] as const;tuple[0][0]=22;", "TS2540")]
    [InlineData("const tuple=[21] as const;let n=tuple[2];", "TS2493")]
    [InlineData("const tuple=[] as const;let n=tuple[0];", "TS2493")]
    [InlineData("const tuple=[21] as const;let n=tuple[2147483648];", "TS2493")]
    [InlineData("const tuple=[21] as const;const i=2;let n=tuple[i];", "TS2493")]
    [InlineData("const tuple=[21,\"go\"] as const;let bad:typeof tuple=[];", "TS2322")]
    [InlineData("const tuple=[21,\"go\"] as const;let bad:typeof tuple=[21];", "TS2322")]
    [InlineData("const tuple=[21,\"go\"] as const;let bad:typeof tuple=[21,\"go\",42];", "TS2322")]
    [InlineData("const tuple=[21,\"go\"] as const;let bad:typeof tuple=[tuple[1],tuple[0]];", "TS2322")]
    [InlineData("const tuple=[21] as const;let n=tuple[-1];", "TSCS1001")]
    [InlineData("const tuple=[21] as const;let n=tuple[0.5];", "TSCS1001")]
    [InlineData("const tuple=[21,\"go\"] as const;let n:22=tuple[0];", "TS2322")]
    [InlineData("const tuple=[21] as const;let m:number[]=tuple;", "TS4104")]
    public void FiniteConstArraysRejectInvalidWritesIndicesAndAssignments(string source,string code) =>
        Assert.Contains(Compilation.Check("const-tuples.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("const state={tag:\"ready\"} as const;state.tag=\"later\";", "TS2540")]
    [InlineData("const state={nested:{count:21}} as const;state.nested.count=22;", "TS2540")]
    [InlineData("const state={count:21} as const;let wrong:22=state.count;", "TS2322")]
    [InlineData("const state={flag:true} as const;let wrong:false=state.flag;", "TS2322")]
    [InlineData("let value=21;const state=value as const;", "TS1355")]
    [InlineData("const state=(21+1) as const;", "TS1355")]
    [InlineData("const state=[21,...[22]] as const;", "TSCS1001")]
    [InlineData("let value:{a:1}={a:1,b:2} as const;", "TS2353")]
    [InlineData("let value=({a:1,b:2} as const) satisfies {a:1};", "TS2353")]
    public void FiniteConstAssertionsRejectWritesAndUnsupportedCases(string source,string code) =>
        Assert.Contains(Compilation.Check("const-assertions.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("const seed=21;const box={seed} as const;let v:21=box.seed;")]
    [InlineData("let seed:number=21;const box={seed} as const;let v:number=box.seed;")]
    [InlineData("const label=\"ready\";const box={nested:{label}} as const;let v:\"ready\"=box.nested.label;")]
    [InlineData("const seed=21;const box=[seed,true] as const;let v:21=box[0];")]
    [InlineData("let seed:number=21;const box=[seed,false] as const;let v:number=box[0];")]
    [InlineData("const seed=21;const box={values:[seed]} as const;let v:21=box.values[0];")]
    [InlineData("const data={count:21};const box={data} as const;box.data.count=42;let v:number=box.data.count;")]
    [InlineData("const data:[number,string]=[21,\"go\"];const box={data} as const;data[0]=42;let v:number=box.data[0];")]
    [InlineData("const box={read} as const;function read():number{return 21;}let value:number=box.read();")]
    public void NamedConstLiteralMembersPreserveExistingTypes(string source) =>
        Assert.Empty(Compilation.Check("named-const-members.ts",source));

    [Theory]
    [InlineData("const seed=21;const box={seed} as const;box.seed=22;","TS2540")]
    [InlineData("const seed=21;const box={seed} as const;let bad:22=box.seed;","TS2322")]
    [InlineData("let seed:number=21;const box={seed} as const;let bad:21=box.seed;","TS2322")]
    [InlineData("const seed=21;const box=[seed] as const;box[0]=22;","TS2540")]
    [InlineData("const seed=21;const box=[seed] as const;let bad:22=box[0];","TS2322")]
    [InlineData("const seed=21;const box={nested:{seed}} as const;box.nested.seed=22;","TS2540")]
    [InlineData("const seed=21;const box=[seed] as const;let mutable:number[]=box;","TS4104")]
    [InlineData("const seed=21;const box={inner:{seed}} as const;let bad:{inner:{seed:22}}=box;","TS2322")]
    [InlineData("const seed=21;const bad=seed as const;","TS1355")]
    public void NamedConstLiteralMembersRejectInvalidAssignments(string source,string code) =>
        Assert.Contains(Compilation.Check("named-const-members-invalid.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("const result={sum:21+1} as const;let n:number=result.sum;")]
    [InlineData("const box={nested:{sum:21+1}} as const;let n:number=box.nested.sum;")]
    [InlineData("const list=[21+1,\"go\"] as const;let n:number=list[0];let s:\"go\"=list[1];")]
    [InlineData("const list=[[21+1],[42+2]] as const;let n:number=list[0][0];")]
    [InlineData("let seed:number=21;const box={sum:seed+1} as const;let n:number=box.sum;")]
    [InlineData("const model={count:21};const box={read:model.count} as const;let n:number=box.read;")]
    [InlineData("const box={label:\"a\"+\"b\"} as const;let s:string=box.label;")]
    [InlineData("function make(n:number):{readonly value:number}{return {value:n+1} as const;}let out:number=make(21).value;")]
    public void FiniteConstMembersPreserveCheckedComputationTypes(string source) =>
        Assert.Empty(Compilation.Check("computed-const-members.ts",source));

    [Theory]
    [InlineData("const result={sum:21+1} as const;result.sum=22;", "TS2540")]
    [InlineData("const result={sum:21+1} as const;let bad:22=result.sum;", "TS2322")]
    [InlineData("const value=[21+1] as const;value[0]=22;", "TS2540")]
    [InlineData("const value=[[21+1]] as const;value[0][0]=22;", "TS2540")]
    [InlineData("const value={nested:{sum:21+1}} as const;value.nested.sum=22;", "TS2540")]
    [InlineData("const value={label:\"a\"+\"b\"} as const;let bad:\"ab\"=value.label;", "TS2322")]
    [InlineData("const value={value:21+1} as const;let bad:{readonly value:21}=value;", "TS2322")]
    [InlineData("const value=[21+1,\"go\"] as const;let bad:21=value[0];", "TS2322")]
    public void ComputedConstMembersDoNotInventLiteralsOrAllowReadonlyWrites(string source,string code) =>
        Assert.Contains(Compilation.Check("computed-const-members-invalid.ts",source),d=>d.Code==code);
    [Theory]
    [InlineData("const base={value:21};const snapshot={...base,tag:\"ready\"} as const;let n:number=snapshot.value;let tag:\"ready\"=snapshot.tag;")]
    [InlineData("const left={a:21};const right={b:\"go\"};const snap={...left,...right} as const;let a:number=snap.a;let b:string=snap.b;")]
    [InlineData("const source={value:21};const snap={...source,value:42} as const;let exact:42=snap.value;")]
    [InlineData("const source={nested:{value:21}};const snap={...source} as const;snap.nested.value=42;let value:number=snap.nested.value;")]
    [InlineData("const source={value:21} as const;const snap={...source} as const;let exact:21=snap.value;")]
    [InlineData("const snap={...{value:21},tag:\"go\"} as const;let n:number=snap.value;let tag:\"go\"=snap.tag;")]
    [InlineData("const source:{value?:number}={};const snap={...source,tag:\"ok\"} as const;let n:number|undefined=snap.value;let label:\"ok\"=snap.tag;")]
    [InlineData("function take(x:{readonly value:number}):number{return x.value;}const source={value:21};let answer:number=take({...source} as const);")]
    public void FiniteConstObjectSpreadsPreserveReadonlyShellsAndCheckedMembers(string source) =>
        Assert.Empty(Compilation.Check("const-object-spreads.ts",source));

    [Theory]
    [InlineData("const base={n:21};const out={...base} as const;out.n=22;", "TS2540")]
    [InlineData("const base={n:21};const out={...base} as const;let bad:21=out.n;", "TS2322")]
    [InlineData("const src={n:1};const out={...src,nested:{value:21}} as const;out.nested.value=22;", "TS2540")]
    [InlineData("const obj={local:21,...{n:22}} as const;obj.n=24;", "TS2540")]
    [InlineData("const src={nested:{n:21}};const out={...src} as const;out.nested={n:2};", "TS2540")]
    [InlineData("const source={tag:\"ok\"};const out={...source} as const;let wrong:\"ok\"=out.tag;", "TS2322")]
    [InlineData("const src={n:21};const out={...src} as const;let wrong:{n:string}=out;", "TS2322")]
    [InlineData("const source={n:21};const out={...source} as const;let bad=out.nope;", "TS2339")]
    public void FiniteConstObjectSpreadsRejectReadonlyWritesAndInvalidTypes(string source,string code) =>
        Assert.Contains(Compilation.Check("const-object-spreads-invalid.ts",source),d=>d.Code==code);

    // Bounded readonly tuple shells formed from fixed-length checked tuple operands.
    // Neither an unknown-length array nor an optional-length tuple is treated as exact.
    [Theory]
    [InlineData("const tail=[21,\"go\"] as const;const x=[true,...tail] as const;let a:true=x[0];let b:21=x[1];let c:\"go\"=x[2];")]
    [InlineData("let tail:[number,string]=[21,\"go\"];const x=[0,...tail] as const;let a:0=x[0];let b:number=x[1];let c:string=x[2];let n:3=x.length;")]
    [InlineData("const tail:readonly [number,string]=[21,\"go\"];const x=[\"first\",...tail] as const;let a:\"first\"=x[0];let b:number=x[1];")]
    [InlineData("const left=[21] as const;const right=[\"go\"] as const;const x=[...left,...right] as const;let a:21=x[0];let b:\"go\"=x[1];")]
    [InlineData("const tail=[[21],{value:42}] as const;const x=[true,...tail] as const;let a:21=x[1][0];let b:42=x[2].value;")]
    [InlineData("let tail:[number,string]=[21,\"go\"];const x=[...tail] as const;type Shape=typeof x;let copy:Shape=[21,\"go\"];")]
    [InlineData("function take(x:readonly [true,number,string]):number{return x[1];}let tail:[number,string]=[21,\"go\"];let result:number=take([true,...tail] as const);")]
    [InlineData("let tail:[number,string]=[21,\"go\"];const x={data:[...tail] as const} as const;let n:2=x.data.length;")]
    public void ConstTupleSpreadsPreserveExactSlots(string source) =>
        Assert.Empty(Compilation.Check("const-tuple-spreads.ts",source));

    [Theory]
    [InlineData("const tail=[21,\"go\"] as const;const x=[true,...tail] as const;x[1]=22;", "TS2540")]
    [InlineData("const tail=[21,\"go\"] as const;const x=[true,...tail] as const;let bad= x[3];", "TS2493")]
    [InlineData("let tail:[number,string]=[21,\"go\"];const x=[true,...tail] as const;let bad:22=x[1];", "TS2322")]
    [InlineData("let tail:[number,string]=[21,\"go\"];const x=[true,...tail] as const;let bad:readonly [true,string,string]=x;", "TS2322")]
    [InlineData("let tail:[number,string]=[21,\"go\"];const x=[true,...tail] as const;let bad:[true,number,string]=x;", "TS4104")]
    [InlineData("const left=[21] as const;const right=[\"go\"] as const;const x=[...left,...right] as const;let bad:\"go\"=x[0];", "TS2322")]
    [InlineData("const tail=[[21]] as const;const x=[...tail] as const;x[0][0]=22;", "TS2540")]
    [InlineData("let tail:[number,string]=[21,\"go\"];const x=[...tail] as const;let bad:readonly [number,boolean]=x;", "TS2322")]
    public void ConstTupleSpreadsRejectUnsafeOperations(string source,string code) =>
        Assert.Contains(Compilation.Check("const-tuple-spreads-invalid.ts",source),d=>d.Code==code);

    [Fact]
    public void ConstTupleSpreadBoundIsIndependentOfElementOrder()
    {
        string tail=string.Join(",",Enumerable.Repeat("0",32));
        string prefix=$"const tail=[{tail}] as const;";
        Assert.Empty(Compilation.Check("const-tuple-exact-limit.ts",
            prefix+"const exact=[...tail] as const;let n:32=exact.length;"));
        Assert.Contains(Compilation.Check("const-tuple-limit-suffix.ts",
            prefix+"const tooMany=[...tail,0] as const;"),
            diagnostic=>diagnostic.Code=="TSCS1001");
        Assert.Contains(Compilation.Check("const-tuple-limit-prefix.ts",
            prefix+"const tooMany=[0,...tail] as const;"),
            diagnostic=>diagnostic.Code=="TSCS1001");
    }

    [Fact]
    public void ConstTupleSpreadsDoNotPretendAnUnknownLengthIsFinite()
    {
        const string source="let tail:number[]=[21,42];const snapshot=[true,...tail] as const;";
        Assert.Contains(Compilation.Check("const-tuple-unbounded.ts",source),d=>d.Code=="TSCS1001");
    }
}


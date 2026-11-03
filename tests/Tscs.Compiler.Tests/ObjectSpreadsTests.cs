using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ObjectSpreadsTests
{
    [Theory]
    [InlineData("let base:{value:number;label:string}={value:21,label:\"ok\"}; let merged:{value:number;label:string}={...base,value:42};")]
    [InlineData("let x={...{value:20},...{value:22}}; let n:number=x.value;")]
    [InlineData("let x={...{nested:{value:21}}}; let n:number=x.nested.value;")]
    [InlineData("let x={...{value:20},label:\"ok\"}; let n:number=x.value; let s:string=x.label;")]
    [InlineData("let x={...{value:20},value:42}; x.value=21;")]
    [InlineData("let x:{value:number}={...{value:21,extra:true}};")]
    [InlineData("let x={...{},value:42}; let n:number=x.value;")]
    [InlineData("function box():{value:number}{return {value:21};} let x={...box(),value:42};")]
    [InlineData("let x={...{value:21},...{label:\"ok\"}}; let n:number=x.value;")]
    public void CheckedSpreadsInferOrderedStructuralProperties(string source)=>
        Assert.Empty(Compilation.Check("object-spreads.ts",source));

    [Theory]
    [InlineData("let x={...42};","TSCS1001")]
    [InlineData("let x={...true};","TSCS1001")]
    [InlineData("let x={...\"bad\"};","TSCS1001")]
    [InlineData("let x={...[1,2]};","TSCS1001")]
    [InlineData("let x={...missing};","TS2304")]
    [InlineData("let x:{value:number}={...{value:\"bad\"}};","TS2322")]
    [InlineData("let x:{value:number}={...{value:20},value:\"bad\"};","TS2322")]
    [InlineData("let x:{value:number}={...{value:21},extra:true};","TS2353")]
    [InlineData("let x={...};","TS1109")]
    public void InvalidObjectSpreadsNeverCountAsImplemented(string source,string code)=>
        Assert.Contains(Compilation.Check("object-spreads.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("let overlay:{n?:string}={};let v={n:21,...overlay};let x:number|string=v.n;")]
    [InlineData("let a:{n?:number}={};let b:{n?:string}={};let v={...a,...b};let x:number|string|undefined=v.n;")]
    [InlineData("let a:{n?:number}={};let b:{n?:string}={};let v={n:true,...a,...b};let v2={...v,n:false};let x:boolean=v2.n;")]
    [InlineData("let first:{n?:number}={};let last:{n?:string;tag?:boolean}={};let v={n:true,...first,...last};let x:boolean|number|string=v.n;let t:boolean|undefined=v.tag;")]
    [InlineData("type Patch={n?:string};function merge(v:Patch):{n:number|string}{return {n:21,...v};}let x:number|string=merge({n:\"a\"}).n;")]
    [InlineData("let patch:{n?:string}={};let v={inner:{n:21,...patch}};let x:number|string=v.inner.n;")]
    [InlineData("let a:{n?:number}={};let b:{n?:number}={};let v={n:21,...a,...b};let x:number=v.n;")]
    [InlineData("type Item={score:number};let patch:{item?:Item}={};let v={item:{score:21},...patch};let x:number=v.item.score;")]
    [InlineData("let patch:{n?:string|undefined}={};let v={n:21,...patch};let x:number|string=v.n;")]
    public void OptionalObjectSpreadOverlapPreservesPreviousValues(string source) =>
        Assert.Empty(Compilation.Check("optional-spread-overlap.ts",source));

    [Theory]
    [InlineData("let overlay:{n?:string}={};let v={n:21,...overlay};let bad:number=v.n;", "TS2322")]
    [InlineData("let a:{n?:number}={};let b:{n?:string}={};let v={...a,...b};let bad:boolean=v.n;", "TS2322")]
    [InlineData("let patch:{n?:number}={};let v={n:\"ok\",...patch};let bad:string=v.n;", "TS2322")]
    [InlineData("let overlay:{n?:string}={};let v={n:21,...overlay};v.n=true;", "TS2322")]
    [InlineData("let a:{n?:number}={};let b:{n?:string}={};let v={...a,...b};let bad:{n:number|string}=v;", "TS2322")]
    [InlineData("let patch:{n?:string}={};let v={...patch,n:21};let bad:string=v.n;", "TS2322")]
    [InlineData("let patch:{n?:string}={};let v={outer:{n:21,...patch}};let bad:number=v.outer.n;", "TS2322")]
    [InlineData("let a:{n?:number}={};let b:{n?:string}={};let v={...a,...b};let bad:{n?:boolean}=v;", "TS2322")]
    [InlineData("let patch:{n?:string}={n:\"bad\"};let v:{n:21}={n:21,...patch};", "TS2322")]
    public void OptionalObjectSpreadOverlapRejectsIncompatibleTypes(string source,string expected) =>
        Assert.Equal(new[] { expected },Compilation.Check("optional-spread-overlap-invalid.ts",source)
            .Select(d=>d.Code).ToArray());

    [Theory]
    [InlineData("let patch:{n?:string}={};const v={n:21,...patch} as const;let x:21|string=v.n;")]
    [InlineData("let patch:{n?:string}={};const v={n:21,...patch} as const;v.n=\"updated\";")]
    [InlineData("let patch:{n?:string}={};const v={n:21,...patch} as const;v.n=21;")]
    [InlineData("let p:{n?:string}={};let q:{n?:boolean}={};const v={n:21,...p,...q} as const;let x:21|string|boolean=v.n;")]
    [InlineData("let patch:{n?:string}={};const v={n:21,...patch,other:true} as const;let x:21|string=v.n;")]
    [InlineData("let patch:{n?:string|undefined}={};const v={n:21,...patch} as const;let x:21|string=v.n;")]
    [InlineData("function merge(p:{n?:string}):{n:21|string}{return {n:21,...p} as const;}let x:21|string=merge({n:\"x\"}).n;")]
    [InlineData("let patch:{n?:string}={};const v={...patch,n:21} as const;let x:21=v.n;")]
    [InlineData("let patch:{n?:string}={};const v={n:21,...patch,tag:true} as const;let good:21|string=v.n;")]
    public void ConstOptionalSpreadTracksValuesAndConditionalWrites(string source) =>
        Assert.Empty(Compilation.Check("const-optional-overlap.ts",source));

    [Theory]
    [InlineData("let p:{n?:string}={};const v={n:21,...p} as const;let bad:number=v.n;", "TS2322")]
    [InlineData("let p:{n?:string}={};const v={n:21,...p} as const;v.n=22;", "TS2322")]
    [InlineData("let p:{n?:string}={};const v={n:21,...p} as const;v.n=true;", "TS2322")]
    [InlineData("let p:{n?:string}={};const v={n:21,...p} as const;let bad:21=v.n;", "TS2322")]
    [InlineData("let p:{n?:string}={};const v={n:21,...p} as const;let bad:\"x\"=v.n;", "TS2322")]
    [InlineData("let p:{n?:string}={};const v={...p,n:21} as const;v.n=21;", "TS2540")]
    [InlineData("let p:{n?:string}={};const v={n:21,...p} as const;let bad:boolean=v.n;", "TS2322")]
    [InlineData("let p:{n?:string}={};const v={n:21,...p} as const;let bad:{readonly n:21}=v;", "TS2322")]
    [InlineData("let p:{n?:string}={};const v={n:21,...p,tag:true} as const;v.n=21;", "TS2540")]
    public void ConstOptionalSpreadRejectsIncompatibleValuesAndReadonlyWrites(string source,string expected) =>
        Assert.Equal(new[] { expected },Compilation.Check("const-optional-invalid.ts",source)
            .Select(d=>d.Code).ToArray());

    // Optional spreads may join nested mutable and readonly shapes.
    // Reads retain all alternatives; writes must respect any readonly branch.
    [Theory]
    [InlineData("let patch:{item?:{readonly n:number}}={};let v={item:{n:21},...patch};let n:number=v.item.n;")]
    [InlineData("let patch:{item?:{readonly n:string}}={};let v={item:{n:\"ok\"},...patch};let s:string=v.item.n;")]
    [InlineData("type Item={readonly n:number};let patch:{item?:Item}={};let v={item:{n:21},...patch};let n:number=v.item.n;")]
    [InlineData("function build(p:{item?:{readonly n:number}}):{item:{readonly n:number}}{return {item:{n:21},...p};}let n:number=build({}).item.n;")]
    [InlineData("let patch:{item?:{readonly n:number}}={};let v={item:{n:21},...patch};let copy:{item:{readonly n:number}}=v;")]
    [InlineData("let patch:{item?:{readonly n:number}}={};let v={item:{n:21},...patch} as const;let n:number=v.item.n;")]
    [InlineData("let patch:{item?:{readonly n:number}}={};let v={item:{n:21},...patch,tag:true};let n:number=v.item.n;")]
    [InlineData("let a:{item?:{readonly n:number}}={};let b:{item?:{n:number}}={};let v={item:{n:21},...a,...b};let n:number=v.item.n;")]
    [InlineData("let patch:{item?:{readonly n:number}}={};let v={item:{n:21},...patch};let x={...v};let n:number=x.item.n;")]
    public void OptionalObjectSpreadsRetainNestedReadonlyTypes(string source) =>
        Assert.Empty(Compilation.Check("optional-nested-readonly.ts",source));

    [Theory]
    [InlineData("let patch:{item?:{readonly n:number}}={};let v={item:{n:21},...patch};v.item.n=22;", "TS2540")]
    [InlineData("let patch:{item?:{readonly n:string}}={};let v={item:{n:21},...patch};let x:number=v.item.n;", "TS2322")]
    [InlineData("let patch:{item?:{readonly n:number}}={};let v={item:{n:21},...patch};let x:string=v.item.n;", "TS2322")]
    [InlineData("let a:{item?:{readonly n:number}}={};let b:{item?:{n:number}}={};let v={item:{n:21},...a,...b};v.item.n=2;", "TS2540")]
    [InlineData("let patch:{item?:{readonly n:number;m:string}}={};let v={item:{n:21,m:\"ok\"},...patch};v.item.n=2;", "TS2540")]
    [InlineData("let patch:{item?:{readonly n:number}}={};let v={item:{n:21},...patch} as const;v.item.n=22;", "TS2540")]
    [InlineData("let patch:{item?:{readonly n:number}}={};let v={...patch,item:{n:21}} as const;v.item.n=22;", "TS2540")]
    [InlineData("let patch:{item?:{readonly n:number}}={};let v={item:{n:21},...patch};let bad:{item:{n:string}}=v;", "TS2322")]
    [InlineData("let patch:{item?:{readonly n:number}}={};let v={item:{n:21},...patch};v.item.n=\"bad\";", "TS2540")]
    public void OptionalNestedReadonlySpreadsRejectUnsafeWritesAndTypes(string source,string code) =>
        Assert.Equal(new[] {code},Compilation.Check("optional-nested-readonly-invalid.ts",source)
            .Select(d=>d.Code).ToArray());

    // Regression for mixed union/non-union members: the later optional
    // shape may contain a readonly nested alternative that must survive.
    [Fact]
    public void NestedUnionReadonlyAlternativeCannotBeLost() =>
        Assert.Equal(new[] {"TS2540"},Compilation.Check("readonly-union-invalid.ts",
            "let patch:{item?:{child:{n:number}|{readonly n:number}}}={};"+
            "let v={item:{child:{n:21}},...patch};v.item.child.n=22;")
            .Select(d=>d.Code).ToArray());

    // Readonly identities are important when deduplicating unions, but
    // mutually assignable intersection members must remain compatible.
    [Fact]
    public void NestedReadonlyIntersectionRemainsCompatible() =>
        Assert.Empty(Compilation.Check("readonly-intersection.ts",
            "type Both={item:{n:number}}&{item:{readonly n:number}};"+
            "let v:Both={item:{n:1}};let n:number=v.item.n;"));

    // Optional overlapping numeric/string keys describe one JS property.
    // TypeScript synthesizes a string-literal keyof key for a conditional
    // override; later required properties still replace the merged key.
    [Theory]
    [InlineData("let p:{0?:string}={};let v={\"0\":21,...p};let x:number|string=v[0];let k:keyof typeof v=\"0\";")]
    [InlineData("let p:{\"0\"?:string}={};let v={0:21,...p};let x:number|string=v[0];let k:keyof typeof v=\"0\";")]
    [InlineData("let p:{0?:string}={};let v={0:21,...p};let k:keyof typeof v=\"0\";")]
    [InlineData("let p:{\"0\"?:string}={};let v={\"0\":21,...p};let k:keyof typeof v=\"0\";")]
    [InlineData("let p:{0?:number}={};let v={0:21,...p};let k:keyof typeof v=\"0\";v[0]=22;")]
    [InlineData("let p:{0?:string}={};let v={0:21,...p,tag:true};let k:keyof typeof v=\"0\";let n:number|string=v[0];")]
    [InlineData("let p:{0?:string}={};let v={0:21,...p,...{tail:true}};let k:keyof typeof v=\"0\";")]
    [InlineData("type Patch={0?:string};function combine(p:Patch):{0:number|string}{return {0:21,...p};}let n:number|string=combine({0:\"x\"})[0];")]
    public void OptionalNumericStringSpreadKeysRetainReadsAndKeyof(string source) =>
        Assert.Empty(Compilation.Check("optional-numeric-keys.ts",source));

    [Theory]
    [InlineData("let p:{0?:string}={};let v={\"0\":21,...p};let k:keyof typeof v=0;")]
    [InlineData("let p:{\"0\"?:string}={};let v={0:21,...p};let k:keyof typeof v=0;")]
    [InlineData("let p:{0?:string}={};let v={0:21,...p};let k:keyof typeof v=0;")]
    [InlineData("let p:{0?:string}={};let v={0:21,...p};v[0]=true;")]
    [InlineData("let p:{\"0\"?:string}={};let v={0:21,...p};let n:number=v[0];")]
    [InlineData("let p:{0?:string}={};let v={\"0\":21,...p};let n:string=v[0];")]
    [InlineData("let p:{0?:string}={};let v={0:21,...p};let k:0=\"0\" as keyof typeof v;")]
    [InlineData("let p:{0?:string}={};let v={0:21,...p};let x:{0:boolean}=v;")]
    public void OptionalNumericStringSpreadKeysRejectWrongTypesAndKeys(string source) =>
        Assert.Equal(new[] {"TS2322"},Compilation.Check("optional-numeric-keys-invalid.ts",source)
            .Select(d=>d.Code).ToArray());


    // Computed numeric key origin is different from plain numeric spelling.
    // Propagate it through an intermediate spread instead of regressing keyof.
    [Theory]
    [InlineData("let p:{0?:string}={};let v={[0]:21,...p};let k:keyof typeof v=0;")]
    [InlineData("let p:{0?:string}={};let first={[0]:21};let v={...first,...p};let k:keyof typeof v=0;")]
    public void ComputedNumericKeysKeepKeyofAfterOptionalSpread(string source) =>
        Assert.Empty(Compilation.Check("computed-numeric-overlap.ts",source));

    [Theory]
    [InlineData("let p:{0?:string}={};let v={[0]:21,...p};let k:keyof typeof v=\"0\";")]
    [InlineData("let p:{0?:string}={};let first={[0]:21};let v={...first,...p};let k:keyof typeof v=\"0\";")]
    public void ComputedNumericKeysRejectStringKeyofAfterOptionalSpread(string source) =>
        Assert.Equal(new[] {"TS2322"},Compilation.Check("computed-numeric-overlap-invalid.ts",source)
            .Select(d=>d.Code).ToArray());

    [Theory]
    [InlineData("let flag=true;let base=flag?{[0]:21}:{0:21};let v={...base};let n:number=v[0];")]
    [InlineData("let flag=true;let base=flag?{[0]:21}:{0:21};let p:{0?:string}={};let v={...base,...p};let n:number|string=v[0];")]
    public void ComputedNumericProvenanceUnionCanStillSpread(string source) =>
        Assert.Empty(Compilation.Check("computed-union-spread.ts",source));

    [Fact]
    public void ComputedNumericProvenanceSurvivesIntersectionFlattening() =>
        Assert.Empty(Compilation.Check("computed-intersection-spread.ts",
            "const first={[0]:21};type Both=typeof first & {};let a:Both=first;"+
            "let p:{0?:string}={};const v={...a,...p} as const;let k:keyof typeof v=0;"));

    // One differing member may be joined across otherwise identical union
    // arms; multi-member correlated differences remain unsupported.
    [Theory]
    [InlineData("type V={value:number;tag:boolean}|{value:string;tag:boolean};function f(x:V):void{let y={...x};let n:number|string=y.value;let b:boolean=y.tag;}")]
    [InlineData("function f(x:{value:number;tag:string}|{value:string;tag:string}):{value:number|string;tag:string}{return {...x};}")]
    [InlineData("type V={inner:{readonly n:number};tag:boolean}|{inner:{n:number};tag:boolean};function f(x:V):void{let y={...x};let n:number=y.inner.n;}")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value:string;tag:boolean}):void{const y={...x} as const;let v:number|string=y.value;}")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value:string;tag:boolean}):void{const y={...x,value:42};let v:number=y.value;}")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value:string;tag:boolean}):void{const y={...x,tail:true};let v:number|string=y.value;}")]
    [InlineData("function f(x:{0:number;tag:boolean}|{0:string;tag:boolean}):void{let y={...x};let v:number|string=y[0];let k:keyof typeof y=0;}")]
    [InlineData("function f(x:{value:number;optional?:string}|{value:string;optional?:string}):void{let y={...x};let v:number|string=y.value;let opt:string|undefined=y.optional;}")]
    public void FiniteSingleVaryingUnionSpreadsPreserveValues(string source) =>
        Assert.Empty(Compilation.Check("finite-union-spread.ts",source));

    [Theory]
    [InlineData("function f(x:{value:number;tag:boolean}|{value:string;tag:boolean}):void{let y={...x};let bad:boolean=y.value;}", "TS2322")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value:string;tag:boolean}):void{let y={...x};let bad:number=y.value;}", "TS2322")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value:string;tag:boolean}):void{let y={...x};let bad:string=y.value;}", "TS2322")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value:string;tag:boolean}):void{let y={...x};y.value=true;}", "TS2322")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value:string;tag:boolean}):void{let y={...x} as const;y.value=42;}", "TS2540")]
    [InlineData("function f(x:{inner:{readonly n:number};tag:boolean}|{inner:{n:number};tag:boolean}):void{let y={...x};y.inner.n=42;}", "TS2540")]
    [InlineData("function f(x:{0:number;tag:boolean}|{0:string;tag:boolean}):void{let y={...x};let bad:boolean=y[0];}", "TS2322")]
    [InlineData("function f(x:{value:number;optional?:string}|{value:string;optional?:string}):void{let y={...x};let bad:string=y.optional;}", "TS2322")]
    public void FiniteSingleVaryingUnionSpreadsRejectBadTypes(string source,string expected) =>
        Assert.Equal(new[] {expected},Compilation.Check("finite-union-spread-invalid.ts",source)
            .Select(d=>d.Code).ToArray());


    // Nullable/undefined object-spread operands are absent at runtime,
    // making every supplied key optional. The existing optional-overlap
    // algorithm must keep a required preceding key, and a later key wins.
    [Theory]
    [InlineData("function f(x:{value:number}|null):void{let y={...x};let n:number|undefined=y.value;}")]
    [InlineData("function f(x:{value:number}|undefined):void{let y={...x};let n:number|undefined=y.value;}")]
    [InlineData("function f(x:{value:number}|null|undefined):void{let y={...x};let n:number|undefined=y.value;}")]
    [InlineData("function f(x:{value:number}|null):void{let y={value:21,...x};let n:number=y.value;}")]
    [InlineData("function f(x:{value:number}|null):void{let y={...x,value:42};let n:number=y.value;}")]
    [InlineData("function f(x:{value:number;tag:boolean}|null):void{let y={...x};let n:number|undefined=y.value;let b:boolean|undefined=y.tag;}")]
    [InlineData("function f(x:{value:number}|null):void{const y={...x} as const;let n:number|undefined=y.value;}")]
    [InlineData("function f(x:{value?:string}|null):void{let y={...x};let n:string|undefined=y.value;}")]
    public void NullableFiniteObjectSpreadsInferOptionalMembers(string source) =>
        Assert.Empty(Compilation.Check("nullable-object-spread.ts",source));

    [Theory]
    [InlineData("function f(x:{value:number}|null):void{let y={...x};let bad:number=y.value;}", "TS2322")]
    [InlineData("function f(x:{value:number}|undefined):void{let y={...x};let bad:string=y.value;}", "TS2322")]
    [InlineData("function f(x:{value:number}|null|undefined):void{let y={...x};let bad:boolean=y.value;}", "TS2322")]
    [InlineData("function f(x:{value:number}|null):void{const y={...x} as const;y.value=42;}", "TS2540")]
    [InlineData("function f(x:{value:number}|null):void{let y={...x};let bad:{value:string}=y;}", "TS2322")]
    [InlineData("function f(x:{value:number}|null):void{let y:{value:number}={...x};}", "TS2322")]
    [InlineData("function f(x:{value:number;tag:boolean}|null):void{let y={...x};let bad:boolean=y.tag;}", "TS2322")]
    [InlineData("function f(x:{0:number}|null):void{let y={...x};let k:keyof typeof y=0;}", "TS2322")]
    public void NullableFiniteObjectSpreadsRejectUnsafeReadsAndWrites(string source,string code) =>
        Assert.Equal(new[] {code},Compilation.Check("nullable-object-spread-invalid.ts",source)
            .Select(d=>d.Code).ToArray());

    [Theory]
    [InlineData("function f(x:{value:number;tag:boolean}|{value:string;tag:string}):void{let y={...x};}")]
    [InlineData("function f(x:{a:number}|{b:string}):void{let y={...x};}")]
    public void CorrelatedOrIncompleteUnionSpreadsRemainExplicitlyUnsupported(string source) =>
        Assert.Contains(Compilation.Check("union-spread-unimplemented.ts",source),
            d=>d.Code=="TSCS1001" && d.Message.Contains("Object spread requires"));


    // One member can vary in both type and optionality; all other keys
    // retain their requiredness. Correlated changes still reject.
    [Theory]
    [InlineData("function f(x:{value:number;tag:boolean}|{value?:number;tag:boolean}):void{let y={...x};let n:number|undefined=y.value;let b:boolean=y.tag;}")]
    [InlineData("function f(x:{value?:number;tag:boolean}|{value:number;tag:boolean}):void{let y={...x};let n:number|undefined=y.value;}")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value?:string;tag:boolean}):void{let y={...x};let n:number|string|undefined=y.value;}")]
    [InlineData("function f(x:{value?:number;tag:boolean}|{value:string;tag:boolean}):void{let y={...x};let n:number|string|undefined=y.value;}")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value?:number;tag:boolean}):void{let y={...x,value:42};let n:number=y.value;}")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value?:string;tag:boolean}):void{let y={value:21,...x};let n:number|string=y.value;}")]
    [InlineData("function f(x:{0:number;tag:boolean}|{0?:string;tag:boolean}):void{let y={...x};let n:number|string|undefined=y[0];}")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value?:number;tag:boolean}):void{const y={...x} as const;let n:number|undefined=y.value;}")]
    public void FiniteUnionSpreadJoinsOneOptionalMember(string source) =>
        Assert.Empty(Compilation.Check("optional-member-union-spread.ts",source));

    [Theory]
    [InlineData("function f(x:{value:number;tag:boolean}|{value?:number;tag:boolean}):void{let y={...x};let bad:number=y.value;}", "TS2322")]
    [InlineData("function f(x:{value?:number;tag:boolean}|{value:number;tag:boolean}):void{let y={...x};let bad:boolean=y.value;}", "TS2322")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value?:string;tag:boolean}):void{let y={...x};let bad:number=y.value;}", "TS2322")]
    [InlineData("function f(x:{value?:number;tag:boolean}|{value:string;tag:boolean}):void{let y={...x};let bad:string=y.value;}", "TS2322")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value?:number;tag:boolean}):void{let y={...x};y.value=true;}", "TS2322")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value?:number;tag:boolean}):void{const y={...x} as const;y.value=42;}", "TS2540")]
    [InlineData("function f(x:{0:number;tag:boolean}|{0?:string;tag:boolean}):void{let y={...x};let bad:boolean=y[0];}", "TS2322")]
    [InlineData("function f(x:{value:number;tag:boolean}|{value?:number;tag:boolean}):void{let y={...x};let bad:string=y.tag;}", "TS2322")]
    public void FiniteUnionSpreadOptionalMemberRejectsUnsafeUse(string source,string code) =>
        Assert.Equal(new[] {code},Compilation.Check("optional-member-union-spread-invalid.ts",source)
            .Select(d=>d.Code).ToArray());

    [Fact]
    public void FiniteUnionSpreadDoesNotEraseTwoIndependentOptionalDifferences() =>
        Assert.Contains(Compilation.Check("correlated-optional-union.ts",
            "function f(x:{value:number;tag?:string}|{value?:number;tag:string}):void{let y={...x};}"),
            d=>d.Code=="TSCS1001" && d.Message.Contains("Object spread requires"));

}

using Xunit;
using Tscs;
namespace Tscs.Tests;

public class DeclarationParserTests
{
    private static AmbientDeclarationFile Clean(string text)
    {
        var result=DeclarationParser.Parse("lib.test.d.ts",text);
        Assert.Empty(result.Diagnostics);
        return result;
    }
    private static T Single<T>(string text) where T:AmbientStatement=>Assert.IsType<T>(Assert.Single(Clean(text).Statements));
    private static AmbientType AliasType(string text)=>Single<AmbientTypeAlias>(text).Type;

    [Fact]
    public void ParsesInterfaceMembersOfEveryKind()
    {
        var face=Single<AmbientInterface>("""
            interface ArrayLike<T> extends Base<T>, Other {
                readonly length: number;
                readonly [n: number]: T;
                (value?: any): boolean;
                new <U>(...items: U[]): U[];
                toString(): string
                at?(index: number): T | undefined,
                [Symbol.iterator](): Iterator<T>;
                "quoted": string;
                0: number;
                get size(): number;
                set size(value: number);
                readonly: boolean;
                get(): void;
                new: number;
                default: string;
            }
            """);
        Assert.Equal("ArrayLike",face.Name.Text);
        Assert.Equal(["Base","Other"],face.Bases.Select(b=>b.Name[0].Text));
        Assert.Collection(face.Members,
            m=>Assert.True(Assert.IsType<AmbientPropertySignature>(m).Readonly),
            m=>Assert.True(Assert.IsType<AmbientIndexSignature>(m).Readonly),
            m=>Assert.False(Assert.IsType<AmbientCallSignature>(m).Construct),
            m=>
            {
                var construct=Assert.IsType<AmbientCallSignature>(m);
                Assert.True(construct.Construct);
                Assert.Equal("U",Assert.Single(construct.Signature.TypeParameters).Name.Text);
                Assert.True(Assert.Single(construct.Signature.Parameters).Rest);
            },
            m=>Assert.Equal("toString",Assert.IsType<AmbientMethodSignature>(m).Name.Text),
            m=>Assert.True(Assert.IsType<AmbientMethodSignature>(m).Optional),
            m=>Assert.Equal("[Symbol.iterator]",Assert.IsType<AmbientMethodSignature>(m).Name.Text),
            m=>Assert.Equal("quoted",Assert.IsType<AmbientPropertySignature>(m).Name.Text),
            m=>Assert.Equal("0",Assert.IsType<AmbientPropertySignature>(m).Name.Text),
            m=>Assert.False(Assert.IsType<AmbientAccessorSignature>(m).Setter),
            m=>Assert.True(Assert.IsType<AmbientAccessorSignature>(m).Setter),
            m=>Assert.False(Assert.IsType<AmbientPropertySignature>(m).Readonly),
            m=>Assert.Equal("get",Assert.IsType<AmbientMethodSignature>(m).Name.Text),
            m=>Assert.Equal("new",Assert.IsType<AmbientPropertySignature>(m).Name.Text),
            m=>Assert.Equal("default",Assert.IsType<AmbientPropertySignature>(m).Name.Text));
    }

    [Fact]
    public void ParsesAmbientStatements()
    {
        var file=Clean("""
            /// <reference no-default-lib="true"/>
            /// <reference lib="es2015.core" />
            /// ECMAScript APIs
            /////////////////////////////
            declare var NaN: number;
            declare const x: unique symbol;
            declare let y;
            declare function parseInt(string: string, radix?: number): number;
            declare function assert(value: unknown): asserts value;
            declare namespace Intl.Inner { interface A {} type B = A; var c: B; function d(): void; }
            declare global { interface G {} }
            export declare type Exported = string;
            export {};
            type Upper<S extends string> = intrinsic;
            ;
            """);
        Assert.Equal([("no-default-lib","true"),("lib","es2015.core")],file.ReferenceDirectives);
        Assert.Collection(file.Statements,
            s=>Assert.Equal("var",Assert.IsType<AmbientVariable>(s).Keyword.Text),
            s=>Assert.Equal("unique",Assert.IsType<AmbientTypeOperator>(Assert.IsType<AmbientVariable>(s).Type).Operator.Text),
            s=>Assert.Null(Assert.IsType<AmbientVariable>(s).Type),
            s=>Assert.Equal(2,Assert.IsType<AmbientFunction>(s).Signature.Parameters.Count),
            s=>Assert.True(Assert.IsType<AmbientTypePredicate>(Assert.IsType<AmbientFunction>(s).Signature.ReturnType).Asserts),
            s=>
            {
                var ns=Assert.IsType<AmbientNamespace>(s);
                Assert.Equal(["Intl","Inner"],ns.Name.Select(t=>t.Text));
                Assert.Equal(4,ns.Body.Count);
            },
            s=>Assert.Empty(Assert.IsType<AmbientNamespace>(s).Name),
            s=>Assert.True(Assert.IsType<AmbientTypeAlias>(s).Exported),
            s=>Assert.Empty(Assert.IsType<AmbientExportList>(s).Names),
            s=>Assert.Equal("intrinsic",Assert.IsType<AmbientTypeReference>(Assert.IsType<AmbientTypeAlias>(s).Type).Name[0].Text));
    }

    [Fact]
    public void ParsesConditionalInferAndConstructorTypes()
    {
        var instance=Assert.IsType<AmbientConditionalType>(AliasType(
            "type InstanceType<T extends abstract new (...args: any) => any> = T extends abstract new (...args: any) => infer R ? R : any;"));
        var constructor=Assert.IsType<AmbientFunctionType>(instance.Extends);
        Assert.True(constructor.Constructor && constructor.Abstract);
        Assert.Equal("R",Assert.IsType<AmbientInferType>(constructor.Signature.ReturnType).Name.Text);

        // In an extends clause the infer constraint is kept and `?` starts the outer branches.
        var kept=Assert.IsType<AmbientConditionalType>(AliasType("type A<T> = T extends infer U extends string ? U : never;"));
        Assert.NotNull(Assert.IsType<AmbientInferType>(kept.Extends).Constraint);
        // Elsewhere a constraint followed by `?` is rewound: it is a nested conditional.
        var tuple=Assert.IsType<AmbientConditionalType>(AliasType("type B<T> = T extends [(infer U extends string ? 1 : 0)] ? U : never;"));
        var element=Assert.IsType<AmbientParenthesizedType>(Assert.Single(Assert.IsType<AmbientTupleType>(tuple.Extends).Elements).Type);
        Assert.IsType<AmbientConditionalType>(element.Type);

        var awaited=Assert.IsType<AmbientConditionalType>(AliasType("""
            type Awaited<T> = T extends null | undefined ? T :
                T extends object & { then(onfulfilled: infer F, ...args: infer _): any; } ?
                    F extends ((value: infer V, ...args: infer _) => any) ? Awaited<V> : never :
                T;
            """));
        Assert.IsType<AmbientUnionType>(awaited.Extends);
        Assert.IsType<AmbientConditionalType>(awaited.False);
    }

    [Fact]
    public void ParsesMappedTupleAndOperatorTypes()
    {
        var partial=Assert.IsType<AmbientMappedType>(AliasType("type P<T> = { -readonly [K in keyof T as Exclude<K, \"x\">]+?: T[K]; };"));
        Assert.Equal(("-","+"),(partial.ReadonlyModifier,partial.OptionalModifier));
        Assert.IsType<AmbientTypeOperator>(partial.Constraint);
        Assert.NotNull(partial.NameType);
        Assert.IsType<AmbientIndexedAccessType>(partial.Type);
        Assert.Equal("readonly",Assert.IsType<AmbientMappedType>(AliasType("type R<T> = { readonly [P in keyof T]: T[P] };")).ReadonlyModifier);
        Assert.IsType<AmbientTypeLiteral>(AliasType("type L = { [key: string]: number };"));

        var tuple=Assert.IsType<AmbientTupleType>(AliasType("type T = [start: number, end?: number, ...rest: string[], boolean?];"));
        Assert.Collection(tuple.Elements,
            e=>Assert.Equal("start",e.Name?.Text),
            e=>Assert.True(e.Optional),
            e=>Assert.True(e.Rest),
            e=>Assert.True(e.Optional && e.Name is null));

        var array=Assert.IsType<AmbientTypeOperator>(AliasType("type A = readonly string[][];"));
        Assert.IsType<AmbientArrayType>(Assert.IsType<AmbientArrayType>(array.Target).Element);
        Assert.IsType<AmbientLiteralType>(Assert.IsType<AmbientUnionType>(AliasType("type N = | -1 | 0 | \"a\" | true;")).Members[0]);
        Assert.Equal(["globalThis","x"],Assert.IsType<AmbientTypeQuery>(AliasType("type Q = typeof globalThis.x;")).Name.Select(t=>t.Text));
        Assert.IsType<AmbientThisType>(AliasType("type S = this;"));
        Assert.Equal("`a`",Assert.IsType<AmbientLiteralType>(AliasType("type N = `a`;")).Literal.Text);
        Assert.True(Assert.IsType<AmbientTypePredicate>(Assert.IsType<AmbientFunctionType>(
            AliasType("type G = (this: void, value: unknown) => value is string;")).Signature.ReturnType) is {Asserts:false,Type:not null});
    }

    [Theory]
    [InlineData("type A = Array<Array<number>>;")]
    [InlineData("type B = Map<string, Array<Array<number>>>;")]
    [InlineData("type C<T>= T;")]
    [InlineData("type D<T = Array<number>>= T;")]
    public void SplitsGluedGenericCloseTokens(string text)=>Clean(text);

    [Fact]
    public void ParsesBindingPatternParameters()
    {
        var method=Assert.IsType<AmbientMethodSignature>(Assert.Single(Single<AmbientInterface>(
            "interface I { next(...[value]: [] | [number]): void; f({ a, b: [c, , ...d] }: X): void; }").Members.Take(1)));
        var parameter=Assert.Single(method.Signature.Parameters);
        Assert.True(parameter.Rest);
        Assert.Equal(["value"],parameter.PatternNames!.Select(t=>t.Text));
        var second=Assert.IsType<AmbientMethodSignature>(Single<AmbientInterface>(
            "interface I { f({ a, b: [c, , ...d] }: X): void; }").Members[0]);
        Assert.Equal(["a","c","d"],Assert.Single(second.Signature.Parameters).PatternNames!.Select(t=>t.Text));
    }

    [Theory]
    [InlineData("interface A { x: number y: string }","TS1005","';' expected.",24)]
    [InlineData("interface A { `x`: number }","TS1131","Property or signature expected.",14)]
    [InlineData("type A = ;","TS1110","Type expected.",9)]
    [InlineData("interface A { x: number;","TS1005","'}' expected.",24)]
    [InlineData("declare var 1: number;","TS1003","Identifier expected.",12)]
    [InlineData("declare function f(): void {}","TS1183","An implementation cannot be declared in ambient contexts.",27)]
    [InlineData("declare function f(x = 1): void;","TS2371","A parameter initializer is only allowed in a function or constructor implementation.",21)]
    [InlineData("interface A { x = 1 }","TS1246","An interface property cannot have an initializer.",16)]
    [InlineData("interface A { readonly f(): void }","TS1024","'readonly' modifier can only appear on a property declaration or index signature.",24)]
    [InlineData("type A<> = 1;","TS1098","Type parameter list cannot be empty.",7)]
    [InlineData("type A = Array<number;","TS1005","'>' expected.",21)]
    [InlineData("type A = T extends U ? X;","TS1005","':' expected.",24)]
    [InlineData("interface A { , }","TS1131","Property or signature expected.",14)]
    [InlineData("x;","TS1128","Declaration or statement expected.",0)]
    [InlineData("global { }","TS1005","'declare' expected.",0)]
    [InlineData("declare var class: number;","TS1359","Identifier expected. 'class' is a reserved word that cannot be used here.",12)]
    [InlineData("interface while {}","TS1359","Identifier expected. 'while' is a reserved word that cannot be used here.",10)]
    [InlineData("type if = 1;","TS1359","Identifier expected. 'if' is a reserved word that cannot be used here.",5)]
    [InlineData("declare function f(class: number): void;","TS1359","Identifier expected. 'class' is a reserved word that cannot be used here.",19)]
    [InlineData("type T<class> = 1;","TS1359","Identifier expected. 'class' is a reserved word that cannot be used here.",7)]
    [InlineData("type T<A> = A extends infer break ? 1 : 2;","TS1359","Identifier expected. 'break' is a reserved word that cannot be used here.",28)]
    [InlineData("declare namespace if {}","TS1359","Identifier expected. 'if' is a reserved word that cannot be used here.",18)]
    [InlineData("type T = { [K in X]: K, };","TS1005","';' expected.",22)]
    [InlineData("type T = this.x;","TS1005","';' expected.",13)]
    [InlineData("type T = null.x;","TS1005","';' expected.",13)]
    [InlineData("type T = void.x;","TS1005","';' expected.",13)]
    [InlineData("type\nFoo = 1;","TS1128","Declaration or statement expected.",0)]
    [InlineData("interface I { [a: string, b: string]: number }","TS1096","An index signature must have exactly one parameter.",36)]
    [InlineData("class C {}","TS1046","Top-level declarations in .d.ts files must start with either a 'declare' or 'export' modifier.",0)]
    [InlineData("declare class C { m(): void {} }","TS1183","An implementation cannot be declared in ambient contexts.",28)]
    [InlineData("declare class C { (): void }","TS1068","Unexpected token. A constructor, method, accessor, or property was expected.",18)]
    [InlineData("declare class C extends A, B {}","TS1174","Classes can only extend a single class.",25)]
    [InlineData("declare class class {}","TS1359","Identifier expected. 'class' is a reserved word that cannot be used here.",14)]
    [InlineData("declare class C { x: number, y: number }","TS1005","';' expected.",27)]
    [InlineData("type A = `a${}`;","TS1110","Type expected.",13)]
    [InlineData("type A = `a${B C}`;","TS1005","'}' expected.",15)]
    [InlineData("type A = `a${class}`;","TS1359","Identifier expected. 'class' is a reserved word that cannot be used here.",13)]
    [InlineData("var x: number;","TS1046","Top-level declarations in .d.ts files must start with either a 'declare' or 'export' modifier.",0)]
    [InlineData("function f(): void;","TS1046","Top-level declarations in .d.ts files must start with either a 'declare' or 'export' modifier.",0)]
    public void ReportsTheFirstSyntaxError(string text,string code,string message,int start)
    {
        var diagnostic=Assert.Single(DeclarationParser.Parse("a.d.ts",text).Diagnostics);
        Assert.Equal((code,message,start),(diagnostic.Code,diagnostic.Message,diagnostic.Start));
    }

    [Theory]
    [InlineData("declare enum E { A }","Enums")]
    [InlineData("declare const enum E { A }","Enums")]
    [InlineData("import { A } from \"a\";","Imports")]
    [InlineData("declare module \"m\" {}","Ambient module declarations")]
    [InlineData("declare class C { x = 1; }","Class property initializers")]
    [InlineData("declare class C { x!: number; }","Definite assignment assertions")]
    [InlineData("type A = typeof import(\"a\");","Import types")]
    [InlineData("declare const x = 1;","Variable initializers")]
    [InlineData("declare var a: number, b: string;","Multiple variable declarators")]
    [InlineData("export { a as b };","Export aliases")]
    [InlineData("export default A;","Default exports and export assignments")]
    [InlineData("type T = { [K in X]: K; x: 1 };","Mapped types with additional members")]
    [InlineData("type T = Foo<>;","Empty type argument lists")]
    [InlineData("type T = Foo<string,>;","Trailing commas in type arguments")]
    public void RejectsUnmodelledValidSyntaxAsUnsupported(string text,string construct)
    {
        var diagnostic=Assert.Single(DeclarationParser.Parse("a.d.ts",text).Diagnostics);
        Assert.Equal(("TSCS1001",$"{construct} are not supported in declaration files yet."),(diagnostic.Code,diagnostic.Message));
    }

    [Fact]
    public void ParsesAmbientClasses()
    {
        var file=Clean("""
            declare class SafeArray<T = any> {
                private constructor();
                private SafeArray_typekey: SafeArray<T>;
            }
            declare abstract class Iterator<T, TResult = undefined> extends Base<T> implements A, B.C<T> {
                abstract next(value?: T): IteratorResult<T, TResult>;
                static readonly count: number;
                [key: string]: unknown;
                get size(): number;
                static: boolean;
                constructor: unknown;
            }
            """);
        Assert.Collection(file.Statements,
            s=>
            {
                var safe=Assert.IsType<AmbientClass>(s);
                Assert.False(safe.Abstract);
                Assert.IsType<AmbientConstructorDeclaration>(safe.Members[0].Member);
                Assert.Equal(["private"],safe.Members[0].Modifiers);
                Assert.Equal("SafeArray_typekey",Assert.IsType<AmbientPropertySignature>(safe.Members[1].Member).Name.Text);
            },
            s=>
            {
                var iterator=Assert.IsType<AmbientClass>(s);
                Assert.True(iterator.Abstract);
                Assert.Equal("Base",iterator.Base!.Name[0].Text);
                Assert.Equal(["A","B"],iterator.Implements.Select(i=>i.Name[0].Text));
                Assert.Collection(iterator.Members,
                    m=>
                    {
                        Assert.Equal(["abstract"],m.Modifiers);
                        Assert.Equal("next",Assert.IsType<AmbientMethodSignature>(m.Member).Name.Text);
                    },
                    m=>Assert.Equal(["static","readonly"],m.Modifiers),
                    m=>Assert.IsType<AmbientIndexSignature>(m.Member),
                    m=>Assert.IsType<AmbientAccessorSignature>(m.Member),
                    m=>Assert.Equal("static",Assert.IsType<AmbientPropertySignature>(m.Member).Name.Text),
                    m=>Assert.Equal("constructor",Assert.IsType<AmbientPropertySignature>(m.Member).Name.Text));
            });
    }

    [Fact]
    public void ClassStaticModifierMayCrossALineBreak()
    {
        var declaration=Single<AmbientClass>("declare class C { static\nfoo(): void; static: number; readonly value: number; }");
        Assert.Collection(declaration.Members,
            m=>
            {
                Assert.Equal(["static"],m.Modifiers);
                Assert.Equal("foo",Assert.IsType<AmbientMethodSignature>(m.Member).Name.Text);
            },
            m=>
            {
                Assert.Empty(m.Modifiers);
                Assert.Equal("static",Assert.IsType<AmbientPropertySignature>(m.Member).Name.Text);
            },
            m=>Assert.Equal(["readonly"],m.Modifiers));

        // Unlike static, a different modifier with a line break remains a property name.
        var separated=Single<AmbientClass>("declare class C { private\nfoo(): void; }");
        Assert.Collection(separated.Members,
            m=>
            {
                Assert.Empty(m.Modifiers);
                Assert.Equal("private",Assert.IsType<AmbientPropertySignature>(m.Member).Name.Text);
            },
            m=>
            {
                Assert.Empty(m.Modifiers);
                Assert.Equal("foo",Assert.IsType<AmbientMethodSignature>(m.Member).Name.Text);
            });
    }

    [Fact]
    public void StringLiteralConstructorNamesUseDecodedValues()
    {
        var declaration=Single<AmbientClass>("""
            declare class C {
                "constructor"(x: number);
                "con\u0073tructor"(x: string);
                "other"(x: boolean);
                "constructor": string;
            }
            """);
        Assert.Collection(declaration.Members,
            m=>
            {
                Assert.Empty(m.Modifiers);
                Assert.Single(Assert.IsType<AmbientConstructorDeclaration>(m.Member).Signature.Parameters);
            },
            m=>
            {
                Assert.Empty(m.Modifiers);
                Assert.Single(Assert.IsType<AmbientConstructorDeclaration>(m.Member).Signature.Parameters);
            },
            m=>
            {
                Assert.Empty(m.Modifiers);
                Assert.Equal("other",Assert.IsType<AmbientMethodSignature>(m.Member).Name.Text);
            },
            m=>
            {
                Assert.Empty(m.Modifiers);
                Assert.Equal("constructor",Assert.IsType<AmbientPropertySignature>(m.Member).Name.Text);
            });
    }

    [Fact]
    public void ParsesTemplateLiteralTypes()
    {
        var uuid=Assert.IsType<AmbientTemplateLiteralType>(AliasType("type U = `${string}-${string}`;"));
        Assert.Equal("",uuid.Head);
        Assert.Equal(["-",""],uuid.Spans.Select(s=>s.Text));
        var nested=Assert.IsType<AmbientTemplateLiteralType>(AliasType("type N<T extends string> = `a\\`${Uppercase<T>}b${`x${T}` | \"y\"}c`;"));
        Assert.Equal("a\\`",nested.Head);
        Assert.IsType<AmbientTypeReference>(nested.Spans[0].Type);
        Assert.IsType<AmbientTemplateLiteralType>(Assert.IsType<AmbientUnionType>(nested.Spans[1].Type).Members[0]);
        Assert.Equal(["b","c"],nested.Spans.Select(s=>s.Text));
        // Diagnostics inside a substitution keep absolute offsets.
        var inner=Assert.Single(DeclarationParser.Parse("a.d.ts","type A = `${string}${1 +}`;").Diagnostics);
        Assert.Equal(("TS1005",23),(inner.Code,inner.Start));
    }

    [Fact]
    public void AccessorKeywordsMayBeFollowedByALineBreak()
    {
        var members=Single<AmbientInterface>("interface I { get\n x(): number; set\n [k](v: number): void; get\n (): void }").Members;
        Assert.Collection(members,
            m=>Assert.False(Assert.IsType<AmbientAccessorSignature>(m).Setter),
            m=>Assert.Equal("[k]",Assert.IsType<AmbientAccessorSignature>(m).Name.Text),
            m=>Assert.Equal("get",Assert.IsType<AmbientMethodSignature>(m).Name.Text));
    }

    [Fact]
    public void NullIsALiteralTypeAndVoidAKeywordType()
    {
        Assert.Equal(TokenKind.Null,Assert.IsType<AmbientLiteralType>(AliasType("type N = null;")).Literal.Kind);
        Assert.Equal("void",Assert.Single(Assert.IsType<AmbientTypeReference>(AliasType("type V = void;")).Name).Text);
    }

    [Theory]
    [InlineData("type F = ({ a, b }: X) => void;")]
    [InlineData("type F = ([a, [b]]) => void;")]
    [InlineData("type F = ({ a }, b: number) => void;")]
    public void FunctionTypesMayStartWithABindingPattern(string text)=>
        Assert.IsType<AmbientFunctionType>(AliasType(text));

    [Fact]
    public void GluedCloseTokensSurviveTheInferRewind()
    {
        // The rewound constraint parse has already split the glued '>>'.
        var outer=Assert.IsType<AmbientConditionalType>(AliasType(
            "type A<T> = T extends Array<(infer U extends Array<number>) extends 1 ? 1 : 0> ? U : never;"));
        Assert.IsType<AmbientTypeReference>(outer.Extends);
        var nested=Assert.IsType<AmbientConditionalType>(AliasType(
            "type B<T> = T extends [(infer U extends Array<Array<string>> ? 1 : 0)] ? U : never;"));
        var element=Assert.IsType<AmbientParenthesizedType>(Assert.Single(Assert.IsType<AmbientTupleType>(nested.Extends).Elements).Type);
        var inner=Assert.IsType<AmbientConditionalType>(element.Type);
        Assert.Null(Assert.IsType<AmbientInferType>(inner.Check).Constraint);
        Clean("type C<T = Array<Array<number>>>= T;");
    }

    [Fact]
    public void DeepNestingFailsClosedInsteadOfOverflowing()
    {
        var types=DeclarationParser.Parse("a.d.ts","type T = "+new string('(',20_000)+"number"+new string(')',20_000)+";");
        Assert.Equal("Declarations nested deeper than 200 levels are not supported in declaration files yet.",Assert.Single(types.Diagnostics).Message);
        var members=DeclarationParser.Parse("a.d.ts","type T = "+string.Concat(Enumerable.Repeat("{ a: ",20_000))+"number"+new string('}',20_000)+";");
        Assert.Equal("TSCS1001",Assert.Single(members.Diagnostics).Code);
        var namespaces=DeclarationParser.Parse("a.d.ts",string.Concat(Enumerable.Repeat("declare namespace A { ",20_000))+new string('}',20_000));
        Assert.Equal("TSCS1001",Assert.Single(namespaces.Diagnostics).Code);
        var patterns=DeclarationParser.Parse("a.d.ts","declare function f("+new string('[',20_000)+"x"+new string(']',20_000)+": unknown): void;");
        Assert.Equal("TSCS1001",Assert.Single(patterns.Diagnostics).Code);
        var objects=DeclarationParser.Parse("a.d.ts","declare function f("+string.Concat(Enumerable.Repeat("{ a: ",20_000))+"x"+new string('}',20_000)+": unknown): void;");
        Assert.Equal("TSCS1001",Assert.Single(objects.Diagnostics).Code);
        Clean("type T = "+new string('(',50)+"number"+new string(')',50)+";");
    }

    [Fact]
    public void OnlyLeadingReferenceDirectivesAreRecorded()
    {
        var file=Clean("/** header */\n/// <reference lib=\"es5\" />\ninterface A {}\n/// <reference lib=\"missing\" />\ninterface B {}");
        Assert.Equal([("lib","es5")],file.ReferenceDirectives);
        Assert.Equal(2,file.Statements.Count);
    }

    [Fact]
    public void ScannerDiagnosticsStopTheParse()
    {
        // An unterminated string is still a scanner failure; no partial AST
        // may escape the lexical error.
        var lexical=DeclarationParser.Parse("a.d.ts","type A = \"open;");
        Assert.Equal("TS1002",Assert.Single(lexical.Diagnostics).Code);
        Assert.Empty(lexical.Statements);

        // In declaration mode '#x' now has a dedicated PrivateIdentifier
        // token. Outside an ambient class it reaches the binding-name parser,
        // which correctly rejects it as TS1003 rather than a scanner TSCS1001.
        var globalPrivate=DeclarationParser.Parse("a.d.ts","declare var #x: number;");
        Assert.Equal("TS1003",Assert.Single(globalPrivate.Diagnostics).Code);
        Assert.Empty(globalPrivate.Statements);
    }

    [Fact]
    public void DeclarationModeDoesNotChangeOrdinarySourceScanning()
    {
        Assert.Contains(Compilation.Check("a.ts","declare var x: number;"),d=>d.Code=="TSCS1001");
        Assert.Contains(Compilation.Check("a.ts","/// <reference lib=\"es5\" />\nlet x = 1;"),d=>d.Code=="TSCS1001");
    }

    [Fact]
    public void ParsesEcmaPrivateNamesInAmbientClassDeclarations()
    {
        // The pinned lib.es2026.json.d.ts uses #isRawJson on an ambient class.
        var file=Clean("""
            export {};
            declare class RawJSONInstance {
                #isRawJson: unknown;
                static #count: number;
                #make(value: string): string;
                get #value(): string;
                set #value(value: string);
                readonly rawJSON: string;
            }
            declare global { interface RawJSON extends RawJSONInstance {} }
            """);
        var declaration=Assert.IsType<AmbientClass>(file.Statements[1]);
        Assert.Equal("RawJSONInstance",declaration.Name.Text);
        Assert.Collection(declaration.Members,
            m=>
            {
                var property=Assert.IsType<AmbientPropertySignature>(m.Member);
                Assert.Equal("#isRawJson",property.Name.Text);
                Assert.Equal(TokenKind.PrivateIdentifier,property.Name.Tokens[0].Kind);
            },
            m=>
            {
                Assert.Equal(["static"],m.Modifiers);
                Assert.Equal("#count",Assert.IsType<AmbientPropertySignature>(m.Member).Name.Text);
            },
            m=>Assert.Equal("#make",Assert.IsType<AmbientMethodSignature>(m.Member).Name.Text),
            m=>Assert.Equal("#value",Assert.IsType<AmbientAccessorSignature>(m.Member).Name.Text),
            m=>Assert.True(Assert.IsType<AmbientAccessorSignature>(m.Member).Setter),
            m=>Assert.Equal("rawJSON",Assert.IsType<AmbientPropertySignature>(m.Member).Name.Text));
        Assert.IsType<AmbientNamespace>(file.Statements[2]);
    }

    [Theory]
    [InlineData("interface I { #secret: number; }", "TSCS1001")]
    [InlineData("type T = { #secret: number };", "TSCS1001")]
    [InlineData("declare var #secret: number;", "TS1003")]
    [InlineData("declare class C { #1: number; }", "TSCS1001")]
    [InlineData("declare class C { #secret?: number; }", "TS18019")]
    [InlineData("declare class C { private #secret: number; }", "TS18010")]
    [InlineData("declare class C { protected get #secret(): number; }", "TS18010")]
    [InlineData("declare class C { #secret = 1; }", "TSCS1001")]
    public void RejectsIllegalOrOutOfScopePrivateNames(string source,string code)
    {
        var diagnostic=Assert.Single(DeclarationParser.Parse("private.d.ts",source).Diagnostics);
        Assert.Equal(code,diagnostic.Code);
    }
}

using Xunit;
using Tscs;
namespace Tscs.Tests;

public class DeclarationResolverTests
{
    private static IReadOnlyList<Diagnostic> Resolve(params string[] texts)
    {
        var files=texts.Select((t,i)=>
        {
            var parsed=DeclarationParser.Parse($"lib.{i}.d.ts",t);
            Assert.Empty(parsed.Diagnostics);
            return parsed;
        }).ToArray();
        var globals=DeclarationBinder.Bind(files);
        Assert.Empty(globals.Diagnostics);
        return DeclarationResolver.Resolve(files,globals);
    }

    private static (string Code,string Message,int Start,int Length) Single(string text)
    {
        var diagnostic=Assert.Single(Resolve(text));
        Assert.Equal("lib.0.d.ts",diagnostic.File);
        return (diagnostic.Code,diagnostic.Message,diagnostic.Start,diagnostic.Length);
    }

    [Fact]
    public void ResolvesNamesAcrossFilesLikeTheStandardLibrary()
    {
        Assert.Empty(Resolve(
            "interface Array<T> { map<U>(f: (value: T, index: number) => U): U[]; [n: number]: T; }"
            +" interface ArrayConstructor { new <T>(...items: T[]): T[]; } declare var Array: ArrayConstructor;"
            +" type Partial<T> = { [P in keyof T]?: T[P] }; type Uppercase<S extends string> = intrinsic;"
            +" type ReturnType<T extends (...args: any) => any> = T extends (...args: any) => infer R ? R : any;"
            +" interface SymbolConstructor { readonly iterator: unique symbol; } declare var Symbol: SymbolConstructor;"
            +" declare function isNaN(n: number): boolean; type IsNaN = typeof isNaN;",
            "interface Array<T> { [Symbol.iterator](): Iterator<T>; } interface Iterator<T> { next(): { done: boolean, value: T }; }"
            +" declare namespace Intl { interface Collator { compare(x: string, y: string): number; } var Collator: { new (): Collator }; }"
            +" declare namespace Intl { interface Options { collator: Collator; } } type C = Intl.Collator; type Q = typeof Intl.Collator;"
            +" declare function assertIsString(value: unknown, label: typeof value): asserts value is string;"
            +" type Keys = `on${Uppercase<string>}`; declare class Box<T> implements Iterator<T> { next(): { done: boolean, value: T }; }"
            +" type Obj = object | bigint | never | void | undefined | null | symbol;"
            +" interface Window { self: Window & typeof globalThis; } type G = globalThis.Window;"));
    }

    [Fact]
    public void ReportsAnUnknownTypeName()
    {
        const string text="interface A { b: Missing; }";
        Assert.Equal(("TS2304","Cannot find name 'Missing'.",text.IndexOf("Missing",StringComparison.Ordinal),"Missing".Length),Single(text));
    }

    [Theory]
    // A signature's type parameters end with the signature.
    [InlineData("interface A { f<T>(x: T): T; g(): T; }","T","TS2304","Cannot find name 'T'.")]
    // `infer R` is visible only in the true branch of its conditional type.
    [InlineData("type F<T> = T extends infer R ? number : R;","R","TS2304","Cannot find name 'R'.")]
    [InlineData("type F<T> = T extends [infer R, R] ? R : never;","R]","TS2304","Cannot find name 'R'.")]
    // A mapped type's parameter ends with the mapped type.
    [InlineData("type M<T> = { [K in keyof T]: K } | K;","K;","TS2304","Cannot find name 'K'.")]
    // `intrinsic` is a keyword only as the whole body of a type alias.
    [InlineData("type U<S> = S | intrinsic;","intrinsic","TS2304","Cannot find name 'intrinsic'.")]
    [InlineData("declare var v: number; type V = v;","v;","TS2749","'v' refers to a value, but is being used as a type here. Did you mean 'typeof v'?")]
    [InlineData("interface I { } type Q = typeof I;","I;","TS2693","'I' only refers to a type, but is being used as a value here.")]
    [InlineData("type Q = typeof missing;","missing","TS2304","Cannot find name 'missing'.")]
    [InlineData("type Q = Missing.Inner;","Missing","TS2503","Cannot find namespace 'Missing'.")]
    [InlineData("declare namespace N { interface I { } } type Q = N.J;","J;","TS2694","Namespace 'N' has no exported member 'J'.")]
    [InlineData("declare namespace N.M { interface I { } } type Q = N.M.J;","J;","TS2694","Namespace 'N.M' has no exported member 'J'.")]
    [InlineData("interface A { [Missing.iterator](): void; }","Missing","TS2304","Cannot find name 'Missing'.")]
    public void ReportsNamesOutOfScopeOrOfTheWrongMeaning(string text,string at,string code,string message)
    {
        var start=text.LastIndexOf(at,StringComparison.Ordinal);
        Assert.Equal((code,message,start,at.TrimEnd(';',']').Length),Single(text));
    }

    [Fact]
    public void ResolvesNamespaceMembersBeforeGlobals()
    {
        // `Collator` inside Intl is Intl.Collator; outside it is not a global.
        var diagnostics=Resolve("declare namespace Intl { interface Collator { } interface Options { c: Collator; } } type C = Collator;");
        var diagnostic=Assert.Single(diagnostics);
        Assert.Equal(("TS2304","Cannot find name 'Collator'."),(diagnostic.Code,diagnostic.Message));
    }

    [Fact]
    public void ResolvesInsideGlobalAugmentations()
    {
        Assert.Empty(Resolve("interface Window { }","export {}; declare global { interface Document { window: Window; } }"));
        var diagnostic=Assert.Single(Resolve("export {}; declare global { interface Document { a: Absent; } }"));
        Assert.Equal("TS2304",diagnostic.Code);
    }

    [Fact]
    public void ResolvesQualifiedNamesPastShadowingValuesAndMergedVariables()
    {
        // A parameter has no namespace meaning, so `Intl.Collator` still finds the global namespace.
        Assert.Empty(Resolve("declare namespace Intl { interface Collator {} } declare function f(Intl: number): Intl.Collator;"));
        // `globalThis` is one of its own exports.
        Assert.Empty(Resolve("type Q = typeof globalThis.globalThis;"));
        // A non-instantiated namespace merged with a variable: `typeof N.a` reads the variable's property.
        Assert.Empty(Resolve("declare namespace N { interface I {} } declare var N: { a: number }; type Q = typeof N.a;"));
    }

    [Fact]
    public void ReportsEveryUnresolvedNameInSourceOrder()
    {
        Assert.Equal(["A","B","C"],Resolve("interface I extends A { b: B; } declare function f(): C;").Select(d=>d.Message.Split('\'')[1]));
    }
}

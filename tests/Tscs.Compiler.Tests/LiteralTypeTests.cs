using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class LiteralTypeTests
{
    [Theory]
    [InlineData("type State = \"ready\" | \"done\"; let s: State = \"ready\"; s = \"done\";")]
    [InlineData("type N = 21 | 42; let a: N = 42;")]
    [InlineData("type Negative = -1 | 0; let x: Negative = -1;")]
    [InlineData("type Flag = true | false; let x: Flag = false;")]
    [InlineData("type State = \"ready\"; function make(): State { return \"ready\"; } let s: State = make();")]
    [InlineData("interface Task { phase: \"ready\"; value: number } let task: Task = { phase: \"ready\", value: 21 };")]
    [InlineData("type Event = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; let e: Event = { kind: \"ok\", value: 21 };")]
    [InlineData("type Event = { kind: \"ok\"; value: number } | { kind: \"bad\"; message: string }; let e: Event = { kind: \"bad\", message: \"why\" };")]
    [InlineData("interface Box { item: { tag: \"one\"; count: 21 } } let box: Box = { item: { tag: \"one\", count: 21 } };")]
    [InlineData("type State = \"ready\" | \"done\"; let a: State = \"ready\"; let b: State = a;")]
    [InlineData("type Code = \"ab\"; let code: Code = \"a\\u0062\";")]
    [InlineData("type Status = \"ready\" | \"done\"; let s: Status = \"ready\"; if (s === \"ready\") { let tag: Status = s; }")]
    public void LiteralTypesAreResolvedAndCheckedThroughTheCompiler(string code) =>
        Assert.Empty(Compilation.Check("literals.ts", code));

    [Theory]
    [InlineData("type State = \"ready\"; let s: State = \"wrong\";", "TS2322")]
    [InlineData("type N = 21 | 42; let x: N = 0;", "TS2322")]
    [InlineData("type Neg = -1; let x: Neg = -2;", "TS2322")]
    [InlineData("type Flag = true; let x: Flag = false;", "TS2322")]
    [InlineData("interface Task { phase: \"ready\"; value: number } let task: Task = { phase: \"invalid\", value: 21 };", "TS2322")]
    [InlineData("type Event = {kind: \"ok\"; value: number} | {kind: \"bad\"; message: string}; let e: Event = {kind: \"other\", value: 21};", "TS2322")]
    [InlineData("function take(s: \"ready\"): number {return 42;} take(\"wrong\");", "TS2345")]
    [InlineData("function make(): \"ready\" {return \"wrong\";}", "TS2322")]
    [InlineData("let s: string = \"ready\"; let literal: \"ready\" = s;", "TS2322")]
    [InlineData("let xs: (\"ready\" | \"done\")[] = [\"ready\", \"wrong\"];", "TS2322")]
    [InlineData("type Tags = (\"ready\" | \"done\")[]; let xs: Tags = [\"ready\", \"done\"];", "TS2322")]
    [InlineData("type State = \"ready\" | \"done\"; let s: State = \"ready\"; s = \"other\";", "TS2322")]
    [InlineData("type Event = {kind: \"ok\";value: number} | {kind: \"bad\";message: string}; let e: Event = {kind: \"ok\",value: 21}; e.message;", "TS2339")]
    public void IncompatibleLiteralsFailWithoutImplicitAny(string code,string expected) =>
        Assert.Contains(Compilation.Check("wrong.ts",code),d=>d.Code==expected);

    [Theory]
    [InlineData("type X = 1.2;")]
    [InlineData("type X = `template`;")]
    [InlineData("type X = -\"wrong\";")]
    [InlineData("type X = \"yes\" & \"no\";")]
    public void UnsupportedLiteralSyntaxDoesNotSilentlySucceed(string code)=>
        Assert.Contains(Compilation.Check("unsupported.ts",code),d=>d.Code=="TSCS1001");

    [Theory]
    [InlineData("type Flags = true | false; let bool: boolean = true; let flag: Flags = bool;")]
    [InlineData("type Values = number | 21; let n: number = 1; let value: Values = n;")]
    [InlineData("let s: \"ready\" = \"ready\"; let boxed: {} = s;")]
    public void PrimitiveLiteralUnionsAndEmptyObjectRetainSoundWidening(string code) =>
        Assert.Empty(Compilation.Check("literal-widening.ts",code));

    [Theory]
    [InlineData("function twice(x: 21): number { return x * 2; } let n: number = twice(21);")]
    [InlineData("function negate(x: true): boolean { return !x; } let result: boolean = negate(true);")]
    [InlineData("let zero: 0 = -0; let negativeZero: -0 = 0;")]
    [InlineData("function pass(x: boolean): true | false { return x; } let f: boolean = pass(true);")]
    [InlineData("let n: 21 = 21; let data: {} = n;")]
    [InlineData("let xs: { tag: \"ready\" | \"done\"; value: number | string }[] = [{ tag: \"ready\", value: 1 }, { tag: \"done\", value: \"x\" }];")]
    public void LiteralSubtypesAndFreshArrayItemsRetainPrimitiveSemantics(string code) =>
        Assert.Empty(Compilation.Check("literal-extensions.ts",code));

    [Fact]
    public void ReusedLiteralUnionIsAssignableWithoutFreshExpressionSpecialCases()
    {
        const string code="type State = \"ready\" | \"done\"; function pass(x: State): State {return x;} let s: State = \"ready\"; let copy: State = pass(s);";
        Assert.Empty(Compilation.Check("union.ts",code));
    }
}

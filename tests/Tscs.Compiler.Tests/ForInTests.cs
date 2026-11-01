using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded compiler regressions, not original upstream TS7 acceptance.
public sealed class ForInTests
{
    [Theory]
    [InlineData("let data={a:20,b:22};let total:number=0;for(const key in data){if(key===\"a\"){total+=20;}if(key===\"b\"){total+=22;}}")]
    [InlineData("let data={a:1};for(let key in data){let text:string=key;}")]
    [InlineData("let data:number[]=[20,22];for(const key in data){let text:string=key;}")]
    [InlineData("interface Item {a:number;b:number} let item:Item={a:20,b:22};for(const key in item){let text:string=key;}")]
    [InlineData("function count(data:{a:number;b:number}):number{let sum:number=0;for(const key in data){if(key===\"a\"){continue;}sum+=42;}return sum;}")]
    [InlineData("function count(data:{a:number}):number{let n:number=0;for(const key in data){if(key===\"a\"){break;}n++;}return n;}")]
    [InlineData("let data:any={a:42};for(const key in data){let text:string=key;}")]
    [InlineData("let data={in:42};for(const key in data){if(key===\"in\"){break;}}")]
    [InlineData("let key:string=\"outer\";let data={a:1};for(let key in data){let s:string=key;}let saved:string=key;")]
    [InlineData("let data={a:1};let values=[1];for(const key in data){for(const index in values){let a:string=key;let b:string=index;}}")]
    public void BracedForInHasTypedLexicalKeys(string source) =>
        Assert.Empty(Compilation.Check("for-in.ts",source));

    [Theory]
    [InlineData("let data={a:1};for(const key in data){let wrong:number=key;}", "TS2322")]
    [InlineData("for(const key in 42){}", "TS2407")]
    [InlineData("for(let key in true){}", "TS2407")]
    [InlineData("let data={a:1};for(const key in data){let ok:string=key;}let outside:string=key;", "TS2304")]
    [InlineData("let data={a:1};for(const key in data){let wrong:string=1;}", "TS2322")]
    public void InvalidForInFormsNeverCompile(string source,string diagnostic) =>
        Assert.Contains(Compilation.Check("for-in-invalid.ts",source),d=>d.Code==diagnostic);

    [Fact]
    public void UnbracedForInUsesTheCheckedLexicalKey() =>
        Assert.Empty(Compilation.Check("for-in-unbraced.ts",
            "let data={a:1};for(const key in data)key;"));

    [Fact]
    public void UnbracedForInLexicalDeclarationIsExplicitlyUnsupported() =>
        Assert.Contains(Compilation.Check("for-in-unbraced.ts",
            "let data={a:1};for(const key in data)const value=1;"),
            d=>d.Code=="TSCS1001");

    [Fact]
    public void LoopBodyCannotFabricatePostLoopInitialization()
    {
        const string source="let record={a:1};let value:number;for(const key in record){value=42;}let result:number=value;";
        Assert.Contains(Compilation.Check("for-in-flow.ts",source),d=>d.Code=="TSCS1001");
    }
    // Source-backed compiler checks, separate from original TS7 acceptance.
    [Theory]
    [InlineData("type Choice={left:number}|{right:number};function count(x:Choice):number{let n:number=0;for(const k in x){n++;}return n;}")]
    [InlineData("function count(x:{a:number}|number[]):number{let n:number=0;for(const k in x){if(k===\"a\"){n++;}}return n;}")]
    [InlineData("function count(x:{a:number}|readonly number[]):number{let n:number=0;for(const k in x){n++;}return n;}")]
    [InlineData("function count(x:{a:number}|undefined):number{let n:number=0;for(const k in x){n++;}return n;}")]
    [InlineData("function count(x:{a:number}|null):number{let n:number=0;for(const k in x){n++;}return n;}")]
    [InlineData("function count(x:{a:number}|{b:string}|null|undefined):number{let n:number=0;for(const k in x){n++;}return n;}")]
    [InlineData("type A={a:number};type B={b:number};function count(x:A|B|undefined):number{let n:number=0;for(const k in x){n++;}return n;}")]
    [InlineData("function last(x:readonly number[]|number[]):string{let last:string=\"\";for(let k in x){last=k;}return last;}")]
    public void FiniteForInUnionsKeepStringKeys(string source) =>
        Assert.Empty(Compilation.Check("for-in-unions.ts",source));

    [Theory]
    [InlineData("function count(x:{a:number}|string):number{let n:number=0;for(const k in x){n++;}return n;}", "TS2407")]
    [InlineData("function count(x:number[]|number):number{let n:number=0;for(const k in x){n++;}return n;}", "TS2407")]
    [InlineData("function count(x:{a:number}|boolean):number{let n:number=0;for(const k in x){n++;}return n;}", "TS2407")]
    [InlineData("function count(x:unknown):number{let n:number=0;for(const k in x){n++;}return n;}", "TS2407")]
    [InlineData("function count(x:string|null):number{let n:number=0;for(const k in x){n++;}return n;}", "TS2407")]
    [InlineData("function count(x:number|string):number{let n:number=0;for(const k in x){n++;}return n;}", "TS2407")]
    [InlineData("function count(x:{a:number}|{b:number}):number{for(const k in x){let n:number=k;}return 1;}", "TS2322")]
    [InlineData("function count(x:readonly number[]|string):number{let n:number=0;for(const k in x){n++;}return n;}", "TS2407")]
    public void NonObjectForInAlternativesAreRejected(string source,string diagnostic) =>
        Assert.Contains(Compilation.Check("for-in-invalid-unions.ts",source),d=>d.Code==diagnostic);

}

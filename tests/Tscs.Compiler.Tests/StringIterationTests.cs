using Tscs;
using Xunit;

namespace Tscs.Tests;

// Compiler behavior only; not original TypeScript 7 acceptance.
public sealed class StringIterationTests
{
    [Theory]
    [InlineData("let text:string=\"ab\";let out:string=\"\";for(const ch of text){out=out+ch;}")]
    [InlineData("let count:number=0;for(const ch of \"a😀b\"){count++;}")]
    [InlineData("let out:string=\"\";for(let ch of \"abc\"){out=out+ch;}")]
    [InlineData("function collect(s:string):string{let out:string=\"\";for(const ch of s){out=out+ch;}return out;}let r:string=collect(\"abc\");")]
    [InlineData("let words:string[]=[\"ab\",\"cd\"];for(const word of words){for(const ch of word){let copy:string=ch;}}")]
    [InlineData("let s:\"x\"=\"x\";for(const ch of s){let copy:string=ch;}")]
    [InlineData("let s:string=\"abc\";for(const ch of s){if(ch===\"b\"){continue;}if(ch===\"c\"){break;}}")]
    public void StringsAreCheckedAsNativeEs2015Iterables(string source) =>
        Assert.Empty(Compilation.Check("string-forof.ts",source));

    [Theory]
    [InlineData("for(const ch of \"ab\"){ch=\"x\";}","TS2588")]
    [InlineData("for(const ch of \"ab\"){let bad:number=ch;}","TS2322")]
    [InlineData("let s:string=\"ab\";for(let ch of s){ch=42;}","TS2322")]
    [InlineData("for(const ch of \"ab\"){ch.missing;}","TS2339")]
    [InlineData("for(const ch of 42){let copy:number=ch;}","TSCS1001")]
    [InlineData("function invalid(s:string|undefined):number{for(const ch of s){return 1;}return 0;}","TSCS1001")]
    public void BadIterationAndUsesDoNotSucceed(string source,string code) =>
        Assert.Contains(Compilation.Check("string-forof-negative.ts",source),d=>d.Code==code);

    // A union source is iterable iff every alternative is a checked array
    // or string, and a yielded value must retain all alternative element types.
    [Theory]
    [InlineData("function count(source:number[]|string):number{let n:number=0;for(const value of source){let checked:number|string=value;n++;}return n;}let a:number=count(\"a😀\");")]
    [InlineData("function count(source:readonly number[]|string):number{let n:number=0;for(let v of source){let either:number|string=v;n++;}return n;}let a:number=count([3,4]);")]
    [InlineData("function take(source:number[]|readonly number[]):number{let out:number=0;for(const v of source){out=out+v;}return out;}let r:number=take([1,2]);")]
    [InlineData("function glue(source:string[]|string):number{let n:number=0;for(const s of source){let text:string=s;n++;}return n;}let result:number=glue([\"ab\",\"cd\"]);")]
    [InlineData("type Feed=number[]|string;function view(feed:Feed):number{let n:number=0;for(const part of feed){let v:number|string=part;n++;}return n;}let count:number=view([1,2]);")]
    [InlineData("type Blend=string[]|number[];function tally(b:Blend):number{let n:number=0;for(const v of b){let m:number|string=v;n++;}return n;}let count:number=tally([\"aa\"]);")]
    public void ArrayAndStringUnionsJoinIterationElementTypes(string source) =>
        Assert.Empty(Compilation.Check("union-forof.ts",source));

    [Theory]
    [InlineData("function reject(xs:number[]|string):void{for(const v of xs){let wrong:number=v;}}","TS2322")]
    [InlineData("function reject(xs:string[]|number[]):void{for(const v of xs){let wrong:string=v;}}","TS2322")]
    [InlineData("function reject(xs:readonly number[]|string):void{for(const v of xs){let wrong:boolean=v;}}","TS2322")]
    [InlineData("function reject(xs:readonly string[]|number[]):void{for(const v of xs){let wrong:number=v;}}","TS2322")]
    [InlineData("function reject(xs:string|number[]):void{for(const v of xs){v=false;}}","TS2588")]
    [InlineData("function reject(xs:string[]|number[]):void{for(let v of xs){v=false;}}","TS2322")]
    [InlineData("function bad(source:string|undefined):void{for(const x of source){let s:string=x;}}","TSCS1001")]
    [InlineData("function bad(source:number[]|undefined):void{for(const x of source){let n:number=x;}}","TSCS1001")]
    [InlineData("function bad(source:number[]|boolean):void{for(const x of source){let n:number=x;}}","TSCS1001")]
    public void InvalidOrUnsafeUnionIterationFailsClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("union-forof-negative.ts",source),d=>d.Code==code);
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded executable C# compiler regressions, not original TS7 acceptance.
public sealed class NumericMutationLoopTests
{
    [Theory]
    [InlineData("function f(n:number):number { let i:number=0; let sum:number=0; do { sum+=i; i++; } while(i<n); return sum; }")]
    [InlineData("function f(n:number):number { let i:number=0; do { i++; if(i===2){continue;} if(i>n){break;} } while(i<10); return i; }")]
    [InlineData("function f():number { let i:number=1; i--; i++; return i; }")]
    [InlineData("function f():number { let n:number=0; --n; ++n; return n; }")]
    [InlineData("function f():number { let result:number=0; do { result+=1; break; } while(true); return result; }")]
    [InlineData("function f():number { do { return 42; } while(false); }")]
    [InlineData("function f(n:number):number { let count:number=0; for(let i=0;i<n;i+=1){ count+=i; } return count; }")]
    [InlineData("function f(n:number):number { let count:number=0; for(let i=n;i>0;i--){ count+=i; } return count; }")]
    [InlineData("function f(values:number[]):number { let total:number=0; for(let i=0;i<values.length;i++){ values[i]*=2; total+=values[i]; } return total; }")]
    [InlineData("function f():number { let holder={count:1}; holder.count+=2; holder.count*=3; holder.count-=5; return holder.count; }")]
    [InlineData("function f():number { let sum:number=0; let i:number=0; do { i++; switch(i){ case 1: { continue; } default: { break; } } sum+=i; } while(i<2); return sum; }")]
    public void NumericMutationsAndBracedDoWhileCompile(string source)=>
        Assert.Empty(Compilation.Check("numeric-mutation.ts",source));

    [Theory]
    [InlineData("function f():number { const n:number=1; n+=1; return n; }","TS2588")]
    [InlineData("function f():number { const n:number=1; n--; return n; }","TS2588")]
    [InlineData("function f():number { let n:number=0; do { n=\"bad\"; } while(false); return n; }","TS2322")]
    [InlineData("function f():number { let n:number=0; do { let x:string=n; break; } while(false); return n; }","TS2322")]
    [InlineData("function f():number { let n:number=1; n+=false; return n; }","TSCS1001")]
    [InlineData("function f():number { let n:number; n+=1; return 0; }","TSCS1001")]
    [InlineData("function f():number { let xs:number[]=[1]; xs[0]*=\"bad\"; return 0; }","TSCS1001")]
    [InlineData("function f():number { let n:number=0; do { n++; } while(1); return n; }","TSCS1001")]
    [InlineData("function f():number { let n:number=0; --(n + 1); return n; }","TSCS1001")]
    [InlineData("function f():number { let n:number=0; do n++; while(n<2); return n; }","TSCS1001")]
    public void TypeAndGrammarErrorsRemainBlocking(string source,string code)=>
        Assert.Contains(Compilation.Check("invalid-mutation.ts",source),d=>d.Code==code);

    [Fact]
    public void DefiniteReturnInDoBodyDoesNotGainAFakeConditionExit()=>
        Assert.Empty(Compilation.Check("return.ts","function f():number { do { return 42; } while(false); }"));

    [Fact]
    public void DoBreakDoesNotProveFunctionReturn()=>
        Assert.Contains(Compilation.Check("return.ts","function f():number { do { break; } while(true); }"),
            d=>d.Code=="TSCS1001");

    [Fact]
    public void ContinueCannotFabricateConditionInitialization()
    {
        const string source="function f(flag:boolean):number { let value:number; do { if(flag){continue;} value=1; } while(value>0); return 0; }";
        Assert.Contains(Compilation.Check("init.ts",source),d=>d.Code=="TSCS1001");
    }
}

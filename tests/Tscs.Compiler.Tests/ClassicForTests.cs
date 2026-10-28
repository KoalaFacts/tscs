using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ClassicForTests
{
    [Theory]
    [InlineData("function sum(values:number[]):number { let total:number=0; for(let i:number=0;i<values.length;i++){ total=total+values[i]; } return total; }")]
    [InlineData("function sum(values:number[]):number { let total:number=0; for(let i=0;i<values.length;i++){ if(values[i]<0){continue;} total=total+values[i]; } return total; }")]
    [InlineData("function f(n:number):number { let total:number=0; for(let i=0;i<n;i++){ total=total+i; if(total>10){break;} } return total; }")]
    [InlineData("function f(n:number):number { let total:number=0; for(let i=0;i<n;i++){ for(let j=0;j<2;j++){ total=total+1; } } return total; }")]
    [InlineData("function f(n:number):number { let total:number=0; for(let i=0;i<n;i=i+1){ total=total+2; } return total; }")]
    [InlineData("function f(n:number):number { let i:number=0; let result:number=0; for(i=0;i<n;i++){ result=result+i; } return result; }")]
    [InlineData("function f(n:number):number { let total:number=0; for(let i=0;i<n;i++){ switch(i){ case 1: {continue;} default: {break;} } total=total+i; } return total; }")]
    [InlineData("function f(values:number[]):number { let total:number=0; for(let i=0;i<values.length;i++){ total=total+values[i]; } return total; }")]
    [InlineData("function f(n:number):number { for(let i=0;i<n;i++){ if(i>3){break;} } let i:number=42; return i; }")]
    [InlineData("function f(n:number):number { let i:number=0; for(;i<n;i++){ if(i===2){continue;} } return i; }")]
    public void SupportedClassicForProgramsCompile(string source) =>
        Assert.Empty(Compilation.Check("classic.ts",source));

    [Theory]
    [InlineData("function f(n:number):number { for(let i=0;i<n;i++){ let s:string=i; } return n; }", "TS2322")]
    [InlineData("function f(n:number):number { for(let i=0;i<n;i++){} return i; }", "TS2304")]
    [InlineData("function f(n:number):number { for(let i=0;i<n;i=\"bad\"){} return n; }", "TS2322")]
    [InlineData("function f(values:number[]):number { for(let i=0;i<values.length;i++){ values[i]=\"bad\"; } return 0; }", "TS2322")]
    public void TypeErrorsAndLoopBindingLeaksAreRejected(string source,string expected) =>
        Assert.Contains(Compilation.Check("classic.ts",source),d=>d.Code==expected);

    [Theory]
    [InlineData("function f(n:number):number { let sum:number=0; for(let i=0;i<n;i+=1){sum+=i;} return sum; }")]
    [InlineData("function f(n:number):number { let sum:number=0; for(let i=n;i>0;i--){sum+=i;} return sum; }")]
    public void NumericMutationForHeadersNowCompile(string source)=>
        Assert.Empty(Compilation.Check("classic.ts",source));

    [Fact]
    public void ForHeaderLocalCanShadowIntrinsicGlobal()
    {
        const string source="function f():number { for(let globalThis=0;globalThis<1;globalThis++){return globalThis;} return 0; }";
        Assert.Empty(Compilation.Check("classic.ts",source));
    }

    [Fact]
    public void UpdateCannotInitializeABodyReadBeforeFirstIteration()
    {
        const string source="function f():number { let x:number; for(let i=0;i<1;x=1){return x;} return 0; }";
        Assert.Contains(Compilation.Check("classic.ts",source),
            d=>d.Code=="TSCS1001" && d.Message.Contains("Reads before initialization",StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("for(const i=0;i<3;i++){}")]
    [InlineData("for(let i=0;i<3;i++) i++;")]
    [InlineData("for(var i=0;i<3;i++){}")]
    public void UnsupportedVariantsRemainExplicit(string source) =>
        Assert.Contains(Compilation.Check("classic.ts",source),d=>d.Code=="TSCS1001");
}

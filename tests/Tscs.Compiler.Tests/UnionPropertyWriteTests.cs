using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded compiler product regressions, not original TS7 acceptance.
public sealed class UnionPropertyWriteTests
{
    [Theory]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function write(x:Both):number{x.n=42;return x.n;}")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function write(x:Both):number{x['n']=42;return x['n'];}")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function write(x:Both):number{x.n+=2;return x.n;}")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function write(x:Both):number{x.n++;return x.n;}")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function write(x:Both):number{++x.n;return x.n;}")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function write(x:Both):number{x.n ||= 42;return x.n;}")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function write(x:Both):number{x['n']+=2;return x['n'];}")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function write(x:Both):number{x.n ??= 42;return x.n;}")]
    public void CommonRequiredWritableMembersAcceptSafeAssignmentForms(string source) =>
        Assert.Empty(Compilation.Check("union-write.ts",source));

    [Theory]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x.n='bad';return 0;}","TS2322")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x.n=true;return 0;}","TS2322")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x.n+='bad';return 0;}","TS2322")]
    [InlineData("function bad(x:{tag:'a';n:number}|{tag:'b';readonly n:number}):number{x.n=42;return 0;}","TS2540")]
    [InlineData("function bad(x:{tag:'a';n:number}|{tag:'b';missing:string}):number{x.n=42;return 0;}","TS2339")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x['n']='bad';return 0;}","TS2322")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x.n ||= 'bad';return 0;}","TS2322")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x.n ??= 'bad';return 0;}","TS2322")]
    public void InvalidUnionWritesRetainCompilerDiagnostics(string source,string code) =>
        Assert.Contains(Compilation.Check("union-write-invalid.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("function bad(x:{tag:'a';n:number}|{tag:'b';n:string}):number{x.n=42;return 0;}")]
    [InlineData("function bad(x:{tag:'a';n?:number}|{tag:'b';n:number}):number{x.n=42;return 0;}")]
    public void UnmodelledUnionWriteContractsFailClosed(string source) =>
        Assert.Contains(Compilation.Check("union-write-outside.ts",source),d=>d.Code=="TSCS1001");
}

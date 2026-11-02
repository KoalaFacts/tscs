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
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function write(x:Both):number{x['n']++;return x['n'];}")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function write(x:Both):number{++x['n'];return x['n'];}")]
    public void LiteralKeyUnionNumericUpdatesUseCheckedCommonMembers(string source) =>
        Assert.Empty(Compilation.Check("union-index-update.ts",source));

    [Theory]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x.n='bad';return 0;}","TS2322")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x.n=true;return 0;}","TS2322")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x.n+='bad';return 0;}","TS2322")]
    [InlineData("function bad(x:{tag:'a';n:number}|{tag:'b';readonly n:number}):number{x.n=42;return 0;}","TS2540")]
    [InlineData("function bad(x:{tag:'a';n:number}|{tag:'b';missing:string}):number{x.n=42;return 0;}","TS2339")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x['n']='bad';return 0;}","TS2322")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x.n ||= 'bad';return 0;}","TS2322")]
    [InlineData("type Both={tag:'a';n:number}|{tag:'b';n:number};function bad(x:Both):number{x.n ??= 'bad';return 0;}","TS2322")]
    [InlineData("function bad(x:{tag:'a';n:number}|{tag:'b';readonly n:number}):number{x['n']++;return 0;}","TS2540")]
    public void InvalidUnionWritesRetainCompilerDiagnostics(string source,string code) =>
        Assert.Contains(Compilation.Check("union-write-invalid.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("function bad(x:{tag:'a';n:number}|{tag:'b';n:string}):number{x.n=42;return 0;}")]
    [InlineData("function bad(x:{tag:'a';n?:number}|{tag:'b';n:number}):number{x.n=42;return 0;}")]
    [InlineData("function bad(x:{tag:'a';n:any}|{tag:'b';n:number}):number{x.n='bad';return 0;}")]
    [InlineData("function bad(x:{tag:'a';n:{value:any}}|{tag:'b';n:{value:number}}):number{x.n={value:'bad'};return 0;}")]
    [InlineData("function bad(x:{tag:'a';n:any[]}|{tag:'b';n:number[]}):number{x.n=['bad'];return 0;}")]
    public void UnmodelledUnionWriteContractsFailClosed(string source) =>
        Assert.Contains(Compilation.Check("union-write-outside.ts",source),d=>d.Code=="TSCS1001");
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedAliasGraphsHaveBoundedWriteComparison(bool incompatible)
    {
        var source=new System.Text.StringBuilder();
        source.Append("type A0={value:number};type B0={value:")
            .Append(incompatible?"string":"number").Append("};");
        for(var level=1;level<=30;level++)
            source.Append($"type A{level}={{left:A{level-1};right:A{level-1}}};")
                .Append($"type B{level}={{left:B{level-1};right:B{level-1}}};");
        source.Append("function write(x:{tag:'a';n:A30}|{tag:'b';n:B30},value:A30):number{x.n=value;return 0;}");
        var diagnostics=Compilation.Check("union-shared-dag.ts",source.ToString());
        if(incompatible)Assert.Contains(diagnostics,d=>d.Code=="TSCS1001");
        else Assert.Empty(diagnostics);
    }

}

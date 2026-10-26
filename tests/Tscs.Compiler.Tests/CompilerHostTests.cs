using Xunit;
using Tscs;

namespace Tscs.Tests;

public sealed class CompilerHostTests
{
    private sealed class MemoryHost : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.OrdinalIgnoreCase;
        public Dictionary<string,string> Files { get; }=new(StringComparer.OrdinalIgnoreCase);
        public List<string> Reads { get; }=[];

        public string GetCanonicalFileName(string path)
        {
            if(path.StartsWith("./",StringComparison.Ordinal))path=path[2..];
            return "/virtual/"+path;
        }

        public string ReadFile(string canonicalFileName)
        {
            Reads.Add(canonicalFileName);
            return Files[canonicalFileName];
        }
    }

    [Fact]
    public void VirtualProgramSnapshotsMultipleSourcesAndReusesAnalysis()
    {
        var host=new MemoryHost();
        host.Files["/virtual/use.ts"]="let answer: number = shared;";
        host.Files["/virtual/define.ts"]="let shared: number = 42;";
        var program=CompilerProgram.Load(host,["use.ts","define.ts"]);

        Assert.Equal(["/virtual/use.ts","/virtual/define.ts"],program.Sources.Select(s=>s.File));
        var first=program.Analyze();
        Assert.Empty(first.Diagnostics);
        Assert.Same(first,program.Analyze());
        Assert.Equal(["/virtual/use.ts","/virtual/define.ts"],host.Reads);
    }

    [Fact]
    public void HostRejectsAliasesOfTheSameSourceWithoutReadingTwice()
    {
        var host=new MemoryHost();
        host.Files["/virtual/define.ts"]="let value = 1;";
        Assert.Throws<ArgumentException>(()=>CompilerProgram.Load(host,["define.ts","./DEFINE.ts"]));
        Assert.Single(host.Reads);
    }

    [Fact]
    public void VirtualSourceIdentityFlowsIntoCrossFileTypeErrors()
    {
        var host=new MemoryHost();
        host.Files["/virtual/use.ts"]="let answer: string = shared;";
        host.Files["/virtual/define.ts"]="let shared: number = 42;";
        var diagnostic=Assert.Single(CompilerProgram.Load(host,["use.ts","define.ts"]).Analyze().Diagnostics);
        Assert.Equal("TS2322",diagnostic.Code);
        Assert.Equal("/virtual/use.ts",diagnostic.File);
        Assert.Equal(4,diagnostic.Start);
    }

    [Fact]
    public void AllFilesContributeSyntaxDiagnosticsBeforeSemanticChecking()
    {
        var host=new MemoryHost();
        host.Files["/virtual/z.ts"]="let z = ;";
        host.Files["/virtual/a.ts"]="let a = ;";
        var result=CompilerProgram.Load(host,["z.ts","a.ts"]).Analyze();

        Assert.True(result.HasSyntaxErrors);
        Assert.Equal(["/virtual/a.ts","/virtual/z.ts"],result.Diagnostics.Select(d=>d.File));
        Assert.All(result.Diagnostics,d=>Assert.Equal("TS1109",d.Code));
        Assert.False(result.EmptyProgram);
    }

    [Fact]
    public void UnsupportedSourceAndSyntaxErrorAreBothRetained()
    {
        var host=new MemoryHost();
        host.Files["/virtual/a.ts"]="function f() {}";
        host.Files["/virtual/b.ts"]="let x = ;";
        var result=CompilerProgram.Load(host,["a.ts","b.ts"]).Analyze();

        Assert.True(result.HasSyntaxErrors);
        Assert.Equal(["TSCS1001","TS1109"],result.Diagnostics.Select(d=>d.Code));
        Assert.Equal(["/virtual/a.ts","/virtual/b.ts"],result.Diagnostics.Select(d=>d.File));
    }
}

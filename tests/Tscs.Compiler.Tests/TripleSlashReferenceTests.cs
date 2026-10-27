using Xunit;
using Tscs;

namespace Tscs.Tests;

public sealed class TripleSlashReferenceTests
{
    private sealed class MemoryHost : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public List<string> Reads { get; }=[];

        public string GetCanonicalFileName(string path) =>
            Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine("/virtual",path));

        public string ReadFile(string canonicalFileName)
        {
            Reads.Add(canonicalFileName);
            return Files.TryGetValue(canonicalFileName,out var source)
                ? source
                : throw new FileNotFoundException("Referenced source not found.",canonicalFileName);
        }
    }

    [Fact]
    public void TransitivePathReferencesFlowIntoGlobalScriptBinding()
    {
        var host=new MemoryHost();
        host.Files["/virtual/main.ts"]="/// <reference path=\"./bridge.ts\" />\nlet answer: number = shared;";
        host.Files["/virtual/bridge.ts"]="/// <reference path='./library.ts' />\n// bridge";
        host.Files["/virtual/library.ts"]="let shared: number = 42;";

        var program=CompilerProgram.Load(host,["main.ts"]);
        Assert.Equal(["/virtual/main.ts","/virtual/bridge.ts","/virtual/library.ts"],
            program.Sources.Select(source=>source.File));
        Assert.Equal(program.Sources.Count,host.Reads.Count);
        Assert.Empty(program.Analyze().Diagnostics);
    }

    [Fact]
    public void DiamondAndCyclicReferencesNeverReloadSources()
    {
        var host=new MemoryHost();
        host.Files["/virtual/main.ts"]="/// <reference path=\"./a.ts\" />\n/// <reference path=\"./b.ts\" />\nlet result: number = shared;";
        host.Files["/virtual/a.ts"]="/// <reference path=\"./shared.ts\" />\n// a";
        host.Files["/virtual/b.ts"]="/// <reference path=\"./shared.ts\" />\n// b";
        host.Files["/virtual/shared.ts"]="/// <reference path=\"./main.ts\" />\nlet shared: number = 1;";

        var program=CompilerProgram.Load(host,["main.ts"]);
        Assert.Equal(4,program.Sources.Count);
        Assert.Equal(4,host.Reads.Count);
        Assert.Empty(program.Analyze().Diagnostics);
    }

    [Fact]
    public void ReferencedExplicitRootIsReadExactlyOnce()
    {
        var host=new MemoryHost();
        host.Files["/virtual/main.ts"]="/// <reference path=\"./shared.ts\" />\nlet answer: number = shared;";
        host.Files["/virtual/shared.ts"]="let shared: number = 1;";

        var program=CompilerProgram.Load(host,["main.ts","shared.ts"]);
        Assert.Equal(2,program.Sources.Count);
        Assert.Equal(2,host.Reads.Count);
        Assert.Empty(program.Analyze().Diagnostics);
    }

    [Fact]
    public void MissingReferenceFailsClosedBeforeChecking()
    {
        var host=new MemoryHost();
        host.Files["/virtual/main.ts"]="/// <reference path=\"./missing.ts\" />\nlet x = 1;";
        var error=Assert.Throws<FileNotFoundException>(()=>CompilerProgram.Load(host,["main.ts"]));
        Assert.Equal("/virtual/missing.ts",error.FileName);
    }

    [Fact]
    public void ADirectiveFollowingCodeCannotSilentlyExtendTheGraph()
    {
        var host=new MemoryHost();
        host.Files["/virtual/main.ts"]="let x = 1;\n/// <reference path=\"./missing.ts\" />";

        var program=CompilerProgram.Load(host,["main.ts"]);
        Assert.Single(program.Sources);
        Assert.Equal("TSCS1001",Assert.Single(program.Analyze().Diagnostics).Code);
    }

    [Fact]
    public void AbsolutePathReferencesFailClosed()
    {
        var host=new MemoryHost();
        host.Files["/virtual/main.ts"]="/// <reference path=\"/other.ts\" />\nlet x = 1;";
        Assert.Throws<ArgumentException>(()=>CompilerProgram.Load(host,["main.ts"]));
    }

    [Fact]
    public void UnsupportedReferenceAttributesCannotBeMistakenForPathReferences()
    {
        var host=new MemoryHost();
        host.Files["/virtual/main.ts"]="/// <reference types=\"node\" />\nlet x = 1;";
        var program=CompilerProgram.Load(host,["main.ts"]);
        Assert.Equal("TSCS1001",Assert.Single(program.Analyze().Diagnostics).Code);
    }

    [Fact]
    public void ExplicitDuplicateRootsRemainAnErrorEvenWithReferenceGraphDeduplication()
    {
        var host=new MemoryHost();
        host.Files["/virtual/main.ts"]="let x = 1;";
        Assert.Throws<ArgumentException>(()=>CompilerProgram.Load(host,["main.ts","./main.ts"]));
    }
}

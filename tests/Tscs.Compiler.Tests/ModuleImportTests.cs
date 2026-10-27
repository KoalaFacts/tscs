using Xunit;
using Tscs;

namespace Tscs.Tests;

public sealed class ModuleImportTests
{
    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public List<string> Reads { get; }=[];
        public string GetCanonicalFileName(string path) =>
            Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine("/virtual",path));
        public string ReadFile(string path)
        {
            Reads.Add(path);
            return Files.TryGetValue(path,out var text)?text:throw new FileNotFoundException(path);
        }
    }

    [Fact]
    public void NamedImportsResolvePrimitiveExportsAcrossFiles()
    {
        var host=new Host();
        host.Files["/virtual/main.ts"]="import { quantity as count } from \"./values\"; let result: number = count * 2;";
        host.Files["/virtual/values.ts"]="export const quantity: number = 4;";
        var program=CompilerProgram.Load(host,["main.ts"]);
        Assert.Equal(2,program.Sources.Count);
        Assert.Equal(2,host.Reads.Count);
        Assert.Empty(program.Analyze().Diagnostics);
    }

    [Fact]
    public void ModuleImportsTransitivelyShareTheSameProgramGraph()
    {
        var host=new Host();
        host.Files["/virtual/main.ts"]="import { result } from \"./middle.ts\"; let n: number = result;";
        host.Files["/virtual/middle.ts"]="import { value } from \"./leaf.ts\"; export const result: number = value;";
        host.Files["/virtual/leaf.ts"]="export const value: number = 7;";
        var program=CompilerProgram.Load(host,["main.ts"]);
        Assert.Equal(3,program.Sources.Count);
        Assert.Equal(3,host.Reads.Count);
        Assert.Empty(program.Analyze().Diagnostics);
    }

    [Fact]
    public void NonExportedModuleLocalsNeverBecomeGlobals()
    {
        var host=new Host();
        host.Files["/virtual/entry.ts"]="let n: number = privateValue;";
        host.Files["/virtual/lib.ts"]="export const published: number = 2; let privateValue: number = 3;";
        var result=CompilerProgram.Load(host,["entry.ts","lib.ts"]).Analyze();
        Assert.Equal("TS2304",Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void MissingNamedExportIsNotConsideredImplemented()
    {
        var host=new Host();
        host.Files["/virtual/main.ts"]="import { secret } from \"./lib.ts\"; let n: number = secret;";
        host.Files["/virtual/lib.ts"]="const secret: number = 1; export const published: number = 2;";
        var diagnostic=Assert.Single(CompilerProgram.Load(host,["main.ts"]).Analyze().Diagnostics);
        Assert.Equal("TS2305",diagnostic.Code);
        Assert.Equal("/virtual/main.ts",diagnostic.File);
    }

    [Fact]
    public void ImportedAliasesAreReadOnlyEvenWhenExporterUsesLet()
    {
        var host=new Host();
        host.Files["/virtual/main.ts"]="import { value as local } from \"./lib.ts\"; local = 2;";
        host.Files["/virtual/lib.ts"]="export let value: number = 1;";
        Assert.Equal("TS2632",Assert.Single(CompilerProgram.Load(host,["main.ts"]).Analyze().Diagnostics).Code);
    }

    [Fact]
    public void NamedLocalExportsAllowAliasesWithoutLeakingPrivateNames()
    {
        var host=new Host();
        host.Files["/virtual/main.ts"]="import { count as result } from \"./values\"; let twice: number = result * 2;";
        host.Files["/virtual/values.ts"]="let internalValue: number = 4; export { internalValue as count };";
        Assert.Empty(CompilerProgram.Load(host,["main.ts"]).Analyze().Diagnostics);

        host.Files["/virtual/main.ts"]="import { internalValue } from \"./values\"; let n: number = internalValue;";
        Assert.Equal("TS2305",Assert.Single(CompilerProgram.Load(host,["main.ts"]).Analyze().Diagnostics).Code);
    }

    [Fact]
    public void EmptyExportListMarksFileAsModuleWithPrivateScope()
    {
        var host=new Host();
        host.Files["/virtual/main.ts"]="let n: number = secret;";
        host.Files["/virtual/values.ts"]="let secret: number = 4; export {};";
        Assert.Equal("TS2304",
            Assert.Single(CompilerProgram.Load(host,["main.ts","values.ts"]).Analyze().Diagnostics).Code);
    }

    [Fact]
    public void NamedExportsRejectUnknownNamesAndDuplicateAliases()
    {
        var host=new Host();
        host.Files["/virtual/main.ts"]="export { missing as exposed };";
        Assert.Equal("TS2304",Assert.Single(CompilerProgram.Load(host,["main.ts"]).Analyze().Diagnostics).Code);

        host.Files["/virtual/main.ts"]="let x=1; let y=2; export { x as alias, y as alias };";
        Assert.Equal("TSCS1001",Assert.Single(CompilerProgram.Load(host,["main.ts"]).Analyze().Diagnostics).Code);
    }

    [Fact]
    public void ImportedBindingReexportsRemainExplicitlyUnsupported()
    {
        var host=new Host();
        host.Files["/virtual/main.ts"]="import { value } from \"./values\"; export { value };";
        host.Files["/virtual/values.ts"]="export const value: number = 4;";
        var diagnostic=Assert.Single(CompilerProgram.Load(host,["main.ts"]).Analyze().Diagnostics);
        Assert.Equal("TSCS1001",diagnostic.Code);
    }

    [Fact]
    public void MissingModuleFailsClosed()
    {
        var host=new Host();
        host.Files["/virtual/main.ts"]="import { x } from \"./missing\"; let n = x;";
        Assert.Throws<FileNotFoundException>(()=>CompilerProgram.Load(host,["main.ts"]));
    }

    [Fact]
    public void PackageImportsRemainExplicitlyUnsupported()
    {
        var host=new Host();
        host.Files["/virtual/main.ts"]="import { x } from \"package\"; let n = x;";
        Assert.Throws<ArgumentException>(()=>CompilerProgram.Load(host,["main.ts"]));
    }
}

using Tscs;
using Xunit;

namespace Tscs.Tests;

// These regressions exercise actual Program + AST-backed emission, not a
// configuration-only DTO or an upstream compatibility case.
public sealed class CompilerOptionsTests
{
    private sealed class MemoryHost : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public string GetCanonicalFileName(string path) => path;
        public string ReadFile(string canonicalFileName) =>
            canonicalFileName=="value.ts" ? "export const answer: number = 42;" :
            throw new FileNotFoundException(canonicalFileName);
    }

    [Fact]
    public void ProgramUsesItsTypedModuleOptionsWithoutACliEmitterFlag()
    {
        var options=new CompilerOptions
        {
            NoLib=true,
            Target=CompilerTarget.Es2015,
            Module=CompilerModule.Es2015
        };
        var program=CompilerProgram.Load(new MemoryHost(),["value.ts"],options);
        Assert.Same(options,program.Options);
        Assert.Empty(program.Analyze().Diagnostics);
        Assert.Equal("export const answer = 42;\n",Assert.Single(program.EmitJavaScript()).Text);
    }

    [Fact]
    public void UnspecifiedModuleStillRefusesNamedModuleEmission()
    {
        var program=CompilerProgram.Load(new MemoryHost(),["value.ts"],new CompilerOptions
        {
            NoLib=true,
            Target=CompilerTarget.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        Assert.Throws<NotSupportedException>(()=>program.EmitJavaScript());
    }

    [Fact]
    public void PreviouslyPublishedClrApiMethodSignaturesRemainCallable()
    {
        // Check exact CLR signatures, not only source-level optional argument binding.
        var load=typeof(CompilerProgram).GetMethod(nameof(CompilerProgram.Load),
            [typeof(ICompilerHost),typeof(IReadOnlyList<string>)]);
        var emit=typeof(CompilerProgram).GetMethod(nameof(CompilerProgram.EmitJavaScript),
            [typeof(bool)]);
        Assert.NotNull(load);
        Assert.NotNull(emit);
        Assert.True(load.IsStatic);
        var loaded=load.Invoke(null,[new MemoryHost(),new string[] {"value.ts"}]);
        var program=Assert.IsType<CompilerProgram>(loaded);
        Assert.Empty(program.Analyze().Diagnostics);
        Assert.Equal("export const answer = 42;\n",
            Assert.Single(program.EmitJavaScript(true)).Text);
    }

    [Fact]
    public void ProgramCannotEmitWhenEffectiveNoEmitIsTrue()
    {
        var program=CompilerProgram.Load(new MemoryHost(),["value.ts"],new CompilerOptions
        {
            NoEmit=true,
            NoLib=true,
            Target=CompilerTarget.Es2015,
            Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript(true));
    }
}

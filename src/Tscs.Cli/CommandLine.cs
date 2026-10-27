using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tscs;

public static class CommandLine
{
    private static readonly string[] GlobalTypes=["Array","Boolean","CallableFunction","Function","IArguments","NewableFunction","Number","Object","RegExp","String"];
    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public static int Run(string[] args,TextWriter stdout,TextWriter stderr)
    {
        bool json=args.Contains("--json",StringComparer.Ordinal);
        try
        {
            var options=Options.Parse(args);
            if(options.EmitJs && (options.NoEmit || !options.NoLib || !options.ExplicitEs2015Target))
                throw new InputFailure("AST-backed output requires --emitJs --noLib --target es2015 and --noEmit false.");
            var host=new FileSystemCompilerHost();
            var inputs=options.Files.Select(host.GetCanonicalFileName).ToArray();
            var codeFiles=inputs.Where(p=>!ProjectSourceDiscovery.IsDeclarationFile(p)).ToArray();
            if(codeFiles.Length==0)throw new InputFailure("The bootstrap requires at least one .ts source file, plus optional minimal .d.ts globals.");
            if(codeFiles.Any(p=>!ProjectSourceDiscovery.IsTypeScriptFile(p)))throw new InputFailure("Only .ts input is supported.");
            var globals=new HashSet<string>(StringComparer.Ordinal);
            foreach(var path in inputs.Where(ProjectSourceDiscovery.IsDeclarationFile))
                ReadGlobals(host.ReadFile(path),globals);
            var program=CompilerProgram.Load(host,codeFiles,options.EffectiveCompilerOptions);
            var analysis=program.Analyze();
            // First original-profile emit slice: an empty parsed program under the
            // explicit ES2015 target. This does NOT silently accept nonempty source
            // or other unimplemented library/emit combinations.
            bool emptyOriginalEmit=!options.EmitJs&&codeFiles.Length==1&&!options.NoEmit&&!options.NoLib&&options.ExplicitEs2015Target&&
                inputs.Length==1&&analysis.EmptyProgram&&analysis.Diagnostics.Count==0;
            if ((!options.NoEmit||!options.NoLib) && !emptyOriginalEmit && !options.EmitJs)
                throw new InputFailure("The bootstrap requires --noEmit and --noLib; supply minimal ambient globals explicitly.");
            var diagnostics=analysis.Diagnostics.ToArray();
            bool unsupported=diagnostics.Any(d=>d.Code.StartsWith("TSCS",StringComparison.Ordinal));
            bool syntax=analysis.HasSyntaxErrors;
            if(!unsupported&&!syntax&&!emptyOriginalEmit)
            {
                var missing=GlobalTypes.Where(t=>!globals.Contains(t)).Select(t=>new Diagnostic("TS2318","error","",0,0,$"Cannot find global type '{t}'.",[],[])).ToArray();
                if(missing.Length>0)diagnostics=missing;
            }
            int exitCode=unsupported?1:diagnostics.Length>0?2:0;
            if(exitCode==0&&options.EmitJs)
            {
                IReadOnlyList<JavaScriptOutput> emitted;
                try { emitted=program.EmitJavaScript(); }
                catch(NotSupportedException error) { throw new InputFailure(error.Message); }

                // Compute all outputs and reject existing files before writing any.
                // No existing JavaScript file is ever overwritten.
                var files=emitted.Select(item=>(Path:Path.ChangeExtension(item.SourceFile,".js"),item.Text)).ToArray();
                if(files.Select(f=>f.Path).Distinct(host.FileNameComparer).Count()!=files.Length)
                    throw new InputFailure("Multiple source files would produce the same JavaScript output.");
                if(files.Any(f=>File.Exists(f.Path)))
                    throw new InputFailure("A JavaScript output already exists; refusing to overwrite it.");
                var created=new List<string>();
                try
                {
                    foreach(var outputFile in files)
                    {
                        using var stream=new FileStream(outputFile.Path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                        created.Add(outputFile.Path);
                        using var writer=new StreamWriter(stream,new UTF8Encoding(false));
                        writer.Write(outputFile.Text);
                    }
                }
                catch
                {
                    // Roll back only files created by this invocation.
                    foreach(var path in created)
                    {
                        try { File.Delete(path); }
                        catch(IOException) { }
                        catch(UnauthorizedAccessException) { }
                    }
                    throw;
                }
            }
            if(exitCode==0&&emptyOriginalEmit)
            {
                // The locked TS7 reference baseline for emptyFile.ts emits precisely
                // a strict prologue with CRLF. Never overwrite a pre-existing file.
                var jsPath=Path.ChangeExtension(codeFiles[0],".js");
                using var output=new FileStream(jsPath,FileMode.CreateNew,FileAccess.Write);
                output.Write(Encoding.UTF8.GetBytes("\"use strict\";\r\n"));
            }
            if(json)
                stdout.WriteLine(JsonSerializer.Serialize(new{diagnostics=JsonSourceIdentities(diagnostics,program.Sources),exitCode},JsonOptions));
            else
            {
                // Diagnostic paths and source snapshots must use the host's file identity rules.
                var sources=program.Sources.ToDictionary(f=>f.File,f=>f.Text,host.FileNameComparer);
                foreach(var d in diagnostics)
                    stdout.WriteLine(Format(d,sources.GetValueOrDefault(d.File) ?? program.Sources[0].Text));
            }
            return exitCode;
        }
        catch(ConfigSyntaxFailure error)
        {
            int exitCode=error.Diagnostics.Any(d=>d.Code.StartsWith("TSCS",StringComparison.Ordinal))?1:2;
            if(json)stdout.WriteLine(JsonSerializer.Serialize(new{diagnostics=error.Diagnostics.Select(d=>d with{File=Path.GetFileName(d.File)}),exitCode},JsonOptions));
            else foreach(var d in error.Diagnostics)stdout.WriteLine(Format(d,error.Text));
            return exitCode;
        }
        catch(Exception error) when(error is InputFailure or IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            var d=new Diagnostic("TSCS1002","error","",0,0,error.Message,[],[]);
            if(json)stdout.WriteLine(JsonSerializer.Serialize(new{diagnostics=new[]{d},exitCode=1},JsonOptions));
            else stderr.WriteLine($"error {d.Code}: {d.Message}");
            return 1;
        }
    }
    // Keep single-file JSON basenames stable. Disambiguate basename collisions
    // using source-relative paths so the original UTF-16 spans stay attributable.
    private static Diagnostic[] JsonSourceIdentities(IEnumerable<Diagnostic> diagnostics,
        IReadOnlyList<SourceInput> sources)
    {
        var comparer=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        var collisions=sources.GroupBy(s=>Path.GetFileName(s.File),comparer)
            .Where(group=>group.Count()>1).Select(group=>group.Key).ToHashSet(comparer);
        return diagnostics.Select(d=>d.File.Length==0?d:d with
        {
            File=collisions.Contains(Path.GetFileName(d.File))
                ? Path.GetRelativePath(Environment.CurrentDirectory,d.File).Replace(Path.DirectorySeparatorChar,'/')
                : Path.GetFileName(d.File)
        }).ToArray();
    }

    private static string Format(Diagnostic d,string text)
    {
        if(d.File.Length==0)return $"error {d.Code}: {d.Message}";
        int line=1,column=1;
        for(int i=0;i<Math.Min(d.Start,text.Length);i++)
        {
            if(text[i]=='\r') {if(i+1<d.Start&&text[i+1]=='\n')i++;line++;column=1;}
            else if(text[i] is '\n' or '\u2028' or '\u2029'){line++;column=1;}
            else column++;
        }
        var path=Path.GetRelativePath(Environment.CurrentDirectory,Path.GetFullPath(d.File)).Replace(Path.DirectorySeparatorChar,'/');
        return $"{path}({line},{column}): error {d.Code}: {d.Message}";
    }
    // Minimal ambient global shapes only. Members, inheritance and arbitrary declarations are rejected.
    private static void ReadGlobals(string text,HashSet<string> globals)
    {
        var pattern=new Regex(@"\G\s*interface\s+([A-Za-z_$][A-Za-z0-9_$]*)(\s*<\s*T\s*>)?\s*\{\s*\}\s*",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        int position=0;
        while(position<text.Length)
        {
            if(string.IsNullOrWhiteSpace(text[position..]))break;
            var match=pattern.Match(text,position);
            if(!match.Success)throw new InputFailure("Only empty bootstrap global interfaces are supported in .d.ts files.");
            string name=match.Groups[1].Value;
            if(!GlobalTypes.Contains(name,StringComparer.Ordinal)||(name=="Array")!=match.Groups[2].Success||!globals.Add(name))
                throw new InputFailure("Unsupported or duplicate bootstrap global declaration.");
            position+=match.Length;
        }
    }
}
internal sealed class InputFailure(string message):Exception(message);

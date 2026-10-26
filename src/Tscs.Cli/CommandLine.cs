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
            var inputs=options.Files.Select(p=>(Path:p,Text:File.ReadAllText(p))).ToArray();
            var codeFiles=inputs.Where(f=>!f.Path.EndsWith(".d.ts",StringComparison.Ordinal)).ToArray();
            if(codeFiles.Length==0)throw new InputFailure("The bootstrap requires at least one .ts source file, plus optional minimal .d.ts globals.");
            if(codeFiles.Any(f=>!f.Path.EndsWith(".ts",StringComparison.Ordinal)))throw new InputFailure("Only .ts input is supported.");
            var globals=new HashSet<string>(StringComparer.Ordinal);
            foreach(var f in inputs.Where(f=>f.Path.EndsWith(".d.ts",StringComparison.Ordinal)))ReadGlobals(f.Text,globals);
            var analysis=Compilation.AnalyzeFiles(codeFiles.Select(f=>new SourceInput(f.Path,f.Text)).ToArray());
            // First original-profile emit slice: an empty parsed program under the
            // explicit ES2015 target. This does NOT silently accept nonempty source
            // or other unimplemented library/emit combinations.
            bool emptyOriginalEmit=codeFiles.Length==1&&!options.NoEmit&&!options.NoLib&&options.ExplicitEs2015Target&&
                inputs.Length==1&&analysis.EmptyProgram&&analysis.Diagnostics.Count==0;
            if ((!options.NoEmit||!options.NoLib) && !emptyOriginalEmit)
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
            if(exitCode==0&&emptyOriginalEmit)
            {
                // The locked TS7 reference baseline for emptyFile.ts emits precisely
                // a strict prologue with CRLF. Never overwrite a pre-existing file.
                var jsPath=Path.ChangeExtension(codeFiles[0].Path,".js");
                using var output=new FileStream(jsPath,FileMode.CreateNew,FileAccess.Write);
                output.Write(Encoding.UTF8.GetBytes("\"use strict\";\r\n"));
            }
            if(json)
                stdout.WriteLine(JsonSerializer.Serialize(new{diagnostics=diagnostics.Select(d=>d with{File=Path.GetFileName(d.File)}),exitCode},JsonOptions));
            else
            {
                var sources=codeFiles.ToDictionary(f=>f.Path,f=>f.Text,StringComparer.Ordinal);
                foreach(var d in diagnostics)
                    stdout.WriteLine(Format(d,sources.GetValueOrDefault(d.File) ?? codeFiles[0].Text));
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
        var path=Path.GetRelativePath(Environment.CurrentDirectory,Path.GetFullPath(d.File)).Replace('\\','/');
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

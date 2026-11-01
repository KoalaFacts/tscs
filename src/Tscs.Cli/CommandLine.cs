using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tscs;

public static class CommandLine
{
    private static readonly string[] GlobalTypes=["Array","Boolean","CallableFunction","Function","IArguments","NewableFunction","Number","Object","RegExp","String"];
    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public static int Run(string[] args,TextWriter stdout,TextWriter stderr) =>
        Run(args,stdout,stderr,new FileSystemCompilerHost());

    // An injected host owns configuration, discovery and source reads.
    // The filesystem CLI remains the only writer of physical .js outputs.
    public static int Run(string[] args,TextWriter stdout,TextWriter stderr,IProjectSourceHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        bool json=args.Contains("--json",StringComparer.Ordinal);
        try
        {
            var options=Options.Parse(args,host);
            if(options.EmitJs && (options.NoEmit || !options.NoLib || !options.ExplicitEs2015Target))
                throw new InputFailure("AST-backed output requires --emitJs --noLib --target es2015 and --noEmit false.");
            var inputs=options.Files.Select(host.GetCanonicalFileName).ToArray();
            var codeFiles=inputs.Where(p=>!ProjectSourceDiscovery.IsDeclarationFile(p,host.FileNameComparer)).ToArray();
            if(codeFiles.Length==0)throw new InputFailure("The bootstrap requires at least one .ts source file, plus optional minimal .d.ts globals.");
            if(codeFiles.Any(p=>!ProjectSourceDiscovery.IsTypeScriptFile(p,host.FileNameComparer)))throw new InputFailure("Only .ts input is supported.");
            var globals=new HashSet<string>(StringComparer.Ordinal);
            foreach(var path in inputs.Where(p=>ProjectSourceDiscovery.IsDeclarationFile(p,host.FileNameComparer)))
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
                if(host is not FileSystemCompilerHost)
                    throw new InputFailure("Filesystem JavaScript output is unavailable for virtual compiler hosts.");
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
                if(host is not FileSystemCompilerHost)
                    throw new InputFailure("Filesystem JavaScript output is unavailable for virtual compiler hosts.");
                // The locked TS7 reference baseline for emptyFile.ts emits precisely
                // a strict prologue with CRLF. Never overwrite a pre-existing file.
                var jsPath=Path.ChangeExtension(codeFiles[0],".js");
                using var output=new FileStream(jsPath,FileMode.CreateNew,FileAccess.Write);
                output.Write(Encoding.UTF8.GetBytes("\"use strict\";\r\n"));
            }
            if(json)
                stdout.WriteLine(JsonSerializer.Serialize(new{diagnostics=JsonSourceIdentities(diagnostics,program.Sources,host.FileNameComparer),exitCode},JsonOptions));
            else
            {
                // Diagnostic paths and source snapshots must use the host's file identity rules.
                var sources=program.Sources.ToDictionary(f=>f.File,f=>f.Text,host.FileNameComparer);
                foreach(var d in diagnostics)
                    stdout.WriteLine(Format(d,sources.GetValueOrDefault(d.File) ?? program.Sources[0].Text));
            }
            return exitCode;
        }
        catch(ConfigDiagnosticFailure error)
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
        IReadOnlyList<SourceInput> sources,StringComparer comparer)
    {
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
    // Ambient globals remain a deliberately bounded, validated declaration
    // subset. ES2015 array spread requires an iterable Array<T> contract in
    // the pinned reference compiler. Recognize that exact structural protocol
    // symmetrically, without treating arbitrary .d.ts declarations as accepted.
    private static void ReadGlobals(string text,HashSet<string> globals)
    {
        var pattern=new Regex(@"\G\s*interface\s+([A-Za-z_$][A-Za-z0-9_$]*)(\s*<\s*T\s*(?:,\s*TReturn\s*=\s*any\s*,\s*TNext\s*=\s*any\s*)?>)?\s*\{([^{}]*)\}\s*",
            RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        var symbolPattern=new Regex(@"\G\s*declare\s+const\s+Symbol\s*:\s*SymbolConstructor\s*;\s*",
            RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        var extras=new HashSet<string>(StringComparer.Ordinal);
        bool symbolDeclared=false,arrayIterable=false;
        int position=0;
        while(position<text.Length)
        {
            if(string.IsNullOrWhiteSpace(text[position..]))break;
            var symbol=symbolPattern.Match(text,position);
            if(symbol.Success)
            {
                if(symbolDeclared)throw new InputFailure("Duplicate ambient Symbol declaration.");
                symbolDeclared=true;
                position+=symbol.Length;
                continue;
            }
            var match=pattern.Match(text,position);
            if(!match.Success)throw new InputFailure("Only bounded bootstrap global interfaces are supported in .d.ts files.");
            string name=match.Groups[1].Value;
            bool generic=match.Groups[2].Success;
            string genericParameters=Regex.Replace(match.Groups[2].Value,@"\s+","");
            var members=match.Groups[3].Value;
            if(name is "SymbolConstructor" or "IteratorResult" or "Iterator")
            {
                // These three protocol helpers do not create usable TSCS value
                // symbols: they only validate the minimal pinned-Go ambient.
                string compact=Regex.Replace(members,@"\s+","");
                bool valid=name switch
                {
                    "SymbolConstructor"=>!generic && compact=="readonlyiterator:uniquesymbol;",
                    "IteratorResult"=>genericParameters=="<T>" && compact=="done?:boolean;value:T;",
                    "Iterator"=>genericParameters=="<T,TReturn=any,TNext=any>" &&
                        compact=="next():IteratorResult<T>;",
                    _=>false
                };
                if(!valid||!extras.Add(name))
                    throw new InputFailure("Unsupported or duplicate iterable ambient declaration.");
            }
            else if(name=="ReadonlyArray")
            {
                // This optional ambient interface describes the pinned Go
                // reference's immutable array view. Do not add it to the
                // required global list or accept arbitrary generic declarations.
                // globals persists across all .d.ts inputs, unlike a local
                // flag: identical index signatures in separate ambient files
                // must not silently merge into one accepted declaration.
                if(genericParameters!="<T>" ||
                   !IsSupportedReadonlyArrayContract(members) ||
                   !globals.Add(name))
                    throw new InputFailure("Only one bounded readonly Array<T> ambient contract is supported.");
            }
            else
            {
                if(!GlobalTypes.Contains(name,StringComparer.Ordinal) ||
                   (name=="Array"?genericParameters!="<T>":generic) || !globals.Add(name))
                    throw new InputFailure("Unsupported or duplicate bootstrap global declaration.");
                if(!string.IsNullOrWhiteSpace(members))
                {
                    if(name=="Array" && IsSupportedArrayContract(members,out var iterable))
                        arrayIterable=iterable;
                    else if(name!="String" || !IsSupportedStringContract(members))
                        throw new InputFailure("Only bounded Array<T> and readonly String member contracts are supported in ambient globals.");
                }
            }
            position+=match.Length;
        }
        // Never accept a partial, invented iterator shape or a value
        // declaration that does not correspond to the recognized protocol.
        if((symbolDeclared||arrayIterable||extras.Count>0) &&
           !(symbolDeclared&&arrayIterable&&extras.SetEquals(
               new[]{"SymbolConstructor","IteratorResult","Iterator"})))
            throw new InputFailure("Incomplete bounded iterable Array<T> declaration protocol.");
    }

    // Exact, shared, non-mutating query signatures on Array<T> and
    // ReadonlyArray<T>. Sequential member parsing below enforces complete
    // member boundaries, prevents substring deletion, and rejects duplicates.
    private const string ArrayQueryMemberPattern =
        @"(?<includes>includes\s*\(\s*[A-Za-z_$][A-Za-z0-9_$]*\s*:\s*T\s*(?:,\s*[A-Za-z_$][A-Za-z0-9_$]*\s*\?\s*:\s*number\s*)?\)\s*:\s*boolean)" +
        @"|(?<indexOf>indexOf\s*\(\s*[A-Za-z_$][A-Za-z0-9_$]*\s*:\s*T\s*(?:,\s*[A-Za-z_$][A-Za-z0-9_$]*\s*\?\s*:\s*number\s*)?\)\s*:\s*number)" +
        @"|(?<lastIndexOf>lastIndexOf\s*\(\s*[A-Za-z_$][A-Za-z0-9_$]*\s*:\s*T\s*(?:,\s*[A-Za-z_$][A-Za-z0-9_$]*\s*\?\s*:\s*number\s*)?\)\s*:\s*number)" +
        @"|(?<join>join\s*\(\s*(?:[A-Za-z_$][A-Za-z0-9_$]*\s*\?\s*:\s*string\s*)?\)\s*:\s*string)";

    // Member order and numeric-index parameter spelling are irrelevant.
    // Length/index must be present exactly once; the optional standard
    // Array<T> mutator declarations supply the pinned reference's actual
    // mutable-array distinction from ReadonlyArray<T>. They validate exact
    // source-level signatures only; the product's independent semantic
    // method table checks calls, never an arbitrary regex-derived declaration.
    private static bool IsSupportedArrayContract(string body,out bool iterator)
    {
        var memberPattern=new Regex(@"\G\s*(?:(?<length>length\s*:\s*number)|(?<index>\[\s*[A-Za-z_$][A-Za-z0-9_$]*\s*:\s*number\s*\]\s*:\s*T)|(?<iterator>\[\s*Symbol\s*\.\s*iterator\s*\]\s*\(\s*\)\s*:\s*Iterator\s*<\s*T\s*>)|(?<push>push\s*\(\s*\.\.\.\s*[A-Za-z_$][A-Za-z0-9_$]*\s*:\s*T\s*\[\s*\]\s*\)\s*:\s*number)|(?<unshift>unshift\s*\(\s*\.\.\.\s*[A-Za-z_$][A-Za-z0-9_$]*\s*:\s*T\s*\[\s*\]\s*\)\s*:\s*number)|(?<pop>pop\s*\(\s*\)\s*:\s*T\s*\|\s*undefined)|(?<shift>shift\s*\(\s*\)\s*:\s*T\s*\|\s*undefined)|"+ArrayQueryMemberPattern+@")\s*;",
            RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        int position=0;
        bool length=false,index=false,push=false,unshift=false,pop=false,shift=false;
        bool includes=false,indexOf=false,lastIndexOf=false,join=false;
        iterator=false;
        while(position<body.Length)
        {
            if(string.IsNullOrWhiteSpace(body[position..]))break;
            var member=memberPattern.Match(body,position);
            if(!member.Success)return false;
            if(member.Groups["length"].Success)
            {
                if(length)return false;
                length=true;
            }
            else if(member.Groups["index"].Success)
            {
                if(index)return false;
                index=true;
            }
            else if(member.Groups["push"].Success)
            {
                if(push)return false;
                push=true;
            }
            else if(member.Groups["unshift"].Success)
            {
                if(unshift)return false;
                unshift=true;
            }
            else if(member.Groups["pop"].Success)
            {
                if(pop)return false;
                pop=true;
            }
            else if(member.Groups["shift"].Success)
            {
                if(shift)return false;
                shift=true;
            }
            else if(member.Groups["includes"].Success)
            {
                if(includes)return false;
                includes=true;
            }
            else if(member.Groups["indexOf"].Success)
            {
                if(indexOf)return false;
                indexOf=true;
            }
            else if(member.Groups["lastIndexOf"].Success)
            {
                if(lastIndexOf)return false;
                lastIndexOf=true;
            }
            else if(member.Groups["join"].Success)
            {
                if(join)return false;
                join=true;
            }
            else
            {
                if(iterator)return false;
                iterator=true;
            }
            position+=member.Length;
        }
        return length&&index;
    }

    // The optional readonly array global is accepted only when both
    // immutable structural contracts are present exactly once. This is
    // strictly ambient validation, not a generic value-declaration parser.
    private static bool IsSupportedReadonlyArrayContract(string body)
    {
        var memberPattern=new Regex(@"\G\s*(?:(?<length>readonly\s+length\s*:\s*number)|(?<index>readonly\s+\[\s*[A-Za-z_$][A-Za-z0-9_$]*\s*:\s*number\s*\]\s*:\s*T)|"+ArrayQueryMemberPattern+@")\s*;",
            RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        int position=0;
        bool length=false,index=false,includes=false,indexOf=false,lastIndexOf=false,join=false;
        while(position<body.Length)
        {
            if(string.IsNullOrWhiteSpace(body[position..]))break;
            var member=memberPattern.Match(body,position);
            if(!member.Success)return false;
            if(member.Groups["length"].Success)
            {
                if(length)return false;
                length=true;
            }
            else if(member.Groups["index"].Success)
            {
                if(index)return false;
                index=true;
            }
            else if(member.Groups["includes"].Success)
            {
                if(includes)return false;
                includes=true;
            }
            else if(member.Groups["indexOf"].Success)
            {
                if(indexOf)return false;
                indexOf=true;
            }
            else if(member.Groups["lastIndexOf"].Success)
            {
                if(lastIndexOf)return false;
                lastIndexOf=true;
            }
            else
            {
                if(join)return false;
                join=true;
            }
            position+=member.Length;
        }
        return length&&index;
    }

    // The String shape is intentionally read-only. Parsing exact source
    // members prevents an ambient declaration from granting unsupported
    // mutable indices, arbitrary methods, or unconstrained value types.
    private static bool IsSupportedStringContract(string body)
    {
        var memberPattern=new Regex(@"\G\s*(?:(?<length>readonly\s+length\s*:\s*number)|(?<index>readonly\s+\[\s*[A-Za-z_$][A-Za-z0-9_$]*\s*:\s*number\s*\]\s*:\s*string))\s*;",
            RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        int position=0;
        bool length=false,index=false;
        while(position<body.Length)
        {
            if(string.IsNullOrWhiteSpace(body[position..]))break;
            var member=memberPattern.Match(body,position);
            if(!member.Success)return false;
            if(member.Groups["length"].Success)
            {
                if(length)return false;
                length=true;
            }
            else
            {
                if(index)return false;
                index=true;
            }
            position+=member.Length;
        }
        return length&&index;
    }
}
internal sealed class InputFailure(string message):Exception(message);

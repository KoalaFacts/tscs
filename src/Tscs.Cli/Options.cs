

namespace Tscs;

internal sealed class Options
{
    public List<string> Files { get; }=[];
    // One effective, immutable compiler settings snapshot. CLI-only --emitJs
    // remains separate from TypeScript's compiler options.
    private CompilerOptions compilerOptions=new();
    public CompilerOptions EffectiveCompilerOptions=>compilerOptions;
    public bool NoEmit=>compilerOptions.NoEmit;
    public bool NoLib=>compilerOptions.NoLib;
    public bool EmitJs {get;private set;}
    public bool EmitEs2015Module=>compilerOptions.Module==CompilerModule.Es2015;
    public bool ExplicitEs2015Target=>compilerOptions.Target==CompilerTarget.Es2015;
    public static Options Parse(string[] args) => Parse(args,new FileSystemCompilerHost());

    internal static Options Parse(string[] args,IProjectSourceHost projectHost)
    {
        ArgumentNullException.ThrowIfNull(projectHost);
        var options=new Options();string? project=null;
        // Read config first; supported command-line flags then override it.
        for(int i=0;i<args.Length;i++)if(args[i] is "-p" or "--project")
        {
            if(project!=null||i+1==args.Length)throw new InputFailure("Specify one project configuration.");
            project=args[++i];
        }
        if(project!=null)options.ReadConfig(project,projectHost);
        for(int i=0;i<args.Length;i++)
        {
            var arg=args[i];
            switch(arg)
            {
                case "-p":case "--project":i++;break;
                case "--json":break;
                case "--emitJs":options.EmitJs=true;break;
                case "--module":
                    if(++i==args.Length || (args[i]!="es2015" && args[i]!="es6"))
                        throw new InputFailure("Only --module es2015/es6 is supported for named ES modules.");
                    options.compilerOptions=options.compilerOptions with {Module=CompilerModule.Es2015};
                    break;
                case "--noEmit":options.compilerOptions=options.compilerOptions with {NoEmit=ReadBool(args,ref i)};break;
                case "--noLib":options.compilerOptions=options.compilerOptions with {NoLib=ReadBool(args,ref i)};break;
                case "--pretty":if(++i==args.Length||args[i]!="false")throw new InputFailure("Only --pretty false is supported.");break;
                case "--target":if(++i==args.Length)throw new InputFailure("Missing target.");ValidateTarget(args[i]);options.compilerOptions=options.compilerOptions with {Target=CompilerTarget.Es2015};break;
                default:
                    if(arg.StartsWith('-'))throw new InputFailure($"Unsupported option '{arg}'.");
                    if(project!=null)throw new InputFailure("Cannot combine a project with source file arguments.");
                    options.Files.Add(projectHost.GetCanonicalFileName(arg));break;
            }
        }
        if(options.Files.Count==0)throw new InputFailure("No source files were specified.");
        if(options.Files.Distinct(projectHost.FileNameComparer).Count()!=options.Files.Count)throw new InputFailure("Duplicate input paths are not supported.");
        return options;
    }
    private static bool ReadBool(string[] args,ref int i)
    {
        if(i+1<args.Length&&args[i+1] is "true" or "false")return args[++i]=="true";
        return true;
    }
    private void ReadConfig(string file,IProjectSourceHost projectHost)
    {
        file=projectHost.GetCanonicalFileName(file);
        if(projectHost.DirectoryExists(file))
            file=projectHost.GetCanonicalFileName(Path.Combine(file,"tsconfig.json"));
        // Read the configuration and its sources through the same host.
        // The JSON AST diagnostics retain this canonical configuration name.
        var text=projectHost.ReadFile(file);var parsed=JsonSyntax.Parse(file,text);
        if(parsed.Diagnostics.Count>0)throw new ConfigDiagnosticFailure(text,parsed.Diagnostics);
        var root=parsed.Root;
        if(root?.Kind!="Object")throw InvalidConfig(file,text,root,"Configuration must be an object.");
        var keys=new HashSet<string>(StringComparer.Ordinal);
        var explicitPaths=new HashSet<string>(projectHost.FileNameComparer);
        var includes=new List<string>();
        var excludes=new List<string>();
        int patternCount=0;
        foreach(var p in root!.Properties)
        {
            if(!keys.Add((p.Name.Token.Value??p.Name.Token.Text)))throw InvalidConfig(file,text,p.Name,"Duplicate configuration keys are not supported.");
            switch((p.Name.Token.Value??p.Name.Token.Text))
            {
                case "files":
                    if(p.Value.Kind!="Array")throw InvalidConfig(file,text,p.Value,"files must be an array.");
                    foreach(var item in p.Value.Elements)
                    {
                        if(item.Kind!="String"||string.IsNullOrEmpty(item.Token.Value))
                            throw InvalidConfig(file,text,item,"File paths must be nonempty strings.");
                        var sourcePath=projectHost.ResolveReferencePath(file,item.Token.Value!);
                        if(!explicitPaths.Add(sourcePath))
                            throw InvalidConfig(file,text,item,"Duplicate input paths are not supported.");
                        Files.Add(sourcePath);
                    }
                    break;
                case "include":includes.AddRange(ProjectPatterns(p.Value,"include",file,text,projectHost.FileNameComparer,ref patternCount));break;
                case "exclude":excludes.AddRange(ProjectPatterns(p.Value,"exclude",file,text,projectHost.FileNameComparer,ref patternCount));break;
                case "compilerOptions":
                    if(p.Value.Kind!="Object")throw InvalidConfig(file,text,p.Value,"compilerOptions must be an object.");
                    var optionKeys=new HashSet<string>(StringComparer.Ordinal);
                    foreach(var option in p.Value.Properties)
                    {
                        if(!optionKeys.Add((option.Name.Token.Value??option.Name.Token.Text)))throw InvalidConfig(file,text,option.Name,"Duplicate compiler options are not supported.");
                        switch((option.Name.Token.Value??option.Name.Token.Text))
                        {
                            case "noEmit":compilerOptions=compilerOptions with {NoEmit=Boolean("noEmit",option.Value,file,text)};break;
                            case "noLib":compilerOptions=compilerOptions with {NoLib=Boolean("noLib",option.Value,file,text)};break;
                            case "target":
                                var target=JsonEnumOptions.Convert(JsonEnumOptions.Declarations["target"],JsonConfigValues.Convert(option.Value),file,expression:option.Value.Token);
                                if(target.Diagnostics.Count>0)throw new ConfigDiagnosticFailure(text,target.Diagnostics);
                                // Valid upstream targets beyond the bootstrap remain unsupported.
                                if(target.Value.Kind=="Number"&&target.Value.Number==2){compilerOptions=compilerOptions with {Target=CompilerTarget.Es2015};break;}
                                throw InvalidConfig(file,text,option.Value,"Only target es2015/es6 is supported by the bootstrap.");
                            case "module":
                                // Use the pinned TS7 declaration and its diagnostic mapping,
                                // not a second unvalidated string/enum parser. The effective
                                // module choice then feeds the same CLI/Program emit path.
                                var module=JsonEnumOptions.Convert(JsonEnumOptions.Declarations["module"],
                                    JsonConfigValues.Convert(option.Value),file,expression:option.Value.Token);
                                if(module.Diagnostics.Count>0)throw new ConfigDiagnosticFailure(text,module.Diagnostics);
                                if(option.Value.Kind=="Null"){compilerOptions=compilerOptions with {Module=CompilerModule.Unspecified};break;}
                                // Pinned ModuleKind.ES2015=5; ES6 is the same enum value.
                                if(module.Value.Kind=="Number"&&module.Value.Number==5){compilerOptions=compilerOptions with {Module=CompilerModule.Es2015};break;}
                                throw InvalidConfig(file,text,option.Value,"Only module es2015/es6 is supported by the bootstrap.");
                            default:throw InvalidConfig(file,text,option.Name,$"Unsupported compiler option '{(option.Name.Token.Value??option.Name.Token.Text)}'.");
                        }
                    }
                    break;
                default:throw InvalidConfig(file,text,p.Name,$"Unsupported configuration property '{(p.Name.Token.Value??p.Name.Token.Text)}'.");
            }
        }
        // Explicit root files are retained; include globs add a deterministic
        // bounded source set, while excludes apply only to discovered files.
        Files.AddRange(ProjectSourceDiscovery.Discover(projectHost,Path.GetDirectoryName(file)!,Files,includes,excludes));
    }
    private static IReadOnlyList<string> ProjectPatterns(JsonSyntaxNode value,string name,string file,string text,
        StringComparer comparer,ref int patternCount)
    {
        if(value.Kind!="Array")throw InvalidConfig(file,text,value,$"{name} must be an array.");
        var patterns=new List<string>();
        foreach(var element in value.Elements)
        {
            if(element.Kind!="String"||string.IsNullOrWhiteSpace(element.Token.Value))
                throw InvalidConfig(file,text,element,$"{name} entries must be nonempty strings.");
            if(patternCount>=ProjectSourceDiscovery.MaximumPatterns)
                throw InvalidConfig(file,text,element,"Project source pattern count exceeds the bootstrap limit.");
            try { ProjectSourceDiscovery.ValidatePattern(element.Token.Value!,comparer); }
            catch(InputFailure error) { throw InvalidConfig(file,text,element,error.Message); }
            patterns.Add(element.Token.Value!);
            patternCount++;
        }
        return patterns;
    }
    // Keep semantic config validation in the same source-bound diagnostic
    // envelope as JSON syntax and fixed-upstream option conversion.
    // TSCS1002 is still an unsupported-input exit (1), not a TS exit (2).
    private static ConfigDiagnosticFailure InvalidConfig(string file,string text,JsonSyntaxNode? node,string message) =>
        new(text,[new Diagnostic("TSCS1002","error",file,node?.Token.Start??0,node?.Token.Length??0,message,[],[])]);

    private static bool Boolean(string name,JsonSyntaxNode value,string file,string text)
    {
        var result=JsonScalarOptions.Convert(new(name,new("boolean",false)),JsonConfigValues.Convert(value),file,expression:value.Token);
        if(result.Diagnostics.Count>0)throw new ConfigDiagnosticFailure(text,result.Diagnostics);
        // Null remains unset/default false, then explicit CLI flags override.
        return result.Value.Kind=="True";
    }
    private static void ValidateTarget(string target)
    {if(!target.Equals("es2015",StringComparison.OrdinalIgnoreCase)&&!target.Equals("es6",StringComparison.OrdinalIgnoreCase))throw new InputFailure("Only target es2015/es6 is supported by the bootstrap.");}
}

internal sealed class ConfigDiagnosticFailure(string text,IReadOnlyList<Diagnostic> diagnostics):Exception("Configuration syntax failed."){public string Text {get;}=text;public IReadOnlyList<Diagnostic> Diagnostics {get;}=diagnostics;}

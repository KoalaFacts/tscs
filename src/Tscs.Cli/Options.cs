

namespace Tscs;

internal sealed class Options
{
    public List<string> Files { get; }=[];
    private bool noEmit,noLib;
    public static Options Parse(string[] args)
    {
        var options=new Options();string? project=null;
        // Read config first; supported command-line flags then override it.
        for(int i=0;i<args.Length;i++)if(args[i] is "-p" or "--project")
        {
            if(project!=null||i+1==args.Length)throw new InputFailure("Specify one project configuration.");
            project=args[++i];
        }
        if(project!=null)options.ReadConfig(project);
        for(int i=0;i<args.Length;i++)
        {
            var arg=args[i];
            switch(arg)
            {
                case "-p":case "--project":i++;break;
                case "--json":break;
                case "--noEmit":options.noEmit=ReadBool(args,ref i);break;
                case "--noLib":options.noLib=ReadBool(args,ref i);break;
                case "--pretty":if(++i==args.Length||args[i]!="false")throw new InputFailure("Only --pretty false is supported.");break;
                case "--target":if(++i==args.Length)throw new InputFailure("Missing target.");ValidateTarget(args[i]);break;
                default:
                    if(arg.StartsWith('-'))throw new InputFailure($"Unsupported option '{arg}'.");
                    if(project!=null)throw new InputFailure("Cannot combine a project with source file arguments.");
                    options.Files.Add(Path.GetFullPath(arg));break;
            }
        }
        if(!options.noEmit||!options.noLib)throw new InputFailure("The bootstrap requires --noEmit and --noLib; supply minimal ambient globals explicitly.");
        if(options.Files.Count==0)throw new InputFailure("No source files were specified.");
        if(options.Files.Distinct(StringComparer.Ordinal).Count()!=options.Files.Count)throw new InputFailure("Duplicate input paths are not supported.");
        return options;
    }
    private static bool ReadBool(string[] args,ref int i)
    {
        if(i+1<args.Length&&args[i+1] is "true" or "false")return args[++i]=="true";
        return true;
    }
    private void ReadConfig(string file)
    {
        if(Directory.Exists(file))file=Path.Combine(file,"tsconfig.json");
        file=Path.GetFullPath(file);
        var text=File.ReadAllText(file);var parsed=JsonSyntax.Parse(file,text);
        if(parsed.Diagnostics.Count>0)throw new ConfigSyntaxFailure(text,parsed.Diagnostics);
        var root=parsed.Root;
        if(root?.Kind!="Object")throw new InputFailure("Configuration must be an object.");
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in root!.Properties)
        {
            if(!keys.Add((p.Name.Token.Value??p.Name.Token.Text)))throw new InputFailure("Duplicate configuration keys are not supported.");
            switch((p.Name.Token.Value??p.Name.Token.Text))
            {
                case "files":
                    if(p.Value.Kind!="Array")throw new InputFailure("files must be an array.");
                    foreach(var item in p.Value.Elements)
                    {if(item.Kind!="String"||string.IsNullOrEmpty(item.Token.Value))throw new InputFailure("File paths must be nonempty strings.");Files.Add(Path.GetFullPath(item.Token.Value!,Path.GetDirectoryName(file)!));}
                    break;
                case "compilerOptions":
                    if(p.Value.Kind!="Object")throw new InputFailure("compilerOptions must be an object.");
                    var optionKeys=new HashSet<string>(StringComparer.Ordinal);
                    foreach(var option in p.Value.Properties)
                    {
                        if(!optionKeys.Add((option.Name.Token.Value??option.Name.Token.Text)))throw new InputFailure("Duplicate compiler options are not supported.");
                        switch((option.Name.Token.Value??option.Name.Token.Text))
                        {
                            case "noEmit":noEmit=Boolean("noEmit",option.Value,file,text);break;
                            case "noLib":noLib=Boolean("noLib",option.Value,file,text);break;
                            case "target":if(option.Value.Kind!="String")throw new InputFailure("target must be a string.");ValidateTarget(JsonConfigValues.Convert(option.Value).Text);break;
                            default:throw new InputFailure($"Unsupported compiler option '{(option.Name.Token.Value??option.Name.Token.Text)}'.");
                        }
                    }
                    break;
                default:throw new InputFailure($"Unsupported configuration property '{(p.Name.Token.Value??p.Name.Token.Text)}'.");
            }
        }
    }
    private static bool Boolean(string name,JsonSyntaxNode value,string file,string text)
    {
        var result=JsonScalarOptions.Convert(new(name,new("boolean",false)),JsonConfigValues.Convert(value),file,expression:value.Token);
        if(result.Diagnostics.Count>0)throw new ConfigSyntaxFailure(text,result.Diagnostics);
        // Null remains unset/default false, then explicit CLI flags override.
        return result.Value.Kind=="True";
    }
    private static void ValidateTarget(string target)
    {if(!target.Equals("es2015",StringComparison.OrdinalIgnoreCase)&&!target.Equals("es6",StringComparison.OrdinalIgnoreCase))throw new InputFailure("Only target es2015/es6 is supported by the bootstrap.");}
}

internal sealed class ConfigSyntaxFailure(string text,IReadOnlyList<Diagnostic> diagnostics):Exception("Configuration syntax failed."){public string Text {get;}=text;public IReadOnlyList<Diagnostic> Diagnostics {get;}=diagnostics;}

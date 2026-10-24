using System.Text.Json;

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
        using var doc=JsonDocument.Parse(File.ReadAllText(file),new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});
        var root=doc.RootElement;
        if(root.ValueKind!=JsonValueKind.Object)throw new InputFailure("Configuration must be an object.");
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in root.EnumerateObject())
        {
            if(!keys.Add(p.Name))throw new InputFailure("Duplicate configuration keys are not supported.");
            switch(p.Name)
            {
                case "files":
                    if(p.Value.ValueKind!=JsonValueKind.Array)throw new InputFailure("files must be an array.");
                    foreach(var item in p.Value.EnumerateArray())
                    {if(item.ValueKind!=JsonValueKind.String||string.IsNullOrEmpty(item.GetString()))throw new InputFailure("File paths must be nonempty strings.");Files.Add(Path.GetFullPath(item.GetString()!,Path.GetDirectoryName(file)!));}
                    break;
                case "compilerOptions":
                    if(p.Value.ValueKind!=JsonValueKind.Object)throw new InputFailure("compilerOptions must be an object.");
                    var optionKeys=new HashSet<string>(StringComparer.Ordinal);
                    foreach(var option in p.Value.EnumerateObject())
                    {
                        if(!optionKeys.Add(option.Name))throw new InputFailure("Duplicate compiler options are not supported.");
                        switch(option.Name)
                        {
                            case "noEmit":noEmit=Boolean(option.Value);break;
                            case "noLib":noLib=Boolean(option.Value);break;
                            case "target":if(option.Value.ValueKind!=JsonValueKind.String)throw new InputFailure("target must be a string.");ValidateTarget(option.Value.GetString()!);break;
                            default:throw new InputFailure($"Unsupported compiler option '{option.Name}'.");
                        }
                    }
                    break;
                default:throw new InputFailure($"Unsupported configuration property '{p.Name}'.");
            }
        }
    }
    private static bool Boolean(JsonElement value)=>value.ValueKind switch{JsonValueKind.True=>true,JsonValueKind.False=>false,_=>throw new InputFailure("Expected a boolean compiler option.")};
    private static void ValidateTarget(string target)
    {if(!target.Equals("es2015",StringComparison.OrdinalIgnoreCase)&&!target.Equals("es6",StringComparison.OrdinalIgnoreCase))throw new InputFailure("Only target es2015/es6 is supported by the bootstrap.");}
}

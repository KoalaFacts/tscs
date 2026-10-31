namespace Tscs;

// Named value exports are linked after all module scopes are prebound.
// Public names preserve the original declaration symbol across module edges.
internal static class ModuleLinker
{
    private sealed record ExportBinding(Token Local,Token? From);
    private sealed record TypeExportBinding(Token Local,Token? From);
    private sealed class ModuleNode(ParsedSource source,Scope scope)
    {
        public ParsedSource Source { get; }=source;
        public Scope Scope { get; }=scope;
        public Dictionary<string,ExportBinding> Exports { get; }=new(StringComparer.Ordinal);
        public Dictionary<string,TypeBinding> TypeExports { get; }=new(StringComparer.Ordinal);
        public Dictionary<string,TypeExportBinding> TypeReexports { get; }=new(StringComparer.Ordinal);
    }

    public static Dictionary<string,Scope> Link(IReadOnlyList<ParsedSource> sources,Scope global,
        IReadOnlyDictionary<string,Dictionary<int,string>>? edges,StringComparer comparer)
    {
        var nodes=new Dictionary<string,ModuleNode>(comparer);
        foreach(var source in sources)
        {
            var node=new ModuleNode(source,Binder.BindModule(source.File,source.Root,global));
            void Add(Token exported,Token local,Token? from)
            {
                if(!node.Exports.TryAdd(exported.Text,new ExportBinding(local,from)))
                    throw new CompilationFailure(Diagnostic.Error(source.File,exported,"TSCS1001",
                        "Duplicate module export names are not supported yet."));
            }
            void AddType(Token exported,Token local,Token? from)
            {
                if(node.TypeExports.ContainsKey(exported.Text) ||
                   !node.TypeReexports.TryAdd(exported.Text,new TypeExportBinding(local,from)))
                    throw new CompilationFailure(Diagnostic.Error(source.File,exported,"TSCS1001",
                        "Duplicate type export names are not supported yet."));
            }
            void AddDirectType(Token name)
            {
                if(node.TypeReexports.ContainsKey(name.Text) ||
                   !node.TypeExports.TryAdd(name.Text,node.Scope.Types[name.Text]))
                    throw new CompilationFailure(Diagnostic.Error(source.File,name,"TSCS1001",
                        "Duplicate type export names are not supported yet."));
            }
            foreach(var statement in source.Root.Statements)
            {
                if(statement is Variable { Exported:true } variable)
                    Add(variable.Name,variable.Name,null);
                else if(statement is FunctionDeclaration { Exported:true } function)
                    Add(function.Name,function.Name,null);
                else if(statement is TypeAliasDeclaration { Exported:true } alias)
                    AddDirectType(alias.Name);
                else if(statement is InterfaceDeclaration { Exported:true } iface)
                    AddDirectType(iface.Name);
                else if(statement is ExportDeclaration declaration)
                    foreach(var specifier in declaration.Specifiers)
                    {
                        if(declaration.TypeOnly)AddType(specifier.Exported,specifier.Local,declaration.ModulePath);
                        else Add(specifier.Exported,specifier.Local,declaration.ModulePath);
                    }
            }
            nodes.Add(source.File,node);
        }

        string Target(string file,Token modulePath)
        {
            if(edges==null || !edges.TryGetValue(file,out var imports) ||
               !imports.TryGetValue(modulePath.Start,out var targetFile))
                throw new CompilationFailure(Diagnostic.Error(file,modulePath,"TSCS1001",
                    "Module imports and re-exports require a host-resolved source graph."));
            if(!nodes.TryGetValue(targetFile,out var target))
                throw new CompilationFailure(Diagnostic.Error(file,modulePath,"TSCS1001",
                    "The referenced source is not an ES module."));
            return target.Source.File;
        }

        Symbol? Resolve(string file,string exportedName,List<(string File,string Name)> active)
        {
            var module=nodes[file];
            // Host comparers may equate different spellings (e.g. B.ts and b.ts).
            // All downstream diagnostics and source positions use the loaded AST's identity.
            file=module.Source.File;
            if(!module.Exports.TryGetValue(exportedName,out var entry))return null;
            if(active.Count>=256 || active.Any(x=>comparer.Equals(x.File,file) &&
                StringComparer.Ordinal.Equals(x.Name,exportedName)))
                throw new CompilationFailure(Diagnostic.Error(file,entry.Local,"TSCS1001",
                    "Cyclic or excessively deep module re-exports are not supported yet."));
            active.Add((file,exportedName));
            try
            {
                if(entry.From is { } from)
                {
                    var target=Target(file,from);
                    var external=Resolve(target,entry.Local.Text,active);
                    if(external==null)
                        throw new CompilationFailure(Diagnostic.Error(file,entry.Local,"TS2305",
                            $"Module '{from.Value}' has no exported member '{entry.Local.Text}'."));
                    return external;
                }
                if(module.Scope.Symbols.TryGetValue(entry.Local.Text,out var local))
                    return local;
                foreach(var import in module.Source.Root.Statements.OfType<ImportDeclaration>().Where(x=>!x.TypeOnly))
                {
                    foreach(var specifier in import.Specifiers)
                    {
                        if(!StringComparer.Ordinal.Equals(specifier.Local.Text,entry.Local.Text))
                            continue;
                        var target=Target(file,import.ModulePath);
                        var imported=Resolve(target,specifier.Imported.Text,active);
                        if(imported==null)
                            throw new CompilationFailure(Diagnostic.Error(file,specifier.Imported,"TS2305",
                                $"Module '{import.ModulePath.Value}' has no exported member '{specifier.Imported.Text}'."));
                        return imported;
                    }
                }
                throw new CompilationFailure(Diagnostic.Error(file,entry.Local,"TS2304",
                    $"Cannot find name '{entry.Local.Text}'."));
            }
            finally { active.RemoveAt(active.Count-1); }
        }

        // Follow type-only reexports through the canonical module graph before
        // staging import-type aliases, preserving the original TypeBinding.
        TypeBinding? ResolveType(string file,string exportedName,List<(string File,string Name)> active)
        {
            var module=nodes[file];
            file=module.Source.File;
            if(module.TypeExports.TryGetValue(exportedName,out var direct))return direct;
            if(!module.TypeReexports.TryGetValue(exportedName,out var entry))return null;
            if(active.Count>=256 || active.Any(x=>comparer.Equals(x.File,file) &&
                StringComparer.Ordinal.Equals(x.Name,exportedName)))
                throw new CompilationFailure(Diagnostic.Error(file,entry.Local,"TSCS1001",
                    "Cyclic or excessively deep type re-exports are not supported yet."));
            active.Add((file,exportedName));
            try
            {
                if(entry.From is { } from)
                {
                    var target=Target(file,from);
                    var binding=ResolveType(target,entry.Local.Text,active);
                    if(binding is null)
                        throw new CompilationFailure(Diagnostic.Error(file,entry.Local,"TS2305",
                            $"Module '{from.Value}' has no exported type member '{entry.Local.Text}'."));
                    return binding;
                }
                if(module.Scope.Types.TryGetValue(entry.Local.Text,out var local))return local;
                foreach(var import in module.Source.Root.Statements.OfType<ImportDeclaration>().Where(x=>x.TypeOnly))
                    foreach(var specifier in import.Specifiers)
                        if(StringComparer.Ordinal.Equals(specifier.Local.Text,entry.Local.Text))
                        {
                            var target=Target(file,import.ModulePath);
                            var binding=ResolveType(target,specifier.Imported.Text,active);
                            if(binding is null)
                                throw new CompilationFailure(Diagnostic.Error(file,specifier.Imported,"TS2305",
                                    $"Module '{import.ModulePath.Value}' has no exported type member '{specifier.Imported.Text}'."));
                            return binding;
                        }
                throw new CompilationFailure(Diagnostic.Error(file,entry.Local,"TS2304",
                    $"Cannot find name '{entry.Local.Text}'."));
            }
            finally { active.RemoveAt(active.Count-1); }
        }

        // Invalid re-exports must fail even when no consumer imports them.
        foreach(var (file,node) in nodes)
            foreach(var name in node.TypeReexports.Keys)
                _=ResolveType(file,name,[]);

        // Type imports are linked before any value signatures are prebound:
        // exported functions may reference type names imported from another file.
        // The original declaring scope stays attached to each TypeBinding.
        foreach(var (file,node) in nodes)
        {
            foreach(var import in node.Source.Root.Statements.OfType<ImportDeclaration>().Where(x=>x.TypeOnly))
            {
                var target=Target(file,import.ModulePath);
                foreach(var specifier in import.Specifiers)
                {
                    var binding=ResolveType(target,specifier.Imported.Text,[]);
                    if(binding is null)
                        throw new CompilationFailure(Diagnostic.Error(file,specifier.Imported,"TS2305",
                            $"Module '{import.ModulePath.Value}' has no exported type member '{specifier.Imported.Text}'."));
                    Binder.ImportType(file,node.Scope,specifier,binding);
                }
            }
        }
        // Invalid export names fail even without consumers. Resolving names
        // here also rejects re-export cycles before any type query is evaluated.
        foreach(var (file,node) in nodes)
            foreach(var name in node.Exports.Keys)
                _=Resolve(file,name,[]);

        // Link value import *identities* before resolving aliases. The exported
        // declaration can be forward/cyclic or use import-type names from its
        // own module. Its semantic type is not yet available: only TypeResolver
        // may inspect the original annotated declaration at this stage.
        var pending=new List<(string File,ImportSpecifier Specifier,Symbol Imported,Symbol Exported)>();
        foreach(var (file,node) in nodes)
        {
            foreach(var import in node.Source.Root.Statements.OfType<ImportDeclaration>().Where(x=>!x.TypeOnly))
            {
                var target=Target(file,import.ModulePath);
                foreach(var specifier in import.Specifiers)
                {
                    var exported=Resolve(target,specifier.Imported.Text,[]);
                    if(exported==null)
                        throw new CompilationFailure(Diagnostic.Error(file,specifier.Imported,"TS2305",
                            $"Module '{import.ModulePath.Value}' has no exported member '{specifier.Imported.Text}'."));
                    var imported=Binder.StageImport(file,node.Scope,specifier,exported);
                    pending.Add((file,specifier,imported,exported));
                }
            }
        }

        // Every module has its complete type and imported-value name spaces.
        // Imported aliases are not treated as local declarations during this pass.
        foreach(var node in nodes.Values)Binder.InitializeDeclaredTypes(node.Scope);
        // Now restore the normal runtime/checker invariant: every value import
        // must have a known semantic type, without inventing an any fallback.
        foreach(var (file,specifier,imported,exported) in pending)
            Binder.CompleteImport(file,specifier,imported,exported);

        return nodes.ToDictionary(pair=>pair.Key,pair=>pair.Value.Scope,comparer);
    }
}

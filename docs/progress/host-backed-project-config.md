# Host-owned tsconfig project loading (bounded implementation)

Dependency path: \`CommandLine.Run(host)\` → \`Options.ReadConfig\` →
\`host.ReadFile(tsconfig)\` / \`JsonSyntax\` / \`JsonEnumOptions\` →
\`ProjectSourceDiscovery.Discover(host)\` → \`CompilerProgram.Load(host)\` →
shared AST / Binder / TypeChecker / optional \`Program.EmitJavaScript\`.

The host now resolves the project directory, reads JSON and explicit \`files\`
entries, enumerates included source paths, and loads source contents. For
project-directory CLI arguments, \`IProjectSourceHost.DirectoryExists\`
is an additive optional capability; existing VFS implementations accepting
explicit config filenames do not need to change. Diagnostics keep the
virtual configuration's source identity. Case-sensitive or case-insensitive
source suffix and duplicate-root handling follow \`FileNameComparer\`.

The physical filesystem CLI retains its existing safe create-new output
contract. Injected VFS CLI runs do not write to the process filesystem;
callers may use the checked \`CompilerProgram.EmitJavaScript\` result in memory.

This is not full TS7 tsconfig support: no default implicit includes, extends,
project references, watch, TSX/JS, general module resolution or VFS output
transaction API. No original pinned compatibility target is promoted by
internal host regressions: original PASS 0, registered
SKIP_UNIMPLEMENTED 15,523, dynamic expansion unknown. The new source
requires exact-head compilation, CI and review before merge.

Next dependency: common diagnostic provenance across virtual config/source
paths, and host-owned output transactions (only when a real emit use case
requires them), while preserving the existing no-overwrite guarantee.

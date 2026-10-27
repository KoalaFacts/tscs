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

## Source-bound project configuration validation

Unsupported/duplicate config keys, wrong `files` / `include` /
`exclude` / `compilerOptions` value shapes, and valid-but-unimplemented
module/target choices now use source-bound diagnostics with the canonical
configuration file identity and UTF-16 token ranges. The existing
`ConfigDiagnosticFailure` also carries scanner/parser and pinned converter
results. Unsupported `TSCS1002` retains exit 1; pinned TypeScript diagnostics
retain exit 2. In-memory CLI regressions cover the semantic failure families.

This is not complete TS7 config semantics: default includes, `extends`,
custom module resolution and extra emit modes remain outside this bounded
package. Original accepted PASS remains 0 / 15,523 registered
SKIP_UNIMPLEMENTED; dynamic expansion is unknown.

## Project source selector provenance (next bounded work package)

`Options.ReadConfig` now validates every explicit include/exclude pattern
through the existing `ProjectSourceDiscovery` matcher while retaining the
original JSON string token, before any host directory enumeration. Malformed
absolute/traversal/unsupported globs and the shared 64-pattern limit report
source-bound `TSCS1002` on the specific selector. Duplicate explicit `files`
paths now use the host's own case-sensitive or case-insensitive identity and
point at the second JSON string, not a fileless CLI error. The direct
`ProjectSourceDiscovery.Discover` API and safety bounds remain unchanged.

The same in-memory CLI → project loader path verifies each error family and
its UTF-16 span. Source code is pending exact-head build, CI and review;
these internal regressions grant no original TS7 acceptance credit.
Next dependency: effective project-configuration snapshots and config-origin
provenance for default source discovery when that feature is implemented.

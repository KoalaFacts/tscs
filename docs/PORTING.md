# Source correspondence

Reference: microsoft/TypeScript 8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f.
This bootstrap is a bounded adaptation, **not a completed function-by-function port**.

| C# module | Reference path / behavior | Current boundary |
|---|---|---|
| Scanner | tsc/internal/scanner/scanner.go, Scan | Trivia, identifier/keyword, decimal integer and quoted string paths; other tokens fail explicitly |
| Parser | tsc/internal/parser/parser.go, parseVariableDeclarationList / parseVariableDeclarationWorker | Named primitive declarations, blocks, references, assignments, restricted recovery |
| Binder | tsc/internal/binder/binder.go, bindBlockScopedDeclaration | Predeclared block tables; no module or declaration merging |
| TypeChecker | tsc/internal/checker/checker.go, checkVariableLikeDeclaration / checkIdentifier / checkBinaryLikeExpression | Primitive relations, missing names, const assignment; no generic relation engine |
| Compilation | tsc/internal/compiler/program.go, GetDiagnosticsOfAnyProgram | Syntax-before-semantics for supported single-file cases |
| CommandLine | tsc/internal/execute/tsc/emit.go | Diagnostic presentation and noEmit exit status; restricted options, not full CLI parity |

Go source offsets are UTF-8 byte offsets; tools/oracle/main.go converts locations
into UTF-16 offsets for comparison with C#. CLI columns are separately compared.
The oracle uses upstream diagnostic sorting/deduplication and the noEmit sequence.

Intentional limits: the initial core checks an in-memory file and the CLI loads real
files; a general virtual filesystem/project host is not yet ported. Standard-library
bootstrap accepts only explicitly supplied empty global interfaces. Type identities,
module resolution, declaration merging, full Unicode identifier support, full
numeric/string syntax and all checker branches require further upstream porting.
Unsupported behavior cannot be interpreted as a conformance pass.

## Shared global-script compilation (2026-10-27; PR #8)

The C# compiler now routes both one-file and multiple-file inputs through
`Compilation.AnalyzeFiles`: lex and parse separate source texts into
file-identified ASTs, prebind top-level `let`/`const` symbols from every
script, then check primitive expressions using a common global scope.
Symbol records carry their declaring file, preventing UTF-16 offsets from
unrelated files being mistaken for same-file forward references. The CLI
accepts multiple noEmit/noLib `.ts` files or the corresponding tsconfig
`files` array and formats each diagnostic against its owning source.

This is a bounded global-script path, not complete TS7 program semantics.
Cross-file types are prebound only for explicit primitive/any annotations
or primitive literals. Unsupported complex inferred types, imports,
module linking, declaration merging, standard libraries, and general
JavaScript emission are not silently accepted. It creates the dependency
for later CompilerHost/VFS, module resolution and multi-file emit work.
No full original upstream configuration target has yet been accepted.

## CompilerHost and program snapshots (2026-10-27; PR #8)

`ICompilerHost` owns source text acquisition and canonical file identities;
`FileSystemCompilerHost` is the CLI adapter, and alternate in-memory hosts
can supply unsaved editor buffers. `CompilerProgram.Load` snapshots each
canonical source exactly once, rejects duplicate identities (using the host's
filename comparer), and reuses one analysis result. The CLI runs its existing
bounded multi-source binder/checker through this shared program path, while
its special minimal `.d.ts` global bootstrap remains separate. Parsing now
collects syntax failures from all input files before global binding, preserving
file identities and ordered UTF-16 spans instead of returning on the first
bad source. No module graph, symlink resolution, incremental invalidation,
general standard-library support, or TypeScript emit is claimed.

Next dependencies: typed options/project host, proper module/source-file graph,
per-source resilient binding, then unified emit. The pinned full original
acceptance ledger remains separate from these local architecture checks.

## Host-backed path-reference source graph (2026-10-27; PR #8)

`CompilerProgram.Load` now expands a bounded, transitive set of **leading**
`/// <reference path="./other.ts" />` source dependencies through
`ICompilerHost.ResolveReferencePath`. The host canonicalizes each dependency,
and a visited-set prevents duplicate source reads across diamonds and cycles.
These referenced global scripts pass through the *same* multi-file binder and
checker as explicit inputs. The lexer admits only correctly formed leading path
references; unsupported triple-slash directives still fail closed.

This is a real source-graph path for global-script primitive programs, **not**
ES module import/export resolution, `types`/`lib` references, declaration-file
semantics, symlink identity, project references or full TS7 resolution rules.
General virtual hosts can override relative dependency resolution. Root aliases
remain errors, and missing referenced files are not silently skipped.

## Bounded ES named-module symbols (2026-10-27; PR #8)

The host-backed loader discovers relative `import { name as alias } from "./module"`
source edges using the real Scanner/Parser AST, with explicit `.ts` and bounded
extensionless module paths. The binder gives modules separate top-level scopes,
prebinds primitive typed exports, and links **only explicitly exported**
`let`/`const` symbols into importing scopes. Global-script symbols remain a
separate shared scope. Imported aliases are read-only; missing exports and
unsupported relative forms fail closed instead of leaking globals. The checker
still supports only its bounded primitive expression semantics, and the host
currently reparses module syntax during analysis after graph discovery.

This is not package/node resolution, default/namespace/re-export, JS output,
ambient modules, type-only imports, mutable live bindings, circular runtime
initialization, or full TypeScript module semantics. No upstream acceptance is
credited without the independent reference and complete original assertions.

## Shared source syntax snapshots (2026-10-27; PR #8)

The host-backed dependency loader and `Compilation.AnalyzeFiles` now share
one scanner/parser result per canonical source, preserving either its AST or its
real lexical/parser/unsupported diagnostics. This removes duplicate parsing for
imported modules without adding a second syntax path. CLI JSON retains the
existing basename contract for unambiguous inputs, using unique relative paths
when loaded source basenames collide. Full TS7 module semantics are still
unimplemented; these changes do not award upstream acceptance.

## Bound local module exports (2026-10-27; PR #8)

The shared Program AST now feeds a separate module export table in Binder.
Local exports retain their declaring symbol across public aliases and source
order; `export {}` creates a module boundary without exposing private globals.
The importer uses this table rather than searching exported variable ASTs
by name. This is a concrete prerequisite for later re-export graphs and richer
symbol linking, not a claim that those features are implemented. Imported
binding re-exports, `from` re-exports, default/namespace exports, packages,
full module resolution and emit still fail closed. CLI source identities
normalize only actual host-platform separators: Unix literal backslashes
remain distinct source characters.

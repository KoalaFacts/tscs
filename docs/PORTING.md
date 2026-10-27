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

## Deferred named value module linking (2026-10-27; PR #8)

`ModuleLinker` prebinds every module's lexical scope, then resolves named
value exports through the host's source graph. An exported name may map to a
local declaration, an imported alias or `export { name as alias } from "./path"`.
Public names preserve the original declaration symbol and file ownership,
without exposing module-private bindings. Non-recursive circular value imports
resolve against prebound exported declarations. Cyclic/deep re-export aliases,
duplicate exported names, missing members and non-module targets fail closed.

This is deliberately bounded named-value linking: no star/default/namespace
or type-only exports, package resolution, type imports, full TS7 module
semantics, runtime initialization, or JavaScript emit. Original upstream
acceptance remains independent from these internal architecture checks.

## Host-canonical module diagnostic provenance (2026-10-27; PR #8)

Module graph edges retain their host-resolved target paths, which can differ in
case from the single loaded SourceInput identity on case-insensitive hosts.
ModuleLinker normalizes a resolved target to the matching ParsedSource.File and
uses that identity through recursive export linking and error construction.
The CLI uses ICompilerHost.FileNameComparer to retrieve diagnostic source text.
This avoids attributing module-cycle diagnostics to an edge spelling while
formatting UTF-16 offsets against another source. It does not add module
resolution beyond the bounded relative paths or release acceptance.

## Shared semantic type identity (2026-10-27; PR #8)

`TypeSymbol` is the single semantic representation of the implemented
`number`, `string`, `boolean` and bounded `any` types. Parser and AST
preserve the source annotation spelling; Binder prebinds canonical types,
ModuleLinker transfers typed symbol information across imports, and
TypeChecker applies the same explicit primitive assignability relation to
declarations and assignments. Unknown inference remains `null` and is
not promoted to `any`. This is a reusable architecture change, not support
for functions, unions, generics or full TypeScript type relations.


## Explicit function signatures and calls (2026-10-27; PR #8)

The scanner/parser now admit named source-level functions with required parameter
and return primitive annotations, a bounded statement body ending in a direct
value `return`, and identifier call expressions with a bounded call nesting depth.
Binder predeclares callable symbols alongside variables, creates distinct typed
parameter scopes, and ModuleLinker shares the same symbol identity for exported
functions. TypeChecker checks call arity, parameter assignability, and return
assignability through `FunctionTypeSymbol`; ordinary functions are available
before their textual declaration, unlike let/const. This is intentionally a
small real working function family, not TypeScript's full callable type system,
closures, overloads, defaults, recursion analysis, hoisting runtime semantics
or JavaScript emit. The frozen upstream original test ledger remains separate.

## Function return-flow boundary (2026-10-27; PR #8)

The Parser retains the exact Scanner line-break flag on the token following
`return`. Under the current value-return-only subset, a line terminator after
`return` (including CRLF or one inside a block comment) is rejected rather
than silently treating the following expression as the return value.
`FunctionControlFlow` now analyzes unconditional sequential/nested lexical
blocks for a definite value return; TypeChecker enforces that semantic
requirement instead of the Parser requiring a direct last statement.
This permits nested straight-line return blocks without treating functions
with no returning path as implemented. There are no branches/loops, void
returns, full ASI semantics or generalized TypeScript control-flow checks yet;
the complete original TS7 acceptance registry is unchanged.

## Braced conditional flow (2026-10-27; PR #8)

The shared parser retains braced `if / else if / else` conditions as
`ConditionalStatement` nodes. `FunctionControlFlow` recognizes a definite
return when both arms return or a following statement returns, while the
existing TypeChecker checks each arm with lexical scopes, restores outer
symbol initialization between arms, and joins definite initialization across
only arms that can reach the following statement. A returning arm contributes no
uninitialized state to that join; when both arms continue, both must initialize
the variable. Bounded conditions permit boolean or any.
Unbraced arms, general truthiness, complex predicates, control-flow
narrowing, loops and the general TypeScript function model remain unsupported.
No fixed TS7 upstream acceptance SKIP is removed without genuine product
assertions and pinned reference results.

## Shared primitive expression precedence (2026-10-27; PR #8)

The same Scanner and AST expression pipeline now supports parenthesized groups,
boolean unary negation, strict/loose equality and inequality, numeric relational
comparisons and boolean `&&` / `||`. Parser precedence is multiplicative,
additive, relational, equality, logical AND and logical OR, with iterative
TypeChecker evaluation of left-deep operator chains. Grouping tokens stay in
`ValueParenthesized` instead of being discarded, so future emission can share
the same AST. These expressions work in existing conditional, variable,
assignment, call and return contexts; this work does **not** add JavaScript emit.

Semantic boundaries are explicit: logical binary operands must be boolean;
relational operands must be number; equality accepts matching primitive types
or `any`, but not unrelated/structural types; unary `!` accepts boolean or
`any`. Other TS/JS coercions, truthiness, short-circuit flow side effects,
operator overloading, optional chaining, nullish coalescing and generalized
TypeScript expression inference remain unsupported and fail closed. No new
upstream full-original acceptance target is credited by this local feature.

## AST-backed bounded ES2015 global-script output (2026-10-27)

`CompilerProgram.EmitJavaScript` consumes the existing immutable parsed
`SourceFile` snapshots **after** one `Analyze()` check; it does not create
an alternative parser, binder, checker, or source-file identity. Its
`JavaScriptEmitter` lowers the supported primitive statements and expressions
to ES2015, with type annotations erased and precedence-preserving iterative
expression traversal. No output is available for code containing diagnostics,
module imports/exports, missing snapshots or unsupported grammar. The CLI
requires `--emitJs --noLib --target es2015` (without `--noEmit`), retains
minimal ambient global declarations and refuses to overwrite any `.js`.
This is a bounded working script pipeline, not an implementation of TS7
commonjs/ES module lowering, standard libraries, declaration/maps, plugins,
transforms, or the full upstream emit baseline. The separate original
15,523 configuration targets remain SKIP_UNIMPLEMENTED until an exact
product/reference acceptance run matches all original expectations.

Next dependency: typed emit options and full module-aware lowering with
preserved module identities, before declaration/map generation.

## Bounded named ES module output (2026-10-27)

The shared cached module graph resolves relative `.js` module specifiers to
corresponding `.ts` sources for type checking, while the emitter preserves
the runtime `.js` spelling. Explicit `--emitJs --module es2015 --noLib
--target es2015` uses the existing Binder/ModuleLinker identity and one checked
Program snapshot; named imports, value exports and named re-exports are written
as ES2015 module syntax, without inventing a second parser or treating internal
passes as original TS7 acceptance. CLI preflight/rollback remains shared with
script output. No commonjs, extension rewriting for arbitrary specifiers,
non-named syntax, package resolution, source maps or declaration emission.

## Project ES2015 module option through the shared emit path (2026-10-28)

The bounded `tsconfig.json` `compilerOptions.module` adapter now uses the frozen
TS7 enum declaration and reports its diagnostics for invalid values. `es2015`
and `es6` select the same effective module mode as the existing CLI
`--module` flag, while `null` means unset; other module modes remain
explicitly unsupported. The independent `--emitJs` opt-in is still required
for output, and the existing `noLib`/`target` restrictions still apply.
A project can now supply its `files`, `module`, `target` and `noLib` in
one config, with `--emitJs -p tsconfig.json` emitting a checked multi-source
named ESM graph. No second parser/emit pipeline or default output behavior was
introduced. Project `extends`, `include`, module-resolution modes, CommonJS,
source maps, declarations and general TS7 compiler options remain unsupported.
A Node24 runtime smoke compiles from a project config and executes the output;
this is not a full original upstream TS7 acceptance PASS.

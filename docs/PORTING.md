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

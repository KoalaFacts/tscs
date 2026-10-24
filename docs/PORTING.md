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

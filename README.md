# tscs

A TypeScript compiler written in C#.

**Experimental bootstrap.** The current implementation checks a small subset of
TypeScript; it does not generally emit JavaScript or replace `tsc` for real projects.
The goal is compatibility with a fixed upstream compiler before expanding coverage
and optimizing the C# implementation.

## Bounded diagnostic directive support

The current `noEmit/noLib` checker supports a restricted **leading, single-line**
`// @ts-ignore` directive: it suppresses semantic checker diagnostics **only
on the immediately following physical line**. It does not suppress scanner or
parser errors, does not implement block-comment, trailing or
`@ts-expect-error` variants, and does not establish full TS7 directive
compatibility. The original pinned upstream acceptance remains NOT_RUN.

## Build and check a file

Install the .NET SDK specified in `global.json` (10.0.402). Node 24 runs the native `.ts` test tools directly, without a transpiler. Go 1.27.3 is also required for source-inventory tests. The compiler itself is C# and does not invoke Node, Go or another compiler.

```sh
dotnet build Tscs.slnx -c Release -m:1
dotnet src/Tscs.Cli/bin/Release/net10.0/tscs.dll \
  --noEmit --noLib --target es2015 \
  fixtures/local/number-mismatch.ts fixtures/minimal.d.ts
```

This intentionally reports TS2322 and exits 2. Use `fixtures/local/number-ok.ts`
for a successful check. `fixtures/minimal.d.ts` supplies **empty global interfaces
for isolated compiler tests**, not a usable standard library.

Supported initially: single-file `let`/`const` declarations, primitive annotations,
primitive literal initializers, identifier references, simple assignments and
nested lexical scopes. Bounded `any` variable annotations accept assignments to
and from primitive types, without implementing the full TypeScript `any` type system. Bounded arithmetic supports numeric unary `+`/`-`, numeric binary `+`/`-`/`*`, numeric identifiers and assignments, with `*` binding more tightly than left-associative `+`/`-`. Long left-deep and `+`/`-` unary chains are checked iteratively instead of recursing the C# call stack. Numeric unary plus is a bounded `number`-only type-checker path, not JavaScript coercion or output. `--` decrement, nonnumeric unary-plus coercion, string concatenation, `any` arithmetic, parentheses, exponentiation and full expression grammar remain unsupported; this does not implement JavaScript emit. Integer decimal literals and quoted strings with simple escapes/line continuations are supported. Valid hexadecimal and Unicode escapes preserve UTF16 code units, including lone surrogates. Malformed hexadecimal/Unicode escapes now retain their spelling and report recoverable lexical diagnostics; unterminated strings preserve all lexical errors. Standalone NUL and identity escapes are supported; legacy octal and decimal escapes preserve decoded values while reporting TS1487/TS1488 syntax errors. This does not implement JSON-specific string validation. Compilation returns lexical errors before parser/checker work; this does not implement general parser recovery.
An initialized, mutable `number` variable also supports a **statement-only**
postfix update such as `let n = 1; n++;` under noEmit/noLib. The checker
rejects `const` updates with TS2588. Prefix `++`, expression-position
postfix updates, nonnumeric operands and emitted JavaScript remain unsupported.
This check does not execute the increment.

Both `--noEmit` and `--noLib` are required for every **nonempty** program.
A separately tested first original-fixture slice now handles a *single trivia-only*
ES2015 `.ts` program under the original default emit/library flags, provided
the target is explicitly selected: it emits the pinned strict prologue to a
new `.js` file, refusing to overwrite an existing file. This is **not**
general JavaScript emission, standard-library checking or original harness
acceptance; the corresponding generated upstream Fact remains SKIP pending
a live independent pinned-Go reference and complete original runner checks. `--pretty false`, `--target es2015/es6`
and a tscs-only `--json` diagnostic mode are available.

A minimal `tsconfig.json` may specify `files` and `compilerOptions.noEmit`, `noLib`,
and `target`. File paths resolve relative to the config. Run it with `-p path/to/tsconfig.json`.
The config path now uses the C# scanner and a bounded JSON AST reader for objects,
arrays, strings, booleans/null and decimal integers (including unary minus).
Numeric AST text uses binary64 rounding and JavaScript fixed/scientific formatting;
raw source spelling and token ranges are retained.
Comments/trailing commas are supported; names and string values get TS1327 quote
validation. Other numeric forms, non-ASCII unquoted names, general syntax recovery
and effective TypeScript configuration expansion remain unsupported. This is not
full TS7 JSONC/config compatibility. System.Text.Json is used for diagnostic output,
not configuration parsing.
The primitive option converter preserves supported values and reports pinned
TS5024 type errors / TS6266 command-line-only restrictions. Its CLI integration
currently covers noEmit/noLib booleans, including null defaults and CLI overrides;
other option types, paths, lists, enums and the config host remain incomplete.

One or more non-declaration `.ts` **global script** files are supported under
`--noEmit --noLib`, with optional minimal global `.d.ts` files. Primitive
top-level symbols can be referenced across files independently of input
order. Complex cross-file inference and module linking still fail closed;
this is not general TypeScript project compilation. Ambient files remain
limited to the empty global interfaces shown in the example.

Unsupported constructs/options return a TSCS diagnostic and exit 1. Ordinary
TypeScript diagnostics return 2; a successful supported check returns 0.
Functions, imports, objects, generics, JSX, standard libraries, declaration merging,
reads before initialization and JS output remain unsupported. This is not a sandbox
for executing TypeScript; no code is executed.

The intrinsic `globalThis` exists even with `--noLib`, but its type is not yet
implemented here: reading or assigning it reports TSCS1001 and exits 1. Block-local
variables named `globalThis` resolve normally; top-level declarations still report
TS2397. Truly unresolved names report TS2304 and exit 2.

## Verify

```sh
./scripts/verify.sh
```

This restores locked dependencies, builds, runs xUnit and harness tests, and compares
29 frozen cases against independently captured upstream diagnostics, CLI text and
exit codes. Seven source fixtures are copied unchanged from upstream. They run with
explicit `noEmit`/`noLib` isolation overrides; these are not full upstream harness runs.
Results go to `.artifacts/conformance.json`. A passing cohort is not a percentage
claim about the entire TypeScript language.

The C# suite has 15,488 verified cases at compiler implementation head `b69e30e` (xUnit 4/MTP, zero failures/skips) (including 764 byte-decoding, 535 directive, 151 state and 750 path and 55 file-map and 154 VFS read plus 534 accessible-entry and 1,414 option-value and 2,920 configuration-product and 36 source-configuration vectors) and uses xUnit 4.0.2 (the official package is still named
`xunit.v3.mtp-v2`) with Microsoft.Testing.Platform 2.5.1, not VSTest.
After building, run just this suite with:

```sh
dotnet test --solution Tscs.slnx -c Release --no-build --no-restore -- --fail-skips on --minimum-expected-tests 15488
```

The .NET 10 MTP runner is selected in `global.json`. If an execution environment
cannot launch the SDK's test coordinator, the same built MTP executable can also
be run directly, without changing permissions or disabling isolation:

```sh
dotnet tests/Tscs.Compiler.Tests/bin/Release/net10.0/Tscs.Compiler.Tests.dll --fail-skips on --minimum-expected-tests 15488
```

Record direct execution separately from `dotnet test`; a direct pass is not a
claim that the SDK coordinator ran. The unchanged test inventory is documented
in [the migration map](docs/evidence/xunit-migration-inventory.json).

To rebuild the independent reference (requires Git and Go 1.27.3):

```sh
./scripts/build-oracle.sh
# Or use an existing clean checkout at the exact pin:
TSCS_UPSTREAM=/path/to/TypeScript ./scripts/build-oracle.sh
```

The script compares the rebuilt reference against frozen results without rewriting
them. Build hashes may differ across paths/platforms; they are recorded as provenance,
not used as a cross-platform reproducible-binary guarantee.

## Explicit implementation SKIPs (separate from internal tests)

The [bounded implementation register](docs/testing/ts7/implementation-coverage.json) marks seven **already existing** local guard inputs as `SKIP / UNIMPLEMENTED` and seven positive inputs as `IMPLEMENTED / upstream NOT_RUN`. The guard tests still execute; their passing rejection assertions do not mean the missing TypeScript features work. A dedicated CI check binds this report to its exact source inputs and rejects unexpected skips, while the xUnit fail-skips gate remains active. This is a starter catalogue, **not the full upstream completion percentage or a release waiver**. [Details](docs/progress/implementation-coverage.md).

## Pinned TS7 test-scope inventory

The [source-configuration placeholder catalogue](docs/progress/upstream-placeholder-catalogue.md) generated **15,523 SKIP targets**. A [post-generation verifier now checks](docs/progress/upstream-placeholder-integrity.md) every target against all C# Fact identities and raw SHA-256 evidence. The generated .NET project is not yet compiled/discovered; these are not full runtime TS7 cases or accepted results.

The [source-scope registry and roadmap](docs/testing/ts7/README.md) inventories all
tracked upstream assets, Go declarations, JS test sources and fixture metadata:

```sh
node tools/inventory-ts7.ts /path/to/pinned-upstream .artifacts/ts7-scope
```

This static inventory is not full conformance: its original25 transpile inputs
have28 source-bound named configurations. The separate complete source
configuration profile records15523 runner configurations with12999 MATCH and
7 reference fatals; effective options and runtime cases remain unresolved.
The selected-config AST profile retains172 config sources (171 MATCH and one
MATCH_DIAGNOSTICS), while12834 no-config sources are NOT_APPLICABLE. It is pre-VFS
syntax preparation, not effective configuration or compiler acceptance. See the
[source AST ledger](docs/testing/ts7/source-config-ast-progress.json).
All original-scope compiler acceptance remains NOT_RUN. The
[domain summary](docs/testing/ts7/inventory-summary.json) keeps assets, declarations,
fixture inputs and upstream skips separate. The original 29-case cohort remains
independent. Release still requires complete scope and executable acceptance with
no missing cases.

See [status](docs/STATUS.md), [source correspondence](docs/PORTING.md), and the
[approved design](docs/superpowers/specs/2026-10-24-tscs-compiler-design.md).
Apache-2.0; see [NOTICE](NOTICE) for upstream attribution.

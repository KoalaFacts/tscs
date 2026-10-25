# tscs

A TypeScript compiler written in C#.

**Experimental bootstrap.** The current implementation checks a small subset of
TypeScript; it does not yet emit JavaScript or replace `tsc` for real projects.
The goal is compatibility with a fixed upstream compiler before expanding coverage
and optimizing the C# implementation.

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
nested lexical scopes. Integer decimal literals and unescaped quoted strings only.
Both `--noEmit` and `--noLib` are required. `--pretty false`, `--target es2015/es6`
and a tscs-only `--json` diagnostic mode are available.

A minimal `tsconfig.json` may specify `files` and `compilerOptions.noEmit`, `noLib`,
and `target`. File paths resolve relative to the config. Run it with `-p path/to/tsconfig.json`.
Exactly one non-declaration `.ts` file is supported, with optional minimal global
`.d.ts` files. Those files may only contain the empty global interfaces shown in the example.

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

The C# suite now has 2,467 cases (including 764 byte-decoding, 535 directive, 151 state and 750 path and 55 file-map and 146 VFS vectors) and uses xUnit 4.0.2 (the official package is still named
`xunit.v3.mtp-v2`) with Microsoft.Testing.Platform 2.5.1, not VSTest.
After building, run just this suite with:

```sh
dotnet test --solution Tscs.slnx -c Release --no-build --no-restore -- --fail-skips on --minimum-expected-tests 2467
```

The .NET 10 MTP runner is selected in `global.json`. If an execution environment
cannot launch the SDK's test coordinator, the same built MTP executable can also
be run directly, without changing permissions or disabling isolation:

```sh
dotnet tests/Tscs.Compiler.Tests/bin/Release/net10.0/Tscs.Compiler.Tests.dll --fail-skips on --minimum-expected-tests 2467
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

## Pinned TS7 test-scope inventory

The [source-scope registry and roadmap](docs/testing/ts7/README.md) inventories all
tracked upstream assets, Go declarations, JS test sources and fixture metadata:

```sh
node tools/inventory-ts7.ts /path/to/pinned-upstream .artifacts/ts7-scope
```

This is an inventory, not full conformance: the 25 transpile inputs have 28
source-bound named configurations. Compiler/conformance configurations and all
runtime-generated cases remain unexpanded, and all acceptance is NOT_RUN. The
[domain summary](docs/testing/ts7/inventory-summary.json) keeps assets, declarations,
fixture inputs and upstream skips separate. The original 29-case cohort remains
independent. Release still requires complete scope and executable acceptance with
no missing cases.

See [status](docs/STATUS.md), [source correspondence](docs/PORTING.md), and the
[approved design](docs/superpowers/specs/2026-10-24-tscs-compiler-design.md).
Apache-2.0; see [NOTICE](NOTICE) for upstream attribution.

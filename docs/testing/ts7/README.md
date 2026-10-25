# TS7 scope inventory: source declarations, not conformance

The release goal is unchanged: **every test in the pinned TS7 scope must be accounted
for and accepted, with no missing cases**. This first slice does not meet that goal.
It records source assets and declarations, expands the bounded transpile
configuration layer, and exposes the remaining unknown expansion. It neither implements compiler features nor runs the
upstream tests.

## Reproduce

Requires Git, Node 24 and Go 1.27.3. Go is only used for the standard-library AST
parser; the C# compiler does not invoke Go or Node. The command reads the commit in
`upstream.lock.json`, not upstream HEAD or the filesystem checkout contents.

```sh
# Use an existing repository containing the pinned commit, or fetch it:
git init .artifacts/ts7-upstream
git -C .artifacts/ts7-upstream fetch --depth=1 \
  https://github.com/microsoft/TypeScript.git \
  8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f
node tools/inventory-ts7.ts .artifacts/ts7-upstream .artifacts/ts7-scope

# Use a fresh output directory for every run; existing output is never overwritten.
node tools/inventory-ts7.ts .artifacts/ts7-upstream .artifacts/ts7-scope-repeat
cmp .artifacts/ts7-scope/summary.json .artifacts/ts7-scope-repeat/summary.json
cmp docs/testing/ts7/inventory-summary.json .artifacts/ts7-scope/summary.json
```

`summary.json` lists every output's byte length and SHA-256. Artifacts contain no
clock time or machine-specific paths, and generation is byte-for-byte repeatable.
The checked-in [summary](inventory-summary.json) is a small, browsable domain index;
the larger JSONL ledgers remain under ignored `.artifacts/` rather than adding
roughly 50 MB of generated text to Git. Preserve the generated directory or an
archive of it with the summary/checksums when recording an acceptance run.

### Source identity

- Repository: [microsoft/TypeScript](https://github.com/microsoft/TypeScript/tree/8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f).
- Commit: `8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f`.
- Tree: `76db580097168684868ef43674fb554dd3018603`.
- This pin is a monorepo: `tsc/`, `tools/`, `packages/`. It has **no gitlinks,
  `.gitmodules`, or Git symlinks**, so there is no second submodule SHA to invent.
  Future gitlinks are retained with their exact commit and `expanded: false`.
- Git tree entries and blob IDs are verified; all blob bytes receive SHA-256.
  Paths are case-sensitive and slash-preserving; duplicate/unsafe/unreadable paths
  fail instead of being silently dropped. Symlink blobs are recorded, never followed.
- Working-copy modifications and untracked files are intentionally excluded. The
  Go helper is built from this repository, not from any upstream source being read.
- Re-pinning upstream requires reviewing the selector/metadata conventions and
  runner registry, not merely regenerating counts.

## What each ledger means

[VFS entries progress](vfs-entries-progress.json) records a separate case-sensitive
boundary:530 actual-Go golden rows and a1601-case synthetic exploratory profile,
with150 read-operation rows retaining the prior146 inputs/expectations. These
results do not run original compiler fixtures or complete VFS/configuration
profiles. See [evidence](../../evidence/vfs-entries.json).

The separate [loading progress](loading-progress.json) now records a complete
raw-source loading profile:13006 inputs/17804 virtual units independently matched
to actual pinned Go, including all42 upstream-skipped sources. It does not alter
the static inventory's historical decoding/expansion fields or award compiler
acceptance. See [raw evidence](../../evidence/fixture-corpus.json).

```sh
dotnet restore Tscs.slnx --locked-mode --disable-parallel -m:1
node tools/compare-fixture-corpus.ts /path/to/clean-pinned-checkout \
  .artifacts/ts7-scope .artifacts/fixture-corpus
```

The command builds the C# harness in Release, calls the actual upstream generic
parser through a read-only Go test overlay, and preserves full byte results and
per-source IDs. Existing output directories are rejected. It compares raw units,
file/global metadata, links, directory and callback counts; it does not invoke
AST/config callbacks or run original compiler assertions. Implicit-first and
callback failures still have their separate151-vector boundary coverage.

| Artifact | Meaning | Not evidence of |
| --- | --- | --- |
| `assets.jsonl` | Every tracked source/support/baseline asset, mode, blob ID, size, SHA-256 and domain | Runnable tests or coverage |
| `fixtures.jsonl` | Selected compiler/conformance/transpile source inputs; raw metadata, encoding evidence, static skips and separate configuration-expansion status | Effective options, virtual units or executed tests |
| `configurations.jsonl` | Source-bound named configurations for the verified transpile policy only; raw settings and NOT_RUN progress | Runtime transpile kinds, emitted units, successful option parsing or conformance |
| `go-sources.jsonl` | AST declarations, support functions, build constraints, string lists and parse errors | Host package/test discovery |
| `declarations.jsonl` | Top-level Go test/benchmark/fuzz/setup candidates, source coordinates, Run/Skip syntax and initial progress | Evaluated subtests, skips, seeds, build constraints or successful execution |
| `js-sources.jsonl` | JS/TS test and benchmark source files | Expanded imported/generated/dynamic test cases |
| `runner-policies.json` | Exact hashed sources for canonical selectors, parsers, generators and execution profiles | Evaluated runner semantics |
| `progress.json` | Initial per-domain UNIMPLEMENTED / NOT_RUN status and expansion blockers | A release certificate |

IDs derive from the exact source commit, record kind and full path/coordinate,
not basenames. Different commits intentionally have different IDs; future progress
migration must explicitly re-verify mappings. Duplicate fixture directives and raw
values remain visible. No comma-list multiplication is presented as an exact case
total. The bounded configuration layer below models its canonical boolean rules;
compiler enums, defaults, embedded configs and runtime-generated combinations
still need their corresponding semantics.

Go discovery uses `go/parser` and syntax checks corresponding to Go's loader, not
regex matching or `go test -list`. Methods are support functions, not top-level
entry candidates. The one `tools/customlint/testdata/..._test.go` file is analyzer
input and excluded from executable-entry candidates. File names, `//go:build`
constraints and their source positions are retained; OS/architecture/build-tag
selection is **not evaluated**. Every candidate has `runtimeDiscovered: false`
and `buildConstraintsEvaluated: false`. TestMain setup, examples without output,
benchmarks and fuzz targets are not interchangeable with ordinary test cases.
Run/Skip callsites are syntactic selector matches and may name unrelated receivers.
Conditions, helper calls, loops and runtime outcomes are not evaluated.

## Verified source-level figures at this pin

- 67,951 tracked assets, including 49,028 reference baseline assets.
- 13,006 selected fixture inputs: 7,034 compiler, 5,947 conformance, 25 transpile.
  Two additional `.js` compiler/conformance files are support assets, not selected
  by the compiler runner's `.ts`/`.tsx` filter.
- 42 fixture basenames occur in the canonical compiler runner's whole-file omission
  list. They remain in the inventory as `UPSTREAM_SKIPPED`, not passes or removals.
  Conditional option skips and output-only skips remain unresolved policies.
- 4,605 Go test source files plus one separate analyzer-test input; 6,064 AST
  declarations: 5,995 tests, 52 benchmarks, 6 fuzz targets and 11 TestMain hooks.
  These are source declaration counts, **not Go-discovered runnable-case totals**.
- 24 JS/TS test sources and 7 benchmark sources. Imports and generated variants may
  overlap; these source counts cannot be added to a concrete-test denominator.
- 8 fixture blobs are not raw UTF-8: 7 have UTF-16 BOMs, 1 intentionally contains
  malformed UTF-8. They retain base64 bytes and hashes; UTF-16/invalid-byte loading
  is explicitly unimplemented here. UTF-8 BOMs and NULs are recorded separately.
- Transpile configuration layer: 25 expanded fixtures, 28 named configurations,
  no blocked transpile expansions at this pin. The 12,981 compiler/conformance
  inputs remain unexpanded. These are configuration records, not executed cases.
- Full concrete-test count, runtime-expanded cases, skips by environment,
  accepted cases and total upstream coverage: **unknown / NOT_RUN**.

An older manifest reported one invalid-UTF8 **unit**, not one invalid raw source
blob. A read-only comparison found identical sets of 13,006 fixture paths and raw
source hashes: its seven UTF-16-BOM inputs have `validUtf8: true` unit records,
while its deliberately malformed fixture has `validUtf8: false`. This explains the
counting boundary without recovering or asserting correctness of the old loader.
The historical configuration/virtual-unit totals are not current acceptance data.

## Bounded transpile configuration expansion

`fixtureConfigurationInventory` reports expanded, blocked and unexpanded fixture
counts separately from recorded configurations. Fixture statuses and configuration
counts reconcile to their respective ledger records. `concreteCases` stays null and every compiler/reference/
acceptance status stays NOT_RUN. A fixture's full `expansion` remains UNEXPANDED,
even when its separate `configurationExpansion` is EXPANDED.

The implementation is guarded by the exact upstream pin and SHA-256 of the
transpile runner, metadata parser, configuration helper, option declarations and
byte reader. Missing or changed policy sources yield CONFIGURATION_POLICY_UNVERIFIED,
not speculative expansion. Raw invalid UTF-8/UTF-16 inputs remain unexpanded with
their base64 bytes; none of the 25 pinned transpile inputs needs that decoding.

The runner varies only `declarationmap`, `sourcemap` and `inlinesourcemap`, which
are boolean declarations. Settings are extracted across the whole source with the
existing anchored regex. Last duplicate wins; Go whitespace and exactly one final
semicolon are removed. Unknown/non-varying settings remain scalar, even with commas.
This preserves settings such as the final `filename` declaration; it does not turn
them into effective compiler options.

Included boolean values deduplicate case-insensitively and retain the first
spelling. `*` adds missing true/false values; `-` and `!` remove recognized values.
Unknown exclusions are ignored, unknown inclusions or an emptied set block the
fixture rather than inventing zero cases. Single values get no name suffix. Empty
lists omit their option unless the entire helper result is empty, in which case
the transpile runner's original-settings fallback is preserved. The three boolean
axes permit at most eight configurations, below the upstream maximum of 25.

Names here are `NamedTestConfiguration.Name` from the canonical helper, with
lowercase sorted varying keys. They are not the later camel-cased transpile test
names. Records are sorted deterministically; Go map iteration order is not claimed.
IDs bind the upstream pin, fixture ID, name and settings. Virtual files, effective
options, `runKind`, JS/declaration branching and output units remain unresolved.

The 28 records are independently reconcilable: 22 fixtures have one configuration;
`declarationBasicSyntax.ts`, `jsWithSourceMapBasic.ts` and
`jsWithInlineSourceMapBasic.ts` each have a true/false pair. This does not award
compiler coverage, waive upstream skips or change the frozen 29-case cohort.

## Domain and runner map

Every package/directory is indexed in the summary, including catch-all support
assets. The catch-all is deliberate: it preserves unfamiliar material for audit,
not a claim that all source has already been classified into executable scope.

1. Compiler, conformance and transpile: `tsc/internal/testrunner/`; fixtures under
   `tsc/testdata/tests/cases/`. Canonical configuration rules are in
   `options_generated.go` and `internal/testutil/harnessutil/harnessutil.go`.
2. Fourslash and language services: `tsc/internal/fourslash/tests/` contains 4,379 Go
   source files with embedded fixtures; `fourslash.go` and `test_parser.go` own
   runtime/metadata expansion. Also include `ls/`, `ls/autoimport/`, `lsp/` and `lsproto/`.
3. CLI, projects, builds and watches: `execute/tsctests/` scenarios/edits/options,
   `project/` (including ATA/background/dirty/logging), `fswatch/`, `ipc/` and
   `lspwatcher/`. Baseline filenames do not define the whole scope.
4. Native compiler/core tests: API/encoder/requestfilesystem, AST/AST navigation,
   binder/checker/compiler, parser/scanner/printer/transformers/transpile, options,
   module resolution, paths, VFS/symlinks, collections, diagnostics, sourcemaps,
   tracing and remaining Go packages, including `tsc/cmd/tsc`.
5. Fuzz/benchmarks: parser, numeric and path fuzz targets plus persisted parser fuzz
   corpus. Seeds can be generated from other fixtures. Runtime fuzz space is not
   finite source-file coverage; acceptance policy and deterministic seed/profile
   scope must be established explicitly.
6. JS API, VS Code extension and tools: `packages/typescript/test/`,
   `packages/vscode-typescript/test/`, `tools/scripts/` and tools Go tests.
   `packages/typescript/scripts/generateSync.ts` generates API variants.
7. Support: test libraries, fixtures, golden files and baseline domains (api,
   astnav, compiler, config, conformance, fourslash, lsp, project, transpile,
   tsbuild, tsbuildWatch, tsc, tscWatch, tsoptions). Presence is not execution.
8. Execution profiles: `Herebyfile.mjs` distinguishes `test`, `test:all`, opt-in
   codegen, smoke, benchmark and validation aliases. Upstream CI adds OS/architecture,
   race, noembed, concurrency and standalone-Go profiles. These are separate
   expansion obligations, not extra source-file counts.

## Progress and the remaining implementation sequence

[progress.schema.json](progress.schema.json) specifies the machine-readable status
model. Compiler/reference/acceptance PASS or FAIL requires matching-kind evidence
with a URI, SHA-256 and upstream commit. Acceptance PASS additionally requires
IMPLEMENTED plus compiler/reference PASS. Evidence still needs independent source,
case and outcome verification; a well-shaped JSON record is not proof. This
first-stage schema fixes release eligibility and scope completeness to false.

1. Audit all source domains and runner/profile selections. Reconcile unsupported,
   generated and helper-expanded families; no silent exclusions.
2. Implement byte-correct canonical fixture loading, virtual files/symlinks and
   configuration expansion. Preserve unsupported/invalid cases and upstream skips.
3. Expand Go/JS dynamic scenarios, helpers, generated variants and deterministic
   fuzz seeds; bind each concrete ID to source, effective options and profile.
4. Implement one bounded compiler/harness capability at a time and run both tscs
   and the exact pinned reference. Keep mismatch, unsupported, crash, timeout,
   skipped and not-run separate.
5. Only assess release when the scope has no unresolved expansion or missing case,
   all required profiles are accounted for, and complete executable acceptance is
   independently evidenced. Upstream skips are not tscs passes or automatic waivers.

The frozen 29-case bootstrap cohort remains unchanged and independent. It includes
synthetic/local cases and seven isolated upstream source copies; there is no
verified full-runner mapping from that cohort to this inventory. It earns zero
credit toward complete upstream acceptance until such a mapping is proven.

## Source construction profile

Run the same corpus tool with a fourth `construction` argument. It builds the
current harness, binds all13006 requests to pinned blobs and calls the real Go
factory. Query payloads are strictly validated; unsupported, reference panic,
crash and mismatches remain distinct, with per-side outcomes and unequal counts.
Current default-options/case-sensitive results match13006 sources/30902 queries.
This does not run AST/config/compiler assertions or all VFS profiles. See
[construction evidence](../../evidence/full-construction.json).

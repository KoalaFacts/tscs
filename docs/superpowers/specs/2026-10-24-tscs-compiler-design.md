# tscs compiler design
Date: 2026-10-24
Status: Proposed design; approach approved, implementation not started.

## Intent and agreed scope
tscs is a TypeScript compiler written in C#. It will read TypeScript and compiler configuration, check types, and emit JavaScript. The user selected compatibility-first porting, followed by C# refactoring and optimization. Language services are a later project. CLR/IL output, a JavaScript runtime, npm runtime compatibility, and .NET interop are outside this compiler effort.

## Approaches
1. Selected: port a pinned mature implementation, preserving algorithms, observable behavior and source correspondence. This reduces semantic invention but initially retains upstream-shaped internals.
2. Rejected for the initial effort: independently design the checker. More architectural freedom, much higher compatibility and debugging risk.
3. A Rust port is useful comparative evidence, but using two implementations as co-equal specifications creates contradictory acceptance rules.

## Reference decision (proposed by implementer)
Use microsoft/TypeScript commit 8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f as the initial semantic reference. Its compiler is under tsc/, with scanner, parser, AST, binder, checker, compiler, transformers, printer, diagnostics, options and virtual filesystem modules. Its tsc/go.mod requires Go 1.27.
This is an immutable development snapshot, not a claim of stable-release compatibility. Before implementation, build it reproducibly and capture version, toolchain and binary hash. If that cannot be reproduced, stop and document a replacement pin rather than silently using latest.
tsrs is secondary implementation guidance, not an oracle. Its published conformance claims are not tscs evidence.
Freeze the baseline through the initial milestone. Upgrades are separate changes with before/after inventories and diagnostic differences.

## Architecture
Keep the production compiler in C#: Tscs.Compiler, with modules corresponding to upstream responsibilities, plus a thin Tscs.Cli. A filesystem host abstraction supports real files and deterministic fixture files. Separate tests and a conformance runner from production code.
Preserve source positions, diagnostic ordering, symbol identity, flags and insertion-sensitive collections explicitly; do not assume C# and Go strings or maps have identical semantics. Derive source-coordinate behavior from upstream tests, including Unicode and CRLF.
Record upstream path/function and pin for ported units. Do not introduce generic compiler frameworks, alternate type semantics, parallel checking or speculative optimization during the first milestone.
Node/Go may be used by development tooling and the reference runner. The shipped compiler must not invoke another compiler or embed a JS engine to perform checking.
Builder chooses exact supported .NET SDK patch, pins it in global.json, and uses NUnit. Prefer a stable SDK over previews; package/tool versions and restore inputs must be locked. Pin the runtime before recording performance.

## Compatibility harness
Inventory upstream conformance and compiler-regression cases before selecting the supported cohort. Resolve referenced fixture assets and submodule commits from the pin. Inventory case/option variants, not just filenames.
Port the test directive semantics: virtual files, option matrices, tsconfig fixtures, symlinks and filesystem case sensitivity. Unsupported harness directives must be recorded, never silently ignored.
Initial oracle evidence is captured from the pinned build. For diagnostics compare code, category, file, start, length, ordered message chain, related information and ordering; separately compare rendered CLI text and exit status. Normalization may replace temporary root prefixes only at defined path boundaries. Preserve meaningful whitespace and newlines; never sort away ordering differences.
During checker-only stages compare the reference under the same noEmit path. Later emit stages reproduce the reference compilation/emit sequence; do not assume its diagnostics are identical across sequences.
Every manifest entry receives exactly one outcome: pass, mismatch, unsupported, crash, timeout, reference failure or not run. Report total inventory and every category; also publish cohort results. An empty selection, missing artifact, oracle failure or unexpectedly reduced inventory fails the run. Upstream exclusions are named and counted separately.
Only explicit supported cohorts gate early parity. Full inventory discovery is not a claim that the whole suite executes. Supported-cohort failures block acceptance; unsupported cases stay visible in the full denominator.
Harness self-tests must detect an intentionally wrong diagnostic, missing result, timeout, crash and empty selection. Expected results cannot be regenerated from tscs output.
All commands and reports record compiler commits, toolchain versions, configuration, case IDs and artifact hashes.

## First milestone: trustworthy noEmit vertical slice
Deliver CLI -> file/config input -> scanner/parser -> binder -> basic checker -> diagnostics, plus an independent differential runner.
Scope: single-file const/let declarations, explicit string/number/boolean annotations, literal initializers, identifier references, basic assignment checking, lexical scope, syntax errors and missing names. No functions, imports, generics, JSX or standard-library completeness claim yet.
CLI accepts an explicit file or a simple tsconfig with files and the supported compilerOptions subset, including noEmit and noLib. Unsupported options/config constructs fail explicitly. No default successful compilation without a completed check.
For isolated tests use noLib plus the same checked-in minimal ambient declarations for both compilers where upstream requires global types. Keep these distinct from full upstream lib.*.d.ts support.
Before coding this slice, freeze at least 20 positive/negative differential fixtures, including Unicode/CRLF and malformed input, with oracle outputs and capability labels. At least five must be unmodified upstream variants with their paths recorded; if the selected scope cannot accommodate them, report that and revise the milestone openly.
Acceptance: a clean reproducible build; every frozen cohort case matches the pinned oracle; harness failure-injection checks pass; unsupported syntax/options produce a namespaced TSCS diagnostic and nonzero exit; full discovered inventory and limitations are reported; repeated runs are identical. No JS output is expected yet.
Unsupported detection is conservative: a preflight capability traversal rejects unsupported constructs before semantic success. Never substitute any or skip a node to hide missing implementation.

## Subsequent milestones
M2: functions, objects, richer scopes and relative cross-file imports; expand cohort before implementation.
M3: structural relations, unions, narrowing and generic inference; then advanced type features, each in bounded slices.
M4: JS emit and transforms, then declaration output and source maps, with output file sets and bytes compared to upstream.
M5: broader tsconfig/module resolution, standard libraries, project references and incremental behavior; pinned real-project validation.
Each stage may need smaller dependency-driven steps. These are capability milestones, not calendar promises. Do not wait for full advanced checking to begin a small validated emit slice when dependencies permit.

## Delivery and continuity
Use small reviewable branches/PRs. Each change names its scope, upstream correspondence, tests, known gaps and next task. Keep source mapping, compatibility status and next steps in the repo.
Maintain green supported-cohort tests. Run focused checks per change and full supported-cohort checks before merge. Run broader inventory execution when a change can affect it.
If hosted CI is unavailable, run the identical pinned commands locally and attach results, explicitly marking CI unavailable. Unavailable validation is a blocker to claiming success, not evidence of a pass.
Performance comes after correctness: fixed inputs/options, successful compatible workloads, repeated measurements and recorded hardware/runtime. No speed or memory superiority claims before measurement.
No automatic future work is implied. Repository state and commands must allow another session to resume without reconstructing chat history.

## Risks and controls
Checker size and cyclic dependencies: port bounded dependency closures, keep a port ledger.
Harness bugs: independently captured reference artifacts and failure-injection tests.
Incomplete acceptance: separate coverage, parity and unsupported counts.
Nondeterministic upstream behavior: begin single-threaded; preserve reproductions and label upstream instability without accepting arbitrary output.
Source evolution: frozen pin with explicit upgrade changes.
Attribution: preserve Apache-2.0 license and copyright notices; add NOTICE/source attribution before incorporating upstream code or fixtures.

## Evidence inspected
- https://github.com/microsoft/TypeScript/tree/8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f/tsc/internal
- https://github.com/microsoft/TypeScript/blob/8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f/tsc/internal/testrunner/compiler_runner.go
- https://github.com/microsoft/TypeScript/blob/8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f/tsc/internal/testrunner/test_case_parser.go
- https://github.com/microsoft/TypeScript/blob/8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f/tsc/go.mod
- https://github.com/oxc-project/tsrs/blob/main/docs/COMPATIBILITY.md (secondary, mutable reference)
At inspection the tscs repository contained only LICENSE. No compiler or test execution has been completed in this design task.

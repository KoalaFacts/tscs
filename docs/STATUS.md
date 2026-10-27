# 2026-10-28 — Explicit tsconfig source discovery (bounded project architecture)

The existing CLI tsconfig JSON AST now accepts explicit `include` and `exclude`
source globs, alongside `files`, before constructing the same immutable
CompilerProgram snapshot. Discovered .ts/.d.ts paths are deterministic, config-
relative and deduplicated against explicit roots; excludes do not remove explicit
`files` entries. A bounded, non-following filesystem walker avoids recursive
symlink traversal and rejects unsafe glob spellings and oversized scans. Nested
and nonrecursive includes, excludes, explicit-file precedence, overlap and
rejection are exercised through real CLI/CompilerProgram regressions.

This is explicitly *not* full TS7 config expansion: default inclusion without
an `include` array, TSX/JS, extends, watch, project references, wildcard edge
cases and VFS directory enumeration remain unimplemented. The existing 15,523
original configuration targets are still SKIP_UNIMPLEMENTED, original accepted
PASS remains 0, and full runtime expansion remains unknown. New-head build,
review and CI are required before merge or acceptance claims.

# 2026-10-28 — API compatibility review repair (PR #12 P1)

Codex review identified the removed two-argument CLR Load signature despite
source-compatible optional options. Restored the exact two-argument
CompilerProgram.Load overload and preserved the original bool EmitJavaScript
overload. A focused runtime reflection regression resolves and invokes both
old CLR signatures against the actual checked Program. This is an important
binary compatibility correction, not new TS7 syntax or original acceptance.

# 2026-10-28 — Immutable CompilerOptions joined to CompilerProgram (bounded architecture)

CLI project/flag configuration now produces one typed effective CompilerOptions
snapshot, passed into the host-backed CompilerProgram. The AST emitter consumes
its Program module mode and enforces noEmit, rather than a separate CLI switch.
The existing explicit Program bool API is preserved for callers, with noEmit
still blocking output. In-memory Program tests check real module JavaScript
emission and both failure boundaries. This improves architecture reuse without
claiming other TS7 option defaults or module transforms. The 15,523 registered
original configuration targets remain SKIP_UNIMPLEMENTED until independent
pinned product/reference acceptance; no original PASS is claimed.

# 2026-10-28 — tsconfig named ES2015 module output (bounded architecture package)

The CLI `compilerOptions.module` setting now flows through the pinned TS7 enum
converter to the same existing checked Program/AST → JavaScriptEmitter path as
`--module es2015`. Projects can supply source files, noLib, target and module
in one `tsconfig.json`, then run explicit `--emitJs -p`. The project's
cross-source module output is exercised by the native Node24 ESM runtime smoke;
invalid/unsupported module options remain diagnostics or fail-closed and do not
write outputs. Other TS7 module modes, full compiler option defaults/extends,
and package resolution are not claimed. Original accepted case IDs unchanged:
0 PASS / 15,523 SKIP_UNIMPLEMENTED, full executable scope unknown.

# 2026-10-27 — ES module strict-mode binding safety (PR #10 review P2)

The independent review identified a real output hazard: the TypeChecker permitted
module function/parameter bindings named `eval` or `arguments`, or import local
aliases with those names, even though emitted ES2015 modules are implicitly strict
and Node rejects them with a SyntaxError. The existing per-source checker now
receives the module identity from Compilation and rejects those module bindings
with TS1100 before output preflight. Sloppy script function parameter behavior is
preserved; existing variable binding restrictions remain unchanged. Focused emit
regressions check function names, parameters, local exports and import aliases,
and assert no JS is written when invalid. This fixes the identified P2 risk; it
does not claim comprehensive strict-mode parsing or original TS7 acceptance.
No original TS7 target is promoted; 0 original PASS, 15,523 SKIP_UNIMPLEMENTED,
dynamic scope unknown. Exact new-head verification is required before merge.

# 2026-10-27 — Bounded ES2015 named-module output (new single package)

The existing AST-backed script emitter now handles the already linked named
ES module syntax under an explicit `--module es2015` emit flag. The same
CompilerProgram AST and ModuleLinker graph resolve relative `.js` specifiers
against `.ts` files while retaining runtime `.js` import/export spelling.
Source-wide diagnostics block all outputs; existing preflight/no-overwrite and
rollback remain unchanged. Targeted xUnit tests and a native Node ESM execution
probe cover a three-file named re-export graph, not just string snapshots.
This is bounded production functionality; original TS7 accepted PASS stays
0/15,523, all registered source-configuration targets remain SKIP_UNIMPLEMENTED,
full dynamic scope unknown. CI/build and independent review must validate this
new head before merge. No complete TS7 module-transform support is claimed.

# 2026-10-27 — Preserve script directive boundaries in the AST (PR #9 review P2)

An independently reported code review found that discarding a leading empty
statement can promote a subsequent string expression into a JavaScript
`"use strict"` directive during emit. The parser now records `EmptyStatement`
and the AST writer retains its semicolon, preserving sloppy-script semantics.
A new xUnit regression checks the exact emitted prefix, and Node24 executes
the emitted non-directive string/legacy binding case to catch a syntax failure.
This is a correctness fix to the new emission module, not a new TS7 accepted
target. Run exact amended-head CI and re-review before merging.

# 2026-10-27 — AST-backed bounded ES2015 script output (new work package)

Added a single Program-snapshot JavaScript emission path using the same parsed,
bound and type-checked AST as diagnostics. The nonrecursive expression writer
preserves binary precedence, source parentheses and associativity while erasing
primitive annotations; scripts may emit variables, assignment, postfix update,
blocks, explicit-signature functions, calls, return and braced conditionals.
The explicit CLI `--emitJs --noLib --target es2015` mode uses preflight
output checks and never overwrites existing files. Module syntax, unsupported
language constructs and programs with diagnostics fail closed; this is
neither general TS7 emit nor declaration/map output. Targeted xUnit checks
and a native Node24 execution smoke are included. Exact-head CI is required;
do not credit any original upstream accepted test before complete pinned
reference/product differential. Frozen original accepted=0/15,523; all original
configuration-level SKIP_UNIMPLEMENTED remain, dynamic scope unknown.
Next package: module-aware emission and compiler option integration, only after
this bounded script work is verified and reviewed.

# 2026-10-27 — Reachability-aware function branch joins (PR #8)

Refined the existing definite-return pass to expose whether a supported block or
conditional can complete normally. The TypeChecker now joins initialized symbols
only across branches reaching the following statement, so a branch ending in a
value return cannot incorrectly poison an initialized value from the other arm.
Nested and else-if branches use the same AST flow helper. The feature remains
limited to braced conditionals, sequential blocks and value returns; no loops,
exceptional control flow, TypeScript narrowing or general emit is claimed.
Original TS7 acceptance is unchanged: 0 PASS / 15,523 SKIP_UNIMPLEMENTED,
with full executable expansion unknown. Fresh-head verification is required.

# 2026-10-27 — Braced conditionals and path-sensitive function returns (PR #8)

Extended the shared Scanner → AST/Parser → Binder/Scope → TypeChecker →
FunctionControlFlow pipeline with braced `if / else if / else` statements,
conservative definite value-return checking, and branch-isolated
initialized-state joins. Boolean/any conditions, lexical branch scopes and
variables initialized in both arms work in the bounded noEmit/noLib subset.
Unbraced arms, arbitrary JavaScript truthiness, comparisons/logical operators,
loops, void returns and full TS7 semantics remain unsupported.
Targeted regressions cover success, uninitialized reads and fail-closed forms.
No original upstream configuration-level SKIP is cleared; accepted original
PASS remains 0/15,523 with dynamic scope unknown. Exact-head verification
must be recorded separately; code submission is not an acceptance claim.

# 2026-10-27 — Straight-line function return flow and return-line boundary (PR #8)

Corrected an reviewed ASI issue: a line break after `return` cannot attach
the next expression as its value; LF, CRLF and multiline-comment boundaries
now fail closed in the current non-void function subset. Replaced the parser's
last-statement-only shortcut with a shared FunctionControlFlow check in the
TypeChecker. Unconditional nested blocks ending in a value return now work,
while nonreturning bodies remain rejected. Conditional/loop control flow,
void returns, full ASI and full TS7 function acceptance are still absent.
The original accepted count and SKIP_UNIMPLEMENTED catalogue are unchanged.
Current head CI must validate this commit; no merge/release is implied.

# 2026-10-27 — Explicit-signature function pipeline (PR #8)

Implemented a bounded, reusable function-declaration and call path across Scanner, AST,
Parser, Binder, ModuleLinker, TypeSymbol and TypeChecker. Named source-level functions
with explicit primitive/any parameters and return annotations can call/return primitive
expressions, bind parameters in their own lexical scope, be referenced before their
declaration and be imported through the existing named-value module graph. The shared
checker validates arity, argument types and return assignability; missing annotations,
unsupported bodies/call targets remain errors, not implicit any. No JavaScript emit,
closures, overloads, default/rest/optional parameters, inference, generics, recursion
soundness proof or complete TypeScript function semantics are claimed. This is a
bounded implementation family, not acceptance of the general function-declaration
local guard: `function f() {}` still fails closed. The existing 15,523 original
upstream configuration-level SKIPs and original PASS=0 remain unchanged, with full
dynamic scope still unknown. Exact-head C#/CI validation and independent review are
required before considering the PR ready to merge.

# 2026-10-27 — Shared semantic TypeSymbol foundation (PR #8)

The bounded compiler now represents checked primitive/any types as semantic
`TypeSymbol` values shared by Binder, ModuleLinker and TypeChecker. AST type
annotation spelling stays in syntax; binder prebinding, module imports and
expression checking use one type identity and assignment relation. A
cross-module regression exercises typed string and any imports plus TS2322
mismatch text. This lays a real code path for future callable/object types,
but functions and general TS7 type relations are still UNIMPLEMENTED.
Existing 15,523 original configuration targets remain SKIP_UNIMPLEMENTED;
no original acceptance PASS or release eligibility is newly claimed.
Exact-head compilation and regression remain required.

# 2026-10-27 — Host-canonical module diagnostic provenance (PR #8)

Fixed the reviewed P2 on case-insensitive module hosts: module edges can spell
`b.ts` while the loaded AST/source identity is `B.ts`. ModuleLinker now
normalizes linked targets and recursive resolutions to the actual parsed
source before constructing diagnostics; CLI text lookup shares the host's
identity comparer. New targeted host regression asserts real cycle provenance,
UTF-16 offset and deduplicated reads. A Windows-only CLI regression verifies
the correct third-line text coordinate; it is not treated as executed on Linux.
This protects the reusable module source graph, not new TS7 acceptance.
Exact-head checks are needed; no merge or upstream skip reduction is claimed.

# 2026-10-27 — Deferred named export linking (PR #8)

The host-backed Program now links named value exports after prebinding all
module-local scopes. Relative named `export { source as public } from "./file"`
and imported-binding aliases follow transitive source-graph edges and keep
the original declaring symbol identity. Non-recursive circular value imports
work against prebound declarations; recursive export aliases explicitly fail.
Missing/duplicate members, package paths and non-module targets stay rejected.
Focused compiler regressions cover real three-file programs and failure cases.

This is not complete TS7 modules, emit or release acceptance. Existing 15,523
configuration-level upstream placeholders remain SKIP_UNIMPLEMENTED, original
accepted PASS remains 0, complete dynamic scope unknown. New-head CI is
required; no merger or independent review is claimed for this change.

# 2026-10-27 — Explicit module export bindings and OS-correct diagnostic identities (PR #8)

Named local `export { local as publicName }` lists now bind through a
per-module export table sharing the declaration symbol, rather than treating
all lexical locals as exports. `export {}` is a module boundary. Named imports
resolve against these tables independent of source input order; duplicate and
unknown exports fail closed. Imported-binding re-exports and
`export {name} from "..."` remain unsupported, as do default/namespace exports,
general module resolution, emit and TypeScript's complete symbol semantics.
JSON and text diagnostics preserve literal Unix backslashes instead of
collapsing them into directory separators. New targeted regressions exist;
the new head still needs its own validation. Upstream accepted PASS remains
0/15,523; SKIP_UNIMPLEMENTED=15,523; full dynamic scope unknown; no merge
or independent review is asserted.

# 2026-10-27 — Shared AST snapshots and multi-file diagnostic identity (PR #8)

The host program now caches each canonical source's actual syntax result for
both graph discovery and compiler analysis, removing duplicate module parsing.
CLI JSON now disambiguates equal source basenames with relative paths while
preserving the single-file output contract. This addresses the open P2 source
identity review finding, subject to exact-head CI. Original TS7 accepted
0/15,523; SKIP_UNIMPLEMENTED=15,523; no release/merge/review claimed.

# 2026-10-27 — Bounded relative named ES modules (PR #8)

Implemented host-backed source loading for relative named imports and per-file
module scopes with explicit primitive `export let/const` declarations. Import
aliases resolve through the binder before checker evaluation; ordinary global
scripts do not see private module declarations, and imports cannot be assigned.
This is an actual, bounded compiler path, not a placeholder. Dynamic/package
resolution, default exports, re-exports, standard library, emit, and complete
module inference remain unsupported. This new source change requires fresh
exact-head build/regression/CI and does **not** reclassify an upstream target.
Frozen configuration-level accepted PASS=0/15,523; SKIP_UNIMPLEMENTED=15,523;
full runtime denominator unknown; releaseEligible=false. No formal review or
merge claimed.

# 2026-10-27 — Host-backed transitive global-script path references (PR #8)

Implemented a bounded source-dependency graph in `CompilerProgram.Load`:
leading relative triple-slash path references load transitively through
`ICompilerHost`, while canonical deduplication prevents cycles/diamonds from
reloading sources. The loaded sources feed the existing multi-file binder and
checker; malformed/late directives, missing dependencies and unsupported
absolute references fail closed. New host tests cover the working cross-file
path and these boundaries. This is not ES-module resolution or original TS7
acceptance. Original configuration-level PASS stays 0/15,523, SKIP_UNIMPLEMENTED
stays 15,523; the full runtime domain denominator remains unknown.

# 2026-10-27 — Host-backed compiler program and project syntax diagnostics (PR #8)

Added a production `ICompilerHost` / `FileSystemCompilerHost` boundary and
`CompilerProgram.Load` to snapshot canonical source identities and contents,
reject aliases/duplicates, and cache the bounded multi-file analysis. The CLI
now uses this program path instead of privately loading and reparsing source
files; alternate VFS/editor hosts can provide in-memory content. Multi-file
scanner/parser failures are aggregated with stable source identity before
binding, avoiding the previous first-bad-file early return. This unlocks
subsequent module graphs and project host expansion without creating another
independent compiler path. Minimal ambient declaration validation, module
resolution, incremental invalidation, symlinks, full TypeScript types and
JavaScript emit remain unsupported. Source-level tests cover real in-memory
host execution, duplicate identity checks, diagnostic ownership and multi-file
failure collection; testing must be verified against this exact new head.

No upstream placeholder is reclassified: frozen 15,523 configurations,
accepted full TS7 PASS 0, SKIP_UNIMPLEMENTED 15,523; 39 original upstream
skip flags and seven reference-fatal sources remain distinct. Full runtime
case denominator unknown, releaseEligible=false. This is architecture
implementation, not original acceptance or an independent review.

# 2026-10-27 — First multi-source program architecture package (PR #8)

Production compiler: `Compilation.AnalyzeFiles`/CLI handles multiple
noEmit/noLib TypeScript global scripts, retaining per-file scanner/AST
contexts and a shared top-level binder scope. Primitive annotated/literal
symbols resolve across input files; unknown inference, declaration merging,
imports, standard libraries and general emit still explicitly remain
unsupported. New xUnit regression exercises multi-file ordering, type
mismatch source positions, duplicate globals and fail-closed behavior.
**This is product architecture implementation, not a full original TS7
acceptance PASS.** Upstream configuration-level catalogue remains
15,523 targets, original accepted 0, SKIP_UNIMPLEMENTED 15,523, full
runtime expansion unknown; scope delta and removed SKIP IDs zero.

The first product-head hosted verify jobs compiled the new C# code and
passed 15,577 internal xUnit4/MTP cases but failed at the later Node
implementation-ledger integrity check: changed
`CompilationTests.cs` had an outdated source Git-blob SHA in both
`tests/implementation-coverage/registry.json` and its derived report.
This corrective commit updates only that source binding to the actual
published Git blob `663e44537aa2f9fac97f66708515624873930638`. It does NOT weaken the fail-closed
checker or reclassify implementation SKIPs. Full final-head CI remains
subject to separate verification. Go reference verifier correction
`e74dddc` now recognizes the two real pinned Go child tests
`type` and `symbol`; previous Go job remains red, product-side original
acceptance NOT_RUN. No independent formal review/merge/release claimed.

# Status — 2026-10-27
- Hosted RED at [job 121865211939](https://github.com/KoalaFacts/tscs/actions/runs/40406416194/job/121865211939): **11/11 Node24 verifier tests passed**, sparse checkout successfully pinned the exact source, but Go dependency acquisition failed before any original test due the upstream root `go.work` referencing `../tools/go.mod`, excluded by the intentionally sparse checkout. The fix isolates the actual `tsc` module with `GOWORK=off` in the acquisition, verification and test phases. This is a genuine infrastructure failure, **not** a successful original test or a billing error; exact fix-head CI must be run separately.

## 2026-10-27 — Live pinned Go TestLocal/emptyFile.ts probe (PR #8; reference-only)

- Added an executable, strictly pinned upstream Go harness probe for the first original configuration target `configuration:c5f81e2689b2db32a694b7b3de45098f5d58941800bfe0683e2f68000e24c110`. Uses the **real** `TestLocal/emptyFile.ts` Go test and all seven required nested checks, not a generated xUnit placeholder. An independent JSON event verifier fails closed on absent/duplicate cases, skips, failures, truncation, other selected tests and package failure.
- New native TypeScript validator was tested locally against synthetic Go event streams: **11/11 GREEN after missing-module RED**, plus CLI JSON/SHA smoke and `bash -n`. Local Node was 22.16.0 experimental strip-types, **not Node24**; local .NET 10 and Go1.27.3 were unavailable. The actual live pinned Go test and hosted checks remain **NOT_RUN until exact-head CI completes**. No independent review has occurred.
- Reference dependency acquisition uses the official module proxy/sumdb and immutable pinned `go.sum`; Go test phase checks `GOPROXY=off GOSUMDB=off GOTOOLCHAIN=local GOTELEMETRY=off` and preserves full output, SHA and failure evidence. [Probe design and limitations](progress/original-emptyfile-go-reference.md).
- **Accounting unchanged:** original 15,523 configurations, original `PASS=0`, `SKIP(UNIMPLEMENTED)=15,523`, new accepted target IDs **none**, removed SKIP IDs **none**, `scopeDelta=0`, 39 UPSTREAM_SKIPPED metadata flags, seven historical reference-fatals, full executable test denominator unknown. Separate 14 local probes: 8 implementation-present / upstream NOT_RUN, six SKIP. Release gate remains blocked. A Go reference PASS alone **cannot** clear the C# product acceptance gate.

## 2026-10-27 — Numeric unary plus, genuine RED → GREEN and preserved lexical regression (PR #8 only)

- Test-first commit [`a60be67`](https://github.com/KoalaFacts/tscs/commit/a60be67ddd5420c2d986d8e0ed6e43c662e6623b) replaces the existing unary-plus rejection test with **real positive** C# compiler assertions for `+1`, `1 + +2`, `2 * +3`, nested `+/-`, typed variables, AST precedence and a 20,000-prefix chain; retains separate rejection of nonnumeric operands and exact TS2304/TS2322 diagnostics. Hosted test-first [RED run 40394957565](https://github.com/KoalaFacts/tscs/actions/runs/40394957565/job/121825428611) built successfully with zero errors and reported **15,557 PASS / 12 new FAIL / 0 SKIP**. These were genuine implementation failures, not fabricated SKIPs.
- Product commit [`76b50ef`](https://github.com/KoalaFacts/tscs/commit/76b50ef7437917975bce9524a240ba27a8d73402) removes the scanner's unsupported *single* `+` guard (while preserving the distinct `++` fail-closed boundary), parses prefix `+`/ `-` iteratively and checks numeric unary expressions iteratively. This does **not** implement nonnumeric unary-plus coercion, prefix increment, JavaScript emit or original upstream runner acceptance.
- The initial product-head CI exposed a genuine preexisting regression: `StringRecoveryTests.EarlierLexicalErrorsSurviveLaterUnsupportedSyntax` previously used a trailing `+` as its unsupported-syntax sentinel. At [run 40395366462](https://github.com/KoalaFacts/tscs/actions/runs/40395366462/job/121826858547), the build passed but MTP recorded **15,569 PASS / 1 FAIL**. The test-only correction [`ce47cb1`](https://github.com/KoalaFacts/tscs/commit/ce47cb14759711da31e15b941ecdf03315f35e78) replaces that sentinel with the still-unsupported `function` token and adds a separate assertion that lexical TS1125 survives a now-supported `+1`. Neither existing diagnostic was silently dropped or marked SKIP.
- Hosted exact-product-and-regression-head [GREEN run 40395996559](https://github.com/KoalaFacts/tscs/actions/runs/40395996559/job/121829124125): **15,570/15,570 strict internal xUnit4/MTP PASS (0 FAIL/0 SKIP)**, **145/145 Node24 PASS**, frozen differential **29/29**, locked Release build **0 errors/0 warnings**, pinned Go source scope and distinct one-test discovery canary successful. The separate full original configuration placeholder xUnit/MTP lane still reports **15,523 discovered / 15,523 SKIP(UNIMPLEMENTED) / 0 original PASS / 0 original FAIL**, with **39 UPSTREAM_SKIPPED metadata flags**, not automatic PASS. Further exact-head checks are recorded independently; a docs follow-up requires its own new-head CI.
- **No new accepted original target**: stable 15,523 configuration targets remain frozen, original upstream end-to-end PASS **0**, removed original SKIP IDs **none**; 7 historical reference-fatal sources remain; full dynamic execution scope is **UNEXPANDED/unknown**, `releaseEligible=false`. The distinct 14-case local guard register is unchanged (**8 implemented but upstream NOT_RUN / 6 SKIP**), because this new arithmetic capability has no verified original source/configuration mapping. No independent code review or formal APPROVE is claimed. Current Go `TestLocal` run and original reference differential for this feature remain **NOT_RUN**, and local .NET is unavailable.

## 2026-10-27 — First pinned original-profile emit slice; original acceptance remains NOT_RUN (PR #8 only)

- Exact upstream `tsc/testdata/tests/cases/compiler/emptyFile.ts` (SHA-256 `a149b5cff5898a63f501618af2c01a132895281e03663831e76f6deb9f3b5bc7`) and source configuration `configuration:c5f81e2689b2db32a694b7b3de45098f5d58941800bfe0683e2f68000e24c110` are used **without** `--noEmit`/`--noLib` overrides. The source is a comment-only test directive. The locked upstream reference `compiler/emptyFile.js` baseline contains the empty program's `"use strict";\r\n` emit and no diagnostics baseline.
- Real test-first **RED** at `22e4b23064080cadde3ebe6936c93f2ace399c78`: both hosted strict internal verify jobs built but failed exactly the new source-bound emit assertion, **15,552 passed / 1 failed / 0 skipped** ([push](https://github.com/KoalaFacts/tscs/actions/runs/40386569208), [PR](https://github.com/KoalaFacts/tscs/actions/runs/40386580533)).
- Product **GREEN** at `308e7dcea54beadb77aa73fb815e4eb5bd2eea30`: compiler analysis exposes empty-program state after parsing; CLI admits *only* the single-file explicit ES2015/noEmit=false/noLib=false empty-program case and emits the pinned strict prologue with CRLF, refusing to overwrite existing JavaScript. Real hosted full verify jobs passed strict MTP **15,553/15,553**; Node24 and frozen 29/29, full original placeholder discovery **15,523 SKIP / 0 PASS** and 4/4 exact-head checks passed ([push](https://github.com/KoalaFacts/tscs/actions/runs/40386981806), [PR](https://github.com/KoalaFacts/tscs/actions/runs/40386992638)). This does not permit syntax-bearing programs with these options.
- These are **baseline-linked product regression tests**, not a complete original `TestLocal` runner invocation: the pinned Go runner's diagnostic/JS/source-map/types-and-symbols/module-resolution/parent-pointer checks have **NOT_RUN** at this original profile. Original accepted **0/15,523**, `SKIP_UNIMPLEMENTED=15,523`; scope +0 and removed SKIP IDs none. Seven reference-fatals and all unexpanded domains unchanged. `releaseEligible=false`. Self-review only; formal review/merge pending.

## 2026-10-27 — Bounded numeric unary-minus compiler implementation (PR #8 only)

- Genuine hosted RED commit `c24c849f04f69bd528853c0aa48023fba305940b`: **15,540 internal MTP PASS / 10 FAIL / 0 SKIP** at both exact-head verify jobs. New tests reported `TSCS1001` for numeric unary minus and `-missing`; Release build itself had zero errors. [RED push](https://github.com/KoalaFacts/tscs/actions/runs/40376690397) · [RED PR](https://github.com/KoalaFacts/tscs/actions/runs/40376698463).
- Product commit `c7cd4036fe2eafd2f84f2106856f3ddc9222f863` adds explicit decrement `--` rejection, iteratively constructed unary `-` AST nodes and iterative numeric-only TypeChecker handling. Covers `-n`, chained prefixes, `2 - -1` and precedence with multiplication. Nonnumeric unary operands remain `TSCS1001`; unresolved identifiers retain `TS2304`. Parser/type checker remain bounded; emitter/full original TS7 acceptance are not implemented.
- **Exact implementation-head hosted GREEN**, two verify jobs [40376954738](https://github.com/KoalaFacts/tscs/actions/runs/40376954738) and [40376966938](https://github.com/KoalaFacts/tscs/actions/runs/40376966938): both strict xUnit4/MTP **15,550/15,550** (no skips/failures), native Node24 **145/145**, frozen isolated differential **29/29**, Release zero warnings/errors, separate 15,523/15,523 actual MTP discovery SKIP with 0 accepted PASS. CodeQL and Actions analysis succeeded (**4/4 checks**). Additional AST-shape assertions in the documentation/test follow-up require their **own** new-head CI; previous GREEN is not credited to that follow-up.
- Immutable upstream configuration corpus checked independently using the recorded SHA-256 (`targets.jsonl` `c1c4077d732ae859ad70899ac3fd85fc25f9eff1c99737f3e36aa806b5c2742e`): **1,304/15,523** target configurations explicitly have `noemit=true`, **12/15,523** explicitly have `nolib=true`, **0** explicitly set *both*. This explains why the current CLI's strict `--noEmit --noLib` bootstrap cannot by itself claim original-profile acceptance; the static counts do not establish effective defaults or the full runtime denominator. First genuine PASS still needs exact original fixture/configuration execution, reference outputs, standard-lib/emit capability, and full assertion evidence. Full upstream acceptance **0 PASS / 15,523 registered SKIP**, releaseEligible=false.
- **Scope delta:** original targets +0, original accepted +0, removed original SKIPs 0. Local 14-probe catalogue remains **8 implemented/upstream NOT_RUN / 6 UNIMPLEMENTED SKIP**, because this extra arithmetic slice has not been mapped to an original profile; internal unit tests are not substituted for upstream PASS. Self-review only; no independent review or formal APPROVE. Main remains separate from open PR #8.

## 2026-10-27 — One fewer genuine local SKIP; full 15,523-Fact MTP discovery confirmed

- Test-first RED `f706c04572cba439efeb7ffe9f5cd15e132eb78a`: two hosted xUnit4/MTP verify jobs reproduced **8 focused failures**, each with **15,492 passed / 8 failed / 0 skipped**, while old bootstrap ignored no `@ts-ignore` directives. [RED job 1](https://github.com/KoalaFacts/tscs/actions/runs/40357878782/job/121695097990) · [RED job 2](https://github.com/KoalaFacts/tscs/actions/runs/40357891412/job/121695598276).
- Product commit `51993ce5fa451a3dcfe71bab47953f842e8e1cda` implements narrowly scoped whole-line `// @ts-ignore`: scanner records the next physical line; checker-only diagnostics (including TS2304/TS2322/TS2588) are suppressed there, but scanner/parser errors, blank-line gaps and other directive spellings remain fail-closed. The formerly negative local guard `bootstrap:ts-ignore-directive` now asserts actual successful behavior; the previous unsupported-only guard still runs for other features. Stable local bootstrap inventory **14 total = 8 IMPLEMENTED/upstream NOT_RUN + 6 SKIP(UNIMPLEMENTED)**, newly removed SKIP **1**. This is **not full TypeScript directive compatibility**.
- Exact product head hosted GREEN: [verify run 40358477859](https://github.com/KoalaFacts/tscs/actions/runs/40358477859) and [40358486127](https://github.com/KoalaFacts/tscs/actions/runs/40358486127), both **xUnit 15,500/15,500 with zero skips**, native Node24 **145/145**, frozen differential **29/29**, Release builds succeeded; both verify checks and CodeQL/Actions analysis succeeded.
- The separate original-source configuration placeholder project **really compiled and ran under xUnit4/MTP2 in both hosted jobs**: **15,523 discovered, 15,523 SKIP(UNIMPLEMENTED), 0 PASS, 0 FAIL**. The one-source canary also correctly discovered one SKIP. Exact GitHub artifact snapshots (temporary retention) are **12667661634**, digest `sha256:37663778c0d041e4de837b3fe9e8ca648e4f0bac4de8d4baeb0cdda2f2ad8aa9` and **12666817064**, digest `sha256:01d9dea88b930c24b74a2e69d67cee997972e116538cbca8e46445a5e6e0ede8`. [Bounded evidence](evidence/ts-ignore-directive.json).
- **Original pinned upstream compiler/referenceCompiler end-to-end acceptance PASS +0**, releaseEligible=false, 7 historical reference-fatal sources and incomplete Go/JS/Fourslash/build/watch/profile expansion unchanged. Local new-head .NET/Go executors were unavailable; no independent code review or formal APPROVE is claimed.

## Git-backed full 15,523 source/configuration target MTP discovery — 2026-10-27

- The historical, SHA-256-bound 3,544,490-byte generated placeholder archive is now stored as 20 ordered binary Git blobs (byte-identical reassembly checked). CI can extract and run the complete 15,523-Fact standalone xUnit4/MTP2 test assembly without needing a Library download. `tools/materialize-upstream-placeholders.ts` fails on missing/extra/corrupt blobs before writing any archive; six Node regressions and exact archive reconstruction passed locally. [Scope](progress/upstream-placeholder-full-discovery.md).
- After the unchanged strict internal regression gate and one-source real canary, a separate CI step performs locked restore/build, actual 15,523-test CTRF discovery and stable-ID/source/config/status reconciliation. Raw discovery evidence is uploaded as a workflow artifact even on failure. **The new-head hosted discovery result must be checked; local .NET discovery is NOT_RUN.**
- Compiler implementation delta **0**, original upstream acceptance PASS **0**, removed SKIP **0**; 15,523 source-configuration placeholders remain SKIP(UNIMPLEMENTED) until original assertions are implemented. The full runtime TS7 denominator remains unknown and releaseEligible=false.

## Standalone xUnit/MTP NuGet lock and real discovery canary — 2026-10-27

- The external generated test bundle now has a repository-reviewed standalone 16-package NuGet lock. It is projected from the existing pinned internal xUnit4/MTP2 lock (same versions/content hashes; three local project-reference entries excluded). The SHA-bound verifier and five local Node22.16.0 fallback regressions passed. See [lock scope](progress/upstream-placeholder-lock.md).
- A separate 1-target original-source xUnit4/MTP2 discovery canary is added **after** the unchanged strict internal CI verification; its new-head hosted result must be checked independently. The 15,523-target discovery entry now validates the reviewed lock and copies it only to an extracted working bundle before locked restore/build/real CTRF reconciliation, rejecting any different existing lock.
- No language implementation added, no original compiler acceptance PASS, no SKIPs eliminated. The full 15,523 MTP discovery remains NOT_RUN locally (no .NET SDK and archive is externally stored); complete runtime TS7 denominator remains UNEXPANDED, releaseEligible=false.


## Separate xUnit/MTP discovery reconciliation — 2026-10-27

- A new CTRF execution report verifier performs fail-closed ID/source/configuration reconciliation for the **15,523 source-config placeholders**. Local Node22 fallback **11/11 tests**, and synthetic 15,523-row CTRF reconciliation passed; actual .NET/MTP discovery is **NOT_RUN** because the standalone bundle lacks a reviewed lock file and local SDK is absent. [Scope](progress/upstream-placeholder-discovery.md), [evidence](evidence/upstream-placeholder-discovery.json).
- New standalone optional script refuses non-locked restore, leaving main strict internal `--fail-skips on` untouched. Compiler feature/accepted-case delta **0**, removed SKIPs **0**, releaseEligible=false.


## Full placeholder corpus integrity — 2026-10-26

- A separate post-generation verifier now reconciles all **15,523 source-configuration target SKIPs** against every generated C# Fact, source/configuration ID and original artifact hash, including seven reference-fatal blockers and upstream-skip metadata. Synthetic corruption/missing-artifact regressions fail closed, and release mode rejects unresolved SKIPs. [Scope/results](progress/upstream-placeholder-integrity.md) and [machine evidence](evidence/upstream-placeholder-integrity.json).
- Genuine local Node22 experimental fallback RED **8 failed** -> GREEN **8 passed**. Full historical generated bundle audit: **15,523/15,523 fact identities**, 13,006 source identities and expected counts, with separate same-author Python reconciliation. No new language features/accepted upstream cases; **0 SKIPs resolved**. New native Node24 tests are included by the existing `node --test tools/*.test.ts` command, but the generated 15,523-Fact .NET project has not been compiled/discovered in this environment.
- The strict existing xUnit internal regression gate is unchanged. Original full upstream compiler/reference/acceptance remains **NOT_RUN**; the complete runtime TS7 denominator remains **unknown**; releaseEligible=false.



## Bounded numeric addition checker stack safety — 2026-10-26

- Confirmed P2 from PR #8 using an **isolated CLI child**: a supported 400,000-operator
  numeric '+' chain (800,010 UTF16 source units, below the existing 1,000,000
  limit) caused SIGABRT/StackOverflow on old checker. Both hosted RED
  verification runs retained the crash in the child, not the xUnit or Node runner.
  The full xUnit suite itself passed 15,488/15,488 on RED; native Node had
  103 pass/1 fail, the new focused regression.
- Changed TypeChecker.TypeOf from recursive binary traversal to iterative
  post-order work/results stacks, retaining left-to-right resolution and first
  unsupported-operand diagnostics. Two xUnit regression checks explicitly lock
  diagnostic order and early-error precedence.
- Implementation commit b69e30eb071da1b67420b64c413f9fc7fd39cd7a:
  both hosted verify runs **xUnit 15,488/15,488; Node 104/104; frozen 29/29**,
  standalone Go scope PASS, Release 0 warnings/errors. CodeQL/Actions analysis
  also success; all four exact-head check-runs completed/success.
- This is a crash/DoS regression fix to an **already implemented bounded**
  numeric-addition path. Newly implemented capabilities: **0**; local
  SKIP(UNIMPLEMENTED) unchanged **7 of 14**. Full original pinned TS7 compiler
  acceptance **NOT_RUN**, executable denominator unknown and releaseEligible=false.
  No fresh original Go compiler differential is claimed. [Scope, raw RED/GREEN
  verification logs](progress/numeric-addition-stack.md).


## Bounded postfix `++` and real SKIP reduction — 2026-10-26

- **New C# checker slice:** the scanner lexes `++` distinctly from binary
  `+`; the parser represents a same-line `identifier++` statement and the
  checker accepts initialized mutable `number` variables. A `const` update
  reports TS2588. Prefix updates, nonnumeric operands, postfix expressions
  in initializers, emit and broader operator grammar remain unsupported.
- **Real hosted RED at `167d240`:** seven intended failures in 15,485
  xUnit tests (**15,478 passed / 7 failed / 0 skipped**), with clean Release
  compilation. The earlier malformed-literal test attempt `7c1aade` is
  excluded from this RED proof.
- **Fixed genuine regressions:** first implementation `48e7f5e` passed
  15,484/15,485 but a pre-existing CLI negative test still expected `x++`
  to be rejected. `7339946` retained the failure assertion for the
  unsupported *prefix* form and added a positive CLI success assertion;
  xUnit became 15,486/15,486 while two Node inventory tests still encoded
  the old 6/8 counts. `5e6927c` corrected those counts and asserted the
  stable feature identity without weakening any real regression or release
  gate.
- **Exact implementation head `5e6927c`:** both hosted verify jobs passed
  **xUnit4/MTP 15,486/15,486**, **Node24 103/103**, frozen differential
  **29/29**, standalone Go scope PASS and Release **0 warnings/errors**.
  CodeQL/Analyze(actions) also succeeded (**4/4 checks**). Raw RED,
  intermediate failure and final hosted logs are preserved in
  [postfix-update-run](evidence/postfix-update-run/); job identities and
  limitations: [evidence ledger](evidence/postfix-update.json).
- **Measured delta:** stable `bootstrap:postfix-update` moved from
  SKIP(UNIMPLEMENTED) to IMPLEMENTED/NOT_RUN; the fixed 14-input local
  catalogue changes **6 implemented / 8 SKIP → 7 implemented / 7 SKIP**.
  Complete pinned upstream compiler acceptance remains **NOT_RUN**,
  executable denominator unknown/unexpanded, releaseEligible=false.
  PR #8 is not merged and has no formal latest-head GitHub approval.

## Bounded numeric addition and measurable SKIP reduction — 2026-10-26

- **Real feature:** scanner recognises binary `+`; the C# parser now records
  left-associative numeric binary expressions; the checker infers `number` from
  two number operands, preserving TS2322 annotation mismatch. Only number-only
  `+` (including numeric identifier operands/chains/assignments) is implemented.
  Unary `+`, string concatenation, `any` arithmetic and all other operators
  remain explicitly unsupported, not silently treated as compatible.
- **TDD RED:** `7cdd14f` compiled cleanly and the 11 new tests produced eight
  expected failures (**15,462 passed / 8 failed / 0 skipped**, both hosted verify
  jobs). This reflects seven positive examples plus one mismatch diagnostic.
- **Regression and correction:** initial code/ledger head `c2b8df2` had one
  genuine pre-existing failure: `StringRecoveryTests.EarlierLexicalErrorsSurviveLaterUnsupportedSyntax`
  because standalone unary `+` stopped reporting TSCS1001 after an earlier
  TS1125. The scanner now rejects unary `+` while preserving earlier lexical
  errors. Three unary guards were added, so the final suite contains 15,473.
- **Validated implementation head `273b4b7`:** four exact-head checks completed
  success (CodeQL, Analyze(actions), two verify jobs). Both hosted runs confirmed
  Release 0 warning/error, **xUnit4/MTP 15,473/15,473 (0 failed/skipped)**,
  native Node **103/103**, frozen differential **29/29**, Go standalone scope
  PASS. All six original hosted RED/intermediate/GREEN logs are retained bytewise
  at [numeric-addition-run](evidence/numeric-addition-run/); original failing
  attempts are excluded as passing proof but not erased.
- Registry stable ID `bootstrap:binary-expression` moved SKIP/UNIMPLEMENTED
  to IMPLEMENTED/NOT_RUN; local 14-probe register is now **6 implemented /
  8 SKIP**. True full upstream acceptance added: **0**; complete executable
  denominator unknown/unexpanded, full compiler/reference still NOT_RUN.
  No actual pinned-Go complete original compiler comparison was run this stage;
  releaseEligible=false. [Evidence ledger](evidence/numeric-addition.json).
  PR #8 remains unmerged pending formal review.

## Review fixes: pinned Unicode path comparisons and generated declarations — 2026-10-26

- Closed two concrete P2 implementation/integrity findings on PR #8 without bypassing comparison: Go `strings.ToLower` recognises dotted-I in the configDir prefix, while Unicode simple-folding `EqualFold` recognises long-s in `file://localhost`. The product uses its pinned Go lowercase map for templates and a target-ASCII simple-fold comparison for URI file/localhost roots. It does not use host `OrdinalIgnoreCase` for these predicates.
- Real hosted TDD RED at `bf7d00f`: two new path regressions fail with the exact predicted wrong values; 15,451/15,453 xUnit pass, two fail. The ASCII control still passes. The separate complete embedded-declaration SHA-256 guard detects two stale nested `rootDirs.element.supported` / `typeRoots.element.supported` fields.
- The committed generated resource now equals the entire freshly regenerated pinned Go payload byte-for-byte: 49,865 bytes, SHA-256 `0d67a643ca7d3dce6315754e177f7d93cbada15682b4eebc45a2d4eea9a9552c` (previous 49,867 bytes, `464d4fb5eaae717e6d8cc61d5dfb8c0bfab132278c0b3f3251d568e4baaf32d9`). Node regression locks the complete hash, not just the two fields.
- Exact implementation head `8473d1ce5dc00b684119244c4f6d9b0f4fd7210f` passed 4/4 hosted checks: Release zero warning/error, xUnit/MTP **15,453/15,453** with zero skipped, Node24 **103/103**, frozen **29/29**, Go standalone scope pass. Both raw hosted verify logs are accessible through [Actions run 40286427237](https://github.com/KoalaFacts/tscs/actions/runs/40286427237) and [40286434368](https://github.com/KoalaFacts/tscs/actions/runs/40286434368). The RED logs are [40286033683](https://github.com/KoalaFacts/tscs/actions/runs/40286033683) and [40286038675](https://github.com/KoalaFacts/tscs/actions/runs/40286038675).
- This P2 closeout adds **no new TypeScript language feature**; ten registered SKIP(UNIMPLEMENTED) probes remain, four local implemented probes still upstream NOT_RUN, full TS7 acceptance and denominator unknown. Historical 172-source/13,006-source profiles were not re-run. PR #8 not yet formally reviewed/merged. [Machine evidence](evidence/path-unicode-review-closure.json).


## Explicit SKIP/implementation status — 2026-10-26

- Introduced a **separate** source-bound catalogue of 14 existing compiler theory inputs: 4 implemented but full upstream acceptance NOT_RUN; 10 `SKIP (UNIMPLEMENTED)` across 7 distinct missing capabilities. The original unsupported guard assertions still run and cannot count as feature implementation.
- Bound all rows to fixed TS7 pin, original `CompilationTests.cs` Git blob, exact method/InlineData source strings, stable local IDs, and explicit implementation locations or nulls. Missing/new rows, falsely implemented statuses, regressions re-labelled SKIP and wrong pins fail the coverage check; 7 new Node regression cases were locally exercised on Node22 (Node24 hosted verification pending).
- [Machine report](testing/ts7/implementation-coverage.json), [registry](../tests/implementation-coverage/registry.json) and [scope/evidence](progress/implementation-coverage.md) remain distinct from the 15,450 xUnit tests, 29 frozen differentials and all original upstream compiler cases. `verify.sh` retains `--fail-skips on --minimum-expected-tests 15450` and adds the bounded report check after its existing gates.
- Release remains false; upstream concrete-case denominator is unknown/unexpanded; original compiler/reference acceptance is NOT_RUN. No actual TypeScript compiler feature, upstream PASS, or removed SKIP is claimed by this accounting update. Single snapshot is insufficient to forecast an actual completion interval.


Working bootstrap of the first noEmit milestone. No JavaScript emitter or language
server. No real-project compatibility or performance claim.

## Bounded `any` variable annotations — 2026-10-26

- Added a bounded parser/type-checker rule for variable `any` annotations; primitive assignment accepts `any` in either direction, while `const` reassignment stays TS2588. This is not full `any`/functions/generics support.
- Hosted TDD RED at `6e2d446`: **7 actual new `any` failures** (TSCS1001), 15,452 passed/15,459 total; both verify jobs failed as expected. Exact-head GREEN at `00317a1`: two hosted verify jobs each passed **15,459/15,459 xUnit 4/MTP**, **Node 103/103**, frozen differential **29/29**, standalone Go scope PASS, Release **0 errors/0 warnings**; CodeQL and Actions analysis both passed. Raw RED/GREEN logs are preserved in `docs/evidence/any-annotation-run/`, metadata in `docs/evidence/any-annotation.json`. The new capability itself was not run against the pinned whole upstream compiler.
- One stable local guard probe (`bootstrap:any-annotation`) moved from SKIP/UNIMPLEMENTED to IMPLEMENTED/NOT_RUN; bounded local catalogue now **5 implemented, 9 SKIP** (14 local probes), with no original upstream compiler credit. Frozen upstream case denominator stays unknown, release=false.


## Rooted path and network diagnostic follow-up — 2026-10-26

- The pinned compiler-option map now dispatches the 15 previously unsupported
  path/locale declarations in addition to the existing 111 entries (126
  declaration positions; 125 distinct names). Rooted PathKinds (1/2/3/7)
  resolve lexically against config directory; source-map path kind retains
  its unrooted value, network/device paths are rejected, and command-line-only
  locale still reports TS6266. This is option-value conversion, not file host,
  typed/effective CompilerOptions or full compiler acceptance.
- Independently inspecting fixed upstream `tsconfigparsing.go` and
  `nativepath/network.go` revealed a formatting boundary: Go's
  `diagnostics.Format` collapses contiguous malformed WTF-8 into one U+FFFD
  in the TS5116 network-path argument. The product now formats just that
  diagnostic argument with the same existing option-name repair helper. Raw
  UTF-16 input still drives path resolution/network rejection. Four xUnit
  regressions construct malformed UTF-16 at runtime (not in InlineData's
  UTF-8 metadata), also checking a valid surrogate pair.
- Exact source head `efb2c56ccee623b83aaeaab3a56a03dbd1dda098` had all
  four hosted checks completed/success: Release0 warnings/errors, xUnit/MTP
  **15,450/15,450** (zero failed/skipped), Node **95/95**, Go standalone
  scope PASS, frozen cohort **29/29**. Two full hosted verify logs are saved
  unchanged in `docs/evidence/network-path-diagnostic-run/`; the four check
  URLs and source IDs are in
  [the evidence ledger](evidence/network-path-diagnostic.json).
- The 316 pinned private-Go option-map rows are historical reference results
  that the updated C# tests compare in full. The complete 172-source map
  CLI profile and 13,006 source selection were **not rerun** by this correction;
  earlier unsupported classifications remain historical evidence. Full
  original compiler/referenceCompiler/acceptance all **NOT_RUN**, concrete
  upstream executable denominator unknown and releaseEligible=false.

## Unknown-option diagnostic UTF-8 normalization P2 — 2026-10-26

- The pinned Go diagnostic formatter collapses each contiguous malformed
  WTF-8 byte sequence to a single U+FFFD. The option-map message path now
  formats invalid surrogate runs the same way, preserving valid pairs and
  leaving original lookup/suggestion keys unchanged.
- Added six focused cases (adjacent high/low invalid surrogates, separated
  invalid runs and valid surrogate pairs) exercising both product conversion
  and the real fixture JSONL path. Exact head ad68f397 passed four hosted
  checks, MTP15,432/15,432, Node95/95 and frozen29/29. Historical15,426
  results retain their original tree/provenance.
- Scope remains option-map diagnostics only. Full original compiler/reference/
  acceptance remains NOT_RUN; release eligibility is unchanged.

## Declaration-driven config option map — 2026-10-26

- Unified actual126 declaration entries (125 names, duplicate help retained)
  into the product dispatcher.111 supported;15 path/locale policies explicitly
  unsupported. Canonical-case errors, weighted suggestions, ordered duplicate
  values, nil error calls and batch diagnostics follow actual convertOptionsFromJson.
-316 actual-Go map inputs;226 complete comparisons/90 unsupported. Real fixture
  preparation JSONL172:90 MATCH,8 MATCH_DIAGNOSTICS,74 UNSUPPORTED, no mismatches.
  All3 selected upstream skips remain flagged. Historical input selection only;
  fresh Go parser/map and C# preparation, no full13006 selection rerun.
- RED490fail/14936pass ->15426/15426; Node95/95, frozen29/29.
  [Scope and evidence](progress/json-options-map.md). ParseOption call boundary,
  not typed/effective CompilerOptions, config host or original compiler acceptance.

## Lib enum-list integration — 2026-10-26

- Connected the actual lib declaration/116 alias mappings to existing list and
  enum converters. Preserves string filenames, order, null/nil/filter semantics
  and full element diagnostics, including pinned converted-index behavior.
- Actual private Go504 rows; all126 declarations retained. Product uses the
  generated lib resource, not test-supplied expectations. Original failed-run
  full-descriptor data retained losslessly; final rows share one declaration
  table without changing any values/diagnostics/case identity.
- RED486fail/14450pass ->14936/14936; Node89/89, frozen29/29.
  [Scope/evidence](progress/json-lib-options.md). No standard-library loading,
  new CLI lib acceptance, effective configuration or original compiler credit.

## Enum config options — 2026-10-26

- Added actual six enum maps/deprecated keys and product conversion: nullable/
  empty string, pinned Go lowercase, valid values and TS5024/TS6046 diagnostics.
  Native enum integers explicitly project to numeric config values.
- Actual Go private converter324 rows across6 enum declarations, all126 retained.
  Includes every real key/uppercase key, deprecated acceptance and ordered error
  suggestions. CLI target invalid values now report syntax/config exit2; other
  valid targets remain bootstrap unsupported, with no new emit/target capability.
- RED300fail/14130pass, plus CLI RED302fail/14130pass ->14432/14432;
  [scope/evidence](progress/json-enum-options.md). Full original acceptance NOT_RUN.

## Primitive-element config lists — 2026-10-26

- Added bounded list option conversion through the product scalar converter:
  element diagnostics, actual element declaration names, null/nil/empty lists,
  ordered filtering and moduleSuffixes preserve-falsy behavior.
- Actual private Go:4 supported lists of126 retained declarations ×20 inputs
  ×2 contexts =160 rows, including108 diagnostic-bearing and52 clean value
  outcomes. All144 TS5024 diagnostics and their UTF16 spans retained.
- RED160fail/13946pass ->14106/14106; Node89/89, frozen29/29.
  [Scope/evidence](progress/json-list-options.md). No new CLI option or full
  config-host/compiler acceptance; historical profiles keep original bindings.

## Primitive config option conversion — 2026-10-26

- Added actual convertJsonOption primitive/object conversion for declarations
  without path/extra validation: command-line-only rejection precedes nullable
  type validation; values retain UTF16, binary64 bits and ordered/nil semantics.
- Actual Go private converter:100 of126 declarations ×15 values ×2 source
  contexts =3000 rows; all126 declaration identities retained,26 excluded from
  this bounded branch.2214 TS5024 /210 TS6266 /576 value outcomes matched.
- CLI noEmit/noLib invalid types now report config TS5024/exit2. Null defaults
  remain false with explicit CLI overrides; five default/false cases now locked.
- RED2822fail/11124pass ->13946/13946; Node89/89, frozen29/29.
  [Scope/evidence](progress/json-scalar-options.md). Full source profiles and
  original compiler acceptance not rerun; releasefalse and all domains retained.

## Config option value type admission — 2026-10-26

- Added the pinned isCompilerOptionsValue shape predicate in the product:
  nullable declarations, exact primitive/object kinds, list outer shape and
  listOrElement scalar recursion. List element/enum membership/path validation
  remains a later conversion step; acceptance here is not effective options.
- Actual Go private helper:126 compiler declarations + extends + nil option +
  missing-element case, each over14 parser/converter inputs =1806 rows.
- CLI noEmit/noLib null values now mean unset/default false, allowing explicit
  supported CLI flags to override them. Existing bootstrap restrictions remain.
- Genuine RED412fail/10521pass ->10933/10933; [scope/evidence](progress/json-option-types.md).
  No full source config profile or original compiler acceptance rerun/claim.

## Config value evidence omission P2 — 2026-10-26

- Fixed the fail-open artifact verifier: empty manifests or omitted required
  payload/manifest entries now fail. Exact base/nested-oracle/shard sets, batch
  coverage/IDs, selected reference bindings and recomputed results/totals are
  required, including the original diagnostic outcome.
- Negative regressions cover every required artifact, empty evidence, rehashed
  calls/IDs and forged counters. Existing three raw profiles revalidate; their
  historical execution/tree binding remains unchanged. No new compiler feature
  or new full Go profile execution is claimed.
- [Evidence](evidence/config-value-integrity.json). Full release still false.

## Source-backed config values — 2026-10-26

- Connected selected raw config units to the existing product converter through
  JSONL, sharing one parsed AST. Actual Go selection retains13006 sources,
 42 upstream skips/8 non-raw-UTF8 blobs;172 selected config values executed on
  both sides:171 MATCH/1 MATCH_DIAGNOSTICS, no mismatch/unsupported/crash.
- The original TS1005 remains;12834 no-config sources receive no C# value or
  compiler credit.3 selected configs retain upstream-skipped metadata.
- Genuine missing-profile RED177/8949 ->9126/9126; Release0 warnings/errors,
  Node81/81, frozen29/29, locked offline restore and Go scope pass. Full profile
  intentionally exits1 for the retained syntax error. SDK coordinator NOT_RUN.
- [Scope and evidence](progress/source-config-values.md). No option notifier,
  host/effective options or original compiler acceptance. All domains/seven prior
  reference fatals/unknown executable denominator remain; releasefalse.

## Config AST to values — 2026-10-26

- Added bounded convertToObject/convertToJson semantics: ordered last-wins
  objects, omitted empty keys, scalar-null filtering and nil/empty array
  distinction, lossless UTF16 strings/keys and exact binary64 values.
- CLI uses the converter for supported boolean/target options; this is not the
  whole config host, option callbacks or effective config.189 actual-Go vectors
  RED189/8760 ->8949/8949; Node75/75, frozen29/29, Release0 warnings/errors.
- [Scope and evidence](progress/config-values.md). Original compiler/reference/
  acceptance NOT_RUN, all domains/seven reference fatals retained, releasefalse.

## Mixed JSON diagnostic event order — 2026-10-26

- Addressed review P2: JSON parser errors now interleave with lexical errors
  when tokens are consumed. No global position sort; final quote validation
  remains last. Ordinary TypeScript scanner results remain unchanged.
- Added16 actual-Go parser regressions (original162 unchanged): RED12/8746,
  plus CLI mutation RED14/8746 -> final8760/8760; Node75/75, frozen29/29,
  Release0 warnings/errors. Independent Go178-vector regeneration byte-identical;
  independent8758-suite/188-focused tests pass before final two CLI additions.
- [Evidence and raw logs](evidence/json-diagnostic-order.json). SDK coordinator
  and original compiler acceptance NOT_RUN; prior full-source evidence remains
  bound to its original tree. Effective configuration remains incomplete.

## Source-backed config AST and missing-comma recovery — 2026-10-26

- Connected selected raw config units to the product AST with rooted filename/
  currentDirectory context and strict source/input/schema bindings. Actual Go
  raw parser/name/path helpers plus a mirrored selection fragment feed actual
  ParseSourceFile(ScriptKindJSON); this is pre-VFS, not effective options.
- All13006 source identities/bytes retained:172 config sources,171 MATCH,
  1 MATCH_DIAGNOSTICS,12834 NOT_APPLICABLE,42 skips/8 non-raw-UTF8 inputs;
  zero AST differences/unsupported/panics/crashes. No-config sources earn no
  compiler credit. The source-backed missing-comma case now retains its AST
  and TS1005; CLI syntax exit2. Full profile command intentionally exits1.
- RED connection/context/real-source regressions -> MTP8742/8742, Node75/75,
  frozen29/29, Release0 warnings/errors. SDK orchestration NOT_RUN.
  Independent reruns reproduce all source/AST results; review caught truncated
  raw evidence, now excluded and replaced by verified output shards.
  [Evidence](evidence/source-config-ast.json) and
  [scope/limits](progress/source-config-ast.md). All original compiler acceptance
  remains NOT_RUN, seven prior reference fatals retained; release not eligible.

## JSON decimal-integer value correction — 2026-10-26

- Addressed the review finding: JSON numeric values, negative-value projections
  and numeric property names now canonicalize binary64 values instead of copying
  raw decimal spelling. Token spelling/ranges stay unchanged. Scientific text
  uses the pinned1e21 boundary; overflow produces the pinned Infinity value text.
- Added140 actual-Go parser boundary cases to the original22 (unchanged), now162.
  RED92fail/8640pass -> MTP8732/8732, Node69/69, frozen29/29, Release0 warnings/
  errors; golden repeats byte-identically. Independent review compared3845
  decimal integers against pinned Go with zero differences and ran172 AST tests.
  [Evidence](evidence/json-numbers.json).
- This repairs the existing integer subset. Fractions/exponents/nondecimal
  source grammar, full config/AST/source profiles and original compiler
  acceptance remain incomplete; no release claim or denominator reduction.

## Product bounded JSON AST and config path — 2026-10-26

- Added an object/array/scalar AST reader over the product scanner's JSON mode.
  Preserves property/element order and UTF16 spans/values, accepts comments and
  trailing commas, and performs the pinned recursive TS1327 quote validation.
  The CLI config path now consumes this AST, replacing System.Text.Json parsing;
  config lexical/quote diagnostics retain locations and use exit2.
-22 actual pinned ParseSourceFile(ScriptKindJSON) vectors compare the complete
  selected tree projection and every diagnostic. RED23fail/8558pass ->8581;
  guards and one independent review corrective pass ->8592/8592, Node69/69,
  frozen29/29, Release0 warnings/errors; golden repeated byte-identically.
- Independent review found the host identifier approximation exposed U+2E2F
  incorrectly through JSON names. Five new guards RED5/8587 -> GREEN8592.
  JSON-mode unquoted names are now ASCII-bounded; quoted Unicode remains
  supported. Raw Go recovery repro retained, not mislabeled as matched support.
  [Evidence and raw logs](evidence/json-ast.json).
- This is a bounded parser/config-host integration, not full JSON recovery or
  effective config. Non-integer numeric forms, unquoted Unicode names, other
  AST/parser grammar and all original compiler acceptance remain incomplete.
  Original source profiles, seven reference fatals and unknown denominators
  are retained; no full corpus rerun or release claim.

## Numeric escape coverage correction — 2026-10-26

- Addressed the review discrepancy: the previous368-vector cohort covered08,
  but its coverage wording incorrectly included09. Added four actual-Go09
  vectors; all original368 rows are unchanged. No product source change.
-372 vectors and8557 MTP tests now pass. Node69/69, frozen29/29, Release0
  warnings/errors; generation repeats byte-identically.09 reports TS1487 over
  backslash+0 only, retains the following9, with exact ASCII/emoji offsets.
  [Evidence](evidence/numeric-09.json). Original compiler acceptance unchanged.

## Product numeric and identity string escapes — 2026-10-26

- Added standalone NUL and identity escapes, including supplementary characters.
  Legacy octal follows pinned two/three-digit consumption, retains decoded
  values and reports TS1487 with the actual suggested hexadecimal spelling.
  Decimal escapes8/9 report TS1488. Invalid numeric escapes are syntax errors,
  not successful acceptance or unimplemented TSCS errors.
-368 actual-Go full token streams compare raw spelling, UTF16 values/ranges,
  flags and all localized diagnostic messages/ranges. Initial RED368fail/8181pass
  -> MTP8553/8553, Node69/69, frozen29/29, Release0 warnings/errors.
  Golden repeats byte-identically. Independent review rebuilt, reran8553 tests
  and regenerated368 Go vectors with identical output; no blocker.
  [Evidence and raw logs](evidence/numeric-identity.json).
- Original0/8/q regression inputs remain, with actual-Go outcomes replacing old
  unsupported assertions. Four CLI rows verify syntax/acceptance exit status and
  supplementary-prefix offsets. This is ordinary string scanning, not JSON,
  parser/AST or original compiler acceptance. No full source profile rerun.

## Product string escape recovery — 2026-10-25

- Malformed hex/Unicode strings now recover with TS1125/TS1198/TS1199,
  retain invalid escape spelling in decoded values, and continue scanning.
  EOF/backslash and unterminated strings retain all TS1126/TS1002 errors.
  Tokens carry pinned string flags (quote, invalid, unterminated, hex/Unicode).
-323 full token streams execute the actual pinned Go scanner; compare every
  token, raw spelling, UTF16 value/range, line break, string flag and every
  diagnostic including order/message/range. Includes both quotes, emoji prefixes,
  overflow, malformed surrogate lookahead, EOF and all58 prior plain literals.
- Initial RED294fail/7882pass -> final direct MTP8181/8181, Node69/69,
  frozen29/29, Go scope tests pass, Release0 warnings/errors. Golden repeats
  byte-identically. Independent review regenerated the323 oracle results and
  reran the built8181-test suite successfully; no blocking finding.
  [Evidence and raw logs](evidence/string-recovery.json).
- Compilation exposes lexical errors before parser/checker work; if unsupported
  syntax is encountered later, earlier lexical diagnostics remain present.
  CLI syntax exit2 and UTF16 JSON positions are covered. Numeric/octal/identity
  escapes remain unsupported; general parser recovery, JSON/config and AST are
  still incomplete. No original corpus/config/compiler acceptance rerun or
  release claim. Historical sections below retain their original slice scope.

## Product valid Unicode/hex string escapes — 2026-10-25

- Supports valid x2/u4/braced Unicode escapes in ordinary strings. UTF16 storage
  preserves lone surrogates and mixed escaped pairs. Per-digit bounds checks
  prevent overflow while retaining arbitrary valid leading zeroes.
-164 actual-Go scanner vectors preserve raw base64 and UTF16 arrays using pinned
  DecodeJSStringRune, avoiding replacement during Go JSON serialization. Tests
  compare raw spelling, token length, code units and public compilation. Three
  CLI additions validate supported exit0; malformed inputs remain TSCS1001/exit1.
- RED164fail/7686pass -> direct MTP7853/7853, Node69/69, frozen29/29,
  Release0 warnings/errors; golden repeats byte-identically. Independent audit
  verified all164 raw-to-UTF16 values, token ends and provenance hashes.
  [Evidence and raw logs](evidence/unicode-escapes.json).
- Malformed escape diagnostics/recovery, token flags, numeric/other escapes,
  JSON/config parsing and AST remain incomplete. No original full corpus run or
  release acceptance is claimed; prior full-source evidence keeps its old tree.

## Product string escape slice — 2026-10-25

- Ordinary strings now decode simple escapes and line continuations, retaining
  raw Token.Text plus decoded Token.Value for subsequent AST work. Unsupported
  Unicode/hex/numeric/other escape branches still report TSCS1001/exit1.
-66 real-Go literal vectors compare decoded values, raw spelling and UTF16 lengths;
  line-break flags include preceding trivia/following tokens. Existing58 plain
  literal regressions now check decoded values too. CLI validates supported exit0
  and unported escape exit1. This is lexical/type-checking support, not execution.
- RED60fail/7613pass -> direct MTP7682/7682, Node69/69, frozen29/29,
  Release zero warnings/errors; golden byte-identical on repeat. One independent
  static review found no blocker; coverage notes addressed in one pass.
  [Evidence and raw logs](evidence/string-escapes.json).
- JSON/config parsing, AST/general scanner and full original compiler acceptance
  remain incomplete. No full source profile rerun or release claim.

## Product scanner string line characters — 2026-10-25

- Fixed ordinary string scanning: U+2028/U+2029 are accepted inside unescaped
  strings as in pinned scanString; only raw CR/LF terminate. Outside strings,
  all four still count as line breaks for comments/ASI. Escape support unchanged.
-58 literal vectors execute the actual fixed Go scanner; product compilation
  diagnostics/acceptance match this bounded distinction. Two outside-string ASI
  regressions and four CLI cases verify LS/PS exit0 versus CR/LF TS1002/exit2.
- Actual RED6fail/7602pass -> direct MTP7608/7608, Node69/69, frozen29/29,
  Release0 warnings/errors. Golden repeats byte-identically; independent static
  review has no blocking finding. [Evidence and raw logs](evidence/string-lines.json).
- No original full source/config/compiler acceptance run. JSON/config parsing
  and AST porting remain incomplete; complete release gate remains false.

## Config-file selection preparation — 2026-10-25

- Added pre-parse first-config selection using pinned basename/lowercase semantics,
  preserving later config files and other units in source order. The global
  runExternalCode gate requires the exact raw value true (not TRUE or true;).
-110 actual-Go raw-parser/config-name-helper vectors: RED110fail/7434pass,
  direct MTP7544/7544, Node69/69, frozen29/29, Release0 warnings/errors;
  golden repeat byte-identical. Raw logs are committed with the evidence.
- This is a preparation snapshot only. Original makeUnitsFromTest constructs VFS
  before selection; VFS/JSON/config parsing and external mappers are not invoked
  by this profile. No complete13006 source rerun or compiler credit.
  [Evidence](evidence/config-selection.json). Effective configuration and AST remain next.

## Source configurations and runner fallback — 2026-10-25

- Whole decoded-byte extraction now matches the fixed Go regex, duplicate last-wins,
  Go TrimSpace and one final semicolon. Raw link settings retain arrow text.
  Invalid UTF8 setting values are explicitly UNSUPPORTED, without replacement.
- Fixed compiler91/transpile3 varyBy keys feed canonical products. Compiler nil
  helper fallback is a nil configuration; transpile fallback retains raw settings.
  Fallback branches are mirrored from pinned runner source, not original runner execution.
- Complete source profile:13006 sources,12999 MATCH,7 REFERENCE_FATAL,0 unequal;
  all42 upstream-skipped sources and8 non-raw-UTF8 inputs retained. The7 fatals
  are upstream-skipped module:none fixtures; actual Go and C# both fail, never PASS.
  Successful sources produce15504 helper records/15523 runner configuration records,
  including19 nil-helper fallback sources. These are not executable compiler cases.
- Direct xUnit/MTP7434/7434, Node69/69, frozen29/29, Go scope tests pass,
  Release0 warnings/errors.36 boundary rows; independent96 probes86 MATCH and10
  UNSUPPORTED,0 unequal. Path/domain binding strengthened with a failing regression.
  Final same-source input/Go/C#/result payloads repeat byte-identically.
- [Evidence](evidence/source-configurations.json), [separate progress](testing/ts7/source-configurations-progress.json).
  Static inventory stays historical25→28; effective config/tsconfig, AST, original
  compiler assertions and full runtime expansion remain NOT_RUN. Release gate false.

## Named configuration products — 2026-10-25

- Ported GetFileBasedTestConfigurations products, exact varyBy key membership,
  scalar merging, varying-only names, nil return and25-configuration cap.
- Actual pinned Go helper supplies2920 settings inputs:2526 normal/nil,
  187 expected panic and207 expected fatal results, producing4328 named records.
  These are synthetic helper inputs/outputs, not fixture or compiler pass counts.
- Independent review found host Unicode tables differed from pinned Go17 for55
  scalars. Generated and embedded all1488 Go simple lowercase mappings; every
  uppercase/lowercase pair is exercised through the real configuration helper.
  Config enumeration is normalized by name then UTF8 key/value tuples.
- RED1432/4478, Unicode regression RED55/7343, final direct MTP7398/7398;
  Node65/65, frozen29/29, Release0/0. Both golden and lowercase table repeat
  byte-identically. Go exit1 is intentional and failed subtests reconciled.
- [Evidence](evidence/configuration-products.json). Source setting extraction,
  compilerVaryBy expansion across fixtures, compiler/transpile runner fallbacks,
  effective config/AST/compiler assertions remain NOT_RUN. Go mixed fatal/panic
  map traversal precedence is unspecified; no deterministic precedence claim.

## Canonical option-value splitting — 2026-10-25

- Ported pinned splitOptionValues in C#: Go whitespace, boolean/enum/string
  values, alias deduplication retaining first spelling, star and exclusions.
  Actual Go extraction retains130 declarations/127 unique names; positional IDs
  preserve duplicate declarations instead of losing36 xUnit rows.
- 1414 actual-Go vectors match:1024 value/nil results,186 expected empty-set
  panics,204 intentional fatal outcomes. Go reference intentionally exits1;
  JSON test events reconcile every failed subtest to its expected fatal row.
- Independent review found dotted-I lowercasing differences. Four failing
  regressions now match Go; final MTP4478/4478, Node65/65, frozen29/29,
  Release0/0. Golden regeneration byte-identical; no second review claimed.
- [Evidence](evidence/option-values.json). This prerequisite does not expand
  Cartesian configurations, compute effective options or parse config/AST.
  Full configuration/compiler acceptance remains NOT_RUN; original inventory,
  pin and frozen expectations are unchanged. Only valid Unicode string inputs
  and pinned ASCII declaration/enum keys are covered by this profile.

## Complete source VFS construction profile — 2026-10-25

- JSONL construction transport and strict schema/status validation now connect
  every selected source to actual pinned Go factory/read/exists/Realpath/entries.
- Default-options, case-sensitive profile:13006 MATCH/30902 queries; all42
  upstream-skipped sources retained. Zero differences/panic/error/unsupported/
  crash. Construction request/reference/actual/per-source results repeat exactly.
- Independent review found unsupported could hide an opposing panic; a failing
  regression now preserves failure priority and both side outcomes. Final
  direct MTP3064/3064, Node65/65, frozen29/29, Release0/0. Original raw and
  file-map full13006 profiles still match, with unchanged payloads.
- [Evidence](evidence/full-construction.json), [progress](testing/ts7/construction-progress.json).
  This is one source-level profile, not all VFS/configuration variants or original
  compiler assertions. Those remain unrun/unresolved; release eligibility false.

## Raw fixtures to VFS construction — 2026-10-25

- Connected raw fixture maps to the actual case-sensitive snapshot: files then
  links, directory/file/link query paths, ReadFile/exists/Realpath and entries.
- Retained all55 map-boundary inputs:36 query results and18 expected upstream
  panics match;1 normalized alias collision remains explicitly UNSUPPORTED.
  A passing regression for that guard does not earn compatibility credit.
- RED55 failures/3009 passes, final direct MTP3064/3064, Node62/62, frozen29/29;
  Release zero warnings/errors and repeated Go goldens byte-identical. Review
  corrected oracle inputs to raw links, matching NewVFSWithSymlinks; regenerated
  and rechecked afterward. No second independent review is claimed.
- [Evidence](evidence/fixture-vfs-construction.json),
  [progress](testing/ts7/construction-progress.json). Full13006 fixture VFS,
  case folding, unresolved link policies, config/AST/compiler acceptance remain
  NOT_RUN or unsupported. CLI and FixtureProtocol are not wired to this profile.

## Final PR 6 byte-path validation correction — 2026-10-25

- Independent whole-branch review found that the standard symlink pass could turn
  an invalid UTF8 custom-resolved path into a readable target. The same ordering
  error could include an unreadable linked child in directory entries.
- Eight actual-Go rows add two invalid-byte cases and valid UTF8/ASCII controls
  to both profiles. Four assertions failed before the fix; all 3,009 now pass.
  The original 150 read and 530 entry rows remain unchanged. Current totals are
  154 read and 534 entry rows, regenerated byte-identically from pinned Go.
- Final Release build: zero warnings/errors; direct MTP 3,009/3,009; Node 62/62;
  Go test/vet pass; unchanged frozen differential 29/29. Both complete 13,006-input
  raw and file-map loading profiles were rerun against actual Go, with all 42
  upstream-skipped sources retained. These are loading results, not compiler passes.
- Independent final-assembly probes matched all four read cases, four entry cases
  and 1,601 original exploratory cases. The SDK coordinator remains NOT_RUN after
  sandbox Unix-pipe denial; the documented direct MTP route is recorded separately.
- [Evidence](evidence/pr6-vfs-open-validation.json). Historical slice evidence below
  remains historical. Full VFS/config/AST/compiler acceptance is still NOT_RUN;
  release eligibility remains false.

## Accessible VFS entries — 2026-10-25

- Added case-sensitive directory enumeration with byte-preserved names, sorted
  files/directories, nil slices and linked-child classification; broken links are
  omitted. Actual pinned Go supplies530 complete input/output goldens.
- Independent review exposed a broken-target fallback that could read an unrelated
  nested file. Four actual-Go regressions reproduced it in reads and entries;
  preserving the terminal unresolved path fixes both. The review's41 constructor
  error-text differences reduce to intermediate requested/resolved path selection;
  four more failing regressions now match. Existing146 read vectors are unchanged,
  with four additions bringing that boundary to150.
- Direct MTP3001/3001, Node62/62, frozen29/29, Release zero warnings/errors.
  Repeated530/150 Go artifacts are byte-identical. The initial exploratory1601
  profile had91 mismatches; author reruns after fixes matched1601/1601. This is a
  separate synthetic profile, not a second independent review or original corpus.
- [Evidence](evidence/vfs-entries.json), [progress](testing/ts7/vfs-entries-progress.json).
  Product CLI/FixtureProtocol wiring, case folding, mutations, cycles/deep/ambiguous
  links, full constructor/path families, full fixture VFS/config and compiler
  acceptance remain unresolved or NOT_RUN. No full-release credit is added.

## Read-only VFS boundary — 2026-10-25

- Added a case-sensitive in-memory snapshot for entry creation, implicit parents,
  file/directory/link lookup, ReadFile decoding and Realpath fallback. Open follows
  both upstream custom lookup and the standard-library parent-first link pass.
- 146 actual Go vectors include POSIX/Windows paths, chained/broken links, parent
  construction, BOM bytes and malformed UTF8 alias/target paths. Review found four
  important differences; all were reproduced and fixed. Direct MTP2467/2467,
  Node62/62, frozen29/29, Release0 warnings/errors; golden regeneration byte-equal.
- Two C# safety tests keep repeated/expanding cycles explicitly unsupported. The
  expanding-cycle regression hit the five-second subprocess timeout before the
  depth guard; afterward the full suite passed. No upstream cyclic run or cycle
  compatibility is claimed.
- [Evidence](evidence/readonly-vfs.json), [progress](testing/ts7/readonly-vfs-progress.json).
  Case folding, enumeration/mutation, overlapping link order, deeper chains and
  complete fixture VFS/config/AST/compiler profiles remain unresolved or NOT_RUN.

## Rooted fixture files and link projections — 2026-10-25

- Connected byte paths to raw fixture maps: default /.src or currentDirectory,
  rooted file names, last duplicate file wins, and less restrictive normalized
  link alias/target paths. Colliding normalized aliases remain separate projections.
- Full fixed-Go/C# profile:13006 MATCH,17804 units,17799 file entries,57 links;
  all42 skipped sources retained. Initial/final raw payloads byte-identical, and
  the original raw profile still13006 MATCH with identical C# output.
- 55 Go boundary vectors, direct MTP2319/2319, Node62/62, frozen29/29, Release0/0.
  Independent review had no important findings; three evidence/coverage gaps were
  addressed. Reference echoes are now bound to actual input/name bytes.
- [Evidence](evidence/fixture-file-map.json), [profile](testing/ts7/file-map-progress.json).
  Actual symlink VFS, config/AST and original compiler acceptance remain NOT_RUN.
  Parent6910398 CI succeeded; latest head needs its own checks. One incomplete
  execution was excluded after automatic approval review rejected waiting on it
  due to possible test/build telemetry; completed independent runs supply results.

## Byte paths for VFS construction — 2026-10-25

- Added byte-preserving encoded-root, normalization, two-path combination and
  rooted resolution helpers matching the pinned tspath implementation.
- 750 actual Go vectors include POSIX/DOS/UNC/URL/dynamic roots, directory and
  URL suffix failures, NUL and retained malformed UTF8. Repeated golden is byte-equal.
- Independent review found dynamic-root fast-path and trailing-backslash directory
  differences. Expanded regressions failed22 cases before the one fix pass;
  direct MTP2264/2264, Node60/60, frozen29/29, Release0 warnings/errors afterward.
- [Evidence](evidence/fixture-paths.json) and [ledger](progress/fixture-paths.md).
  This is a development harness foundation; VFS/link maps, tsconfig/AST and original
  compiler assertions remain pending. Parent5d2c6c5 hosted verify succeeded; latest
  work requires its own CI. PR5 remote review remains stale and incomplete.

## Complete raw fixture loading profile — 2026-10-25

- Added an executable C# developer harness with the same151 reference vectors,
  plus stream/error regressions. The differential command builds current sources
  before execution and fails on malformed/missing/duplicate result records.
- Actual pinned Go and C# runs agree on all13006 source fixtures and17804 raw
  units/callbacks. All42 upstream-skipped sources are retained. Repeated complete
  request/result payloads are byte-identical. Source IDs, byte hashes, metadata,
  versions and raw-output hashes are in [evidence](evidence/fixture-corpus.json)
  and a separate [loading ledger](testing/ts7/loading-progress.json).
- One independent review and one RED→GREEN fix pass closed malformed-result and
  stale-binary evidence gaps. Direct xUnit/MTP1514/1514, Node60/60, frozen29/29;
  Release zero warnings/errors. SDK test coordinator still aborts134 on Unix pipe.
- This is raw/default-options/unit-snapshot-callback loading only. AST callbacks,
  VFS/tsconfig construction, full configuration expansion and original compiler
  acceptance are NOT_RUN. No full-corpus compiler PASS or release claim.

## Raw multi-file state and callbacks — 2026-10-25

- Added byte-preserving C# raw-unit state: file/global options, currentDirectory,
  link/symlink ownership, empty/implicit-first files, host-independent basename,
  discarded-prelude validation using the pinned default scanner behavior.
- Callback boundaries preserve partial results: a failed mid-file value is omitted
  and stops subsequent metadata; the EOF callback value is appended even on error.
  Go nil slices and a list containing a null value remain distinct.
- Actual pinned generic Go parser/scanner/path/VFS functions produced151 vectors,
  including8 full raw non-UTF8 fixture sources. Repeated generation is byte-identical.
  Direct xUnit/MTP1512/1512, Node54/54, frozen29/29, Release zero warnings/errors.
- Independent review found Unicode localhost folding at file-URL roots; a failing
  reference vector reproduced it and the fix passed. Nil-result normalization was
  also removed from the oracle and corrected RED→GREEN. Evidence:
  [raw state](evidence/fixture-state.json).
- These are developer-harness units and generic callbacks, not AST parsing or CLI
  integration. VFS/tsconfig, full-corpus loading comparison and all original compiler
  acceptance remain NOT_RUN. SDK coordinator still aborts on Unix pipe permission;
  direct MTP is reported separately. PR5 remains pending remote review.

## Byte-preserving directive boundary — 2026-10-25

- Added a C# development-harness reader using SourceDecoder. It preserves decoded
  source bytes, exact line byte offsets, CRLF/empty-line boundaries, ordered raw
  directives and link alias/target bytes. No decoding of invalid UTF8 is attempted.
- Actual pinned Go regex/link functions generated 535 vectors, including all 256
  byte values in values/links and all eight non-UTF8 fixture sources. Repeated Go
  generation is byte-identical. RED535/825 then direct xUnit/MTP1361/1361; Node54/54,
  frozen29/29, Release zero warnings/errors. Source versions and exact IDs:
  [directive evidence](evidence/fixture-directives.json).
- Independent review found no correctness defect. A long arrowless-link timeout
  was reproduced and fixed with non-backtracking matching plus a Go golden case.
- This helper is linked into the test assembly, not the compiler CLI. It is a
  lexical boundary, not full fixture loading: file/global state, virtual units,
  prelude validation, callbacks and VFS/tsconfig still need implementation.
- Source-scope progress, inventory payloads and original acceptance remain unchanged
  NOT_RUN. PR5 remains pending remote review; prior decoder branch CI passed.

## Byte decoding foundation — 2026-10-25

- Added C# `SourceDecoder.Decode`: UTF8 BOM stripping and UTF16 LE/BE BOM decoding,
  odd UTF16 trailing-byte handling, and byte-exact malformed UTF8 preservation.
  This foundation is not yet wired into the CLI, inventory loader, scanner or VFS.
- Executed the pinned Go decodeBytes/decodeUtf16 functions to generate 764 golden
  cases: 756 byte/surrogate/endian boundaries and all eight full non-UTF8 fixture
  blobs. Repeated generation is byte-identical. No fixture is awarded compiler PASS.
- Direct xUnit/MTP 825/825 (original60, 764 vectors, buffer-ownership regression);
  Release zero warnings/errors; Node54/54; frozen29/29. Reference and C# test IDs,
  exact versions, raw byte vectors and hashes are recorded in
  [decoder evidence](evidence/source-decoder.json).
- The generated scope progress remains unchanged/NOT_RUN. Next: use this byte
  boundary in faithful directive/multi-file loading, preserving raw names/values,
  links, partial callback results and tsconfig/VFS semantics.

## Evidence

- Pinned upstream: Microsoft TypeScript 8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f (7.1.0-dev).
- Reference built with Go 1.27.3; C# SDK 10.0.402.
- Frozen differential cohort: 29 cases, including 7 unchanged upstream source files.
- Historical bootstrap-only discovery: 12,981 compiler/conformance source files; 15,174 known option
  variants and 90 files with unresolved variant expansion. Counts are provisional,
  not a complete faithful upstream harness inventory.
- Entire upstream suite: not run. Seven upstream sources exercised in isolation;
  remaining 12,974 upstream source files not exercised by this cohort. All corpus
  pass/mismatch/unsupported/crash/timeout counts are therefore **unmeasured**, not zero.
- CI workflow is provided. Local verification evidence does not imply hosted CI ran.

## Static TS7 scope inventory

The [new scope registry](testing/ts7/README.md) independently inventories the entire
pinned Git tree: 67,951 assets, 13,006 fixture inputs, 6,064 Go AST declarations,
24 JS test sources and 7 JS benchmark sources. These are separate source-level
measures, not additive test counts. Compiler whole-fixture omissions, byte-invalid
inputs, analyzer-test fixtures and unevaluated build constraints remain explicit.
The bounded transpile configuration layer expands 25 fixtures into 28 named
configurations. The other 12,981 fixture inputs, all runtime cases and executable
acceptance remain unknown or NOT_RUN. The full-coverage release gate is not satisfied.

The old 12,981-file provisional inventory is retained unchanged because the frozen
29-case bootstrap evidence binds it. It excludes transpile and other test domains;
it is not the denominator for TS7-wide coverage.

## Native TypeScript tools — 2026-10-25

- Migrated all 13 owned `.mjs` modules/tests to native `.ts`; updated imports,
  shell commands and current documentation. No loader, transpiler or new dependency.
  Upstream Herebyfile.mjs and frozen inputs remain unchanged.
- Release build zero warnings/errors; Node 54/54 (52 existing plus two native-entry
  guards), direct xUnit/MTP 60/60, Go tests/vet pass, frozen differential 29/29.
- All eight complete inventory payloads are byte-identical before/after; only the
  summary's generator path/hash provenance changes. Repeated native generation,
  including summary, is byte-identical. Transpile remains 25 inputs/28 configs;
  no original compiler/reference/acceptance record is promoted.
- Full verify.sh attempted; SDK coordinator still aborts on Unix pipe permission.
  Direct MTP is a separate successful run. Independent review: no important findings.
- Evidence: [native tools](evidence/native-typescript-tools.json). The migration
  preserves the recorded inventory payloads and the bounded transpile configuration
  set; complete upstream fixture/configuration acceptance remains NOT_RUN.

## Local verification and review

- Release build: zero warnings/errors.
- C# suite: 60 cases, migrated from NUnit to xUnit 4.0.2 / MTP 2.5.1.
  All method names and parameter rows are preserved in the
  [migration map](evidence/xunit-migration-inventory.json). The original 17 Node harness/evidence cases remain; source-inventory regressions
  and Go AST parser tests are additional, not upstream conformance cases.
- Frozen differential cohort: 29/29 matched; repeated reports were identical.
- Independent reference rebuild: 29/29 frozen results reproduced.
- Five additional oracle probes validate review fixes (strict binding names,
  top-level globalThis conflict and diagnostic phase ordering).
- Independent review found three important issues; all were reproduced by failing
  tests and fixed. An additional diagnostic-phase bug was fixed in the same pass.
- Evidence: [cohort](evidence/bootstrap-conformance.json),
  [reference build](evidence/reference-build.json),
  [review regression probes](evidence/review-regressions.json).

Known minor compatibility defect deferred: U+2028/U+2029 inside quoted strings
currently produce TS1002 although the pinned compiler accepts them. Do not interpret
this bootstrap's string-literal handling as complete JavaScript lexical compatibility.

## Milestone gaps and next step

The bootstrap establishes an executable chain and a small oracle-backed cohort.
Full first-milestone infrastructure remains incomplete: faithful directive/multi-file/
symlink harness behavior and complete variant accounting need implementation. General
filesystem hosting is also deferred. Keep these visible rather than call the full
milestone finished.

Next: audit the unexpanded source families, then implement faithful byte/directive/
virtual-file loading and canonical configuration expansion with exact executable
variants, while preserving the separate 29-case gate.
Then expand scanner/parser/binder/checker coverage in dependency-sized ports. Do not
start IL output, a runtime, or a language server during compiler completion.

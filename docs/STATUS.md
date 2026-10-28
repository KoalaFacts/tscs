# tscs status

## Discriminated union branch narrowing — integration candidate (NOT_RUN)

The shared type checker now branches the existing named object-union type when a
strict equality/inequality condition checks a known literal discriminant on a
simple identifier. This supports safe per-arm member reads in if/else and
else-if, including imported type aliases and array loop bindings. Narrowing is
reverted at the end of each branch; arbitrary condition trees, broad member
types and post-conditional narrowing are not yet claimed. See
[bounded work package](progress/discriminant-flow.md). This commit has no local
SDK build; actual-hosted focused integration, generated Node JS execution and
fixed Go differential are pending. No original TS7 acceptance is credited.


## Primitive literal type family — implementation candidate, not merged

Source-level literal annotations for strings, numbers and booleans now pass through
alias/interface/union/type-checker and ES2015 emit; a bounded contextual checker
handles fresh objects and arrays without treating stored widened variables as
literals. Strict verify passed on an intermediate head; the first real pinned-Go differential
found one widened primitive array mismatch (17/18). Source and stable ID retained;
checker and expected TS2322 corrected from actual pinned Go output. The corrected
head still requires exact-head CI and review closure. No original TS7 acceptance is credited; full scope remains unknown.
See [the new work package](progress/primitive-literal-types.md).

## 2026-10-28 — Named types, interface shapes and unions (integrated candidate)

The existing AST/binder/type checker/module linker/emitter now carries a
separate lexical type namespace: bounded non-generic `type` aliases,
required-member interfaces, unions and explicit cross-module `import type`.
The compiler erases these declarations from ES2015 JS. Three modules produce
`result=42`, `label=seed`, `count=2` under actual Node24 runtime execution.
Invalid alias/interface assignments block all output, and unions do not
pretend to support narrowing or intersection-safe property writes.

Local focused tests/build and full strict verification results must be tied
to the exact delivered head; independent pinned-Go diagnostics and review
remain separate requirements. This is bounded language functionality, not
full upstream compatibility or a release. Details: [work package](progress/named-types-union.md).

Original TS7 acceptance remains **0 accepted** with **15,523 explicit
SKIP_UNIMPLEMENTED configuration targets**; dynamically expanded scope
remains unknown. No ledger, ID or gate was changed to make this package pass.

## 2026-10-28 — Array indexing and for..of (one work package, not yet merged)

On top of merged PR #18, the shared compiler AST/binder/checker/emitter now
handles homogeneous array literals, postfix `T[]`, array element reads/writes,
array length, and braced `for..of` iteration with lexically scoped `let/const`.
The four-module ES2015 runtime graph computes 42 after changing an imported
array element and summing via a typed function with a for..of loop. Incorrect
indexed writes fail compilation and emit no JavaScript. Local focused build
passes with 0 warnings/errors; 31 array and 44 object checks passed. The fixed
Go reference differential is wired into the normal reference lane but its
14 array cases are NOT_RUN at this source-edit stage. No original TS7 accept
credit is taken. Full scope and limitations: `docs/progress/array-index-forof.md`.

## 2026-10-28 — Structural object data (PR #18 merged)

PR #18 merged as `a643cba95c523db4e37bc1e879ddfc990b853ec5`.
The final source tree `ab1e9415c212fb443a393cc7a9412e1a7ad2477d`
was identical to the twice-verified synthetic merge. Fixed Go checked 29
bounded object cases and executed both graphs (result42). The independent
whole-change read-only source review found no blocker; it was not a formal
GitHub APPROVE and Codex review credits were exhausted.


The integrated object family now compiles and executes across modules: inline
required member shapes, literals, nested property reads/writes, structural
assignment, object function parameters/returns, and AST-backed ES2015 output.
Local execution of the emitted four-module graph returns 42 and preserves the
other properties. Missing properties, incompatible writes and fresh excess
properties are rejected. Shared-type diagnostics and parser nesting are bounded.
Unsupported object forms remain explicit; no source-case special handling.

The current container now has the exact SDK and tools. Actual local SDK build
and MTP coordinator passed: focused object42/42, internal15,788/15,788,
Node157/157, frozen29/29, and actual Node24 execution of emitted object modules.
Commands, raw logs and limits are in `docs/evidence/structural-object-data.json`
and `docs/progress/structural-object-data.md`. The first commit had NOT_RUN reference evidence; the final head later
passed the fixed Go reference lane, as recorded above.

Original TS7 accounting is unchanged: 15,523 configuration targets remain SKIP,
0 original accepts, full dynamic scope unknown. Only the existing local probe
`bootstrap:object-literal` moved to implemented: local probe registry is now
9 implemented/NOT_RUN + 5 SKIP; that is not original acceptance.

The object work package is closed; subsequent language families are handled separately.

## Reference-corrective pass (same work package)

The first live fixed-Go differential found 28 matches and one real mismatch:
`object-data:negative-06` must report TS2322 at the object property, not TS2345
at the call. Contextual member checking is now corrected; whole-argument
non-literal mismatches retain TS2345. Local RED2/42 became GREEN44/44, build
0 warnings/errors, and the emitted module graph still executes correctly.
All29 fresh local product results now match the captured actual pinned Go
codes/exits with identical input hashes. Fresh corrected-head CI later passed;
see `docs/evidence/object-contextual-diagnostic.json`. No case was skipped or removed.

Codex review failed due to exhausted review credits, not a clean approval. An independent read-only source review and normal merge subsequently completed.

## Existing foundation and remaining scope

The source contains host-backed project/config loading, module linking, typed
CompilerOptions, basic functions/control flow and bounded ES2015 output.
Full TS7 compatibility, standard libraries, arbitrary object semantics,
declarations/maps, incremental/watch and other original domains remain incomplete.
No release is asserted and existing strict gates remain unchanged.

[Unmodified prior status and evidence handoffs](progress/status-through-project-selectors.md).

## 2026-10-29 — Braced try/catch/finally with lexical error binding (source candidate; NOT_RUN)

The existing checked C# AST and compiler now handle braced try/catch, optional
single catch identifier or omitted binding, and optional finally. A catch
binding is local to that block and uses the default non-strict any policy.
Typed/destructured catch parameters and duplicate catch-local declarations
fail closed. Try/catch initialization is conservatively joined: no branch
donates speculative initialization to its sibling or cleanup. Finally retains
abrupt-override semantics. Four-module Node 24 execution covers real throw,
catch, rethrow and finally behaviour, plus negative no-emit programs.
14 new stable bounded Go reference sources run independently alongside the
unchanged 22 throw-flow sources and preserve raw per-case evidence.
Hosted verification is NOT_RUN at source creation; original TS7 acceptance
remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED.

## 2026-10-29 — Checked try/finally abrupt cleanup (source candidate; NOT_RUN)

The existing throw/return/break/continue flow model now accepts braced
try/finally without a catch clause. Cleanup executes on normal and abrupt
exits; abrupt cleanup overrides a pending control transfer. The type
checker checks try and finally separately, restoring incoming definite-
assignment and flow facts before cleanup, rather than assuming an
assignment happened before a throw. This intentionally rejects some
safe post-try initialization cases; catch and precise try-join analysis
are not implemented. A four-module Node execution and 9 additional
pinned-Go source-bound cases (13 to 22, original IDs unchanged) are
wired into existing gates, NOT_RUN at source creation. Original TS7:
0 PASS, 15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED.

## 2026-10-29 — Checked throw statements and abrupt function exits (source candidate; NOT_RUN)

The scanner, AST/parser, type checker, function/switch/loop abrupt-exit analysis
and ES2015 emitter now carry checked `throw expression;`. A throw may terminate
a typed function path without inventing a return value; thrown operands remain
fully checked. A line terminator after throw and a missing operand are errors.
Try/catch, new and arbitrary exceptions remain outside the supported subset.
Exact-head CI, four-module Node execution and 13 pinned-Go diagnostic/exit cases
are NOT_RUN at source creation. This is not original acceptance; original 0 PASS,
15,523 SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED.

# tscs status

## 2026-10-29 — Numeric postfix value expressions (source candidate; NOT_RUN)

Native ES2015 postfix ++/-- now produces the prior numeric value in nested
checked expressions, including object/array targets and indices with their
own side-effect updates. The existing lvalue restrictions and conservative loop
backedge write analysis are reused. Focused C# regressions, four-module Node
execution and thirteen additive stable pinned-Go cases are wired into existing
strict gates, but execution is NOT_RUN at source creation. Original TS7
acceptance remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED.
See [work package](progress/postfix-numeric-expressions.md).


## 2026-10-29 — Prefix numeric updates (candidate, NOT_RUN)

Checked prefix ++/-- numeric mutations now share the existing AST, mutable
symbol and readonly property checks and ES2015 emitter. This includes value
expressions, nested array indices and classic for updates. Four-module
execution and ten additive stable pinned-Go cases are wired to normal gates;
exact-head hosted checks are NOT_RUN at source creation. Original TS7
acceptance remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic
scope UNEXPANDED. No full compatibility or release claim.

## 2026-10-29 — Readonly named object/interface properties (source candidate; NOT_RUN)

Required named properties in interface declarations and structural object types
may now carry contextual `readonly` modifiers. The same semantic object model
and binder preserve the metadata through aliases/imported type bindings and
nested members; the checker rejects direct and compound property writes with
TS2540, while reads and mutable non-readonly members still emit ES2015.
The literal member named `readonly` remains valid, without being mistaken for a
modifier. Existing 19 named-type reference IDs are unchanged; six stable
positive/negative cases and four-module Node execution are wired to the
normal gates. No local SDK or checkout is available: exact-head hosted checks
are NOT_RUN at source creation. Original TS7 acceptance stays 0 PASS /
15,523 SKIP_UNIMPLEMENTED; dynamic scope remains UNEXPANDED.


## 2026-10-29 — Numeric bitwise compound mutation (source candidate; NOT_RUN)

The existing bitwise/shift expression pipeline now accepts six numeric compound
assignment operators (`&=`, `|=`, `^=`, `<<=`, `>>=`, `>>>=`) on checked
identifier, property and array targets, including classic `for` updates.
This reuses the original type-checker mutation safeguards and ES2015 compound
assignment emission without duplicate lvalue evaluation. The existing bitwise
pinned-Go cohort grows from 18 to 30 source-bound cases without reassigning
existing IDs; four-module Node execution covers mutations and iteration.
At source creation .NET/Node/Go validation is NOT_RUN locally, so no pass is
claimed before exact-head hosted results. Original upstream acceptance remains
0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED.

## 2026-10-29 — Fractional/exponent numeric syntax (source candidate; NOT_RUN)

The C# scanner now tokenizes finite decimal fractions, leading/trailing decimal
points and e/E signed exponents as one numeric literal in TS source mode.
The shared type system canonicalizes numeric literal types by Number value
(e.g. `1.0` and `1e0`), including negative zero, so aliases, unions, arguments
and contextual member checking do not invent distinct spelled-number types.
The existing parser/binder/checker/ES2015 emitter and module graph are reused.
JSON mode and unsupported radix/separator forms are not widened. A 22-source
fixed-Go diagnostic/exit cohort, four-module Node runtime and negative no-emit
verification are wired to the normal CI gates, but are NOT_RUN at source
creation. Original TS7 acceptance remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED;
the full dynamic scope is UNEXPANDED. No release is claimed.

## 2026-10-29 — String primitive reads and ordering (source candidate; NOT_RUN)

The checked C# type model now recognizes primitive/literal string `length`,
numeric character indexing, and lexical `< <= > >=` for two string-family
operands, including string-literal unions. String element and `length` writes,
including compound mutation, reject with TS2542/TS2540 rather than emitting
unsafe JavaScript; mixed string/number ordering fails closed. The existing
scanner, AST, module linker and ES2015 emitter are reused. An 18-source
fixed-Go reference cohort plus four-module Node execution and explicit negative
no-emit validation are wired to the normal gates but NOT_RUN at creation.
The original TS7 source/configuration ledger remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED; unexpanded domains remain UNEXPANDED.

## 2026-10-29 — Numeric exponentiation work package (candidate; NOT_RUN)

The checked scanner, parser, type checker and ES2015 emitter now carry numeric
`**` and `**=`, with right-associativity, precedence above product/unary,
unparenthesized unary-left rejection, depth limits, and existing mutable
identifier/property/array target checks. The same four-module runtime and
20-source fixed-Go comparison are wired to the strict gates. They are
NOT_RUN at source creation; do not count as original TS7 acceptance.
Original TS7 PASS remains 0, 15,523 SKIP_UNIMPLEMENTED targets and
unexpanded dynamic scope remain unchanged.


## 2026-10-29 — Bounded as/satisfies compile-time type operators (source candidate; NOT_RUN)

Contextual type assertions now reuse the existing named/structural/union type
resolver and check for sufficient overlap (TS2352); checked satisfies constraints
retain inferred types, fresh-object excess errors and contextual literal members,
with TS1360 for incompatible primitive constraints. Both operators erase in
ES2015 output with safe grouping; cross-module export prebinding supports the
new annotation-bearing initializer path. Discriminated object-union satisfies
contextual inference remains explicitly unsupported rather than unsafely widened.
The new 25-source pinned-Go code/exit lane and real four-module Node runtime lane
are wired into existing gates but NOT_RUN at source creation. Full original TS7
acceptance remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED and dynamic scope UNEXPANDED.


## 2026-10-29 — Bounded string addition and compound mutation (source; NOT_RUN)

Shared semantic addition distinguishes numeric, definite-string and any
results, including string concatenation of structured values. Existing AST
and emitter carry += for strings and refuse numeric targets that would
become strings with TS2322. Four-module runtime and 25 stable pinned-Go
code/exit cases are wired into strict gates but NOT_RUN at creation.
Original TS7 accepted remains 0 / 15,523 SKIP_UNIMPLEMENTED;
unexpanded dynamic scope remains UNEXPANDED.

## 2026-10-29 — Numeric division and remainder (source candidate; NOT_RUN)

The checked scanner/parser/AST/type checker/ES2015 emitter now carry source-mode
`/`, `%`, `/=`, `%=` with left-associative product precedence, comments
unaffected, correct numeric result types and existing const/import safeguards.
The four-module Node execution and 24-case frozen Go semantic diagnostic/exit
comparison are wired into strict gates, but are NOT_RUN at source creation.
Original TS7 accepted 0, 15,523 SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED.

## 2026-10-29 — Numeric bitwise and shift expression family (source candidate; NOT_RUN)

Scanner/parser/typechecker/ES2015 emitter now have bounded numeric & | ^ ~ << >> >>>,
with checked precedence, fixed-Go differential and cross-module runtime lanes.
Both validations are NOT_RUN at source creation; original upstream accepted
cases remain 0, and 15,523 configuration targets remain SKIP_UNIMPLEMENTED.
See [numeric bitwise package](progress/numeric-bitwise.md).

## 2026-10-28 — Compound Boolean guard flow (source candidate; NOT_RUN)

The checker now evaluates short-circuit RHS expressions under their true/false
LHS type views, combines same-variable compound `typeof` facts, recognizes
negation and carries guarded types into `if` and `?:`. It reuses the emitted
ES2015 short-circuit syntax. Dedicated 18-case fixed-Go and four-module runtime
evidence are wired into existing gates, **NOT_RUN at source creation**.
Original TS7 PASS remains 0; 15,523 configuration targets stay SKIP_UNIMPLEMENTED.
See [bounded work package](progress/compound-guards.md).


## 2026-10-28 — Conditional value expressions (single integration candidate; NOT_RUN)

The existing scanner/parser/AST/checker/emitter now carry right-associative
`condition ? whenTrue : whenFalse` below `||`, with required boolean/any
condition, established primitive/structural union joins and branch-local
discriminant/typeof narrowing restored after each expression. Contextual
literal checks inspect both arms, and ES2015 output preserves lazy evaluation.
The 23-source fixed-Go differential and four-module Node execution are wired
into the strict gates but **NOT_RUN at source creation**. Unsupported numeric
truthiness, expression assignment and full CFG are not claimed.
Original TS7 PASS 0, SKIP_UNIMPLEMENTED 15,523, scope UNEXPANDED.
See [conditional values](progress/conditional-values.md).

## 2026-10-28 — Primitive typeof narrowing (source candidate; NOT_RUN)

The scanner, unary AST/parser, semantic checker, existing discriminant/guard
flow and ES2015 emitter now carry `typeof` runtime expressions and
`===`/`!==` narrowing over bounded primitive/literal unions, including
reversed comparisons, early-return guards and imported type aliases.
Unknown, object/function and mixed unions are not guessed as narrowed.
The 18-source fixed-Go differential and real four-module Node runtime are
wired into existing strict gates; **at creation NOT_RUN**, not a pass.
No original TS7 configuration acceptance, upstream pin or release gate changed;
0 original PASS and 15,523 SKIP_UNIMPLEMENTED remain. Full scope UNEXPANDED.
See [the work package](progress/typeof-primitive-narrowing.md).

## 2026-10-28 — Numeric compound mutation and do/while (source candidate, NOT_RUN)

One bounded product package introduces braced `do/while` with break/continue,
numeric `+=`/`-=`/`*=` mutations of identifier/property/array element and
postfix `--` beside existing `++`. It reuses the checked scopes, structured
control-flow exits and ES2015 output, with classic `for` update support. The
first do condition is checked conservatively against entry-state initialization
to prevent unsafe writes hidden behind `continue`. Runtime and 14 fixed-Go
diagnostic cases are wired into existing gates but **NOT_RUN at source creation**.
No TS7 original-case acceptance is added; 15,523 targets remain
SKIP_UNIMPLEMENTED and all unexpanded domains are preserved.
See [bounded package](progress/numeric-loop-mutation.md).

## 2026-10-28 — Bounded while/continue and general break (integration candidate)

A single candidate extends the existing compiler pipeline with braced while
loops, continue in for..of/while (including a nested switch), and arbitrary
position break in braced branches. The common flow exits now carry distinct
Next/Return/Break/Continue and consume loop/switch exits at their proper
boundaries. Parser/checker/ES2015 emitter and 14 stable Go comparison sources
are changed together. Exact-head CI/Go/runtime are NOT_RUN at candidate
creation; no new original TS7 PASS claimed. See
[bounded work package](progress/loop-control.md).

## 2026-10-28 — Switch completion and loop break (unmerged candidate)

The same AST/checker/emitter now models a bounded exit set (normal completion,
return, break) for braced switch arms and for..of loop bodies. Switch case
fallthrough joins the previous arm's discriminated-union type with the next
case's direct-entry types; a break exits the nearest construct, not the
function. The release gate and original upstream acceptance ledger are
unchanged. Hosted build, JS runtime and pinned-Go differential are NOT_RUN
at candidate creation. Full break/continue/CFG support is not claimed.
See [switch-completion](progress/switch-completion.md).

## Returning discriminant-switch arms — integration candidate

AST/parser/checker/control-flow/ES2015 emitter now share a bounded switch
statement for braced literal case arms that return on all paths. Cross-module
runtime and fixed-Go semantic cases are wired into normal CI but NOT_RUN at
candidate creation. Break, fallthrough, dynamic labels and full CFG remain
unsupported. Original TS7 acceptance/SKIP numbers are unchanged.
See [switch-flow](progress/switch-flow.md).

## Returning-guard control-flow types — candidate, not yet accepted

The checker now carries a discriminant type to following statements only
when exactly one arm completes normally (early-return guard clauses). It
restores lexical types at the end of each block, even on errors; consecutive
guards narrow three-way unions before final member reads. The checked AST and
ES2015 emitter are unchanged.

Real source tests, Node ESM smoke and pinned-Go code/exit comparison are in
`DiscriminantGuardFlowTests.cs`, `tools/verify-guard-emit.ts` and
`fixtures/guard-flow/semantic-cases.json`. These are **NOT_RUN** at candidate
creation. Compound predicates, arbitrary writes, loops and full reachability
are not claimed. Original TS7 acceptance remains 0, with 15,523 skips.


## Discriminated union branch narrowing — integration candidate (NOT_RUN)

The initial fixed-Go run revealed a separate heterogeneous raw-object-array
widening mismatch (11/12); original mismatch evidence is preserved. The new
candidate follows pinned array inference and retains the unchanged failing
source as a TS2322 negative; a positive annotated-value array/for..of input
has been added (13 total bounded profiles). New-head CI/reference is NOT_RUN.
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

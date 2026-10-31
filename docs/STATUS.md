## 2026-10-31 — Guarded optional-member deletion (candidate; NOT_RUN)

Finite `delete box?.value`, `delete box?.["value"]` and parenthesized
variants now support named object receivers with explicit nullish alternatives.
The generated ES2015 code evaluates the receiver once and returns true on
nullish input; otherwise it removes the actual member. Required/readonly
members, dynamic keys, arrays, nested/effectful receivers and continuation
chains remain rejected. Five new immutable-ID positive pinned-Go references,
C# source checks, four-module Node runtime and no-emit negatives are registered
but NOT_RUN at source creation. No original TS7 acceptance credit is claimed;
the 15,523 registered original cases remain SKIP_UNIMPLEMENTED and dynamic
scope remains UNEXPANDED.

## 2026-10-31 — Bounded optional-member delete (source candidate; NOT_RUN)

The C# scanner, parser, structural checker and ES2015 emitter support
native `delete obj.optional` and statically named `delete obj["optional"]`
for mutable optional members on a named finite object. A successful operation
has checked boolean type and preserves JavaScript runtime property removal.
Required/readonly fields, dynamic indices, array holes, effectful/nested
receivers and optional-chain deletes remain explicitly unsupported or errors.
New source-backed positive/negative regressions, four-module executed ES2015
and six independent pinned-Go code/exit cases are registered as NOT_RUN
at source creation. Original TS7 remains 0 PASS / 15,523
SKIP_UNIMPLEMENTED, dynamic UNEXPANDED; no accepted upstream credit,
pin or release gate changed.

## 2026-10-31 — Checked undefined-sentinel guard flow (candidate; NOT_RUN)

Finite nullable identifier unions may be narrowed by strict/loose comparisons
against unshadowed intrinsic `undefined` and side-effect-free `void 0`.
Strict comparisons remove only `undefined`; loose comparisons remove both
`null` and `undefined`. Reverse comparisons, early returns, short-circuit
conditions and ternary flow reuse the existing C# TypeChecker branch refinement.
A lexical binding named `undefined` prevents this sentinel inference.
Assignments and loop writes must still invalidate narrowed reads.
Source-backed xUnit cases, four-module ES2015 execution, no-emit errors and
11 immutable bounded pinned-Go code/exit comparisons were added. At candidate
creation they are NOT_RUN, not accepted. Full original TS7 stays
0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED.

## 2026-10-31 — Explicit null values and nullish structural access (candidate; NOT_RUN)

The C# source scanner/parser and semantic type system now represent explicit
`null` expressions and annotations instead of rejecting them. Finite
`T | null` values flow through aliases, functions, objects and modules.
Optional member/index/call reads, postfix `!` and `??` distinguish null
from undefined while preserving checked present-path types. ES2015 output
emits native `null`, with the existing one-evaluation short-circuit guards.
Source-backed C# tests, four-module Node output and a separate 18-ID fixed-Go
diagnostic/exit cohort are registered as NOT_RUN at source creation, not
original TS7 acceptance. Complete original scope remains 0 PASS / 15,523
SKIP_UNIMPLEMENTED and dynamic UNEXPANDED. Null equality flow and complete
strictNullChecks option semantics remain unsupported.

## 2026-10-31 — Checked unary `void` expression (candidate; NOT_RUN)

C# Parser, TypeChecker and ES2015 emitter now support bounded unary `void`
with checked operands, normal unary precedence and exactly-one evaluation.
The operator returns semantic `undefined`, which may satisfy `void` annotations
but cannot silently become a number/string. Source-backed xUnit positive and
negative programs, four-module emitted Node execution and 15 pinned-Go
source/code/exit cases are registered; at source creation they are **NOT_RUN**.
Full original TS7 remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic
UNEXPANDED. No change to releases, thresholds or upstream pin.

## 2026-10-31 — Finite guarded optional-chain continuations (candidate; NOT_RUN)

The C# parser, checker and ES2015 emitter now share present-path types
across required property, static/numeric index and callable continuations
after an optional member/index segment. The guarded emitter evaluates the
nullish receiver once and skips all remaining indices, calls and arguments
when it is absent. Direct method continuations retain JavaScript receiver
binding. Nullable intermediate data members are *not* silently narrowed:
they must use a separately supported optional segment. Parentheses start a
distinct chain. Continuations through postfix non-null assertions or the
result of an optional function call remain explicitly unsupported.

Existing bounded optional-access cases are unchanged; twelve additional
stable source/code/exit reference cases, xUnit regressions and four-module
Node execution are registered. At source creation all new checks are
NOT_RUN, not complete upstream acceptance. Original TS7 remains
0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic UNEXPANDED; no release
threshold, protection, pin or test minimum was changed.

## 2026-10-31 — Guarded optional function and member calls (source candidate; NOT_RUN)

C# Parser, AST, TypeChecker and ES2015 emitter now check finite `fn?.(args)`,
`box.optionalMethod?.(args)` and static bracket-member calls. Optional
function values retain typed arguments and return `T | undefined` when their
callee can be missing; runtime lowering evaluates receivers/callees exactly
once, delays arguments until the function exists, and preserves member-call
`this` through `Reflect.apply`. Multi-module Node runtime and
negative no-emit cases, source-backed xUnit cases, and ten pinned-Go bounded
source/code/exit comparisons are wired into existing gates. At source creation
all checks are NOT_RUN, not full original TS7 acceptance. Compound short-circuit
chains (`obj?.method?.()`), optional-chain continuations, arbitrary `null`
source syntax, untyped `this` source expressions, and general flow narrowing
remain unimplemented and fail closed. Original TS7 acceptance stays
0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic UNEXPANDED; no release gate,
pin, protection or minimum-count change.

## 2026-10-31 — Finite optional member/index reads (source candidate; NOT_RUN)

C# scanner/parser, AST, TypeChecker, structural unions and ES2015 emitter now
support guarded `receiver?.member` and `receiver?.[index]` reads over
bounded object, string and array types. The result includes `undefined`
only where the receiver might be absent. ES2015 output evaluates receivers
once and indices lazily on the present path. Unparenthesized ordinary
property/index/call continuations after an optional segment are explicitly
rejected until their short-circuit semantics can be lowered faithfully; they
must never compile to JS that throws on an absent receiver. Optional calls,
optional-chain writes, dynamic index signatures and complete `null`/flow
semantics remain unimplemented. Source-backed xUnit regressions, four-module actual Node runtime,
fail-closed no-emit tests and fourteen stable pinned-Go source/code/exit cases
are registered. At source creation all evidence is NOT_RUN, not accepted.
Original TS7 acceptance remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED and
dynamic scope UNEXPANDED. No releases, pins, thresholds or protections changed.

## 2026-10-31 — Explicit postfix non-null assertions (source candidate; NOT_RUN)

New C# AST/parser/checker and ES2015 erasure support `value!`, `box.optional!.member`,
`box["key"]!` and `box.optionalMethod!(args)` using the already checked
optional object/interface data and function types. The postfix `!` strips
explicit `undefined` union alternatives and never emits a runtime guard; a
wrong assertion can still throw at runtime. Scoping, mutable-loop write scans,
and emitter name capture traverse the new AST. Source-backed C# positive and
negative regression tests, four-module real Node output and fourteen pinned
Go source/code/exit cases are registered, but at source creation are NOT_RUN.
This is not optional chaining or general nullability support. Full original
TS7 acceptance remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope
remains UNEXPANDED. No release threshold, pin or protection changed.

## 2026-10-31 — Typed nullish fallback lowering (source candidate; NOT_RUN)

The existing C# scanner, parser, checker and emitter now handle bounded `??`
for optional object members, union values, chained fallbacks and callable
values. The left expression is evaluated once and the right only if it is
undefined/null at runtime; ES2015 output lowers the syntax without requiring
ES2020. Unparenthesized mixing with `&&`/`||` is rejected. New source-backed
xUnit positives/negatives, four-module Node output and ten pinned-Go
code/exit cases are wired into the existing gates; **NOT_RUN at source
creation**, with no release acceptance credit. Optional chaining and complete
nullable flow analysis remain unsupported. Original TS7 remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED; dynamically expanded scope is UNEXPANDED.

## 2026-10-31 — Optional object/interface method contracts (source candidate; NOT_RUN)

Finite `run?(n: T): R` signatures reuse method-type parsing, optional
structural-member metadata, function variance and checked interface inheritance.
Omitted methods satisfy optional shapes; present methods retain checked parameter
and return types. Reads stay function-or-undefined and unsafe direct calls fail
closed. Type aliases, `keyof`, indexed access, module imports and native ES2015
values share the existing compiler. Required overrides of optional inherited
methods remain checked. Source-backed xUnit cases, four-module emitted Node
execution and nine real no-emit errors accompany twelve new bounded Go
source/code/exit references in the 28-case frozen object-method cohort.
At source creation results are NOT_RUN, not original TS7 acceptance:
0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED.
General optional-call narrowing, optional chaining, generics, overload merging
and complete `this` semantics remain unsupported.

## 2026-10-30 — Finite optional object data properties (source candidate; NOT_RUN)

Parser, structural TypeResolver/TypeChecker, interface heritage, `keyof`/indexed
access and ES2015 type erasure now model optional data members without silently
treating missing keys as present. Reads retain `T | undefined`, writes check
`T | undefined`, fresh excess checks and required-member diagnostics remain
active. Conditional object spread overlap and optional methods remain unsupported.
Source-backed C# positive/negative cases, four-module emitted Node execution and
real error/no-emit checks are separate from full original TS7 acceptance.
At candidate creation all new tests are NOT_RUN; full original scope remains
0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED.

## 2026-10-30 — Checked native ES2015 object literal methods (candidate; NOT_RUN)

C# Parser accepts explicitly typed object literal method parameters and optional
return annotations, checking bodies using the existing lexical function value,
structural method contract, and callable-member pipeline. JavaScriptEmitter
writes native ES2015 method shorthand (not `key: function`), preserving its
non-constructible method identity. Positives cover default/rest parameters,
nested objects, callbacks and cross-module interface use; negatives reject
wrong returns, arguments, arity and missing names without emitting JavaScript.
A separate 14-source fixed pinned-Go code/exit cohort is registered without
altering the 34 existing function-expression references. At source creation
new tests are NOT_RUN, not accepted. Original upstream TS7 remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED, with dynamic scope UNEXPANDED. No pin, release
gate, protection or minimum-count changes.

## 2026-10-30 — Checked direct member and callable-expression invocation (candidate; NOT_RUN)

C# Parser, TypeChecker and JavaScriptEmitter now carry a checked callee
expression rather than restricting calls to identifier tokens. This enables
direct `obj.method(args)`, static bracket member calls, chained receivers,
parenthesized function expressions and returned callables, using existing
function-valued structural members, lexical bindings and optional/default/rest
argument checks. Positive/negative C# regressions, four-module emitted JS
execution and nine additive fixed pinned-Go source/code/exit cases are
registered. At candidate creation these are NOT_RUN: registration and source
push are not PASS. The 25 earlier function-type reference IDs and expectations
are preserved. Full original TS7 stays 0 PASS / 15,523 SKIP_UNIMPLEMENTED,
dynamic UNEXPANDED. No release gate, pin, minimum test count or protection
change.

## 2026-10-30 — Required callable object/interface method signatures (candidate; NOT_RUN)

Parser now accepts bounded `method(name: Type): Result` signatures within
required-member object types and interfaces, lowering them into the existing
FunctionTypeSyntax. TypeResolver, structural checker, interface inheritance,
type aliases, imports and emitted JavaScript reuse their current checked
function-property pipeline. Optional trailing and final array rest method
parameters are supported, but optional methods, generic method parameters,
overload merging, method bodies, readonly method modifiers and special `this`
semantics remain unsupported. Internal C# positives/negatives, four-module
Node output and nine additive fixed pinned-Go source/code/exit comparisons
are registered; at candidate creation these are NOT_RUN. Frozen original
TS7 acceptance stays 0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic UNEXPANDED.
No release gate or upstream pin changes.

## 2026-10-30 — Lexically scoped named function expressions (candidate; NOT_RUN)

The C# function-expression parser preserves an optional private self name.
Binder builds a read-only self signature before checking parameters and the
body, supporting explicitly annotated named recursion, shadowing, callbacks,
defaults and array rest through the actual TypeChecker and ES2015 emitter.
Named inferred returns, generators, `this` and untyped parameters still fail
closed. Positive/negative C# regressions, four-module Node output, twelve
no-emit errors and twelve additive pinned-Go source/code/exit IDs are wired.
At source creation verification is NOT_RUN, not PASS. Original TS7 acceptance
remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED with dynamic scope UNEXPANDED.

## 2026-10-30 — Typed anonymous function expressions (candidate; NOT_RUN)

C# Parser and ES2015 emitter now admit anonymous `function(n: T): R { ... }`
values using existing typed parameter, local lexical binding, inferred or
annotated return, checked callback signatures, default/optional/array-rest
parameter and module pipelines. Named function expressions, generators,
untyped parameters and `this` remain unsupported. C# positive/negative
regressions, a four-module Node runtime and nine invalid no-emit programs,
plus twelve pinned-Go independent source/code/exit references are registered
as NOT_RUN at source creation. Original TS7 acceptance remains
0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic UNEXPANDED, release gate unchanged.
See [typed-function-expressions](progress/typed-function-expressions.md).

## 2026-10-30 — Typed arrow array-rest support (candidate; NOT_RUN)

Parser, Binder scopes, TypeChecker and ES2015 emitter now support final
`...items: T[]` parameters in typed arrow expressions and braced bodies.
The array is usable for indexed access, length and for..of loops; arguments
are checked against the element type, including through callback signatures
and cross-module imports. Non-array, optional, initialized and non-final
rest parameters diagnose without JS emission. A four-module runtime probe,
16 real C# positives/negatives, and 16 new pinned-Go reference cases are
registered; existing 48 frozen source/expected-code cases remain unchanged.
The new array-aware reference profile is pinned per added source and hashed
in raw evidence. At source creation checks are NOT_RUN, and no complete
original TS7 case has been accepted: 0 PASS / 15,523 SKIP_UNIMPLEMENTED,
dynamic scope UNEXPANDED.

## 2026-10-30 — Checked typed arrow optional/default parameters (candidate; NOT_RUN)

The C# typed-arrow parser, checker and ES2015 emitter share the named-function
trailing optional/default subset. Default initializers are checked against their
annotations before declaring the parameter and may read earlier parameters or
lexical captures. Function signatures retain the true minimum call arity.
Optional typed parameters are accepted in trailing positions; required parameters
after defaults remain explicitly unsupported. Untyped parameters, parameter
rest/spread in arrows, general undefined-aware optional-body flow and nontrailing
defaults are not claimed. Positive/negative xUnit cases, cross-module checked
output executed by Node and 12 additive pinned TS7 Go code/exit comparisons are
wired to existing gates; evidence at source creation is NOT_RUN. No changes to
release thresholds, pin or original acceptance. Original TS7 remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED and dynamic scope UNEXPANDED.

## 2026-10-30 — Inferred braced arrow returns (candidate; NOT_RUN)

The existing C# checked braced typed-arrow path now accepts omitted return
annotations. Real lexical block checking collects return types through local
bindings and guarded branches; a closed union is inferred for compatible
returns, void for no-return bodies, and never for throw-only bodies. Inferred
value-returning bodies must definitely return on all paths; mixed bare/value
returns are rejected rather than inventing an unsafe undefined type. Nested
arrows isolate their own collectors and explicitly typed arrows retain their
old validation. Four-module ES2015 runtime, genuine error/no-emit cases, C#
regressions, and twelve additive fixed-Go source/code/exit cases are registered.
**At candidate creation all new checks are NOT_RUN.** Original TS7 accepted
cases remain 0 of the registered 15,523 SKIP_UNIMPLEMENTED; full dynamic scope
UNEXPANDED and no release claimed.

## 2026-10-30 — Typed expression-bodied arrows (candidate; NOT_RUN)

Parser, Binder scopes, TypeChecker callback contracts and ES2015 emitter now
accept parenthesized typed arrow parameters with expression bodies, optional
return annotations, lexical reads/captures, nested arrows and cross-module
typed values. Untyped parameters, braced bodies, async/generics and callable
expression targets remain explicitly unsupported. The previous negative
"const lambda=(n:number)=>n" is now implemented; an untyped lambda remains
unsupported. Twelve fixed pinned-Go source/code/exit cases, four-module
Node runtime and seven negative no-emit checks are wired to the normal gates.
At source creation these are NOT_RUN. Original TS7 remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED. No release claim.

## 2026-10-30 — Finite structural object intersections (source candidate; NOT_RUN)

The bounded C# type grammar now accepts `A & B` between finite required
structural objects with union-lower precedence, merging disjoint fields,
equivalent repeated members, literal/primitive refinements, numeric key identity
and readonly metadata. The result uses the existing object checker,
`keyof`, indexed access, imported types and ES2015 type erasure. Incompatible
or non-object operands explicitly fail closed; general intersection normalization,
generic constraints, index signatures and conflicting numeric/string key aliases
are unimplemented. Eight source-backed positive and four TypeScript-diagnostic
negative pinned-Go cases are registered, plus four-module Node execution and
eight rejection/no-emit tests. This source candidate is NOT_RUN until exact-head
hosted gates complete. **Original TS7 acceptance remains 0 PASS / 15,523
SKIP_UNIMPLEMENTED; dynamic full scope UNEXPANDED.**

## 2026-10-30 — Checked structural indexed-access types (candidate; NOT_RUN)

Type annotations now resolve finite type-level `T["key"]`, `T[0]`,
`T[K]`, `T[keyof T]` and nested selections, including safe reads
through unions of statically known objects. Interfaces, aliases, imported
types and arrays of selected member types use the same checked semantic types.
Type selections erase during ES2015 output; general index signatures,
generic parameters, tuples and dynamic keys remain unsupported. The twelve
bounded pinned-Go cases, actual four-module JavaScript execution, and
negative no-emit validation are wired but NOT_RUN at source creation.
Original TS7 acceptance is still 0 PASS / 15,523 SKIP_UNIMPLEMENTED;
the dynamic denominator is UNEXPANDED.

## 2026-10-30 — Numeric and quoted-numeric keyof identity (candidate; NOT_RUN)

C# structural object types now preserve numeric source-token keys independently
of canonical JavaScript member lookup. Bounded `keyof` yields numeric literals
for `{0:T}`, string literals for `{"0":T}`, and intersects object unions by
both key name and numeric/string identity. Metadata is carried through aliases,
interfaces, inferred literals, spreads, and type-preserving contextual checking.
The existing 24 fixed-Go reference IDs/sources/expectations are unchanged;
12 additive source-bound cases and four-module Node execution are wired to
the existing strict gates. **Source creation is NOT_RUN; exact-head hosted
results are required before merge.** Original TS7 remains 0 PASS / 15,523
SKIP_UNIMPLEMENTED, dynamic cases UNEXPANDED, no release claim.

## 2026-10-30 — Common keys of structural object unions (candidate; NOT_RUN)

The bounded C# resolver now evaluates `keyof (A | B)` as the intersection of
property names across object/interface alternatives, producing literal unions
or `never` when no name is shared. Cross-module aliases and callable signatures
reuse the checked semantic types. Mixed primitive/object unions, numeric-looking
keys, arrays, index signatures and full TypeScript reflection remain unsupported.
The 12 existing fixed-Go source/code/exit IDs are unchanged; 12 additive cases,
C# regressions, four-module ES2015 execution and negative no-emit checks are
wired but NOT_RUN at source creation. Original TS7 acceptance remains 0 PASS /
15,523 registered SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED.

## 2026-10-30 — Bounded structural `keyof` (source candidate; NOT_RUN)

The actual parser and type resolver now produce finite string-literal key unions
from statically known object/interface shapes (including `keyof {}` = never),
nested aliases and imported type bindings. The value identifier `keyof` stays
legal. Numeric-looking keys, array/union operands, index signatures, symbols
and general dynamic indexing are explicitly unsupported rather than widened.
The four-module ES2015 execution and twelve stable-ID pinned-Go reference
comparisons are wired but NOT_RUN at source creation. Full original TS7
acceptance remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic UNEXPANDED.

## 2026-10-29 — Checked lexical `for..in` enumeration (source candidate; NOT_RUN)

One bounded C# core feature: braced `for(const/let key in object)` over known
object/array or any source types, typed string iteration keys, lexical shadowing,
break/continue, conservative loop writes and native ES2015 output. A four-module
Node execution, fail-closed negatives and 12 new immutable pinned Go references
are wired to normal CI. Source creation alone is NOT_RUN. Full original TS7
remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED.
General index signatures, `var`/assignment targets and unbraced loops remain
unsupported.

## 2026-10-29 — Checked property-existence `in` expressions (candidate; NOT_RUN)

C# Scanner, Parser, TypeChecker, semantic types and ES2015 emitter now carry
`key in object` at relational precedence. Typed string/number/any keys,
objects, arrays and any receivers are supported. Invalid operand kinds fail
with the pinned TS7 TS2322 assignment diagnostic; missing properties return a boolean rather than a
lookup error. No in-guard narrowing, index signatures or symbol keys are
claimed. Four-module Node runtime and 12 pinned-Go code/exit cases are wired
but NOT_RUN at source creation. Original TS7 remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED.

## 2026-10-29 — Literal-string object bracket access (source candidate; NOT_RUN)

Checked structural objects support statically named bracket reads/writes
(`object["key"]`) with decoded string keys, readonly diagnostics, compound
and prefix/postfix updates. It reuses existing parser AST and ES2015 emitter;
missing members do not become implicit any. Dynamic index expressions and
general index signatures remain unsupported. Four-module emitted Node runtime,
real no-emit negatives and twelve pinned-Go code/exit sources are wired to CI.
This source candidate has NOT_RUN; exact-head hosted checks are necessary.
Original upstream acceptance remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED,
dynamic case scope UNEXPANDED.

## 2026-10-29 — Checked object shorthand members (candidate; NOT_RUN)

Object literal shorthand `{value}` now reuses the same lexical value binding,
structural object inference, module linker and ES2015 output as explicit
`{value:value}`. Missing shorthand bindings report TS18004 rather than
normal identifier TS2304; duplicate and fresh excess-property checks remain
active. Named imports, function parameters, ordered object spreads and nested
members share the same implementation. A four-module Node runtime, fail-closed
negative cases and twelve pinned-Go code/exit sources are wired to normal gates.
These are NOT_RUN at source creation; this is not original TS7 acceptance.
Original upstream ledger remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED;
dynamic case scope UNEXPANDED. Methods, computed names, accessors and general
destructuring remain unsupported.

## 2026-10-29 — Checked object literal spreads (source candidate; NOT_RUN)

Ordered `{...source, value: replacement}` copies statically checked object members
through the existing C# parser, checker and ES2015 writer. Later spreads and named
fields replace earlier member types; explicit excess-property checks remain
active while spread-source keys are not fresh explicit members. Non-object
spread operands fail closed. Four-module Node runtime, negative xUnit cases and
12 bounded pinned Go source comparisons are wired into strict gates. Local
execution is NOT_RUN at source creation; exact-head hosted checks are required.
Generic iterable/array spreads, getters, computed keys and descriptor semantics
remain unsupported. Original TS7: 0 PASS / 15,523 SKIP_UNIMPLEMENTED,
dynamic scope UNEXPANDED.

## 2026-10-29 — Checked array literal spreads (candidate; validation pending)

The C# parser, semantic checker and ES2015 writer accept array literals with
multiple ordered `...array` elements, including heterogeneous element unions,
object arrays, nested arrays and cross-module values. Each operand must be an
actual statically checked array; arbitrary iterables, tuple spreads and array
holes remain unsupported. Source-bound unit tests and four-module Node runtime
are in this one package. A new immutable 12-case pinned Go differential cohort
retains raw hashes and independent reference/product results. At creation these
checks are NOT_RUN, not claimed as PASS; exact-head CI and Go reference must
pass before normal merge. Original upstream acceptance remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED with dynamic scope UNEXPANDED.

## 2026-10-29 — Trailing checked array spread at rest call sites (source candidate; NOT_RUN)

The C# parser, call AST, TypeChecker and ES2015 emitter now pass one trailing
statically typed array spread to a function with a checked array rest parameter.
All fixed parameters must be supplied explicitly. Each expanded element type
is checked against the rest element type, including cross-module named
structural elements; JS preserves native spread semantics. Interleaved/multiple
spreads, tuple expansion into fixed parameters, unknown iterables and general
spread expressions remain unsupported (never counted as implemented). A
four-module Node runtime, negative no-emit checks and 12 immutable source-ID
pinned-Go cases use a shared iterator-capable minimal ambient profile.
Execution is NOT_RUN at source creation; exact-head CI and Go oracle are
required. Original upstream TS7 acceptance remains 0 PASS / 15,523
SKIP_UNIMPLEMENTED, dynamic case scope UNEXPANDED.

## 2026-10-29 — Typed trailing default parameters (candidate; NOT_RUN)

The C# function declaration pipeline now carries trailing typed parameter default
expressions through Parser/AST, ordered TypeChecker initialization, signatures,
Binder and ES2015 JavaScript emission. Omission and explicit arguments use the
same callable signature across named modules. Checked defaults may reference
previous parameters; incorrect initializer types and invalid arity are rejected.
Non-trailing defaults and untyped/default rest/destructuring parameters remain
explicitly unsupported, not silently compiled. The bounded 17-source fixed-Go
oracle and four-module Node 24 runtime are wired to the existing CI lanes,
but are NOT_RUN at candidate creation. Internal tests and bounded comparison
are NOT original upstream acceptance: original 0 PASS, 15,523
SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED.

## 2026-10-29 — Optional typed function parameters (candidate; NOT_RUN)

This bounded compiler package supports trailing `name?: T` parameters with checked
minimum/maximum call arity, type checking for supplied arguments, cross-module
function signatures and erased ES2015 annotations. It does not implement
strict-null-aware parameter flow, defaults, rest, generic functions or a general
`undefined` type. Four-module runtime, five negative no-emit programs and twelve
new immutable pinned-Go comparisons are wired into CI; NOT_RUN at commit creation.
Original TS7 acceptance remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic
case scope UNEXPANDED.

## 2026-10-29 — Pinned TS7 catch-unknown semantic correction (candidate; NOT_RUN)

Initial PR #45's exact-head Go differential run 40806731638 failed for
five source-bound IDs. The reference showed TS2322 for unknown-to-number
assignments/returns, TS18046 for unknown property access and TS2304 for
the separate out-of-scope reference. Product incorrectly treated catch
as implicit any. This patch introduces a real bounded unknown type and
conservative type checking; safe rethrows remain legal.
Five oracle-backed expected diagnostic arrays have been corrected without
changing source text or stable IDs. The original failed raw artifact
12868538099 remains retained for comparison. Source regressions now
explicitly reject formerly unsafe unknown operations; four-module JS
execution uses valid recovery and eight no-emit negative cases.
Fixed-reference and exact-new-head CI are NOT_RUN at patch creation.
Original TS7 acceptance is 0 PASS / 15,523 SKIP_UNIMPLEMENTED and
dynamic scope remains UNEXPANDED.

## 2026-10-29 — Braced try/catch/finally with lexical error binding (source candidate; NOT_RUN)

The existing checked C# AST and compiler now handle braced try/catch, optional
single catch identifier or omitted binding, and optional finally. A catch
binding is local to that block and uses the pinned reference unknown type.
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

## 2026-11-02 — Common callable members across object unions (candidate; NOT_RUN)

When every finite object-union branch exposes a callable property with exactly
matching checked input types, required arity and rest contract, the compiler now
unifies those calls and joins their return types. Dot and literal-bracket member
calls reuse the existing Parser, Binder, TypeChecker, module graph and native
ES2015 emitter. Optional/missing properties, differing parameter contracts,
mixed any/typed parameters and native intrinsic overloads are **not**
silently merged. Sixteen stable bounded pinned-Go differential source/diagnostic
IDs, source-backed C# tests and four linked Node modules have been registered,
but are NOT_RUN pending exact-SHA hosted validation. Existing frozen cohorts and
the full original TS7 0 PASS / 15,523 SKIP_UNIMPLEMENTED,
dynamic UNEXPANDED, are unchanged. No source pin, release gate, test minimum
or protection settings changed.

## 2026-11-02 — Native ES2022 String.at with signed UTF-16 indexing (candidate; NOT_RUN)

The C# compiler now checks native `string.at(index:number): string | undefined`
for primitive, literal and string-family receivers through its existing member
resolver, checked calls and ES2015 emitter. Signed indices and out-of-range
results preserve native runtime behavior; astral characters are indexed as
UTF-16 code units (not Unicode code points), with no polyfill supplied.
The existing 24 pinned-Go string-method IDs, sources and expected diagnostics
are unchanged; eight positive and eight negative IDs extend the bounded cohort
to 40. Source-backed xUnit checks, four linked Node modules, rejected invalid
programs and malformed ambient contracts are registered but NOT_RUN pending
exact-head hosted verification. No original TS7 acceptance credit; historically
0 PASS / 15,523 SKIP_UNIMPLEMENTED with dynamic scope UNEXPANDED. No release
threshold, fixed upstream pin, branch protection or global test count changed.

## 2026-11-02 — Checked mixed array/string iterable unions (candidate; NOT_RUN)

The existing for..of checker now admits finite union-typed sources when every
alternative is a checked mutable/readonly array or string family. Binding types
are the union of all yielded elements; a nullable/unknown/noniterable alternative
rejects rather than silently broadening or emitting unsafe code. Existing native
ES2015 output and module linking are unchanged. Eight positive and eight negative
immutable bounded pinned-Go source/diagnostic IDs extend the 26-case array/string
iteration cohort to 42. Source-backed xUnit, four linked JavaScript modules,
invalid/no-emission and ambient validation checks are registered, NOT_RUN
until the exact-head CI executes. This does not implement general iterators,
downlevel iteration or original TS7 suite acceptance. Full original scope
remains historically 0 PASS / 15,523 SKIP_UNIMPLEMENTED with
dynamic UNEXPANDED; no pin, threshold, release gate or protection changed.

## 2026-11-02 — Checked Unicode string for..of (candidate; NOT_RUN)

Strings, literal strings and string-only unions now pass through the existing
for..of Binder, TypeChecker and JavaScriptEmitter. The element has type string;
native ES2015 iteration preserves Unicode code points. Twelve new source-backed
fixed-Go cases extend the unchanged fourteen-case array iteration cohort to
twenty-six, using a symmetric validated String iterator ambient only on the
new cases. Four linked JS files, illegal program no-emission, strict compiler
regressions and reference comparison are NOT_RUN until exact-head hosted CI.
Other iterables and downlevel output are not claimed. Original TS7 acceptance
remains historically 0 PASS / 15,523 SKIP_UNIMPLEMENTED with UNEXPANDED
dynamic scope. No original acceptance credit or release-gate changes.

## 2026-11-02 — Bounded ES2023 reverse array predicate callbacks

Native `findLast` and `findLastIndex` are checked on mutable and readonly
arrays through the existing type relations and callable checker. Their results
are `T | undefined` and `number`, respectively. Emitted JavaScript keeps
native right-to-left predicate evaluation without mutating the array. This
bounded contract requires an explicitly typed single-value boolean predicate;
it does not implement contextual inference, the optional `thisArg` or
unrestricted standard-library callback overloads.

Eight positive and eight negative stable differential IDs augment the unchanged
19-case array-callback cohort to 35. Source-backed C# tests, four linked native
ES2015 modules executed in Node, and invalid/no-JavaScript checks are attached;
exact-SHA workflow results establish execution, not this source statement.
Complete original TS7 acceptance remains historically 0 PASS /
15,523 SKIP_UNIMPLEMENTED with dynamic UNEXPANDED. No original acceptance
credit, upstream pin, global test threshold or release gate is changed.

## 2026-11-02 — Finite typed expression parameter patterns (candidate; NOT_RUN)

Finite object/array binding patterns in typed arrows, function expressions and
concise object methods lower through existing typed hidden positional arguments,
mutable entry locals, Binder, TypeChecker, modules and native ES2015 emission.
Expression-bodied arrows become checked return blocks while retaining lexical
capture and single-evaluation semantics; defaults are lazy. Incorrect property
reads, return types and arguments are rejected before output. Rest/computed
bindings, general iterable destructuring, untyped parameters, self references,
unsafe parameter-default ordering and shadowed parameter-default references
remain outside this bounded slice. Sixteen additive pinned-Go source/code/exit
IDs extend the frozen binding cohort from 40 to 56 unchanged old cases. C#
source-backed tests and four-module Node execution are registered; **NOT_RUN**
until exact-head validation. Original TS7 acceptance remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED. No upstream pin,
test minimum, release gate or protection changes.

## 2026-11-01 — Typed finite function parameter binding patterns (candidate; NOT_RUN)

Named function declarations lower finite typed object/array parameter patterns
through their existing signatures, checked property/index reads, scoped mutable
variables and native ES2015 emitter. Nested and defaulted selections are
checked, with no repeated argument evaluation. Rest/computed and expression
function parameter patterns remain unsupported. The bounded parser rejects
prior or later parameter defaults that would change binding order, as well as
duplicate names across parameter forms, rather than emitting incorrect JS.
Invalid constrained element defaults retain their primary type error without
propagating a rejected fallback type into secondary return diagnostics.
Parameter element defaults now reject body-shadowing references, while both
element and whole-parameter defaults reject references to their own pattern
bindings, avoiding incorrect initialization scope and TDZ behaviour. These
are bounded fail-closed restrictions, not full TS7 parameter initialization. Twelve new stable pinned-Go
source/diagnostic/exit cases extend the original 28-case binding cohort.
Source-backed xUnit, four linked Node modules and negative/no-emission cases
are registered, NOT_RUN until exact-head verification. Original upstream remains
0 PASS / 15,523 SKIP_UNIMPLEMENTED with dynamic UNEXPANDED.

## 2026-11-01 — Finite variable binding defaults (candidate, NOT_RUN)

Checked nested object and array variable binding defaults lower through the
existing Parser, scoped Binder, TypeChecker and native ES2015 emitter.
Each selection is read once; the unshadowable void-zero sentinel triggers a
lazy fallback only on undefined, including aliases and nested members.
Six positive/six negative new stable pinned-Go source/code/exit IDs extend
the existing 16-case binding cohort without changing the old cases.
Four linked runtime modules and source-backed C# tests are registered.
New checks are NOT_RUN until exact-head CI. Iterator-based arbitrary arrays,
rest/computed/parameter destructuring remain unsupported. Original upstream
acceptance is still 0 PASS / 15,523 SKIP_UNIMPLEMENTED with dynamic
UNEXPANDED; internal checks are not upstream credit.

## 2026-11-01 — Finite object and array variable binding patterns (candidate; NOT_RUN)

The Parser lowers checked `const`/`let` finite object and array binding patterns
into ordinary lexical variables, with a collision-avoiding per-source temporary
evaluating the initializer once. Nested receivers are bound once; renamed
object members, skipped array indices, nested object/array selections and
explicit source annotations reuse existing scope binding, TypeChecker,
ModuleLinker and ES2015 emitter. Direct cross-module **use** of destructured
values in functions is tested. General iterator semantics, defaults, rest,
computed binding keys, parameter/catch patterns and unrestricted direct
destructured exports are not claimed. Invalid reads/assignments must emit no JS.

Source-backed xUnit, a four-module Node runtime check and 16 stable frozen
pinned-Go source/code/exit cases (8 positive, 8 negative) have been registered.
**NOT_RUN** until exact-head CI; neither fixture count nor local cases count
as original upstream acceptance. Original TS7 remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED. No upstream pin,
global test minimum, release gate or branch protections changed.

## 2026-11-01 — Different accumulator types for native array folds (candidate; NOT_RUN)

Native `reduce` and `reduceRight` derive distinct accumulator U from an explicitly typed callback when a checked U initial value is present. Mutable and readonly T arrays use the ordinary function checker; no-initial-value folds keep the same-element T contract. Intrinsic erasure into ordinary function types is forbidden. Sixteen immutable bounded source/diagnostic IDs, source-backed xUnit and four-module Node runtime execution are registered; **NOT_RUN** until exact-head CI. This is not full generic overloads or contextual inference. Original TS7: 0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED.

## 2026-11-01 — Bounded native string transformations and searches (candidate; NOT_RUN)

The C# semantic String property resolver now provides seven non-mutating
methods for primitive and literal strings, including case conversion,
whitespace trimming, slicing, substring search, prefix and suffix checks.
The ordinary call checker enforces argument types, optional numeric positions
and string/boolean results, while four linked ES2015 modules exercise native
runtime semantics. A strict `String` ambient contract rejects duplicate and
unsupported member declarations. Twenty-four stable-ID pinned-Go comparison
cases (12 positive, 12 negative), source-backed xUnit checks and negative
no-JavaScript-emission scenarios are registered. These are candidate
NOT_RUN until the exact-head checks execute. Original TS7 remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED. No pin, CI threshold
or release gate has changed.

## 2026-11-01 — Native array map with checked output element types (candidate; NOT_RUN)

C# array property resolution now recognises native `map` on mutable and
readonly arrays. A finite intrinsic marker lets the ordinary call checker
validate explicitly typed callback input while deriving the fresh writable
result array's element type from its checked return, including `number[]`
to `string[]`, objects and chained calls. This is not general generics or
contextual callback inference. Twelve positive and eleven negative source-backed
checks, four linked ES2015 modules executed through Node and 20 stable pinned-Go
reference cases are registered as NOT_RUN pending exact-head CI. The
bounded String length/index ambient contract is shared by both reference
and product for string-to-number map cases; original raw Go TS2339 failures
at source IDs positive-03/09 were retained as evidence before repair. The complete
original TS7 scope remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope
UNEXPANDED. No pinned upstream, CI threshold, release gate or protection changed.

## 2026-11-01 — Checked in-place array methods (candidate, NOT_RUN)

The C# semantic member resolver and ordinary checked-call path support native
ES2015 `reverse`, `sort` with optional explicitly typed comparator, `fill`
and `copyWithin`. Results retain mutable element types, and JavaScript mutates
the existing receiver. Readonly arrays reject mutators. Thirteen positive and
thirteen negative source-backed pinned-Go cases use a separate ambient profile
and stable IDs: all old fixture bytes, expected codes and profiles remain intact.
Four linked executable ES2015 modules and invalid/no-output checks are
registered but NOT_RUN before exact-head CI. Original full TS7 acceptance:
0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED. No release
gate, source pin, test minimum or branch protection changed.

## 2026-11-01 — Checked non-mutating array queries (candidate; NOT_RUN)

Bounded mutable and readonly array values now expose native `includes`,
`indexOf`, `lastIndexOf` and `join` call contracts through the existing
C# semantic property resolver, TypeChecker and ES2015 emitter. Mandatory
element arguments, optional numeric start positions (including explicit
`undefined`), optional string separators and return types are checked;
readonly views remain non-mutating, and mutation methods remain unavailable.
Four linked executable Node modules, negative/no-emission checks and 25
source-backed pinned-Go comparison IDs are registered as NOT_RUN pending
exact-head CI. This is not original TS7 acceptance: 0 PASS / 15,523
SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED. No pin, release gate,
test minimum or branch protections changed.

## 2026-11-01 — Checked mutable array method calls (candidate; NOT_RUN)

Four native mutators (`push`, `pop`, `unshift`, `shift`) on finite mutable
array types reuse existing semantic property lookup, call argument checking,
module linking and ES2015 emitter. Readonly array types reject mutation;
wrong element types and arities are diagnosed. Source-backed xUnit, four
linked Node runtime modules, nine invalid/no-emission checks and 21 stable
pinned-Go source/code/exit comparisons are registered as NOT_RUN until exact-head
CI. Full original TS7 remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED and dynamic
UNEXPANDED. No pin, minimum, release gate or protections changed.

## 2026-11-01 — Bounded nested untagged template interpolation (candidate; NOT_RUN)

Nested untagged backtick values within `${expression}` now use the existing scanner,
parser, checked expression AST, type checker and native ES2015 emitter. Lexical
closing-brace detection skips nested templates, quoted braces and comments with
a finite depth cap (eight levels). Source-backed xUnit, four linked executable
ES2015 modules and 11 new pinned-Go source/code/exit cases extend the existing
26 template cases to 37; exact-head gates are NOT_RUN at source creation.
Tagged templates, template literal types and unbounded nesting remain unsupported.
Original TS7 acceptance remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic
scope UNEXPANDED. No upstream pin, release gate or thresholds changed.

## 2026-11-01 — Checked expression-bearing template strings (candidate; NOT_RUN)

The shared C# Scanner/Parser now retain native template chunks and parse each
${expression} using the existing checked expression AST with original UTF-16
positions. TypeChecker visits embedded identifiers, calls and member reads;
ES2015 emission preserves native interpolation, evaluated at runtime. Bounded
source-backed tests, four linked Node runtime modules and 13 new stable
pinned-Go code/exit comparisons extend the existing 13-case template cohort.
Nested tagged templates and template literal types remain explicitly unsupported.
At source creation new tests are NOT_RUN, not original TS7 acceptance.
Full original TS7 remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope
UNEXPANDED. No pin, gates, test minimum or protections changed.


## 2026-11-01 — Type-only reexports (source candidate; NOT_RUN)

Bounded local and transitive `export type { Name as Alias }` now reuse the
canonical module graph, preserving the original TypeBinding through Binder,
TypeResolver and checker. Type-only exports emit no runtime bindings.
Missing, duplicate and cyclic aliases fail closed. Source-backed xUnit,
five-module executed JS and 12 stable pinned-Go multi-file source/code/exit
cases are registered as NOT_RUN until exact-head CI.
Original TS7: 0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic UNEXPANDED.
No upstream pin, test minimum or release gate changes.

## 2026-11-01 — Intrinsic generic array type spellings (source candidate; NOT_RUN)

The bounded C# Parser maps one-argument intrinsic `Array<T>` and
`ReadonlyArray<T>` type annotations into the same checked array type model
as `T[]` and `readonly T[]`. Type aliases, functions, module imports,
nested arrays, writable indices and readonly rejection reuse the Binder,
TypeResolver, TypeChecker and ES2015 emitter. Only in type-argument contexts,
the parser splits leading `>` tokens from shift/assignment punctuation;
ordinary JS shift expressions remain unchanged. Shadowing named types
reject unsupported generic application instead of silently resolving a builtin.
No general generic declarations, mapped types, tuples or Array methods.

Source-backed xUnit checks, four actual ES2015 modules with error/no-emission
checks and 14 additive bounded pinned-Go IDs are registered (existing 13
preserved). Until exact-head validation these new checks are NOT_RUN. Original
full TS7 acceptance remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic
scope UNEXPANDED, and no pin/gate/threshold/protection changed.

## 2026-11-01 — Bounded readonly array annotations (candidate; NOT_RUN)

The checked C# type pipeline now distinguishes `readonly T[]` from mutable
`T[]` through aliases, checked array reads, length and for..of. Mutable arrays
may be viewed through readonly references; readonly references cannot flow
back into mutable array contracts. Index assignment, updates and length writes
are rejected on readonly arrays, while a nested mutable element remains writable.
Source-backed C# tests, four linked ES2015 runtime modules, negative no-JS cases
and 13 additive frozen pinned-Go source/code/exit cases are registered as NOT_RUN
until exact-head validation. The bounded ambient Array<T>.push signature
makes mutable and readonly arrays structurally distinct in the fixed Go
reference; it does not implement product array method calls. This does not
implement `ReadonlyArray<T>` expressions, tuples or mapped readonly types. Original TS7 acceptance remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED. No pin, release gate,
threshold or protection changes.

## 2026-11-01 — Checked radix numeric literals (candidate; NOT_RUN)

Hexadecimal, binary and octal safe integers with digit separators now reuse
scanner Number values, existing type identities, structural property keys and
ES2015 output. Source spans remain raw, emitted values are normalized decimal.
Malformed spelling and values above 2^53 - 1 fail closed. Source-backed C#
tests, four-module Node runtime and 15 immutable new IDs extend the existing
pinned-Go numeric-literals cohort (33 to 48), retaining prior expectations.
Exact-head checks are NOT_RUN until CI. Original TS7 acceptance remains
0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED.

## 2026-11-01 — Bounded no-substitution templates (candidate; NOT_RUN)

The scanner now recognizes complete untagged backtick strings without interpolation,
retaining checked literal/string types and native ES2015 output. Source-backed
xUnit, four linked Node modules and 13 frozen pinned-Go code/exit cases are
registered as NOT_RUN pending exact-head gates. Escaped backticks, literal
`${` and physical line breaks are supported; interpolation `${expression}`
remains explicitly unsupported and fails closed. Tagged call continuations,
including cross-newline tags, and backticks in quoted-only module/property positions
are rejected instead of emitting malformed JavaScript. The original pre-feature
negative literal syntax regression now tests interpolation, which remains
unimplemented; no regression was skipped or removed. Full original TS7 acceptance
remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED.

## 2026-11-01 — Single-statement loop bodies (source candidate; NOT_RUN until exact-head gates)

The shared C# Parser now admits one unbraced statement for while, do/while,
classic for, for..of and for..in, using the existing Binder, TypeChecker,
loop-backedge flow and native ES2015 emitter. Braces remain required for
lexical let/const declarations directly within loop bodies. The bounded package
registers 15 new pinned TS7 code/exit cases (10 positive, 5 negative), C#
source-backed tests and four-module executed JS with six no-emission invalid
checks. New checks are NOT_RUN at source creation; old cohorts retain their
IDs and expectations. Full original TS7 remains 0 PASS / 15,523
SKIP_UNIMPLEMENTED with dynamic expansion UNEXPANDED; no release gate, pin,
minimum or protection has been loosened.

## 2026-10-31 — Checked logical operand-value expressions (candidate; NOT_RUN)

C# TypeChecker now models bounded non-boolean \`&&\` and \`||\` expression
results as operand values. This preserves zero/empty-string/false/nullish
short-circuit alternatives, exact finite literal identities, and the RHS type
on the corresponding execution path; existing discriminant narrowing still
checks the RHS with safe branch facts. The native ES2015 emitter already
preserves lazy evaluation, so the package includes a four-module Node runtime
with side-effect counters, five invalid/no-JS programs, and fourteen additive
stable-ID pinned-Go source/code/exit comparisons. At source creation these
checks are NOT_RUN. \`void\`, \`unknown\` and other unbounded types remain explicit
non-implementation, not test success. Original TS7 remains 0 PASS /
15,523 SKIP_UNIMPLEMENTED, dynamic range UNEXPANDED, with no upstream pin,
release gate or verification threshold changes.

## 2026-10-31 — Runtime expression truthiness in control flow (source candidate; NOT_RUN)

C# TypeChecker now accepts checked numeric, string, member, optional-member,
call and arithmetic conditions in braced if, ternary, while, do/while and
classic for. Direct unary void expressions diagnose TS2873 (always falsy), while
void-typed function calls used as conditions diagnose TS1345. The existing stable symbol
guards still narrow; arbitrary property paths do not gain unsafe flow facts.
Ten positive and five negative immutable-ID pinned-Go comparisons, a four-module
ES2015 Node runtime, and seven no-JS invalid scenarios are registered as NOT_RUN
at candidate creation. The pinned Go reference on run 41199073081
revealed the exact TS2873 diagnostic for the five frozen unary-void inputs;
the candidate initially predicted TS1345 incorrectly. Their stable IDs and
source hashes were retained, product diagnostics were corrected, and the
explicit expected diagnostic was reconciled with the independently observed
reference rather than relaxing any negative assertion. Original TS7 acceptance
remains 0 PASS / 15,523
SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED. Pin, release gate, CI
threshold and protection are unchanged.

## 2026-10-31 — Bounded truthiness identifier guards (source candidate; NOT_RUN)

The shared C# TypeChecker accepts runtime truthiness of checked identifier
values in if/else and ternary conditions, including `!value`. Finite union
alternatives are narrowed conservatively: null/undefined and falsy literals
fall out of the true arm, structural objects/arrays/functions are truthy, and
broad numbers/strings and empty structural `{}` types stay in the
false arm because 0 and empty strings are possible. Runtime-valued
`void` constituents remain in both arms rather than being guessed falsy. Assignment invalidation and early-return flow reuse existing symbol
tracking. General truthy expression guards and non-boolean logical binary
result typing remain unsupported. Seventeen bounded pinned-Go cases
(12 positive, 5 negative), a four-module executed-JS lane, and unit
regressions are registered as NOT_RUN pending exact-head CI. This is not
original upstream acceptance: the 15,523 registered original cases remain
SKIP_UNIMPLEMENTED, dynamic scope UNEXPANDED. No release gate or pin change.

## 2026-10-31 — Optional-member property-existence flow (source candidate; NOT_RUN)

A bounded literal `"value" in receiver` guard now partitions finite structural
unions containing optional properties. Optional alternatives survive in BOTH
branches; a required member survives only when present and a missing member
only when absent. Dynamic keys, broad/unknown receivers and unsafe reads of
other union members remain rejected. The existing checker flow handles inverse
guards, returns, ternaries, short-circuit conditions and assignment invalidation.
Five additive positive and two negative pinned-Go code/exit cases, source-backed
C# regressions, and four linked ES2015 runtime modules are registered as
NOT_RUN before exact-head CI. The first 20 bounded `in` IDs remain intact;
no original TS7 acceptance credit, reference pin or release gate changes.

## 2026-10-31 — Finite required-member `in` guard flow (source candidate; NOT_RUN)

The shared C# TypeChecker now partitions finite structural unions on literal
property-existence guards such as `"value" in item`, including negation,
short-circuit RHS checking, inverse/early-return flow and assignment invalidation.
Only named identifier receivers and required members with distinct presence are
classified; dynamic keys, optional members and arbitrary shapes keep their
unrefined types. Five source-backed positives, negative diagnostic guards, four
linked ES2015 Node runtime modules and eight additive fixed-Go code/exit cases
are registered, pending exact-head CI. The prior twelve in-operator reference IDs
remain unchanged. **Not** original TS7 acceptance: 0 PASS / 15,523
SKIP_UNIMPLEMENTED; dynamic domains UNEXPANDED.

## 2026-10-31 — Checked typeof function/undefined/null guards (candidate; NOT_RUN)

The C# TypeChecker now partitions finite primitive, callable and explicit nullish
unions by JavaScript runtime typeof tags. Callable signatures can be invoked
after a function guard, undefined is distinguished from other primitives, and
null follows the runtime object tag. Inverse and early-return guards reuse
existing flow and rebinding invalidation. Structural objects, any, unknown and
void remain deliberately unclassified. Source-backed C# tests, four-module
ES2015 Node execution, and 11 additional stable pinned-Go cases are registered;
new verification remains NOT_RUN pending exact-head CI. The original 18 oracle
IDs remain, and TS7 original acceptance is still 0 PASS / 15,523
SKIP_UNIMPLEMENTED with dynamic scope UNEXPANDED.

## 2026-10-31 — Checked numeric, string and object truthiness in logical assignment (source candidate; NOT_RUN)

The C# TypeChecker now accepts `&&=` and `||=` on bounded numeric, string,
nullable and finite object lvalues, including typed members and array indices,
instead of incorrectly requiring a boolean target. RHS writes remain checked
against the declared type, retaining readonly and uninitialized errors.
Existing ES2015 output keeps JS truthiness, lazy RHS and once-only lvalue
receiver/index capture. This package registers 13 source-backed xUnit positives,
corrects former unsupported-only negative expectations, adds seven executed JS
scenarios to the same four modules, and 16 additive bounded pinned-Go IDs.
At source creation these new checks are NOT_RUN. Existing 22 reference IDs stay
unchanged. Full TS7 original acceptance remains 0 PASS / 15,523
SKIP_UNIMPLEMENTED; dynamic case expansion remains UNEXPANDED.

## 2026-10-31 — Checked logical assignments through object properties and indices (candidate; NOT_RUN)

The C# type-checker now accepts bounded logical `&&=`, `||=` and `??=`
writes to mutable finite object members and indexed array/object elements,
including typed factory results and nested receivers. ES2015 lowering captures
both receiver and computed key once and the original value for `??=`, while
keeping RHS evaluation lazy. Required type and readonly checks remain active;
writes through optional chains, union-typed receivers and unrestricted JS
truthiness remain explicitly unsupported.

Source-backed xUnit positives/negatives, four linked ES2015 Node modules,
once-only side-effect probes, nine no-JS invalid cases and ten additive pinned
Go source/code/exit comparison IDs are registered as NOT_RUN pending exact-head
CI. Existing twelve oracle IDs remain unchanged. Complete original TS7 stays
0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED, no release
acceptance credit, gate, pin or test minimum change.

## 2026-10-31 — Bounded logical assignment operators (candidate; NOT_RUN)

The shared scanner/parser/checker accepts short-circuit `&&=`, `||=`,
and `??=` on initialized mutable identifier lvalues. Checked boolean
`&&=`/`||=` and nullable `??=` preserve lazy RHS evaluation and
lower into ES2015 without emitting native logical assignment syntax.
Rebinding invalidates stale flow facts; const, imports and reads before
initialization fail closed. Member/index logical assignment and
general JavaScript truthiness are not yet implemented.
Four-module runtime execution, negative no-JS programs and 12
bounded pinned-Go diagnostic/exit cases are registered, NOT_RUN before CI.
Original TS7 remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED,
dynamic scope UNEXPANDED. Internal regressions do not count as upstream
acceptance; no release gate, pin or test-count minimum was changed.

## 2026-10-31 — Checked nested and effectful optional deletion (candidate; NOT_RUN)

Finite optional-member deletion now accepts type-checked nested object receivers,
array elements and typed function results, including nullable nested receivers.
Native or guarded ES2015 deletion removes the actual member and evaluates the
receiver once. Dynamic keys, readonly/required members and optional-chain
continuations still reject. The existing four-module Node run checks factory
call counts, 13 invalid/no-emit programs and six additive pinned-Go source IDs.
New-head checks remain NOT_RUN until CI executes. Existing 11 reference IDs,
TS7 pin, internal test minimum and release gate are unchanged. Original full TS7
remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED, dynamic UNEXPANDED.

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

## 2026-11-01 — Checked array filtering and same-element folds (candidate; NOT_RUN)

The existing C# array member resolver now exposes native ES2015 `filter`,
`reduce` and `reduceRight` on mutable and readonly arrays. `filter`
returns a fresh writable array and folds retain the element type `T`
for callback accumulator, optional initial value and result. Source-backed
xUnit, four-module Node execution and invalid/no-JS assertions, plus 20
independent fixed-Go source/diagnostic/exit cases, are registered as NOT_RUN
before exact-head CI. Distinct accumulator types, contextual callback
inference and callback index parameters remain unsupported. Original TS7:
0 PASS / 15,523 SKIP_UNIMPLEMENTED; dynamic scope UNEXPANDED. No release
gate, threshold, upstream pin or protection changes.


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

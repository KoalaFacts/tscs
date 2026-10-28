# Primitive literal type family (one bounded work package)

Source commit target: pinned microsoft/TypeScript 8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f.
Follow the already merged alias/interface/union pipeline; no parallel work package.

C# implementation parses string/number/true/false and signed negative-number
literal annotations into TypeSyntax, resolves semantic LiteralTypeSymbols, and
reuses structural assignment, union normalization, function annotations and
cross-module type-only imports. A fresh literal expression is contextually
checked against required literal members without inferring a narrower type for
an arbitrary stored primitive variable. The same JavaScript emitter erases all
type syntax. Direct/indirect incompatible writes must report errors and prevent
output.

Supported: literal annotations in aliases, interfaces, object/array members,
unions, parameters and returns; direct literal assignments and literals in
fresh contextual object/array expressions; primitive widening when assigning
a literal-typed value into its primitive base. No automatic `as const`, template
literal types, bigint, numeric separators/floats, full control-flow narrowing,
complex discriminant narrowing or arbitrary literal inference is claimed.
Literal equality on distinct constants and TS diagnostic formatting may still
differ; those require separate upstream acceptance.

The 18-source bounded pinned-Go differential compares real exit codes and
diagnostic-code arrays and preserves original raw outputs/identity hashes.
The Node24 end-to-end test compiles an interface/union/literal module graph,
actually executes JavaScript (expected result=42, state=done), and checks invalid
literal assignments reject emission. These are NOT original TS7 acceptance
and do not change the frozen 15,523 SKIP_UNIMPLEMENTED count.

Status at commit staging: NOT_RUN (no SDK/checkout in this worker; install path
requires pinned official downloads). A GitHub Actions exact-head build and
independent review are required before considering a merge. Do not borrow the
earlier PR #20 passing checks for new product code.

## Pinned-reference correction, 2026-10-28

The first actual fixed-Go differential on source SHA
`b686837ea773bc5337d5f6f29bbc0eed84fae027` returned 17/18
matching profiles. Stable ID `literal-types:positive-08` was initially
misclassified as successful: under exact `--noEmit --noLib --target es2015`
with the checked-in minimal declarations, the fixed Go compiler reports
`TS2322: Type 'string[]' is not assignable to type 'Tags'.` for its unchanged
source `type Tags = ("ready" | "done")[]; let tags: Tags = ["ready", "done"];`.
The original raw reference and product results are preserved in hosted
run `40620886547`, artifact `12787641300` (SHA-indexed manifest).

The original source and stable ID remain unchanged, but its *expected execution*
is now the genuine pinned `TS2322` rather than the original guessed PASS.
The name `positive-08` is a historic label, **not** an assertion of success.
The product relation checks this general literal-only primitive-array widening
boundary, rather than treating one filename or fixture ID specially. A real
xUnit negative now guards the original program. Arrays of structured objects
retain per-element contextual checking. No SKIP, missing negative, or original
TS7 acceptance credit has been added.

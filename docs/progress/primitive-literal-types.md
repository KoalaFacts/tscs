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

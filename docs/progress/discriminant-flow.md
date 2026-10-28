# Discriminated union flow: one complete bounded language family

Source base: merged main `516249612b418dcdef4e77cbacf800ce28e43fef`.
Pinned TypeScript reference remains `8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f`.

## Reusable compiler path

The existing AST represents strict equality/inequality and braced if/else.
The existing binder carries named object-union types across functions, modules
and for..of bindings. `TypeChecker` now gives each supported branch a
temporary type view of the **same** symbol when every object alternative has
a required, explicit primitive literal member. The appropriate variant subset
is selected for `===` and reversed for `!==`.
Nested conditions inherit outer branch types. After each branch, the original
symbol type is restored in a `finally` path, including error exits;
definite-initialization joins retain their existing behavior. The same checked
AST emits normal ES2015 `if` JavaScript, with no runtime casts.

The entire package must compile a type from one module, narrow it inside a
function from another module, and execute emitted entry JavaScript in Node24.
Invalid member access in the wrong variant must fail with TS2339 and emit
nothing. A pinned-Go 12-input differential independently checks codes/exits;
it is a local bounded profile, not original TS7 runner acceptance.

## Deliberate limitations and next dependencies

Only simple-identifier named property comparison against a string/number/boolean
literal; all object variants must have a **single literal** discriminator.
Neither optional/broad discriminator members, arbitrary truthiness,
`&&` / `||` compositions, comparison of two variables,
assignment-sensitive flow after the branch, discriminant exhaustiveness,
control-flow reachability after returns, nor recursive type narrowing is claimed.
If the predicate is unsupported it does not silently narrow a symbol.
No generic constraint solving or full TypeScript control-flow graph is asserted.

## Verification and release ledger

At candidate creation in a disconnected worker: local SDK unavailable;
source/tests/tooling authored but **NOT_RUN**. GitHub strict verify, pinned
Go reference, CodeQL and independent review are required before normal merge.
No upstream original acceptance IDs are changed: 15,523 configuration
SKIP_UNIMPLEMENTED targets, 0 original accepted, full dynamic denominator
UNEXPANDED. Do not conflate internal xUnit/Node cases with original acceptance.

## Actual pinned-Go correction (2026-10-28)

The initial exact-head reference run `40622934807` (artifact `12788302317`)
found **11/12** matching bounded cases. Stable source ID
`discriminant-flow:positive-08`, SHA-256
`2dde477f24602cc14128575710156a07186fa8840269154bd2a058ecd99a6384`,
produced Go TS2322 while the product incorrectly returned success:
under this frozen `--noLib --target es2015` profile a directly inferred
heterogeneous array widens its object-tag properties to `string`, preventing
assignment to `Result[]`. The source and ID were **not** removed or changed;
its expected **negative** execution now follows the actual raw oracle, and the
product checks the general array-element-type relation instead of allowing
contextual refinement through a union of distinct object shapes. Existing
homogeneous contextual object-array checks remain independent.

A new separate stable ID `discriminant-flow:positive-09` tests the supported
array path with individually annotated union values, including a narrowed
`for..of` body, for **13 total** registered bounded sources (scope delta +1).
The first run remains recorded as MISMATCH, never retroactively rewritten to
PASS. This correction requires new-head focused validation and a fresh fixed-Go
run. Original TS7 acceptance and all SKIP identities remain unchanged.

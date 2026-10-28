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

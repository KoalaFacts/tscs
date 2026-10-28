# Bounded `as` and `satisfies` expressions

This source package adds a shared AST type-operator node in the existing
parser/type checker/emitter, with no runtime conversions. `as` requires
assignability in at least one direction (unrelated conversions TS2352).
`satisfies` requires contextual assignability (TS1360 for simple mismatch),
preserves inferred values and fresh-object excess checks, and keeps contextual
literal types for fresh named members so later writes cannot silently widen.
Named aliases and multi-module imports use the existing type namespace.
Discriminated object-union `satisfies` and broader TypeScript contextual
inference are *not* claimed; they fail explicitly when unsafe.

The 25 stable cases in `fixtures/type-operators/semantic-cases.json` compare
actual pinned Go and product diagnostic code arrays and exit codes. Raw source,
reference/product outputs, pinned version and per-file hashes are preserved
by `tools/verify-type-operators-reference.ts`. The independent actual Node
four-module emit/execute proof is `tools/verify-type-operators-emit.ts`,
including negative tests requiring no JavaScript output.

At source construction: **NOT_RUN**. Exact head validation must be read from
GitHub Actions before asserting passing evidence. This suite earns no original
TS7 runner acceptance and changes no release gate or upstream pin.

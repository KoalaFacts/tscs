# Named types, interface shapes and unions — one compiler work package

## Product boundary

This package extends the existing Scanner → AST/Parser → Binder/Scope →
TypeChecker/TypeSymbol → ModuleLinker → JavaScriptEmitter pipeline. A separate
lexical **type namespace** binds non-generic `type` declarations and required
property-only `interface` declarations without generating runtime values.
Declaration references are resolved in their original lexical scope with bounded
cycle rejection, including explicit `import type { Name }` across project files.

Within this bounded implementation, union types (`A | B`) and array postfixed
types (`(A | B)[]`) share structural compatibility with primitive/object types.
Union sources must satisfy the required target through **every** branch; union
targets accept an assignable branch. Common named-property reads of object
unions return the union of their member types. Fresh object literals are checked
against the union's known property names. Heterogeneous array literals can
infer an element union instead of fabricating `any`; the former negative guard
for `const xs = [1, "text"]` is now a positive regression, while assignment to
`number[]` remains a failing negative regression.

Type-only imports, aliases and interfaces disappear from ES2015 output. A real
three-module program imports interface/alias declarations, passes an object
through a typed function, and runs generated JS on Node24 with `result=42`,
`label="seed"`, `count=2`. Incorrect alias/interface values prevent output.

## Explicit limitations

No interface merging, `extends`, type parameters/generics, recursive aliases,
conditional/mapped/intersection/template/literal types, optional/readonly
properties, index signatures, discriminated union narrowing, arbitrary module
`export type {}` re-exports, JSX or complete TypeScript union diagnostics.
Union-typed receiver **writes** fail explicitly because they require additional
intersection-like safety relations. JavaScript `|` expressions are not silently
interpreted as type unions. Existing source limits and fail-closed behavior remain.

## Evidence and continuation

The bounded local xUnit suite exercises actual product APIs and negative cases.
`tools/verify-named-types-emit.ts` compiles and executes actual ES2015 modules;
`fixtures/named-types/semantic-cases.json` and
`tools/verify-named-types-reference.ts` compare real diagnostic code arrays and
exits against the original pinned Go compiler in the existing CI reference lane,
with immutable SHA/length manifests and mismatches preserved. They **do not**
constitute original TS7 harness acceptance, and the 15,523 original configuration
SKIP_UNIMPLEMENTED ledger and unknown runtime expansion are untouched.

Delivery gates: one focused build/test stage and one normal strict verify after
integration, followed by exact-head fixed-Go comparison and independent review.
The package remains incomplete until those actual results are recorded and a
normal repository-compliant merge occurs. No release/date prediction is made.

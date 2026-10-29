# Bounded value-type queries — one compiler work package

Type-level `typeof name`, dotted known object paths, `keyof typeof name`,
`(typeof name)["key"]`, and function signature queries use the existing
C# Scanner/Parser, Binder, TypeResolver, TypeSymbol relationships, ModuleLinker,
TypeChecker and type-erasing ES2015 emitter. Value names may be declared later
in the same lexical scope; queries preserve explicit annotations and callable
signatures rather than guessing an `any` type. Exported type aliases can be
imported into other modules, and typed callbacks run through the ordinary
cross-module function pipeline.

This bounded package does **not** support inferred/unannotated variable queries,
`typeof` on imported runtime values, namespaces, dynamic/computed type-query
paths or general TypeScript type query semantics. They fail closed rather than
fabricating a type. Circular alias queries retain the real TS2456 failure.

The new stable 13-case bounded source/code/exit reference runner uses the fixed
TypeScript 7 Go executable, records unchanged source text, hashes, per-case raw
results and pin. Real four-module emitted ES2015 JavaScript must execute and
invalid programs must emit no output. These are **not** original upstream runner
acceptance tests. Original 0/15,523 configuration targets remain SKIP_UNIMPLEMENTED;
full dynamically expanded scope is UNEXPANDED, no release claimed.

Source candidate requires exact-head strict PR CI, Go reference, static analysis,
review/thread checks and normal merge before reporting implementation on main.

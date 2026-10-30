# Typed anonymous and named function expressions — bounded compiler core

The C# compiler now checks and emits anonymous `function (...): ... { ... }`
values, not only arrow expressions and named declarations. This shares the
existing Parser's typed parameters, Binder scope, TypeResolver, TypeChecker
function relations, control-flow return checking and ES2015 emitter. Callable
values may be assigned to a callback signature, passed to a declared function,
returned by another function, or captured in named module exports. Trailing
defaults, optional typed values and final typed array-rest parameters share
the existing checking semantics; JavaScript output uses native ES2015
`function` syntax, never an arrow replacement.

Named expressions `function self(n: number): number { ... }` use a private
self-name binding between outer values and parameters, allowing checked
recursion and captures without exporting the self name. The self name is
immutable and parameters can shadow it. Native ES2015 named functions are
emitted; no outer declaration is hoisted. Named inferred returns, generators,
`this`, untyped parameters, overloads and generics remain unsupported and
fail closed. Semantic violations stop emission.
`tools/verify-function-expression-emit.ts` executes checked four-module
ES2015 output under Node 24, including nested callbacks and closure capture;
twelve invalid programs must generate no JavaScript.

The independent fixed-Go comparison in
`tools/verify-function-expression-reference.ts` runs twenty-four stable
source IDs (twelve original unchanged, twelve new named cases) from `fixtures/function-expression/semantic-cases.json`,
compares actual diagnostic codes and exit statuses, and saves raw input,
reference/product results, source hashes and evidence manifest.
At source creation these checks are **NOT_RUN** until exact-head CI confirms
otherwise. Neither these bounded cases nor the C# regression tests earn
full-original TS7 acceptance. The original registered range remains
0 PASS / 15,523 SKIP_UNIMPLEMENTED and dynamic scope UNEXPANDED.

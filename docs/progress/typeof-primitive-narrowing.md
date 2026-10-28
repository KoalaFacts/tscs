# Primitive typeof checks and flow narrowing

A single bounded compiler work package extends the existing AST/binder/
typechecker/flow/emitter rather than adding a parallel interpreter.

- Runtime `typeof expression` has static `string` type and emits real ES2015
  `typeof` with correct unary precedence and token separation.
- Strict `===` and `!==` comparisons against `"number"`, `"string"`,
  or `"boolean"` refine simple identifiers whose declared types are unions
  of primitive/literal alternatives; reversed comparisons and parentheses
  work. Both type partitions must be nonempty.
- Branch type facts restore on exit and propagate through returning guards
  via existing flow logic. Cross-module `import type` reuses type namespace.
- Do not claim narrowing for `any`, objects/functions/mixed unions, arbitrary
  property accesses, boolean expression trees or `switch(typeof value)`.
  Valid `typeof` expressions still emit normally.
- Stable source/diagnostic IDs in
  `fixtures/typeof-primitive/semantic-cases.json` use the same frozen
  declaration profile for both compilers. The comparison retains raw
  diagnostics, exit codes, hashes, and all mismatches as errors.
- Existing strict internal gate, Node four-module runtime and independent
  fixed Go reference lane validate the candidate at its accurate head.
  At source creation checks remain NOT_RUN, not historical successes.

No original upstream harness case is accepted by this bounded cohort.
Original compiler PASS=0 and SKIP_UNIMPLEMENTED=15,523; dynamic
scope UNEXPANDED. Release eligibility and upstream pin are unchanged.

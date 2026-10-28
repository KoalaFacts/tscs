# Conditional values (one bounded implementation package)

Base main `60297cbedcb22a979ae4f03995d80a6a96ecd897`.
The scanner, expression precedence ladder, AST, checker and ES2015 emitter
now carry right-associative `?:` value expressions. A condition is boolean/any
and both arms are statically checked even when one arm will be inactive.
The checker reuses discriminant / typeof partitions with try/finally lexical
restoration. It joins the arm result types through the existing union engine
and checks contextual literals per arm, including extra object properties.
The emitter preserves JavaScript's one-arm evaluation.

Source-creation acceptance state: **NOT_RUN**. Normal strict
`scripts/verify.sh` executes the four-module graph (result=42, other=0,
label="bad", shortCircuit=42), checks erroneous assignment blocks emission,
and retains all existing internal/frozen gates. Separately the pinned Go
7.1.0-dev oracle compares diagnostics/exits for 23 stable source IDs in
`fixtures/conditional-value/semantic-cases.json`, saving original bytes,
raw outputs, hashes and manifest. Both new-head checks must pass, followed by
review and ordinary merge, before reporting integration.

Unimplemented: numeric truthiness, assignment expressions, optional members,
arbitrary side-effect-aware CFG and complete TypeScript contextual inference.
This feature is not original TS7 harness acceptance. The original 15,523
configuration target placeholders stay SKIP_UNIMPLEMENTED, 0 original PASS,
and the full dynamic domain remains UNEXPANDED. Release remains ineligible.

# Bounded postfix numeric value expressions

This package extends the existing checked numeric mutation pipeline so `x++`,
`x--`, `object.count++`, and `array[index++]++` can occur in initializers,
returns, call arguments and classic-for updates. An expression returns the old
number; native ES2015 syntax evaluates the receiver/index only once.
The existing identifier, imported-binding, readonly-property, immutable
string-index, numeric-operand and definite-initialization restrictions apply.
Postfix tokens following a line break begin a new statement.

Unsupported grouped lvalues fail closed; assignment expressions, optional
chains and generics are not claimed. The loop backedge analysis conservatively
tracks writes nested in expressions, without credit for unreachable paths.

Validation: dedicated C# positive and negative cases; four-module Node ES2015
script `tools/verify-postfix-update-emit.ts`; 13 stable additional positive/
negative source IDs appended to the pinned-Go numeric-mutation cohort. Existing
24 IDs, sources, diagnoses and all upstream pin, release gates and original
target skips are unchanged.

At source creation this worker has no local checkout/.NET SDK and GitHub clone
DNS resolution fails. CI and reference are NOT_RUN pending exact-head hosted
checks; this is not original TS7 case acceptance. Original upstream ledger is
0 PASS, 15,523 SKIP_UNIMPLEMENTED configuration targets; remaining dynamic
cases and other domains UNEXPANDED.

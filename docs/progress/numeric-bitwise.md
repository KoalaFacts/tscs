# Numeric bitwise and shift expressions (bounded compiler package)

Source candidate based on main `f7267b17117b5180bb0711342e350182ac3369a7`. The scanner distinguishes `&`, `|`,
`^`, `~`, `<<`, `>>`, and `>>>` from logical operators, type unions,
comparisons and JSON syntax. The existing iterative AST/checker/emitter now
carries these six binary operators and unary complement with JavaScript/TS
operator precedence (including additive-before-shift and equality-before-bitwise).

For this bounded family, operands are `number` or `any`, and the static result
is `number`; unsupported string/boolean coercion fails closed. JavaScript uses
native 32-bit bitwise semantics, with unsigned right shift returning a number.
This is **not** general `bigint`, operator overloading, shift assignments, numeric
literal variants, template strings or an implementation of TS's entire arithmetic
grammar.

Integration tests: `BitwiseNumericTests.cs`; four-module ES2015 execution and
negative no-emit: `tools/verify-bitwise-emit.ts`; independently executed pinned
Go comparison of 18 explicitly registered positive/negative source cases:
`fixtures/numeric-bitwise/semantic-cases.json` through
`tools/verify-bitwise-reference.ts`. The latter compares real diagnostic codes
and exit statuses, preserves both raw outputs and sha256, and keeps
`originalAcceptanceCredit:0`. Both lanes are part of the existing strict CI;
**at source creation they are NOT_RUN**, not accepted results.

Original TS7 target status unchanged: 0 original PASS, 15,523
SKIP_UNIMPLEMENTED configuration targets, complete dynamic scope UNEXPANDED.
No implementation flags or original test IDs are fabricated or removed.

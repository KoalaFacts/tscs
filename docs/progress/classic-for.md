# Classic for loops: bounded end-to-end compiler package

Base: `dbc2f2c15cae993d8cfc10fce241f51a1b5032b5` (main after PR #26). This implements a lexical
`let` initializer or an existing mutable binding, optional boolean
condition, checked assignment/postfix increment, and braced body.
`continue` executes the increment, `break` exits its nearest construct,
and a loop-header binding does not escape into following statements.
The existing AST, binder, checker, structured flow and ES2015 emitter
are reused. The loop may run zero times; body/update initialisation
effects are not credited afterward.

Not supported: `var`, destructuring, multiple declarations, arbitrary
prefix/compound assignments, nonboolean truthiness conditions, unbraced
bodies, full fixed-point flow or general iterable semantics.

Real ES2015 runtime smoke: `tools/verify-classic-for-emit.ts`.
Pinned Go oracle: 14 positive/negative stable cases in
`fixtures/classic-for/semantic-cases.json` using the existing
`tools/verify-switch-reference.ts`, with both compilers given the same
bounded array declaration profile. No original TS7 runner acceptance
is credited: 15,523 configuration targets remain SKIP_UNIMPLEMENTED,
0 original accepted and full dynamic scope unknown. Exact-head
CI/reference/runtime and independent review must succeed before merge;
initial source commit itself does not claim passed checks.

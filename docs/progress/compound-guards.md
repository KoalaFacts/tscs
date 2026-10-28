# Compound boolean guard narrowing (bounded)

This work package uses the existing scanner/AST/parser, discriminated-union
types, flow checker and JavaScript emitter. The type checker evaluates `&&`
RHS expressions under the true LHS branch, and `||` RHS expressions under
the false LHS branch. It restores the original lexical flow type afterward.
A leading `!` flips the two exits, and chained same-symbol `typeof`
predicates intersect/join the retained union alternatives. This applies to
braced if/else and returning guards, and conditional value expressions.

A checked program can now use `typeof v === "number" && v > 0` or
`packet.kind === "ok" && packet.value > 0` to safely read the RHS and
the true arm; `!(packet.kind !== "ok")` narrows to the matching object.
The opposite branch remains broad when the RHS is an arbitrary boolean.
Evaluation remains short-circuiting in emitted JS.

**Safety boundary:** only already-supported Boolean operands are accepted;
general JS truthiness, mutation inside condition expressions, arbitrary
Boolean function effects, correlated narrowing of multiple distinct symbols,
full CFG and unreachable/never analysis are not claimed. Logical recursion
has an explicit 64-level limit, rather than unbounded stack use.

`CompoundGuardTests.cs` tests supported and rejected programs.
`tools/verify-compound-guards-emit.ts` compiles four real ES2015 modules,
executes JS and checks that bad member reads cannot emit.
`fixtures/compound-guards/semantic-cases.json` freezes 18 positive/negative
diagnostic cases, compared with the fixed Go TS7 compiler through the existing
source-bound, hash-manifest reference verifier. At source creation, hosted CI,
runtime and reference comparison are **NOT_RUN**; never count this as an
original upstream case PASS.

The original upstream ledger is unaffected: 0 original PASS,
15,523 SKIP_UNIMPLEMENTED; full dynamic scope UNEXPANDED.

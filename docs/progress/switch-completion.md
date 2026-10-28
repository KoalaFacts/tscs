# Bounded switch completion and loop break

Base main: `dc18ba9c11287e318160055e060f9097226deb10` (PR #24 merged). The compiler's existing AST,
Binder, semantic type relations and ES2015 emitter are reused; no duplicate
parse or output path was added. This work package extends returning-only
braced switch arms with normal completion (fallthrough), `break` and
branch-local discriminant safety. `break` is supported in switch clauses and
`for..of` braced bodies (including nested if/block branches), but only as the
last direct statement in its immediate block. Unbraced switch arms,
`continue`, general labels, arbitrary break placement, unbounded
case expressions, classic for/while and fully general CFG remain unsupported.

`FunctionControlFlow` has one structured exit-set transfer model: Next,
Return and Break. It consumes Break at the nearest switch or for..of
boundary; return analysis cannot confuse a break with a definite return.
A switch without default retains an unmatched-selector path. Case entry
may come directly from its literal label or by normal fallthrough from
its predecessor, so the checker joins the associated object-union types
before permitting a member read. A prior break/return eliminates that
fallthrough. Initialisation facts are conservatively restored between
arms; they are not spuriously promoted as definitely assigned.

A real three-module ES2015 smoke compiles a type-only model, runtime logic
and entry module: `dispatch` uses break after a narrowed member read,
`fall` uses implicit fallthrough, and `first` breaks from a for..of loop.
Expected Node runtime: result 42, alternate 1, firstValue 21.
The negative smoke expects TS2339 and **no emitted bad.js**.
The independent pinned Go lane reuses the existing frozen switch differential
runner with a separate 12-case suite and stable source IDs. Actual outcomes
are NOT_RUN at candidate creation. Internal tests and bounded Go profiles
are not original TS7 test acceptance.

The original 15,523 config targets remain SKIP_UNIMPLEMENTED, zero original
compiler accepts, and dynamic scope UNEXPANDED. Do not lower test minima,
swallow failures or count this bounded family as a release certificate.
Only one PR carries this whole work package. Close its real check/review
findings before starting another family.

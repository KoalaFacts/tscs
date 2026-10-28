# Discriminant switch with returning case arms

Base: merged PR #23 `1d375eb369740d822f77bd745dd91531aeb76eb0`.
Fixed upstream: `8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f`.

This single work package adds source tokens, a bounded switch/arm AST,
parsing of primitive-literal labels, branch-local discriminated-union members,
definite value-return analysis, and JavaScript emission through the existing
compiler context. Each arm must be braced and return on every path, so there
is no implicit fallthrough. Every branch is checked, and failure blocks emit.
There is no second parser, scope model or output pipeline.

The supported end-to-end program imports a union type from one module, handles
its tag through a switch in another module, and executes a third module's
checked ES2015 output with result 42 and message 'bad'. The candidate includes
10 positive and 4 negative pinned-Go differential inputs, including member
access and incomparable labels. Test code and emitted runtime are checked by
normal CI; they are **NOT_RUN** in the worker that created this candidate.

Boundaries: no break/continue, implicit fallthrough, unbraced case arm,
shared-case lexical binding, generic dynamic labels, CFG-based exhaustiveness
without a default, or arbitrary type predicate. Non-returning switch statements
and top-level switches remain explicitly unsupported. All dynamic TS7 scope,
original 15,523 SKIP IDs, and release criteria remain unchanged.

Next: fix only genuine exact-head build/Go/reference failures; collect independent
review and deliver through the same PR when normal gates are met. Do not make
another peripheral PR while this one is open.

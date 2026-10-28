# Returning-guard facts — bounded control-flow work package

Base: merged PR #22, `64b37826749168b0da6d600bc3f35fdddcb188a3`. Fixed Go reference:
`8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f`.

TypeChecker.CheckConditional already selects branch-local union alternatives;
FunctionControlFlow already knows which branch returns. CheckBlock now keeps
the surviving type for subsequent statements if exactly one arm falls through,
and restores the lexical symbol type on every block exit. Consecutive
returning guards progressively refine an object union without a duplicate AST,
binder or emitter. No invented any or swallowed error.

Acceptance: actual three-module JS must execute result=42/message=bad, wrong
branch members must fail and emit nothing, and 14 bounded source IDs must match
fixed Go diagnostic codes/exits. This is not original TS7 harness acceptance.

Limits: supported literal discriminants/strict equality on a simple identifier,
returning braced branches only. Compound boolean predicates, arbitrary writes,
post-loop facts, exceptions and a complete TypeScript control-flow graph remain
unsupported. No original 15,523 SKIP identity changes.

The current worker lacks local checkout/cache/SDK; git cannot resolve
`github.com`, so product code and tests are **NOT_RUN locally**. Exact-head
hosted full verification, live pinned Go and code review are required before
normal merge. Do not reuse parent-SHA results.

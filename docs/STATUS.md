# tscs status

## 2026-10-28 — Structural object data (PR #18, not yet merged)

The integrated object family now compiles and executes across modules: inline
required member shapes, literals, nested property reads/writes, structural
assignment, object function parameters/returns, and AST-backed ES2015 output.
Local execution of the emitted four-module graph returns 42 and preserves the
other properties. Missing properties, incompatible writes and fresh excess
properties are rejected. Shared-type diagnostics and parser nesting are bounded.
Unsupported object forms remain explicit; no source-case special handling.

The current container now has the exact SDK and tools. Actual local SDK build
and MTP coordinator passed: focused object42/42, internal15,788/15,788,
Node157/157, frozen29/29, and actual Node24 execution of emitted object modules.
Commands, raw logs and limits are in `docs/evidence/structural-object-data.json`
and `docs/progress/structural-object-data.md`. Fixed-Go object differential is
wired into the existing reference lane but NOT_RUN at commit creation.

Original TS7 accounting is unchanged: 15,523 configuration targets remain SKIP,
0 original accepts, full dynamic scope unknown. Only the existing local probe
`bootstrap:object-literal` moved to implemented: local probe registry is now
9 implemented/NOT_RUN + 5 SKIP; that is not original acceptance.

Next: complete exact-head CI/reference and whole-package review on PR #18;
do not open overlapping work or return to configuration-only micro-deliveries.

## Existing foundation and remaining scope

The source contains host-backed project/config loading, module linking, typed
CompilerOptions, basic functions/control flow and bounded ES2015 output.
Full TS7 compatibility, standard libraries, arbitrary object semantics,
declarations/maps, incremental/watch and other original domains remain incomplete.
No release is asserted and existing strict gates remain unchanged.

[Unmodified prior status and evidence handoffs](progress/status-through-project-selectors.md).

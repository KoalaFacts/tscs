# Implementation-aware coverage (bounded bootstrap probes)

This is a **bounded local guard catalogue**, not a TS7 upstream conformance score. Existing `CompilationTests.cs` rows are bound to its exact Git blob and retained in their original xUnit tests. Their fourteen explicitly registered local inputs classify as **7 implemented / upstream NOT_RUN** and **7 SKIP (reason: UNIMPLEMENTED)** spanning four distinct missing capabilities: function declarations, `@ts-ignore`, object literals and intrinsic `globalThis`. The stable `bootstrap:binary-expression` probe now passes only a number-plus-number slice, not all binary operators. The previously skipped `let n: any = 1;` is now a bounded implemented variable annotation/assignment case; full TypeScript type-system completeness is not claimed.

The seven unsupported-feature xUnit tests **continue to run**, proving that the bootstrap rejects such inputs instead of silently accepting them. This success is **not** evidence that those features are implemented. The SKIP classification is maintained in a separate machine-readable compatibility-status report, not by disabling existing regressions or removing `--fail-skips on`. Neither this catalogue nor the 15,486 internal tests have verified mapping to the full TS7 executable-case denominator.

Run `node tools/implementation-coverage.ts --check` to fail closed on unregistered/moved guard inputs, changed source Git blob, changed reference pin, fabricated upstream mapping or invalid status/reason pairs. Use `--write` for intentional report updates; `--release` continues to fail while skips and unknown upstream expansion remain. `node --test tools/implementation-coverage.test.ts` covers these guards and verifies an implemented mismatch stays FAIL, never SKIP.

The validated machine-readable counts and stable per-probe identities are in [implementation-coverage.json](../testing/ts7/implementation-coverage.json), generated from [the registration](../../tests/implementation-coverage/registry.json). The **original compiler/reference acceptance remains NOT_RUN**, and 13,006 source fixtures/67,951 tracked assets must never be mistaken for an executable test denominator. Upstream skips, seven reference fatals, unexpanded source variants, helper-vector suites and the separate 29-case frozen cohort retain their own scope and status.

**2026-10-26 bounded compiler addition:** parser and primitive checker now admit variable `any` annotations and assignment compatibility with primitive types; stable probe `bootstrap:any-annotation` moved from `SKIP / UNIMPLEMENTED` to `IMPLEMENTED / upstream NOT_RUN`. Full-upstream compiler acceptance added = 0; local probe SKIPs reduced by 1 (10 → 9). Genuine hosted test-first RED at `6e2d446` recorded 7 `any` failures (TSCS1001) and 15,452 successes. Exact-head GREEN at `00317a1` passed two full hosted verify jobs: xUnit 15,459/15,459, Node 103/103, frozen 29/29, Go scope PASS, 0 Release warnings/errors, CodeQL and Actions analysis success. Four raw hosted logs and run IDs are preserved in [the evidence](../evidence/any-annotation.json). The original pinned whole-upstream compiler acceptance remains NOT_RUN. General `any` behavior in functions, generics, libraries remains unsupported. An ETA cannot yet be measured from one dated snapshot with no fixed, fully expanded upstream case denominator and no time-separated net completion samples. Future estimates must use stable case IDs and actual PASS deltas separately for scanner/parser/binder/checker/emit, including all remaining SKIPs and failures.

## 2026-10-26: bounded numeric addition

The concrete stable ID `bootstrap:binary-expression` moved from SKIP/UNIMPLEMENTED to IMPLEMENTED/NOT_RUN. C# now parses left-associative number-only `+` expressions and verifies inferred/annotated numeric operands; it does not claim string concatenation, unary plus, any arithmetic or other binary operators. A hosted test-first RED at `7cdd14f` had 8 expected failing tests. The first implementation caught one unrelated existing lexical-error regression (15469/15470); a corrective pass preserved both TS1125 and TSCS1001. Final implementation `273b4b7` passed 15473/15473 xUnit, 103/103 Node, and frozen 29/29 on both hosted verify jobs, CodeQL/Actions analysis also succeeded. See [raw evidence](../evidence/numeric-addition.json). The full fixed Go compiler case was NOT_RUN; accepted full-upstream cases do not increase.

The local cohort has two successive dated state points (5 implemented/9 SKIP and 6 implemented/8 SKIP), but only one observed net feature completion. It is insufficient to infer scanner/parser/checker/emit-wide throughput, and upstream executable-case denominator remains unknown. Report this local delta explicitly; do not multiply 15,473 helper/unit checks into TypeScript feature coverage or forecast a release date from one package.

## 2026-10-26: bounded number postfix increment

`bootstrap:postfix-update` moves SKIP/UNIMPLEMENTED to IMPLEMENTED/NOT_RUN
after an actual scanner/parser/checker feature for initialized, mutable numeric
`identifier++;` statements. Existing guards still exercise unsupported
prefix, nonnumeric and expression-position variants; the prior CLI negative
guard now uses unsupported prefix `++x`, while positive CLI `x++` exits 0.

Hosted test-first RED at `167d240` recorded 7 expected failures of 15,485.
Intermediate `48e7f5e` retained a real obsolete CLI input regression; `7339946`
passed xUnit 15,486/15,486 while two Node coverage assertions failed. At
exact head `5e6927c`, both verify jobs passed xUnit 15,486/15,486, Node
103/103, frozen 29/29, Go scope PASS and four hosted checks completed
success. Raw logs: [postfix evidence](../evidence/postfix-update.json).
The fixed local 14-probe cohort is now **7 implemented / 7 SKIP**
(one fewer SKIP), but actual full-upstream acceptance added is **zero**.
The original upstream denominator is still unexpanded/unknown; no full
compiler completion ETA is claimed.

## 2026-10-27 — bounded canonical line-comment @ts-ignore

Stable local guard `bootstrap:ts-ignore-directive` was moved only after
the C# scanner learned to recognize whole-line `// @ts-ignore` and the
checker filtered its own following-line diagnostics. Syntactic/lexical
failures are never converted to SKIP or filtered. New real test cases pin
next-line semantic suppression, CRLF, blank-line boundaries, string
contents and explicit unsupported directive variants. Hosted TDD RED
`f706c04`: 8 failures; GREEN `51993ce`: internal xUnit4/MTP
15,500/15,500 and native Node24 145/145, with both verify jobs and
CodeQL/Actions succeeded. The bounded local inventory is **14 =
8 implemented/upstream NOT_RUN + 6 SKIP(UNIMPLEMENTED)**, net removed
one stable local SKIP. Original upstream accepted +0, and the 15,523
source/configuration targets remain separately discovered SKIP until real
runner assertions and reference comparison exist. See
[exact-head evidence](../evidence/ts-ignore-directive.json).


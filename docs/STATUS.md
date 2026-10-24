# Status — 2026-10-24

Working bootstrap of the first noEmit milestone. No JavaScript emitter or language
server. No real-project compatibility or performance claim.

## Evidence

- Pinned upstream: Microsoft TypeScript 8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f (7.1.0-dev).
- Reference built with Go 1.27.3; C# SDK 10.0.402.
- Frozen differential cohort: 29 cases, including 7 unchanged upstream source files.
- Discovery: 12,981 upstream compiler/conformance source files; 15,174 known option
  variants and 90 files with unresolved variant expansion. Counts are provisional,
  not a complete faithful upstream harness inventory.
- Entire upstream suite: not run. Seven upstream sources exercised in isolation;
  remaining 12,974 upstream source files not exercised by this cohort. All corpus
  pass/mismatch/unsupported/crash/timeout counts are therefore **unmeasured**, not zero.
- CI workflow is provided. Local verification evidence does not imply hosted CI ran.

## Local verification and review

- Release build: zero warnings/errors.
- NUnit: 40 passed; Node harness/evidence tests: 17 passed.
- Frozen differential cohort: 29/29 matched; repeated reports were identical.
- Independent reference rebuild: 29/29 frozen results reproduced.
- Five additional oracle probes validate review fixes (strict binding names,
  top-level globalThis conflict and diagnostic phase ordering).
- Independent review found three important issues; all were reproduced by failing
  tests and fixed. An additional diagnostic-phase bug was fixed in the same pass.
- Evidence: [cohort](evidence/bootstrap-conformance.json),
  [reference build](evidence/reference-build.json),
  [review regression probes](evidence/review-regressions.json).

Known minor compatibility defect deferred: U+2028/U+2029 inside quoted strings
currently produce TS1002 although the pinned compiler accepts them. Do not interpret
this bootstrap's string-literal handling as complete JavaScript lexical compatibility.

## Milestone gaps and next step

The bootstrap establishes an executable chain and a small oracle-backed cohort.
Full first-milestone infrastructure remains incomplete: faithful directive/multi-file/
symlink harness behavior and complete variant accounting need implementation. General
filesystem hosting is also deferred. Keep these visible rather than call the full
milestone finished.

Next: implement faithful upstream fixture/directive loading and replace provisional
inventory counts with exact executable variants, while preserving the 29-case gate.
Then expand scanner/parser/binder/checker coverage in dependency-sized ports. Do not
start IL output, a runtime, or a language server during compiler completion.

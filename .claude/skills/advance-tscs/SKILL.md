---
name: advance-tscs
description: How the scheduled task advances tscs — original-upstream acceptance metric, toolchain and gate, ledger in docs/STATUS.md, work order, anti-sprawl and lock rules. Read with agent-harness/autonomous-dev at the start of every run.
---

# Advance tscs

Repo-specific half of the scheduled development task; the run loop is `agent-harness/skills/autonomous-dev`. The task keeps this file current through its own PRs. It may make rules stricter, never looser, and never relaxes the hard limits in the scheduled task's prompt.

## Goal and metric
- tscs is a TypeScript compiler written in C# (.NET 10; `src/Tscs.Compiler`, `src/Tscs.Cli`), targeting compatibility with the pinned TypeScript revision in `upstream.lock.json` (TS 7.1.0-dev, Go reference).
- **Headline metric:** original upstream compiler cases passing against the pinned reference — PASS / FAIL / SKIP, with the commit measured on. The current value is the Headline of `docs/STATUS.md` (3 PASS / 0 FAIL / 15,520 SKIP when this skill was written); CI job `original-go-reference` fails when that line disagrees with the measurement.
- **Real progress** = original cases moving to PASS, the standard library loading further, or duplicated code/docs removed with behaviour proven unchanged. A code PR changes compiler or harness code and tests. Every PR body carries original PASS/FAIL/SKIP before and after.
- **verify-tscs's real boundary:** an original upstream case run through tscs and compared with the pinned Go reference by the harness. Never mark a case PASS that the harness did not actually compare.

## Anti-sprawl rule
A long run of PRs each added one narrow "bounded" construct (classic-for header variants, switch label variants, single native methods) while original acceptance stayed at 0. Do **not** add another single-construct micro-feature unless it is needed to make a named original upstream case pass, or to load/check the standard library (A2) — and say which in the PR.

## Setup
- Read `AGENTS.md` (binding), `README.md`, `docs/DEVELOPMENT.md`, `docs/PORTING.md`, `docs/STATUS.md`, `docs/superpowers/specs` and `plans`, `tests/conformance/*`.
- Toolchain: `bash scripts/setup-dev-environment.sh`, then source the `env.sh` it prints (exact SDK 10.0.402, Node 24.22.1, Go 1.27.3), as `AGENTS.md` says.
- Trap: earlier sessions reported "no local .NET SDK" and opened PRs untested. If setup fails, read the actual error and fix it (proxy CA bundle at `/root/.ccr/ca-bundle.crt`, see `/root/.ccr/README.md`). Never change `global.json` or relax locks. A permission or access denial (not a broken tool) is a stop condition: report it, do not route around it.
- **Hosted CI never replaces the local gate.** Every PR merges only after the applicable local gate has run on its final source. Results from earlier runs may be reused only for code the final diff does not touch, and the PR says what was reused; anything the edits affect is re-run. Stages that were blocked or not run are listed as such, never reported as passed. A PR opened before its local gate ran must say so and stays unmerged until the gate passes locally.

## Gate
1. `./scripts/verify.sh` — what `ci.yml` runs: locked restore, Release build (warnings are errors), all test projects, `go test` for `tools/scope-go`, `node --test tools/*.test.ts`, `tools/check-docs.ts`, then the conformance and emit checks.
2. The rest of `ci.yml`: `scripts/verify-upstream-lock-probe.sh` and `scripts/verify-upstream-full-discovery.sh`.
3. The original-acceptance measurement locally (the `verify-tscs` headline row: `node tools/original-acceptance.ts ... --expect-headline docs/STATUS.md`) for every PR body's before/after PASS/FAIL/SKIP; when touching the oracle/harness, also `scripts/verify-original-emptyfile-go.sh`. Hosted jobs add evidence; they never stand in for this step.
For a quick inner loop, `dotnet build Tscs.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false` after a restore.

## Ledger
The "Roadmap" section of `docs/STATUS.md`: headline metric with commit, then the work table with original case IDs and counts as evidence per row.

## Work order
The Roadmap table in `docs/STATUS.md` is the order; take its first row that is not done or blocked. Direction when the table runs out or needs new rows:
- **A1** Runner coverage — multi-file tests, `@module`, `.types`/`.symbols` comparison — so more targets run.
- **A2** Standard library — check enough of es5/es2015 to load them as the default lib instead of `fixtures/minimal.d.ts` (unblocks the targets that need only the library), in sub-steps.
- **A3** From the runner's histogram, close the cheapest family of failing or skipped original cases, one family per PR, stating which case IDs moved to PASS and the new totals.
- **C1** Remove duplicated checker/emitter paths and per-feature `tools/verify-*-emit.ts` scripts where a shared test proves identical behaviour; delete dead code.

## Authority
- Branch prefixes `feat/`, `fix/`, `test/`, `docs/`, `refactor/`, `harness/`; squash merge pinned to head SHA.
- First line of every PR body: "Opened by the tscs scheduled task."
- Other sessions work here. Time without a push is **not** evidence that a PR's executor is gone: it may be running a long local validation or review. Adopt another PR only when all hold: no push, comment or review activity on it for 6+ hours; its author or executor is not shown active elsewhere (recent comments, other open PRs or branches from the same session, a scheduled task that names it); no other writer overlaps its files; and adopting it respects its dependencies and any owner priority. Then say so in a PR comment and treat it as this task's PR under the same merge conditions. Never close someone else's PR — a quiet branch, an anti-sprawl classification or a failing review does not authorise closure; report it instead. Adopting a PR never breaks the one-PR-at-a-time rule. Dependabot PRs: report only.

## Project rules
- The compiler stays C#; Node and Go are reference/dev tools only.
- Never edit `LICENSE`, `NOTICE` or `licenses/`.
- Never change `upstream.lock.json`, `global.json`, the pinned upstream revision, the SKIP registry semantics, protections or release gates, for any reason. No SKIP registration or protection weakened.
- Extra merge condition: every hosted CI check on the head ran and passed (Compiler bootstrap, the original Go reference job, CodeQL if present). Only after a confirmed billing block (jobs with zero steps and a billing annotation) may missing hosted checks be set aside, and then only for changes the local gate fully covers; the billing exception never waives the local gate or any repository protection.

## Docs scope
- No `docs/ROADMAP.md`, `docs/README.md`, `docs/features/` pages or `docs/progress/history-*.md`. The README is the index. Feature detail per area lives in `docs/PORTING.md`, `docs/DEVELOPMENT.md` or `docs/STATUS.md`. Files under `docs/evidence/` are kept records: never rewrite them.
- `tools/check-docs.ts` enforces: relative links and anchors resolve; every `docs/*.md` is linked from `README.md`; `README.md` ≤ 10 KB and `docs/STATUS.md` ≤ 25 KB; headline counts in README and STATUS agree. Never raise a budget to pass.
- Daily housekeeping: fold dated change notes that crept into README/STATUS into current-state text and drop the dates; merge overlapping pages and delete pages for removed behaviour (say which); fix claims the last day's merges made stale; check numbers against the code and the latest harness run; run check-docs and the gate.

## Report extras
Whether the local toolchain worked; original PASS/FAIL/SKIP before → after for each merged PR; README/STATUS sizes after housekeeping.

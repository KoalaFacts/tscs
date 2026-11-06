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
- **Local verification is the default, with a narrowly scoped hosted fallback.** Run the applicable local gate on the final source whenever toolchain and access permit. If the prescribed setup cannot provide the local SDK, network or execution environment, record the actual error and local stages as `NOT_RUN/BLOCKED`, never PASS. The scheduled task expressly permits its existing GitHub-hosted alternative for this environment blocker: complete all applicable checks on the exact PR SHA, including locked reference acceptance and discovery, before normal merge. Hosted evidence must not be described as a local run; permission denials do not authorise bypassing access controls, and repository protections and release gates remain binding. Reuse prior results only for unchanged relevant source, inputs and environment, state what was reused, and rerun affected checks. An early-opened PR stays unmerged until the applicable verification path has passed.

## Gate
1. `./scripts/verify.sh` — what `ci.yml` runs: locked restore, Release build (warnings are errors), all test projects, `go test` for `tools/scope-go`, `node --test tools/*.test.ts`, `tools/check-docs.ts`, then the conformance and emit checks.
2. The rest of `ci.yml`: `scripts/verify-upstream-lock-probe.sh` and `scripts/verify-upstream-full-discovery.sh`.
3. The original-acceptance measurement (the `verify-tscs` headline row: `node tools/original-acceptance.ts ... --expect-headline docs/STATUS.md`) for every PR body's before/after PASS/FAIL/SKIP. Run it locally when possible; if the documented local-environment blocker applies, require the exact-SHA hosted Go reference evidence instead and report local execution as `NOT_RUN/BLOCKED`. When touching the oracle/harness, also run `scripts/verify-original-emptyfile-go.sh` via an authorised available execution path, or explicitly retain its `BLOCKED` status. Do not turn missing evidence into PASS.
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
- Before starting an item, list remote branches newest first (`git branch -r --sort=-committerdate`) and open PRs: a branch from another session on the same item, even without a PR, means that item is taken — pick the next independent one and report the overlap. (A run on 2026-11-06 duplicated A3.3 because `feat/target-esnext` had no PR.)
- Other sessions work here. Time without a push is **not** evidence that a PR's executor is gone: it may be running a long local validation or review. Adopt another PR only when all hold: no push, comment or review activity on it for 6+ hours; its author or executor is not shown active elsewhere (recent comments, other open PRs or branches from the same session, a scheduled task that names it); no other writer overlaps its files; and adopting it respects its dependencies and any owner priority. Then say so in a PR comment and treat it as this task's PR under the same merge conditions. Never close someone else's PR — a quiet branch, an anti-sprawl classification or a failing review does not authorise closure; report it instead. Adopting a PR never breaks the one-PR-at-a-time rule. Dependabot PRs: report only.

## Project rules
- The compiler stays C#; Node and Go are reference/dev tools only.
- Never edit `LICENSE`, `NOTICE` or `licenses/`.
- Do not change `upstream.lock.json`, `global.json` or the pinned upstream revision during ordinary feature work or in this PR. A future upstream-pin change requires separate explicit authorisation and review, retaining old/new differences and pinned reference evidence; this rule alone grants no permission to change it. Never weaken SKIP registry semantics, protections or release gates. No SKIP registration or protection weakened.
- Extra merge condition: ordinarily every hosted CI check on the exact head must run and pass (Compiler bootstrap, the original Go reference job, CodeQL if present). Never infer billing failure from a queued job or zero steps alone. Only when GitHub explicitly confirms billing prevented execution, record the affected checks as `BLOCKED`, not PASS; follow the task's separately authorised billing path with complete applicable local verification, reference evidence and all feasible safeguards. Normal merge remains conditional on existing permissions and unmodified branch protection, required checks and release gates; never bypass a real failure, access denial or mandatory check. A local-environment outage is not a billing exception. The narrowly authorised hosted fallback above applies only while the controlling owner/task instructions permit it; if a later explicit owner instruction requires local execution without fallback, follow that stricter rule.

## Docs scope
- No `docs/ROADMAP.md`, `docs/README.md`, `docs/features/` pages or `docs/progress/history-*.md`. The README is the index. Feature detail per area lives in `docs/PORTING.md`, `docs/DEVELOPMENT.md` or `docs/STATUS.md`. Files under `docs/evidence/` are kept records: never rewrite them.
- `tools/check-docs.ts` enforces: relative links and anchors resolve; every `docs/*.md` is linked from `README.md`; `README.md` ≤ 10 KB and `docs/STATUS.md` ≤ 25 KB; headline counts in README and STATUS agree. Never raise a budget to pass.
- Daily housekeeping: fold dated change notes that crept into README/STATUS into current-state text and drop the dates; merge overlapping pages and delete pages for removed behaviour (say which); fix claims the last day's merges made stale; check numbers against the code and the latest harness run; run check-docs and the gate.

## Report extras
Whether the local toolchain worked; original PASS/FAIL/SKIP before → after for each merged PR; README/STATUS sizes after housekeeping.

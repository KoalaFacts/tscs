---
name: verify-tscs
description: How to exercise tscs for real (original upstream cases against the pinned TS7 reference, the standard library check, the CLI) and what evidence proves a change works.
---

# Verify tscs

The real boundary is an original upstream case from the pinned revision
(`upstream.lock.json`, commit `8d9b6da`) run through tscs by
`tools/original-acceptance.ts` and compared with the pinned reference
baselines. Everything else is a prerequisite or a narrower check.

## Setup

```sh
bash scripts/setup-dev-environment.sh            # exact SDK, Node 24.22.1, Go 1.27.3; builds Release
source "${TSCS_DEV_ROOT:-${XDG_CACHE_HOME:-$HOME/.cache}/tscs/linux-x64}/env.sh"
# One fresh evidence root per session; earlier sessions' evidence is never touched.
ACC="$(mktemp -d "${TMPDIR:-/tmp}/tscs-verify.XXXXXX")"
# Pinned reference checkout + actual Go TestLocal/emptyFile.ts run (network once, ~5 min).
# The script refuses an existing output folder, so pass a child it creates itself.
TSCS_ORIGINAL_GO_RESULTS="$ACC/oracle-go" bash scripts/verify-original-emptyfile-go.sh
U="$ACC/oracle-go/TypeScript"                     # used by the rows below
# Original targets list
node tools/materialize-upstream-placeholders.ts tests/Tscs.Upstream.Placeholders/packed "$ACC/placeholders.zip"
unzip -p "$ACC/placeholders.zip" targets.jsonl > "$ACC/targets.jsonl"
```

All paths below are under `$ACC`; quote them, since `TMPDIR` may contain spaces.

After a source change, rebuild before exercising anything:
`dotnet build Tscs.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false`.

## Feature map

| Feature | Code entry points | How to exercise it for real | Evidence to capture | Neighbours at risk |
|---|---|---|---|---|
| Original upstream acceptance (headline) | `tools/original-acceptance.ts`, `src/Tscs.Cli`, `src/Tscs.Compiler/Compilation.cs` | `node tools/original-acceptance.ts --targets "$ACC/targets.jsonl" --upstream "$U" --tscs src/Tscs.Cli/bin/Release/net10.0/tscs.dll --out "$ACC/report.json" --expect-headline docs/STATUS.md` | headline line `N PASS / N FAIL / N SKIP` (`@lib` is passed as `--lib` and only runs with `@noLib`), `soleMissingCapability`, the `results` entries whose `status` changed (by `upstreamPath`), before and after | README/STATUS headline (the run fails on disagreement), every executed target |
| Standard library parse, bind, resolve (A2) | `DeclarationParser.cs`, `DeclarationBinder.cs`, `DeclarationResolver.cs`, `tools/fixture-harness`, `tools/check-standard-library.ts` | `node tools/check-standard-library.ts --upstream "$U" --harness tools/Tscs.FixtureHarness/bin/Release/net10.0/Tscs.FixtureHarness.dll --out "$ACC/standard-library.json"` | the four summary lines: files clean (116/116), required 20/20, global symbols / merged / diagnostics, unresolved names | parser for user `.ts` (shared scanner), `DeclarationParserTests`, `DeclarationResolverTests` |
| CLI check | `src/Tscs.Cli/Program.cs`, `CommandLine.cs`, `TypeChecker.cs` | `dotnet src/Tscs.Cli/bin/Release/net10.0/tscs.dll --noEmit --noLib --target es2015 fixtures/local/assignment-mismatch.ts fixtures/minimal.d.ts` | exit code (0 clean, 2 diagnostics) and the diagnostic lines, e.g. `TS2322: Type 'string' is not assignable to type 'number'.` | unsupported constructs must report a TSCS diagnostic, never compile silently |
| CLI emit | `JavaScriptEmitter.cs` | `E="$(mktemp -d "$ACC/emit.XXXXXX")" && cp fixtures/local/number-ok.ts "$E/" && dotnet src/Tscs.Cli/bin/Release/net10.0/tscs.dll --emitJs --noLib --target es2015 "$E/number-ok.ts" fixtures/minimal.d.ts` (a fresh folder per run: the CLI refuses to overwrite an existing `.js`, TSCS1002) | the written `.js` next to the input (`"use strict";` then `let count = 1;`: every script gets the pinned prologue) | original `.js` baselines (four-space blocks), `tools/verify-*-emit.ts` executed modules |
| CLI emit, `--target esnext` | `Options.cs` (`ParseTarget`), `JavaScriptEmitter.cs` (`Lowers`) | the CLI emit row with `--target esnext`, then the same with a file containing `let a: number \| undefined = undefined; let b = a ?? 1;` | identical `.js` to es2015 for the first; exit 1, `error TSCS1002: --target esnext output with ?? is not implemented` and no `.js` for the second | every ES2015 lowering site (`??`, `**`, `**=`, logical assignment, optional chaining, object spread, numeric separators) |
| Isolated Go-oracle cohort | `tools/verify-conformance.ts`, `tests/conformance` | `node tools/verify-conformance.ts` | `Differential cohort: 29/29` | earns no original credit; do not cite it as acceptance |
| Pinned Go reference run | `scripts/verify-original-emptyfile-go.sh`, `tools/verify-original-go-case.ts` | the Setup command above | the line `Original pinned Go TestLocal/emptyFile.ts completed; tscs product-side original acceptance remains NOT_RUN.`, exit 0 | reference provenance (`upstream-ref.txt`, `upstream-sources.sha256`) |
| Docs truth | `tools/check-docs.ts` | `node tools/check-docs.ts` | `check-docs: ok`; README ≤ 10 KB, STATUS ≤ 25 KB, matching headline counts | every PR that edits README or docs/ |

## Gate

```sh
./scripts/verify.sh        # restore, warnings-as-errors build, xUnit (--fail-skips on), Go scope test, node --test, check-docs, emit checks
```

Plus, when the compiler, harness or oracle changes: the two reference rows
above (acceptance with `--expect-headline`, standard library check). CI also
runs `verify-upstream-lock-probe.sh` and `verify-upstream-full-discovery.sh`.

## Traps

- The 15,523 xUnit placeholders (packed in `tests/Tscs.Upstream.Placeholders/packed`,
  materialized only by `verify-upstream-full-discovery.sh`) are
  `SKIP_UNIMPLEMENTED` registrations; a green xUnit run says nothing about
  original acceptance.
- The Go `emptyFile.ts` PASS is the reference running, not tscs passing.
- The 29-case cohort and `tools/verify-*-emit.ts` checks are bounded fixtures
  written for tscs; they are regressions, not upstream cases.
- `0 unresolved names` in the library check is name resolution only; it is
  not type checking and not default-library loading.
- A target is PASS only if the runner actually compared it with the pinned
  baselines; never edit the targets list, SKIP registry or headline to move
  the number.
- The Codex usage-limit comment on a PR is not a review.
- The acceptance runner is fast (about 1 s) because almost every target is
  SKIP; a quick green run is expected, not suspicious.

## Last verified

2026-11-06 on `feat/target-esnext-2` (main `00fde7b`): acceptance 4 PASS / 0 FAIL /
15,519 SKIP (adds `arrayIterationLibES5TargetDifferent(nolib=true,target=esnext)`);
CLI emit esnext row as shown. Earlier: 2026-11-06 on `feat/array-iteration-nolib` (main `2cfd095` merged): acceptance
3 PASS / 0 FAIL / 15,520 SKIP (`parser509698`, `noCrashOnNoLib`,
`arrayIterationLibES5TargetDifferent(nolib=true,target=es2015)`); CLI check
and emit rows as shown (script output now starts with `"use strict";`);
`./scripts/verify.sh` 19,780 xUnit passed. Earlier: 2026-11-06 on `61dfa09`
library 116/116 clean, 20 required files, 2,250 global symbols (791 merged),
0 diagnostics, 0 unresolved names; cohort 29/29; Go `emptyFile.ts` completed.

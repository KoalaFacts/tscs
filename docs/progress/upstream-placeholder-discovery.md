# Source-configuration placeholder discovery reconciliation — 2026-10-27

This is a **separate, optional xUnit 4 / MTP 2 discovery lane** for the existing 15,523 source-configuration placeholders, not full TS7 compiler acceptance. The main `scripts/verify.sh` continues to run its internal regression tests with `--fail-skips on` and the existing minimum test count.

## Implemented in this work package

- `tools/verify-upstream-discovery.ts` accepts a *real* CTRF test execution report and the original `targets.jsonl`. It requires a perfect one-to-one correspondence between all 15,523 stable source/configuration IDs and discovered skipped test names, including the original upstream path/configuration suffix. Missing, duplicate, unknown or renamed tests, forged summaries, unexpected PASS/FAIL/pending outcomes and metadata loss fail closed.
- `tools/verify-upstream-discovery.test.ts` contains **11 actual negative/positive Node tests**. Existing source-bound bundle verification runs again in the CLI before trusting the execution result. The separate shell entry `scripts/verify-upstream-placeholder-discovery.sh` sets .NET/MTP opt-outs, preserves restore/build/run logs, requests an xUnit 4 CTRF report and checks the real discovered test statuses with the Node verifier. Only this separate lane uses `--fail-skips off`; a skip is reported as SKIP, never as PASS.
- The shell runner requires a **reviewed `packages.lock.json`** for the generated standalone project before `dotnet restore --locked-mode`, deliberately failing with code 3 rather than performing an unpinned restore. The historical archive contains no such lock file. Its preparation and a .NET SDK execution are still necessary.

## Reproduction

Extract the immutable `tscs-upstream-test-placeholders-2026-10-26.zip` from Library ID `libfile_00d083171c708191a3a2affd64acfeff` into a **new** directory. The archived ZIP is source-bound to pinned TypeScript `8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f` and must not be silently regenerated with a different denominator.

```sh
node --test tools/verify-upstream-discovery.test.ts
# When a reviewed packages.lock.json and .NET 10 SDK are available:
./scripts/verify-upstream-placeholder-discovery.sh /path/to/extracted-bundle /path/to/new-results
# To validate an existing genuine MTP-generated report:
node tools/verify-upstream-discovery.ts /path/to/extracted-bundle /path/to/discovery.ctrf.json
node tools/verify-upstream-discovery.ts /path/to/extracted-bundle /path/to/discovery.ctrf.json --release
# The last command must fail while any registered SKIP remains.
```

## Verification and limitations

**Actual local Node 22.16.0 experimental strip-types fallback:** initial 10-case RED had 10 failures, then 10 passed; a separate source/configuration-name forgery test failed before repair, then all **11/11 passed**. Independently produced **synthetic CTRF input** representing all 15,523 previously archived IDs passed the ledger reconciler and the release mode deliberately rejected 15,523 unresolved SKIPs. This synthetic input is **not** a discovered .NET test report.

Shell syntax check passed, and a mocked pre-verifier confirmed exit **3/BLOCKED** on the historical bundle because `packages.lock.json` is missing. This is **not** a successful original .NET compile, restore, MTP discovery or upstream compiler run.

**NOT_RUN:** .NET SDK / MTP execution of the 63-shard generated project; creation/verification of its standalone NuGet lock; original TS7 compiler/reference differential; full upstream runtime test expansion; independent reviewer. Existing 14 local capability probes remain 7 IMPLEMENTED/upstream NOT_RUN + 7 SKIP(UNIMPLEMENTED), separate from these 15,523 SKIP(ORIGINAL_RUNNER_END_TO_END_ASSERTIONS).

Archive of actual local RED/GREEN/synthetic/blocker logs: Library `libfile_f7edf4f7b3d8819184d92ba0ed5cbe8f`, SHA-256 `d3bb6861ddf8dd3073f8acd95d7ca415f307d098314a390be63c2613a2076001`. Codes and script source Git blob identities in `docs/evidence/upstream-placeholder-discovery.json`.

**Delta:** upstream compiler accepted cases +0; actual compiler capabilities +0; removed SKIP IDs 0. The working discovery reconciliation/check is implemented; the .NET execution itself is still blocked. Release remains ineligible.

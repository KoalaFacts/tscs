# Full source-configuration MTP discovery from a Git-backed archive — 2026-10-27

The goal is **actual xUnit4/Microsoft.Testing.Platform 2 discovery** of all **15,523 previously identified upstream source/configuration targets** in a standalone test assembly, not more parameterized helper tests or compiler acceptance claims.

## Durable, hash-locked source bundle

The unchanged historical archive `tscs-upstream-test-placeholders-2026-10-26.zip` (SHA-256 `e45e033d458a318b792777f7d141973e5d457f4f9e1f04801cb4bcf3a6e4512b`, length **3,544,490 bytes**) is tracked in this PR as twenty byte-exact binary fragments under `tests/Tscs.Upstream.Placeholders/packed/part-00.bin` … `part-19.bin`. The archive's original 63 generated C# test shards, project, manifest, all source-bound IDs, and historical generation evidence have not been rewritten. Retaining the ZIP rather than checking in 63 separately edited generated shards keeps the generator's exact original evidence and avoids silent drift. No network fetch or temporary Library dependency is needed to reproduce it in CI.

`tools/materialize-upstream-placeholders.ts` requires precisely the ordered, expected 20 fragments. It refuses duplicate/missing/extra parts, checks the complete original byte count and SHA-256 **before writing** the materialized archive, and never overwrites existing evidence. `tools/materialize-upstream-placeholders.test.ts` tests exact concatenation, omission, insertion, corruption, truncation, oversized data and overwrite protection. The shell entry `scripts/verify-upstream-full-discovery.sh` materializes and unpacks that verified archive into a new `.artifacts/upstream-full-discovery/` working directory. It then invokes the existing standalone xUnit4/MTP2 runner, which validates all source and generated-Fact bindings, copies the exact reviewed 16-package NuGet lock only into the extracted working copy, performs **locked** restore, Release build and real CTRF test execution, and reconciles **every** observed test name and SKIP outcome to the original 15,523 stable IDs.

The CI job invokes this lane **after** the original strict internal `scripts/verify.sh` and one-source real MTP canary. `--fail-skips off` is limited to the separately reconciled upstream placeholder project; the internal `--fail-skips on --minimum-expected-tests 15488` is unchanged. Failed/partial MTP execution, missing or duplicated targets, lost upstream path/config/name, any PASS/FAIL returned by an unimplemented stub, and mismatched summary totals all block this additional CI step. The generated full scope is still configuration-level, not the dynamically expanded runtime TS7 denominator. Complete original compiler/referenceCompiler assertions remain NOT_RUN and the release gate remains ineligible.

Hosted CI uploads raw archive/restore/build/CTRF/discovery/reconciliation logs as an Actions artifact, including failures if the step ran. It does not upload ephemeral restored packages or 26 MB of uncompressed generated sources.

## Validation and remaining scope

Local Node **22.16.0** with experimental type stripping: six materialization regression tests passed, and a separate full 20-part reconstruction produced exactly the original ZIP SHA-256; `unzip -t` accepted all 74 entries and reported no compressed-data errors. Shell `bash -n` passed. This local container lacks .NET 10, so **actual 15,523-Fact build/CTRF discovery is NOT_RUN locally**; only the new exact-head hosted run can supply that evidence. There is no independent reviewer and the PR is not merged.

Totals remain 13,006 source identities, 15,523 configuration targets (15,495 compiler/28 transpile), 7 blocked source configurations, 42 upstream-skipped source flags (39 target metadata flags), and **15,523 SKIP(UNIMPLEMENTED)** for the original end-to-end runners until genuine original assertions exist. This package changes **0 compiler capabilities, 0 accepted upstream tests and 0 SKIP removals**. Additional Go/JS/Fourslash/API/build/watch and runtime profile expansion remains **UNEXPANDED** and must not be dropped from the release target.

## Hosted result verified 2026-10-27

This stage is now confirmed by actual hosted .NET SDK/MTP test execution:
at exact head \`51993ce5fa451a3dcfe71bab47953f842e8e1cda\`,
[run 40358477859](https://github.com/KoalaFacts/tscs/actions/runs/40358477859)
and [run 40358486127](https://github.com/KoalaFacts/tscs/actions/runs/40358486127)
each performed a successful locked Release build, discovered all
**15,523 named Facts**, and reconciled **15,523 SKIP(UNIMPLEMENTED),
0 PASS, 0 FAIL** with original paths/configuration IDs. A separate
one-source discovery canary reported 1/1 SKIP. The existing internal lane
still ran strictly with 15,500/15,500 PASS and zero skip. This supersedes
the older head's hosted NOT_RUN status only; the original upstream compiler
acceptance is still NOT_RUN and the full runtime denominator remains unknown.

Raw hosted discovery/CTRF logs are available in workflow artifacts
12667661634 (\`sha256:37663778c0d041e4de837b3fe9e8ca648e4f0bac4de8d4baeb0cdda2f2ad8aa9\`)
and 12666817064 (\`sha256:01d9dea88b930c24b74a2e69d67cee997972e116538cbca8e46445a5e6e0ede8\`).
Artifacts are subject to retention; the exact full result counts, run/job
IDs and SHA digests are repeated in
[evidence](../evidence/ts-ignore-directive.json). No original upstream
execution PASS was counted.


# Source-configuration placeholder backlog — 2026-10-26

The user's priority is inventory first, then implement against stable targets, not a 14-probe proxy. `tools/upstream-placeholders.ts` now generates all **15,523 named/fallback configuration targets** in the retained source-configuration snapshot: **15,495 compiler / 28 transpile**. All **13,006 source identities** remain accounted for, including **7 reference-fatal sources** in a separate blocker list and **42 upstream-skipped sources** (39 configuration-target flags). No local regression is disabled.

## Deliverable

The generator binds the exact input lengths/SHA-256 and upstream pin, then emits `targets.jsonl`, 63 C# files containing **15,523 individually named xUnit Fact placeholders**, `blocked-sources.json`, a standalone xUnit4/MTP2 project and an artifact hash-indexed summary. Each target retains its stable ID, original upstream path/hash/configuration and missing runner. Every Fact has an explicit `Skip = "UNIMPLEMENTED: ..."` and throws `NotImplementedException` if enabled without implementing its body. No empty passing tests.

Input archive: `libfile_bc0f70bc02cc8191bb14c3b962ff0072`, `tscs-source-configurations-evidence-2026-10-25.tar.gz`, SHA-256 `4a11e9a0452607285bb7e453db0276f656c3bfc47bcd84e4daff5d18efae4835`. Use `source-config-verified-repeat/results.jsonl` and `summary.json` from that archive:

```sh
node tools/upstream-placeholders.ts RESULTS_JSONL SUMMARY_JSON NEW_OUTPUT_DIRECTORY
node --test tools/upstream-placeholders.test.ts
```

Complete generated output and raw RED/GREEN/generation/reconciliation logs are persisted as Library file `libfile_00d083171c708191a3a2affd64acfeff` (`tscs-upstream-test-placeholders-2026-10-26.zip`, 3544490 bytes, SHA-256 `e45e033d458a318b792777f7d141973e5d457f4f9e1f04801cb4bcf3a6e4512b`). The full target ledger SHA-256 is `c1c4077d732ae859ad70899ac3fd85fc25f9eff1c99737f3e36aa806b5c2742e`. The input contract is immutable; a new source snapshot requires a reviewed change, not a silent denominator reset.

## What the statuses mean

The missing implementation is the **original pinned compiler/transpile runner's end-to-end assertion pipeline**, not a claim that every language feature in every source is absent. `productFeatureImplementation` remains `UNASSESSED`; compiler/referenceCompiler/acceptance remain `NOT_RUN`. Existing local implemented features and the 29-case frozen cohort are not reclassified. Inputs with prior compiler/reference/acceptance results are rejected, never downgraded to SKIP.

This is a source-configuration work-target catalogue, **not the complete runtime TS7 denominator**. Output/run-kind variants, Go/JS dynamic tests, Fourslash/API/build/watch and environment profiles still require their own stable placeholders/expansion. Preserve this catalogue while adding those domains. The 14 local probes must not substitute for the full backlog.

## Verification and limits

Local Node22.16.0 experimental strip-types fallback: stub RED **8 failed** -> implementation GREEN **8 passed**. Target runtime remains Node24. Full generation ran twice; a separate Python reconciliation by the same author checked every generated ID/source/configuration, all 15,523 skipped throwing Fact bodies and all 68 output files byte-for-byte. This is self-verification, not an independent review.

No local .NET SDK was available; cloning the full repository failed DNS. The generated C# project is **not yet compiled/discovered/executed**; the repository-wide existing suite was **not run locally**. This commit leaves `--fail-skips on` and the existing regression lane unchanged. Next validate a separate placeholder discovery/verification lane, reconcile every discovered SKIP ID, and retain strict release blocking. Do not disable fail-skips globally. Then enable targets only when their original assertions and product path exist, preserving genuine FAIL/MISMATCH/CRASH/TIMEOUT.

New product language capabilities: 0. New full-upstream acceptance: 0. Concrete deliverable: the source-bound test skeleton/backlog, no longer only a 14-item sample.

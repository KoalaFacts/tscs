# Independent read-only review receipt

Repository: `/workspace/scratch/daf3e6c2e318/tscs`
Base HEAD: `47b245cd68f1045a0c6d9348368838d1572758ab`
Reviewed: uncommitted `tools/source-config-values.ts` and `tools/source-config-values.test.ts` diff, captured in `reviewed.diff`.

## Verification

- Focused `node --test tools/source-config-values.test.ts`: 11/11 pass (`focused.log`).
- Full offline tool suite `node --test tools/*.test.ts`: 86/86 pass (`full.log`). Used supplied pinned Go/.NET PATH and offline environment.
- Independently verified all three pre-existing historical raw profiles: `.artifacts/source-config-values-verified`, `.artifacts/source-config-values-verified-repeat`, `/tmp/tscs-source-values-review/profile`. Counts remain 13,006 sources, 172 selected configs, 171 MATCH, 1 MATCH_DIAGNOSTICS, no mismatches/unsupported/crashes (`historical-and-mutations.log`).

## Important findings

1. P2, `tools/source-config-values.ts:64`: reference response echo fields are not bound to parse requests. On a copied historical profile, changed `oracle/reference.json[0]` id, sourceBytes, fileName to foreign values; copied that reference into golden; recomputed results and outer/nested hashes. Verification accepted it. Require reference.id/sourceBytes/fileName to equal parseRequests, plus regression coverage.
2. P2, `tools/source-config-values.ts:68-69`: diagnostic and selected-source counters remain trusted. Independent report-only mutation of parserErrorConfigs, parserDiagnostics, conversionDiagnostics, selectedUpstreamSkipped to 999 is accepted. Recompute those from references/vectors and check all applicable summaries. upstreamSkippedSources and nonRawUtf8Sources also accept 999; full-source metadata is not retained in this profile, so complete independent recomputation of those two is unavailable without more evidence. At minimum bind duplicated summary metadata and accurately state the limit.

`review.ts` contains native TypeScript reproduction and historical verification; `mutation-profile` is a copied profile used for mutations. Original historical profiles and repository tracked content were not edited. Review establishes no original compiler execution, new config feature, or formal approval. The two findings were sent to the parent agent before corrective work.

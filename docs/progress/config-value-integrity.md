# Config value evidence omission P2 — 2026-10-26

Review of47b245c demonstrated that verifySourceConfigValueArtifacts accepted
artifacts:[] and could lose oracle/reference.json together with its manifest
entry. This was a verifier defect, not evidence that the published raw profiles
were corrupt. The fix requires the complete twelve base artifacts plus the
exact32-request batch shard/log inventory derived from selected config count.
Each file hash/length and nested six-file oracle manifest is checked. Calls,
row counts/ordered IDs, selection/input/context/reference bindings, golden,
recomputed results and summary outcome counts must agree. No-config totals are
also reconciled. An internally consistent manifest is not independent proof of
reference authenticity; the actual-Go generation/source provenance is separate.

Tests exercise a complete synthetic producer fixture, then omit every required
entry+file, empty the manifest, reduce calls with updated hash, repeat shard IDs
with updated hashes, forge outcome totals, and truncate a shard. Genuine initial
RED4fail/6pass; an additional forged-counter regression RED1fail/10pass. The first
implementation check exposed a nested-path mistake in the test fixture; that
failed attempt is retained and excluded. Corrected fixture matches the producer.
Final focused14/14, full Node89/89, directMTP9126/9126, Release0 warnings/errors;
SDK coordinator NOT_RUN. Three original172-config profiles pass the strengthened
verifier without changed payloads (171MATCH/1MATCH_DIAGNOSTICS each). They remain
historical executions, not this run's Go rerun or new compiler acceptance.

[Evidence](../evidence/config-value-integrity.json) retains raw red/green logs
and independent review limits. Existing original compiler/reference/acceptance
NOT_RUN, seven prior reference fatals, all domains and unknown denominator remain;
releaseEligible=false. Next after review closure: config host and declaration/
notifier option conversion, not another repeat of the source value matrix.

Independent review reran the initial11 focused tests, full Node86 and three
historical profiles. It found two remaining P2 gaps: missing reference echo
binding and unchecked diagnostic/skip counters. Author corrective regressions
RED3fail/11pass ->14/14 focused and89/89 full Node. Direct reference input echoes
and derived diagnostic/selected-skip counters now bind; full-source skip/encoding
counts are checked for agreement across report/golden/oracle, not independently
reconstructed from selected-only data. The original generator/provenance remains
responsible for those full-source counts. No second independent review is claimed.

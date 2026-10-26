# Independent read-only review — JSON option map

Reviewed base `aeffd714012c6afba07e555e4f74c522df24f1bb` through implementation
`ca49842f681c6914e84b11f7073f44d1b26877be` on 2026-10-26. The pinned upstream is
`8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f`.

Verdict: no Critical, Important, or Minor implementation findings. The bounded
ParseOption-recording change is consistent with its plan and supported scope.
No production/test source files were edited by this reviewer. Author documentation,
status and minimum-count edits made after ca49842 are outside this implementation
review. No merge, publication or remote message was performed.

## Static review

Read the complete base-to-HEAD change and docs/progress/json-options-map.md; checked
actual pinned convertOptionsFromJson, commandLineOptionsToMap/Get, unknown-option
errors, core.GetSpellingSuggestion/levenshteinWithMax, convertToObject and scalar,
list, enum and lib semantics against the production implementation and existing
converters. In particular:

- All 126 actual declaration entries remain present (125 names, duplicated help),
  with 111 supported entries and 15 explicitly unsupported path/locale entries.
- Canonical case is required. Wrong-case keys emit a diagnostic without ParseOption;
  unsupported declarations still participate in spelling suggestions.
- Ordered object conversion replaces duplicate values at the original key position;
  recognized supported options record nil error values and retain diagnostics.
- The actual declaration inventory and former scalar declaration positions agree
  by name and kind for all 126 generated declaration-position inputs.
- Fixture preparation supplies the real parsed AST. Parser diagnostics influence
  the outcome; unsupported parsing/preparation/options never earn a clean match.
- The reconciler checks selection/context, parser diagnostics, unsupported keys,
  full calls/options diagnostics and outcome; its negative tests reject omissions
  and false clean results. Unsupported maps keep both complete Go reference data
  and partial product output without receiving matching credit.
- Historical source cohort binding checks its fixed SHA, source/blob/raw bytes,
  exact request input, filename and stable IDs. This is fresh parser/map execution
  on historical selected inputs, not a fresh full-source selection.
- Current 316 rows have empty convertToObject conversionDiagnostics arrays. The
  profile's supported grammar and existing rejection boundary were examined;
  no claim is made for the general upstream grammar/recovery implementation.
- The original compiler acceptance domains and reference failures are untouched.

## Independently executed verification

Every build/test/generator command used `source /tmp/tscs-options-env`: Go telemetry
was verified off; GOPROXY/GOSUMDB off and GOTOOLCHAIN local; explicit .NET/MTP
telemetry opt-outs, empty restore feed and NuGetAudit=false. See environment.log.

1. `TSCS_STRING_LOG=.artifacts/review-options-map/go.log node tools/generate-json-options-map.ts .artifacts/ts7-upstream .artifacts/review-options-map/golden.json .artifacts/review-options-map/declarations.json`
   regenerated 316 vectors / 172 source inputs / 126 declarations / 111 supported.
   Both outputs compare byte-for-byte equal to committed golden and production
   resource via cmp. Logs: generate.log, go.log, regeneration-comparison.log.
2. `dotnet build Tscs.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false`
   passed serially, zero warnings and zero errors. Log: build.log.
3. `dotnet tests/Tscs.Compiler.Tests/bin/Release/net10.0/Tscs.Compiler.Tests.dll --fail-skips on --minimum-expected-tests 15426`
   passed 15,426/15,426, zero skipped. Log: tests.log.
4. `node --test tools/*.test.ts` passed 95/95, zero skipped. Log: node-tests.log.
5. `node tools/verify-conformance.ts` passed the separate frozen 29/29 cohort;
   inventory 12,981 files, 90 unresolved variant files. Log: frozen.log.
6. `node tools/compare-source-config-options.ts .artifacts/ts7-upstream .artifacts/review-source-options-map`
   produced 90 MATCH, 8 MATCH_DIAGNOSTICS, 74 UNSUPPORTED and zero mismatch/crash/
   timeout/skipped/not-run. Exit 1 is intentional for diagnostics/unsupported
   results. Log: source-profile.log. The six batches contain 32/32/32/32/32/12 rows.

The committed RED log was inspected: 490 failures, 14,936 passes, zero skipped.
It was not independently rerun at the stub commit. New passing coverage comprises
316 map rows + 172 source-profile rows + 2 missing/unsupported-config guard rows.

## Independent profile integrity and comparison

All 19 artifacts listed by the independent summary were reread and their SHA-256
and byte lengths verified. Compared to .artifacts/source-options-map-final, 18
artifacts are byte-identical: oracle.json, declarations.json, generator.log,
requests.jsonl, actual-calls.json, results.jsonl, and all six actual JSONL outputs
and six stderr logs. Only go.log differs, in the Go test elapsed-time text.
See profile-integrity.json for the exact artifact list.

summary.json is metadata, outside that 19-artifact comparison. Both sourceCommit
fields are ca49842f681c6914e84b11f7073f44d1b26877be. The independently rebuilt
FixtureHarness and Compiler DLL hashes differ from the author's pre-review DLL
hashes; they are recorded separately in summary-metadata.json. No identical
assembly or identical whole-summary claim is made. Behavioral JSONL payloads are
identical despite those separate assembly identities.

## Limits

This establishes the bounded convertOptionsFromJson/record-ParseOption boundary.
It does not establish typed/effective CompilerOptions, defaults, existingOptions,
notifier, config host, external-code execution, extends, globs, full 13,006-source
selection, or original compiler/host acceptance. The 74 source unsupported outcomes
and all selected upstream-skip flags remain visible. The whole verify.sh and SDK
test coordinator were not run; direct MTP execution is reported separately.
No path/locale support or acceptance beyond the declared boundary is inferred.

Raw review logs and this receipt are under .artifacts/review-options-map/.
Independent source-profile data and its integrity summary are under
.artifacts/review-source-options-map/.

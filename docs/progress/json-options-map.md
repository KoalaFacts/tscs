# Declaration-driven configuration option map — 2026-10-26

Plan/spec: implement the pinned convertOptionsFromJson iteration and dispatch to
existing scalar, list, enum and lib converters using an actual-Go-generated
126-declaration inventory. Preserve canonical case, spelling suggestions, ordered
last-value duplicate semantics and all diagnostics, including nil error values
passed to ParseOption. Record ParseOption calls rather than pretending this is
core.CompilerOptions construction. Path/locale policies remain explicitly
UNSUPPORTED and registered, never silently accepted or removed.

Use existing172 selected source-config requests/identities as an input cohort;
verify each upstream blob/raw hash, bind selected config bytes/name/context to
that historical selection, then freshly execute real Go parser/convertToObject/
convertOptionsFromJson. This does not freshly rerun all13006 source selection.
Connect product conversion to the harness config-options profile. Syntax errors,
unsupported parse/options, no config and diagnostic outcomes remain separate.
No notifier, host, existingOptions/defaults, runExternalCode execution, extends,
globs, typed CompilerOptions, effective config or original compiler credit.

Implementation steps (native execution, user already authorized routine choices):
- [x] Generate actual declarations and whole-map Go outputs for selected sources
      and bounded mixed-order/unknown/case/duplicate/null/nonobject scenarios.
- [x] Add production-entry tests and genuine stub/protocol RED, retaining raw logs.
- [x] Implement descriptor dispatcher/map/suggestions and harness integration.
- [x] Run serial offline Release/directMTP, Node/frozen, repeated Go generation.
- [x] One fresh independent review, any important corrections RED→GREEN, save
      evidence/status and publish to original branch with expected SHA/tree check.

Review focus: duplicate canonical keys; case mismatch must not ParseOption;
unknown suggestions include unsupported declarations; error values still recorded;
source syntax diagnostics cannot become clean acceptance. Tests exercise each.
All seven reference fatals/domains/unknown concrete denominator/releasefalse stay.

Executed316 whole-map Go inputs:172 selected historical source configs,18 mixed
scenarios and126 declaration-position inputs (125 distinct names; help appears
twice).111 supported declarations and15 registered unsupported path/locale
policies.226 complete comparisons (204 clean,22 diagnostic-bearing);90 inputs
retain unsupported policies and full Go results, without a match claim.

C# genuine stub/protocol RED490fail/14936pass ->15426/15426, zero skips.
Node reconciliation stub RED5fail/1pass ->6focused/6 and95/95 total. Serial
Release0 warnings/errors and frozen29/29. First generator attempt rejected
repeated help ID; fixed by including declaration position, original excluded
binding log retained. No oracle failure, TDD RED or compiler failure claimed from
that producer rejection. Initial baseline was a direct existing-binary run without
explicit telemetry environment; it is excluded from final validation. All new
build/Go/MTP/Node verification uses explicit telemetry opt-outs and offline feeds.

Fresh source JSONL172:90 MATCH,8 MATCH_DIAGNOSTICS,74 UNSUPPORTED, zero mismatch/
crash/timeout. All3 selected upstream skips remain flagged, no original compiler
acceptance. Every supported comparison checks raw calls, compiler diagnostics,
parser diagnostics, exact selection/context and outcome. Unsupported results
retain partial C# calls/diagnostics alongside complete Go output and cannot match.
19 immutable raw artifacts/profile (six shards (five32-request, one12-request) and logs); post-completion
length/SHA checks pass. Two final Go regenerations match golden/resource bytewise.

No new CLI options, no full13006 selection rerun, no typed CompilerOptions/defaults/
existingOptions/host/notifier/runExternalCode execution/extends/globs/effective
configuration or original compiler acceptance. SDK coordinator/wholeverify.sh
NOT_RUN. All domains, seven reference fatals and unknown denominator retained.

Next: implement the registered path/locale conversion policies against pinned
path/nativepath behavior, then typed options/defaults/notifier/host. Do not repeat
316 maps or172 already-bound input rows as new work.

One independent read-only review of ca49842 found no Critical/Important/Minor
implementation finding. Actual Go golden/production resource byte-identical;
serial Release0 warnings/errors, directMTP15426/15426, Node95/95 and frozen29/29.
Independent source profile reproduces90/8/74 with19 verified artifacts;18 payloads
are byte-identical to author-final, Go timing log differs. Per-run DLL hashes and
summary metadata remain separate, not a reproducible-build claim. No second or
formal GitHub approval. Author documentation/minimum/status updates are outside
reviewed implementation range; no product edits after review head.

Complete raw profiles and preliminary-profile history are preserved in
[tscs-source-options-map-evidence-2026-10-26.tar.gz](https://chatgpt.com/library?file_id=libfile_01fe35b6091c8191a274e7c06d8d9852&account_id=personal),276358bytes,
SHA25687ff05abdb34b83743c525a599f609e40ec6da6a03bea6ff71174ca01efdae20.
55 raw payloads/37 standard tar hardlinks are storage deduplication only; all
archived bytes and19 artifacts per profile verified. Five identical Git-backed
oracle/declaration payloads are omitted with exact path/SHA/Gitblob IDs for
recovery; final Git golden/resource provide those bytes. Preliminary profile
predates input-provenance hardening and is excluded as final-source proof.
Source, tests, golden and raw author/reviewer logs are committed separately.
[Machine evidence](../evidence/json-options-map.json).

## Follow-up: diagnostic invalid-sequence P2 — 2026-10-26

Review identified that the raw UTF-16 unknown key could be serialized with
separate U+FFFD characters for adjacent unpaired surrogates, while pinned Go
`diagnostics.Format` uses `strings.ToValidUTF8` and collapses a contiguous
invalid-byte run into one replacement. The product now normalizes only its
formatted argument; raw keys, canonical lookup and weighted spelling inputs
are unchanged. Six focused tests exercise direct conversion plus fixture JSONL
transport: consecutive high, consecutive low, low/high, separated runs,
valid high/low pair and invalid/valid/invalid sequences. The prior 316-row
oracle and 172-source profile are historical; the newly added cases are not
presented as a newly executed pinned-Go private-converter run. Hosted tests
must be checked at the exact new commit before this correction is accepted.

## Rooted-path dispatch and TS5116 UTF-8 diagnostics — 2026-10-26

The follow-up commits add the rooted/nonrooted lexical path policies and
command-line locale rejection for all 126 pinned option declaration positions.
The original 316 actual-Go outputs and 172 historical selected source config
inputs are retained without rewriting their original provenance. The new
production tests require the complete pinned option-map values and diagnostics,
including formerly unsupported path outputs. This does **not** constitute a
fresh execution of the complete 172-source differential or the original TS7
compiler/reference/acceptance profiles.

A separate review of the fixed Go formatter found the TS5116 diagnostic
argument must collapse contiguous malformed WTF-8 bytes as the existing
unknown-name diagnostic formatter does. Four targeted xUnit cases build
unpaired-surrogate input at runtime (InlineData cannot preserve malformed
UTF-16 in attribute metadata) and preserve a valid surrogate pair. The raw
path still determines network rejection; only display text changes. This
fix is in `efb2c56ccee623b83aaeaab3a56a03dbd1dda098`.
Exact-head hosted GitHub logs confirm 15,450/15,450 MTP, Node95/95,
frozen29/29, Go-scope pass and all four check-runs successful.
See [machine evidence](../evidence/network-path-diagnostic.json) and raw
[verify logs](../evidence/network-path-diagnostic-run/). No claim of local
Go1.27 private-converter rerun, SDK test-coordinator local execution, typed
CompilerOptions, effective tsconfig, full TypeScript coverage or release.

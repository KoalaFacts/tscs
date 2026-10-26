# Lib enum-list integration — 2026-10-26

Plan: connect actual lib EnumMap to the existing production list/enum converters.
Generate the real descriptor/map from pinned private Go, retain all126 option
identities, test all library aliases and uppercase names plus list boundary cases
in compiler-only/source-node contexts. Preserve original index/null/nil/filtering
semantics and diagnostics. Stub/unsupported RED, minimal integration, full checks,
repeat both generated artifacts, independent review, save all source/log evidence.
This converts lib names to filenames only; no standard-library loading or new CLI
lib acceptance, host/effective options or original compiler acceptance is implied.
Seven reference fatals, all domains, unknown denominator and releasefalse remain.

Executed504 actual-Go cases:116 library aliases ×raw/uppercase array forms plus20
boundary literals, each with/without source nodes. Every value and diagnostic
matches; production descriptor/map is generated from actual Go and embedded.
JsonListOption adds an optional enum element, reusing existing filtering/indexing;
old primitive-element and six enum regressions remain unchanged and passing.

Actual unported RED486fail/14450pass ->14936/14936 (zero skips), serialRelease0
warnings/errors, Node89/89, frozen29/29. After RED, the protocol deduplicates the
full repeated descriptor into shared inventory; all504 other row fields and the
entire inventory were deep-compared equal. No assertion/case removed. The original
12801425-byte RED data is retained as red-full-golden.json.gz.b64, losslessly
base64+gzip encoded; decode then gunzip to restore its exact bytes (SHA256 in
red-data-dedup.json). Final288113-byte golden and production descriptor repeat
byte-identically, metadata binds the final probe/generator schema.

No original13006/172/source/VFS profile rerun. The existing172 selected config
value ledger contains no lib property (static inspection only, not fresh oracle
execution). No source-fixture/compiler acceptance is claimed from this cohort.
SDK coordinator/wholeverify.sh NOT_RUN. Original compiler/referenceCompiler/
acceptance NOT_RUN, all domains/seven reference fatals/unknown denominator retained.

Next: declaration-driven dispatch/options map and actual source config preparation,
then unsupported path/locale policies, notifier/host/effective configuration and
compiler AST. Do not repeat504/324/160 cohorts as new progress.

One independent read-only review found no Critical/Important/Minor finding.
Fresh Go504 and production descriptor byte-identical; offline serialRelease0
warnings/errors and directMTP14936/14936, zero skips. Verified original RED
bytes/hash and all504 row/non-descriptor fields,126 inventory and every old
descriptor against final inventory. No corrective pass or second/formal approval.
Historical RED/Node/frozen not independently rerun.
[Evidence and all raw logs](../evidence/json-lib-options.json).

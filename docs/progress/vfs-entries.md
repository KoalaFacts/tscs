# VFS accessible entries — 2026-10-25

Base f5ba422, integrated main919bc4b (PR5's evidence correction only). Port the
case-sensitive Common.GetAccessibleEntries boundary into the existing snapshot.
The fixed upstream is the spec, with actual Go execution supplying complete
request/output goldens. No change to original compiler acceptance.

Plan: (1) add actual-Go enumeration vectors and xUnit tests, observe stub RED;
(2) implement directory lists, regular/linked child classification, byte names,
nil slices and broken-link omission; (3) run full suite and independent review,
fix important findings once with regression evidence; (4) save source/progress
and raw evidence, update existing feature branch using expected remote head.

Ruling: follow the existing case-sensitive snapshot and public entries operation;
mutations, case folding, cycles, ambiguous/deep chains and complete fixture VFS
remain unresolved. Do not expand this boundary's acceptance into those profiles.

Pre-flight: shared Snapshot keeps both custom and fstest symlink resolution;
entry lists must use resolved Open paths while child Stat uses query-directory
paths, preserving Go's different resolution layers. Existing146 read goldens and
two safety guards must continue passing. Baseline total2467; Node62; frozen29.

Task1: complete — initial530-minus-four vector set526 failed against stub;
original2467 passed. Implemented Entries, new suite2993 green.

Final review: one independent review, one corrective pass. Broken nested links
lost terminal unresolved cp and could expose unrelated sibling files. New two
entry/two read regressions RED4/2993; terminal cp preserved through Resolve and
shared OpenName. Reviewer exploratory1601 initially had50 result mismatches and41
constructor error-text mismatches, not a pass.

Ruling: retain the41 existing constructor differences as failures, then address
their single intermediate-path cause within the corrective pass. Four additional
Go regressions RED4/2997; fast full-directory checks retain requested path, while
intermediate-prefix failures name resolved node path. No broad error-family claim.

Task2: complete — direct MTP3001/3001 (530 entries/150 reads plus prior tests),
Node62/62, frozen29/29, Release0/0. Both Go artifacts repeat byte-identically.
Author reran all1601 exact reviewer inputs against the saved actual Go results:
1601MATCH, zero differences, no independent second review. Original146 read rows,
frozen expectations, lock and static inventory/progress are unchanged.

Task3: complete — source, full goldens and this evidence index are part of the
feature commit. Raw logs and all three exploratory stages are retained in
libfile_471260f46c7c81918324c3e18c3acbf9, with SHA256 in the evidence JSON.
Next: bind rooted fixture maps to actual VFS construction and queries, then
makeUnitsFromTest/config/canonical options and AST. No full acceptance promoted. SDK coordinator NOT_RUN; new commands disable telemetry; dependency restore
uses only a local empty feed and cached packages; builds and direct MTP are separate evidence.

## Independent final PR verification

Whole-branch review of 976925f found one additional P2: Open validated only after
standard parent-link traversal, allowing malformed UTF8 custom-resolved paths to
become readable targets and linked-child entries. Four real-Go regression failures
(plus four passing controls) preceded the one-line validation-order correction.
Final direct MTP is 3009/3009; Node 62/62; frozen 29/29; Release 0/0; Go test/vet pass.
Original 530/150 rows are unchanged; new totals 534/154 regenerate byte-identically.
A separate reviewer reran its exact 4 read and 4 entry requests against the final
assembly, plus all original 1601 exploratory inputs: zero differences. Both full
13006-input raw/file-map profiles were independently rerun, without compiler credit.
See [final evidence](../evidence/pr6-vfs-open-validation.json). The local SDK
coordinator remains NOT_RUN after the Unix-pipe permission failure; no retry or
permission change was used. Current upstream pin and release boundaries are unchanged.

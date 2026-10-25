# Complete raw fixture loading differential — 2026-10-25

Base43574d8, unchanged TS7 pin8d9b6da. Plan: expose the existing raw loader as a
reusable C# JSONL development harness; reconcile the entire source inventory with
the actual pinned Go generic parser; preserve compiler NOT_RUN; review and save.

The harness is a separate executable project referenced by xUnit. The151 unchanged
Go golden cases now pass through its shared protocol. Two new stream/error tests
prove parser panic continuation, nil callback values and malformed transport failure.
Initial stub RED153/1361; final direct MTP1514/1514.

The full source inventory is reconciled against the pinned Git tree and verified
blob bytes before running:13006 sources, including all42 upstream-skipped sources.
The actual Go parser/VFS callback and C# protocol both return17804 raw units and
callbacks. All13006 results match, including byte arrays and metadata. Repeated
requests, full Go results, C# results and per-source reconciliation are byte-equal.
This is the normal raw-unit callback profile, not AST parsing, compiler execution
or the full configuration/implicit-first/error-injection profile space.

Independent review found two P2 evidence defects: malformed output objects could
look like matches; an old binary could be labeled with current source hashes.
One fix pass added complete output schema validation and a mandatory Release build
inside the differential command. Both failures were reproduced RED→GREEN, including
executing an edited tiny C# project and rejecting a real compiler error. Final
Node60/60, Release0 warnings/errors, frozen29/29. No second review is claimed.
The SDK coordinator still aborts134 on Unix-pipe permission; direct MTP is separate.

No source-scope or frozen acceptance records were promoted. The new
loading-progress.json is a separate loading profile ledger. Raw results and exact
hashes are indexed by docs/evidence/fixture-corpus.json. PR5 remains pending stale
remote review; continue this branch without another duplicate PR.

Next: rooted paths and symlink VFS, then makeUnitsFromTest/config construction,
canonical configuration expansion and AST callbacks. Compiler acceptance stays
NOT_RUN; complete release scope and executable case count remain unresolved.

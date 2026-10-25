# Status — 2026-10-24

Working bootstrap of the first noEmit milestone. No JavaScript emitter or language
server. No real-project compatibility or performance claim.

## Accessible VFS entries — 2026-10-25

- Added case-sensitive directory enumeration with byte-preserved names, sorted
  files/directories, nil slices and linked-child classification; broken links are
  omitted. Actual pinned Go supplies530 complete input/output goldens.
- Independent review exposed a broken-target fallback that could read an unrelated
  nested file. Four actual-Go regressions reproduced it in reads and entries;
  preserving the terminal unresolved path fixes both. The review's41 constructor
  error-text differences reduce to intermediate requested/resolved path selection;
  four more failing regressions now match. Existing146 read vectors are unchanged,
  with four additions bringing that boundary to150.
- Direct MTP3001/3001, Node62/62, frozen29/29, Release zero warnings/errors.
  Repeated530/150 Go artifacts are byte-identical. The initial exploratory1601
  profile had91 mismatches; author reruns after fixes matched1601/1601. This is a
  separate synthetic profile, not a second independent review or original corpus.
- [Evidence](evidence/vfs-entries.json), [progress](testing/ts7/vfs-entries-progress.json).
  Product CLI/FixtureProtocol wiring, case folding, mutations, cycles/deep/ambiguous
  links, full constructor/path families, full fixture VFS/config and compiler
  acceptance remain unresolved or NOT_RUN. No full-release credit is added.

## Read-only VFS boundary — 2026-10-25

- Added a case-sensitive in-memory snapshot for entry creation, implicit parents,
  file/directory/link lookup, ReadFile decoding and Realpath fallback. Open follows
  both upstream custom lookup and the standard-library parent-first link pass.
- 146 actual Go vectors include POSIX/Windows paths, chained/broken links, parent
  construction, BOM bytes and malformed UTF8 alias/target paths. Review found four
  important differences; all were reproduced and fixed. Direct MTP2467/2467,
  Node62/62, frozen29/29, Release0 warnings/errors; golden regeneration byte-equal.
- Two C# safety tests keep repeated/expanding cycles explicitly unsupported. The
  expanding-cycle regression hit the five-second subprocess timeout before the
  depth guard; afterward the full suite passed. No upstream cyclic run or cycle
  compatibility is claimed.
- [Evidence](evidence/readonly-vfs.json), [progress](testing/ts7/readonly-vfs-progress.json).
  Case folding, enumeration/mutation, overlapping link order, deeper chains and
  complete fixture VFS/config/AST/compiler profiles remain unresolved or NOT_RUN.

## Rooted fixture files and link projections — 2026-10-25

- Connected byte paths to raw fixture maps: default /.src or currentDirectory,
  rooted file names, last duplicate file wins, and less restrictive normalized
  link alias/target paths. Colliding normalized aliases remain separate projections.
- Full fixed-Go/C# profile:13006 MATCH,17804 units,17799 file entries,57 links;
  all42 skipped sources retained. Initial/final raw payloads byte-identical, and
  the original raw profile still13006 MATCH with identical C# output.
- 55 Go boundary vectors, direct MTP2319/2319, Node62/62, frozen29/29, Release0/0.
  Independent review had no important findings; three evidence/coverage gaps were
  addressed. Reference echoes are now bound to actual input/name bytes.
- [Evidence](evidence/fixture-file-map.json), [profile](testing/ts7/file-map-progress.json).
  Actual symlink VFS, config/AST and original compiler acceptance remain NOT_RUN.
  Parent6910398 CI succeeded; latest head needs its own checks. One incomplete
  execution was excluded after automatic approval review rejected waiting on it
  due to possible test/build telemetry; completed independent runs supply results.

## Byte paths for VFS construction — 2026-10-25

- Added byte-preserving encoded-root, normalization, two-path combination and
  rooted resolution helpers matching the pinned tspath implementation.
- 750 actual Go vectors include POSIX/DOS/UNC/URL/dynamic roots, directory and
  URL suffix failures, NUL and retained malformed UTF8. Repeated golden is byte-equal.
- Independent review found dynamic-root fast-path and trailing-backslash directory
  differences. Expanded regressions failed22 cases before the one fix pass;
  direct MTP2264/2264, Node60/60, frozen29/29, Release0 warnings/errors afterward.
- [Evidence](evidence/fixture-paths.json) and [ledger](progress/fixture-paths.md).
  This is a development harness foundation; VFS/link maps, tsconfig/AST and original
  compiler assertions remain pending. Parent5d2c6c5 hosted verify succeeded; latest
  work requires its own CI. PR5 remote review remains stale and incomplete.

## Complete raw fixture loading profile — 2026-10-25

- Added an executable C# developer harness with the same151 reference vectors,
  plus stream/error regressions. The differential command builds current sources
  before execution and fails on malformed/missing/duplicate result records.
- Actual pinned Go and C# runs agree on all13006 source fixtures and17804 raw
  units/callbacks. All42 upstream-skipped sources are retained. Repeated complete
  request/result payloads are byte-identical. Source IDs, byte hashes, metadata,
  versions and raw-output hashes are in [evidence](evidence/fixture-corpus.json)
  and a separate [loading ledger](testing/ts7/loading-progress.json).
- One independent review and one RED→GREEN fix pass closed malformed-result and
  stale-binary evidence gaps. Direct xUnit/MTP1514/1514, Node60/60, frozen29/29;
  Release zero warnings/errors. SDK test coordinator still aborts134 on Unix pipe.
- This is raw/default-options/unit-snapshot-callback loading only. AST callbacks,
  VFS/tsconfig construction, full configuration expansion and original compiler
  acceptance are NOT_RUN. No full-corpus compiler PASS or release claim.

## Raw multi-file state and callbacks — 2026-10-25

- Added byte-preserving C# raw-unit state: file/global options, currentDirectory,
  link/symlink ownership, empty/implicit-first files, host-independent basename,
  discarded-prelude validation using the pinned default scanner behavior.
- Callback boundaries preserve partial results: a failed mid-file value is omitted
  and stops subsequent metadata; the EOF callback value is appended even on error.
  Go nil slices and a list containing a null value remain distinct.
- Actual pinned generic Go parser/scanner/path/VFS functions produced151 vectors,
  including8 full raw non-UTF8 fixture sources. Repeated generation is byte-identical.
  Direct xUnit/MTP1512/1512, Node54/54, frozen29/29, Release zero warnings/errors.
- Independent review found Unicode localhost folding at file-URL roots; a failing
  reference vector reproduced it and the fix passed. Nil-result normalization was
  also removed from the oracle and corrected RED→GREEN. Evidence:
  [raw state](evidence/fixture-state.json).
- These are developer-harness units and generic callbacks, not AST parsing or CLI
  integration. VFS/tsconfig, full-corpus loading comparison and all original compiler
  acceptance remain NOT_RUN. SDK coordinator still aborts on Unix pipe permission;
  direct MTP is reported separately. PR5 remains pending remote review.

## Byte-preserving directive boundary — 2026-10-25

- Added a C# development-harness reader using SourceDecoder. It preserves decoded
  source bytes, exact line byte offsets, CRLF/empty-line boundaries, ordered raw
  directives and link alias/target bytes. No decoding of invalid UTF8 is attempted.
- Actual pinned Go regex/link functions generated 535 vectors, including all 256
  byte values in values/links and all eight non-UTF8 fixture sources. Repeated Go
  generation is byte-identical. RED535/825 then direct xUnit/MTP1361/1361; Node54/54,
  frozen29/29, Release zero warnings/errors. Source versions and exact IDs:
  [directive evidence](evidence/fixture-directives.json).
- Independent review found no correctness defect. A long arrowless-link timeout
  was reproduced and fixed with non-backtracking matching plus a Go golden case.
- This helper is linked into the test assembly, not the compiler CLI. It is a
  lexical boundary, not full fixture loading: file/global state, virtual units,
  prelude validation, callbacks and VFS/tsconfig still need implementation.
- Source-scope progress, inventory payloads and original acceptance remain unchanged
  NOT_RUN. PR5 remains pending remote review; prior decoder branch CI passed.

## Byte decoding foundation — 2026-10-25

- Added C# `SourceDecoder.Decode`: UTF8 BOM stripping and UTF16 LE/BE BOM decoding,
  odd UTF16 trailing-byte handling, and byte-exact malformed UTF8 preservation.
  This foundation is not yet wired into the CLI, inventory loader, scanner or VFS.
- Executed the pinned Go decodeBytes/decodeUtf16 functions to generate 764 golden
  cases: 756 byte/surrogate/endian boundaries and all eight full non-UTF8 fixture
  blobs. Repeated generation is byte-identical. No fixture is awarded compiler PASS.
- Direct xUnit/MTP 825/825 (original60, 764 vectors, buffer-ownership regression);
  Release zero warnings/errors; Node54/54; frozen29/29. Reference and C# test IDs,
  exact versions, raw byte vectors and hashes are recorded in
  [decoder evidence](evidence/source-decoder.json).
- The generated scope progress remains unchanged/NOT_RUN. Next: use this byte
  boundary in faithful directive/multi-file loading, preserving raw names/values,
  links, partial callback results and tsconfig/VFS semantics.

## Evidence

- Pinned upstream: Microsoft TypeScript 8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f (7.1.0-dev).
- Reference built with Go 1.27.3; C# SDK 10.0.402.
- Frozen differential cohort: 29 cases, including 7 unchanged upstream source files.
- Historical bootstrap-only discovery: 12,981 compiler/conformance source files; 15,174 known option
  variants and 90 files with unresolved variant expansion. Counts are provisional,
  not a complete faithful upstream harness inventory.
- Entire upstream suite: not run. Seven upstream sources exercised in isolation;
  remaining 12,974 upstream source files not exercised by this cohort. All corpus
  pass/mismatch/unsupported/crash/timeout counts are therefore **unmeasured**, not zero.
- CI workflow is provided. Local verification evidence does not imply hosted CI ran.

## Static TS7 scope inventory

The [new scope registry](testing/ts7/README.md) independently inventories the entire
pinned Git tree: 67,951 assets, 13,006 fixture inputs, 6,064 Go AST declarations,
24 JS test sources and 7 JS benchmark sources. These are separate source-level
measures, not additive test counts. Compiler whole-fixture omissions, byte-invalid
inputs, analyzer-test fixtures and unevaluated build constraints remain explicit.
The bounded transpile configuration layer expands 25 fixtures into 28 named
configurations. The other 12,981 fixture inputs, all runtime cases and executable
acceptance remain unknown or NOT_RUN. The full-coverage release gate is not satisfied.

The old 12,981-file provisional inventory is retained unchanged because the frozen
29-case bootstrap evidence binds it. It excludes transpile and other test domains;
it is not the denominator for TS7-wide coverage.

## Native TypeScript tools — 2026-10-25

- Migrated all 13 owned `.mjs` modules/tests to native `.ts`; updated imports,
  shell commands and current documentation. No loader, transpiler or new dependency.
  Upstream Herebyfile.mjs and frozen inputs remain unchanged.
- Release build zero warnings/errors; Node 54/54 (52 existing plus two native-entry
  guards), direct xUnit/MTP 60/60, Go tests/vet pass, frozen differential 29/29.
- All eight complete inventory payloads are byte-identical before/after; only the
  summary's generator path/hash provenance changes. Repeated native generation,
  including summary, is byte-identical. Transpile remains 25 inputs/28 configs;
  no original compiler/reference/acceptance record is promoted.
- Full verify.sh attempted; SDK coordinator still aborts on Unix pipe permission.
  Direct MTP is a separate successful run. Independent review: no important findings.
- Evidence: [native tools](evidence/native-typescript-tools.json). The migration
  preserves the recorded inventory payloads and the bounded transpile configuration
  set; complete upstream fixture/configuration acceptance remains NOT_RUN.

## Local verification and review

- Release build: zero warnings/errors.
- C# suite: 60 cases, migrated from NUnit to xUnit 4.0.2 / MTP 2.5.1.
  All method names and parameter rows are preserved in the
  [migration map](evidence/xunit-migration-inventory.json). The original 17 Node harness/evidence cases remain; source-inventory regressions
  and Go AST parser tests are additional, not upstream conformance cases.
- Frozen differential cohort: 29/29 matched; repeated reports were identical.
- Independent reference rebuild: 29/29 frozen results reproduced.
- Five additional oracle probes validate review fixes (strict binding names,
  top-level globalThis conflict and diagnostic phase ordering).
- Independent review found three important issues; all were reproduced by failing
  tests and fixed. An additional diagnostic-phase bug was fixed in the same pass.
- Evidence: [cohort](evidence/bootstrap-conformance.json),
  [reference build](evidence/reference-build.json),
  [review regression probes](evidence/review-regressions.json).

Known minor compatibility defect deferred: U+2028/U+2029 inside quoted strings
currently produce TS1002 although the pinned compiler accepts them. Do not interpret
this bootstrap's string-literal handling as complete JavaScript lexical compatibility.

## Milestone gaps and next step

The bootstrap establishes an executable chain and a small oracle-backed cohort.
Full first-milestone infrastructure remains incomplete: faithful directive/multi-file/
symlink harness behavior and complete variant accounting need implementation. General
filesystem hosting is also deferred. Keep these visible rather than call the full
milestone finished.

Next: audit the unexpanded source families, then implement faithful byte/directive/
virtual-file loading and canonical configuration expansion with exact executable
variants, while preserving the separate 29-case gate.
Then expand scanner/parser/binder/checker coverage in dependency-sized ports. Do not
start IL output, a runtime, or a language server during compiler completion.

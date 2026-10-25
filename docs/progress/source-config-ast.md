# Source-backed selected config AST — 2026-10-26

The config-ast JSONL development profile connects raw fixture units to the current
product JsonSyntax AST. It preserves the raw selected name/content, remaining
unit order, selected index and runExternalCode gate. Both sides derive the
rooted currentDirectory/config filename using the fixed path semantics. This is
pre-VFS syntax preparation: selection/removal/rooting mirrors the corresponding
makeUnitsFromTest fragment, calling the actual raw parser/name/path helpers on
the Go side. It does not construct configFS, apply external mappers, run
ParseJsonSourceFileConfigFileContent, resolve extends/globs or execute compiler
assertions. The reference AST itself is actual ParseSourceFile(ScriptKindJSON).

All13006 pinned source blobs and stable inventory identities are checked, retaining
42 upstream skips and8 non-raw-UTF8 inputs.172 sources select a config;12834 have
none and are NOT_APPLICABLE, never compiler passes. The source-backed default
profile yields171 MATCH and1 MATCH_DIAGNOSTICS, zero unequal AST results,
UNSUPPORTED, reference panic or crash.2091 reference projected nodes and1
syntax diagnostic are retained. Config records bind source/profile IDs, raw
bytes/hash, selected filename, directory/rooted path and index; original compiler,
referenceCompiler and acceptance remain NOT_RUN. Other configuration/runtime
profiles and the complete executable-case denominator remain unresolved.

The real tsconfigRootdirInclude.ts config lacks a property comma before include.
Initially the bounded C# parser returned TSCS1001 while Go retained the object and
reported TS1005 at UTF16 start164/length9. A regression extracted only from the
completed actual-Go source profile reproduces it. JsonSyntax now retains a next
property when an already supported name and colon are present, reporting the
pinned comma error at that token. This is a bounded parseDelimitedList recovery,
not general object/array/expression recovery. The CLI config path reports the
same syntax diagnostic and exit2; syntax errors are not silently accepted.

Genuine RED6fail/8732pass proved the missing JSONL connection; two rooted-context
regressions RED2/8738 proved relative names must be rooted. The original-source
comma regression RED1/8740 and the same product mutation with CLI RED2/8740
verify retained AST/diagnostic and CLI behavior. Final MTP8742/8742, Node75/75,
frozen29/29, Release0 warnings/errors. SDK orchestration NOT_RUN. Node schema and
classification tests include unsupported/diagnostic/no-config/input/context
bindings and malformed/duplicate result envelopes; schema RED4 plus envelope
RED1 and artifact-integrity RED1 were corrected without reducing assertions.

Independent review reproduced every source binding and both actual-Go stages,
then independently reran C#13006 rows and recomputed all results. It found one
blocking evidence defect: the first monolithic actual.jsonl lost221 rows after
completion (observed12785/13006). That copy is excluded.
Root cause remains unknown. The author corrective pass replaces inherited
child output file descriptors with bounded stdout capture and51 Node-written,
flushed immutable shards, with per-batch counts/hashes and independent later
manifest checks. A regression rejects post-completion truncation before a summary
can be reused. Final sharded executions/archives are checked separately; no
second independent review is claimed.

Full comparison commands intentionally exit1 because one original selected
config has a syntax diagnostic. Go adapter tests exit0 after serializing that
actual diagnostic. MATCH_DIAGNOSTICS is differential equality, not a clean config
or compiler PASS. Two final executions and their artifact hashes are preserved;
build logs/timings are not claimed byte-identical.

Excluded attempts remain as raw history: the initial adapter had an int32/int
build typo (no parser execution). The next adapter incorrectly passed relative
names to a rooted-only constructor, causing36 adapter panics; a missing source
hash filename also prevented its summary. Neither is an upstream parser failure
or accepted profile. The corrected-context profile before the product comma fix
records171 MATCH/1 UNSUPPORTED. An earlier C# test's reserved-keyword build error
is excluded from RED. Complete raw source/profile data is retained separately;
Git contains source, the original-source regression and the local run logs.

See [evidence](../evidence/source-config-ast.json) and
[profile ledger](../testing/ts7/source-config-ast-progress.json). Seven previous
module:none reference fatals, all test domains and unknown denominators are
unchanged. releaseEligible=false. Next: actual config host/effective-option and
makeUnitsFromTest dependencies, driven by real source/profile gaps, while keeping
the remaining JSON numeric/classifier/recovery limits explicit.

# Source-backed config values — 2026-10-26

The config-values JSONL profile now connects raw fixture preparation to the
existing product JsonConfigValues converter. Preparation and conversion share
one product JSON AST, including its original diagnostics. An absent config is
NOT_APPLICABLE; an unsupported parse produces no converted value; a successful
empty config keeps the distinct EmptySource sentinel. The existing config-ast
profile has the same payload contract and remains covered by its regressions.

The generator checks the complete pinned Git fixture tree, inventory hash,
source IDs/domains/blob OIDs/raw hashes, then runs actual Go raw preparation for
all13006 sources. Config selection/removal/rooting remains a mirrored runner
fragment using actual raw parser/config-name/path helpers, not whole
makeUnitsFromTest. All42 upstream skips and8 non-raw-UTF8 blobs remain in that
source scope.172 selected configs, including3 skipped sources, enter actual Go
ParseSourceFile(ScriptKindJSON) followed by private convertToObject. The generated
rows preserve raw request/config bytes/name/index/context, skip metadata, stable
source+profile+config IDs, parser diagnostics, conversion diagnostics and exact
UTF16/binary64/order/nil value projections.

The C# profile executes all172 selected configs:171 MATCH and1
MATCH_DIAGNOSTICS, zero mismatch/unsupported/crash. tsconfigRootdirInclude.ts
retains TS1005 at UTF16 start164,length9; its value tree matches while its syntax
error remains. Conversion diagnostics are empty within this supported AST
scope. The tool deliberately exits1 rather than treating that error as clean.
12834 no-config sources were selected as absent by Go; no C# value execution or
compiler pass is claimed for them. This is not15523 configuration variants,
notifier/option declarations, config host, filesystem/extends/globs, effective
options, runExternalCode/existingOptions execution or original compiler tests.

172 source-backed MTP rows plus5 absent/unsupported/empty guards genuinely failed
for the missing JSONL profile:177 fail/8949 pass. The first attempt incorrectly
sent pretty JSON as multiline JSONL and is explicitly excluded; corrected input
then demonstrated the missing profile. Final Release0 warnings/errors and direct
MTP9126/9126, Node81/81, frozen29/29, locked offline restore and standalone Go
scope tests pass. A two-fault Node mutation (dropped parser-diagnostic comparison
and artifact hash check) fails2 tests; restored implementation passes6 focused.
SDK test coordinator and one end-to-end verify.sh invocation remain NOT_RUN.

Two complete final profiles each retain24 manifest artifacts. All48 hashes were
verified after completion;13 nonlog payloads (including six immutable C# stdout
shards) are byte-identical. Go/build timing logs and reports are not claimed
byte-identical. The committed golden is exactly the generated profile golden. Author and
reviewer DLL hashes are kept separately. Git build metadata differed; attempted
metadata overrides did not reproduce the original DLL hashes, so the exact
cause is not established and no reproducible-build claim is made. Those extra
builds are excluded from profile/full-suite execution proof.
The temporary SDK/Go/dependency caches were restored at their existing versions;
unchanged locked NuGet content hashes were validated by offline restore.

Independent read-only review found no important issue. The reviewer reran the
actual-Go/C# profile, independently verified72 artifact entries across three
runs and all30 source hashes per run, and compared13 payloads byte-for-byte.
It rebuilt Release serially with0 warnings/errors, then ran9126/9126 direct MTP
and81/81 Node tests. It did not repeat historical RED/mutations, locked restore,
frozen29 or standalone Go scope. The temporary independent checker was first
executed as.mjs; it was moved to native.ts and actually rerun. The receipt
retains both command histories; no claim rewrites the earlier execution. No
second review or formal GitHub approval is claimed.

[Evidence](../evidence/source-config-values.json), the complete golden, original
logs and the172-record ledger are committed. Full raw profiles are preserved in
the evidence archive referenced there. All original compiler/referenceCompiler/
acceptance remain NOT_RUN, full concrete-case denominator unknown, seven prior
reference fatals and every remaining test domain/profile retained, releasefalse.
Next: actual config-host/option-declaration conversion and callbacks, defaults/
paths/invalid values/existingOptions gate, then effective config and compiler AST.

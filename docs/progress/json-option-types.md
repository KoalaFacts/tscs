# Configuration value type admission — 2026-10-26

JsonCompilerOptionTypes.Accepts ports pinned isCompilerOptionsValue. A missing
option accepts nothing. JSON null is allowed unless the declaration disallows
null (currently extends). Lists accept the outer array type, including empty/nil
slices, without checking elements. listOrElement accepts arrays or recursively
checks its scalar element declaration. Strings/enums, booleans, numbers and
objects accept their exact value kinds. EmptySource is not JSON null. This is
shape admission only: enum membership, command-line-only rules, per-element
conversion, paths/network restrictions, declaration callbacks, host and effective
options remain unimplemented here.

The CLI uses this predicate for its supported noEmit/noLib boolean options.
Null means unset, retaining the existing false default; explicit CLI flags can
then override it. This does not enable emit/default libraries or relax the
bootstrap's final noEmit/noLib requirement. Invalid nonboolean values keep the
existing bootstrap input error policy, not claimed upstream full option diagnostics.

The actual Go overlay enumerates126 OptionsDeclarations plus actual extends,
nil option and a missing-element listOrElement probe. For each descriptor it
runs14 sources through actual ParseSourceFile/convertToObject and private
isCompilerOptionsValue:1806 unique-ID rows,411 true/1395 false. The Go declaration
accessors supply null/element policy; no hand-edited expected values. This counts
helper rows, not source fixtures/config variants/compiler acceptance. Genuine
stubRED411 type rows plus1 CLI failure,10521 pass ->10933/10933 total. The old
frozen29 and all previous regression rows remain unchanged.

[Evidence](../evidence/json-option-types.json) contains source/pin/probe/golden
hashes, versions, commands, original logs and independent review limits. No full
13006/172 source profiles were rerun, no original compiler assertions executed,
all existing reference fatals/domains/unknown denominators remain, releasefalse.
Next: actual declaration-driven convertJsonOption validation and conversion,
including invalid values, defaults, paths, notifier/host and existingOptions gate.

Independent review found no critical/important issue, regenerated1806 rows byte-
identically, serially rebuilt Release0 warnings/errors and reran10933 tests,0
skipped. Its CLI exploration confirmed no flags/one flag/explicit false remain
exit1 and both true flags exit0. Minor deferred: only the both-flag override is
a new locked CLI regression; the other default-value boundaries are recorded
exploratory checks. No second review or original compiler acceptance claim.

This section records its original stage. The later [primitive conversion](json-scalar-options.md) supersedes the invalid-boolean input-error policy with TS5024 / exit2; the original evidence keeps its historical binding.

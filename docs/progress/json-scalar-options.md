# Scalar config option conversion — 2026-10-26

Plan: port the primitive `convertJsonOption` branch for real declarations whose
kind is boolean/number/string/object, PathKind is None and extraValidation is
empty. Command-line-only rejection precedes type/null admission. Preserve values
on success and produce TS6266/TS5024 with property-name/value locations as pinned.
Other declaration branches are explicitly unsupported by this component.

Use actual Go declarations + private convertJsonOption, with and without a source
node, comparing full values and diagnostics. Wire supported CLI boolean options
through this conversion so invalid types report config diagnostics/exit2. Keep
bootstrap noEmit/noLib requirements and null command-line override semantics.

- [x] Oracle and failing product/CLI regressions (actual RED).
- [x] Product implementation, full MTP/Node/frozen checks and repeat oracle.
- [x] Independent review, corrective pass if needed, source/log hashes ready for publication.

No effective options/host/notifier, source profile rerun or original compiler
acceptance is implied. All scope, unknown denominator and releasefalse remain.

Actual oracle:100 declarations (92 boolean/5 string/2 number/1 object) of126
retained descriptors,15 values and compiler-only/file-node contexts =3000 IDs.
Results:576 values (including allowed null),2214 TS5024,210 TS6266. Negative
numeric values are not range-validated by this private upstream branch either;
these results do not establish that the complete configuration is valid.

Genuine RED2822fail/11124pass ->13946/13946. The false/Null stub already passed
some negative/value-null rows; eight CLI type-error tests were part of the RED.
Five null/default/explicit-false CLI guards also passed, closing the earlier
minor coverage gap. Author Release0 warnings/errors, Node89/89, frozen29/29,
two oracle generations byte-identical. Initial adapter Statements indexing typo
prevented Go compilation; initial C# golden CopyToOutput omission caused a test
inventory violation. Both attempts retained/excluded; neither is the genuine RED.

One independent review found no Critical/Important issue, regenerated all3000
byte-identically, rebuilt serial Release0 warnings/errors and ran13946/13946.
Its minor stale historical wording was annotated as superseded in the earlier
progress page to keep the requested progress accurate; no product corrective
pass or second review was needed. Node/frozen and historical RED were not
independently rerun. Source-only/mixed missing-node contexts were inspected,
not separately exercised. CLI still stops at its first failed supported boolean;
this is not convertOptionsFromJson batch diagnostics or effective configuration.

All source/golden and raw logs are Git-backed. [Evidence](../evidence/json-scalar-options.json).
SDK coordinator and end-to-end verify.sh NOT_RUN. Full13006/172 profiles were not
rerun; old evidence retains its earlier source/tree binding. Original compiler,
reference compiler and acceptance remain NOT_RUN. Seven prior reference fatals,
all test domains and unknown concrete-case denominator retained; releasefalse.

Next: actual enum/list/path/extra-validation and declaration-driven option map
conversion, then config notifier/host/effective options and compiler AST. Do not
repeat these3000 helper cases as new delivery or award original fixture credit.

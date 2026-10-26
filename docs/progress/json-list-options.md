# Primitive-element config lists — 2026-10-26

Bounded plan: actual convertJsonOption list branch plus convertJsonOptionOfListType,
with primitive/object element conversion through the existing scalar component.
Retain all126 declarations in the oracle inventory; execute only real lists whose
elements need no paths/enums/extra validation. Compare source-node/compiler-only
contexts, actual element names, nil/empty lists, diagnostics and preserve-falsy.
First generate actual private Go results, run stub RED, implement product, run
full suites and repeat oracle, then independent review and persist source/logs.
No new CLI option support, host/effective config or compiler acceptance is implied.
Prior full profiles, all domains/seven reference fatals/unknown denominator remain.

Executed160 actual private-Go conversions:4 declarations ×20 literals ×2 source
contexts.108 cases retain144 TS5024 diagnostics,52 have clean values; all match.
Nil outer JSON null becomes a typed nil list, allocated empty remains distinct.
Scalar null removal happens in convertToObject before this stage; the pinned
list function then indexes the original syntax array by converted index. Tests
retain that observed diagnostic-location behavior rather than changing the oracle.
moduleSuffixes preserves falsy results (including nil values from element errors);
other lists filter nil/false/zero/empty string. customConditions/plugin element
names come from actual declarations, not the outer option name.

Stub RED160fail/13946pass ->14106/14106, zero skips; Release0 warnings/errors,
Node89/89 and frozen29/29. Two author golden generations byte-identical.
No excluded preliminary attempts in this package. All source/golden/raw logs Git.
SDK coordinator/end-to-end verify.sh NOT_RUN. No full13006/172 profile rerun;
original compiler/referenceCompiler/acceptance NOT_RUN. All domains, seven old
reference fatals and unknown concrete-case denominator retained; releasefalse.

Next: connect declaration-driven option map conversion and remaining enum/path/
extra-validation dependencies to config-host preparation. Do not expand synthetic
list matrices as a substitute for effective config and compiler AST work.

One independent read-only review found no Critical/Important/Minor issue within
the four actual options. Regenerated160 byte-identically, serial Release0
warnings/errors and fresh MTP14106/14106 (via dotnet run --no-build --no-restore).
Node/frozen/RED not independently repeated; no second/formal approval. Author
progress/minimum updates lie outside the reviewed implementation head.
[Evidence and raw commands/logs](../evidence/json-list-options.json).

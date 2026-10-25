# Fixture VFS construction — 2026-10-25

Base main51b892c. Connect existing raw file mapping to the actual read-only VFS
snapshot. Use the55 retained map boundary inputs, preserve all source bytes,
and compare construction/read/entries to the actual pinned Go factory.

Plan: write Go oracle and xUnit vectors, observe stub RED; implement adapter;
run full MTP/Node/frozen checks, independently review, save source and evidence.
Normalized link-alias collisions and cyclic/deep/ambiguous resolution remain
explicit UNSUPPORTED, never a guessed Go-map winner. Complete13006 actual VFS
construction, configuration, AST and original compiler acceptance remain NOT_RUN.
This is a bounded developer harness profile, not product CLI integration.

Result: stub RED55/3009; final MTP3064/3064, Node62/62, frozen29/29,
Release0/0. Actual-Go outputs:36 normal,18 expected panic,1 policy UNSUPPORTED.
Independent review corrected double-normalization in the oracle: pass raw links
into the fixed factory, preserving normalized query paths and collision guard.
Both final generations compare byte-identically and final C# suite passed.
Reviewer's separate build failed without diagnostics and is not test evidence;
author's configured final build and MTP logs are the successful validation.
No second review. Full corpus and original compiler acceptance stay NOT_RUN.
Next: JSONL profile and complete13006 source differential with strict schemas,
then config/makeUnitsFromTest and AST. No source scope denominator changed.

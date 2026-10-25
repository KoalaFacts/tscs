# Source-backed config values implementation plan

Goal: connect the selected raw fixture config to the existing product converter,
with actual pinned Go selection/parser/convertToObject results. This is a
pre-VFS, no-notifier value profile, not effective options or compiler acceptance.

The native TypeScript generator verifies the entire pinned fixture tree and
inventory, runs real Go raw preparation for all13006 identities, and retains
all selected configs without dropping skip/error metadata. It generates one
source-bound row per selected config, with raw input, selection/context, parser
diagnostics and exact value projection. No-config sources are counted separately.

- [x] Generate actual Go source-backed vectors with source/blob/config identities.
- [x] Add JSONL tests comparing selection/context, diagnostics and values; observe
  the missing profile fail, then implement the profile with the product converter.
- [x] Exercise absent config, unsupported parse and empty source separately so a
  failed/null AST cannot silently convert to an empty-source success.
- [x] Run Release, direct MTP, Node, frozen cohort, locked restore and Go scope;
  regenerate byte-identically and obtain independent review.
- [x] Save source, golden, logs, status/ledger and exact evidence; publish on PR8
  using the expected remote SHA and matching tested tree.

Review focus: preserved original syntax errors; missing config vs empty config;
unsupported grammar vs null root; duplicate/array nil semantics; all pinned raw
identities/skips and exact selected config name/content/context.

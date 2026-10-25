# Read-only VFS boundary — 2026-10-25

Basee534fd3. Implement the next dependency after rooted file/link projection:
case-sensitive MapFS snapshot entry creation and public read/exists/realpath.
No host IO. Actual Go vfstest.FromMap/iovfs queries supply146 complete golden rows.
The old complete fixture profiles remain historical, unchanged and separate.

Initial stub RED141/2319; repeated-cycle guard stub RED142/2319. Independent review
found resolved-path UTF8 validation, comparePathsByParts ordering, relative query
acceptance and requested mkdir-path error differences.146-vector expansion RED7/
2459. One fix pass also handled Windows root Open paths, parent-directory reset
through links and fstest.MapFS's separate parent-first symlink traversal. Full
suite2466 green. A bounded expanding-cycle regression timed out after5 seconds
(exit124); added256-hop guard, final2467/2467. No upstream cycles executed.

Node62/62, frozen29/29, Release0/0, repeated Go golden byte-identical. Telemetry
opted out, Go mode off and module networking off; build used cached dependencies
without restore. SDK coordinator not re-run. Independent review fix pass verified
by regressions; no second review claimed.

Unresolved profiles remain explicit: case folding, mutations/clocks/enumeration,
ambiguous overlapping link prefixes, deep chains/cycles, broader constructor/query
validation and full fixture VFS/config/AST/compiler execution. No original corpus
acceptance promoted. Next: extend supported VFS profiles and bind fixture maps to
actual VFS construction, then config parsing and canonical configurations.

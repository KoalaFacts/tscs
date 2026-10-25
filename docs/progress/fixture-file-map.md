# Rooted file and link projection — 2026-10-25

Base6910398. Next pre-VFS slice, faithful to makeUnitsFromTest's raw file/current
Directory loop and tsoptionstest.NewVFSWithSymlinks path normalization. No actual
VFS creation, symlink traversal or config parsing. Normalized alias collisions are
kept as separate raw-alias records, never assigned an invented iteration winner.

54 Go boundary vectors RED54/2264 then GREEN2318. Added permissive URL link case,
final55 golden and MTP2319/2319. Independent review had no important findings;
added dependent probe hash, empty/query/fragment link regression and enforced
reference input/fileName binding (new binding test RED→GREEN). Node62, frozen29,
Release0/0. No second review claimed. SDK coordinator abort134 remains separate.

Full13006 source profile MATCH,17804 units→17799 files plus57 link projections.
All42 skipped inputs retained. Initial/final full payloads byte-identical, though
pipeline added binding checks; raw profile regression13006 MATCH and unchanged
C# output. One other incomplete run excluded after automatic approval rejected
waiting due to possible test/build telemetry. No retry or bypass was performed.

Next: actual symlink VFS entries, traversal and conflict semantics, then config
construction. Do not call file/link projection full makeUnitsFromTest or compiler
compatibility. Original source-scope acceptance and release gate remain unchanged.

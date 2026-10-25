# Byte paths — 2026-10-25

Base5d2c6c5. Port the next dependency for rooted VFS construction: encoded root,
normalization, two-path combination and rooted resolution. Raw-byte APIs preserve
invalid UTF8 and host-independent POSIX/DOS/UNC/URL/dynamic paths. No actual VFS or
config construction yet; existing fixture loader and full-corpus profile unchanged.

Actual pinned Go overlay calls tspath operations. Initial448 vectors RED448/1514
then GREEN1962. Independent review's29812 synthetic probes found57 discrepancies:
dynamic-root dot normalization and malformed trailing-backslash directories.
Expanded golden750 includes these boundaries and256 retained raw-byte segments;
RED22/2242 then whole suite GREEN2264. Port preserves Go whole-string fast-path
quirks and root recomputation instead of imposing a different normalization rule.
One review/fix pass only, no second independent review or all-probe pass claimed.

Repeat750 Go golden generation byte-equal. Release0 warnings/errors, direct
MTP2264/2264, Node60/60, frozen29/29. SDK coordinator abort134 remains separate.
Evidence docs/evidence/fixture-paths.json binds all vector IDs, source/probe hashes,
versions and commands. No original compiler acceptance progress was promoted.

Next: apply these path primitives to raw fixture file/currentDirectory/link maps,
then port symlink VFS behavior and makeUnitsFromTest/config construction. Keep all
original inputs, skips, configurations and compiler baselines in release scope.

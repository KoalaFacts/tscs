# Numeric and identity string escapes — 2026-10-26

Port remaining ordinary-string escape switch paths from the fixed Go scanner:
standalone0 returns NUL unless followed by an ASCII digit; legacy octal consumes
up to3 digits for first0..3, otherwise2. It decodes the consumed value, sets the
invalid-escape flag and emits TS1487 over the consumed spelling with a lowercase
hexadecimal suggestion.8/9 emit TS1488 and retain the digit value. Identity
escapes retain their full character, including supplementary UTF16 pairs.

The new Go overlay executes actual Scan/ErrorCallback and Message.Localize with
default locale, keeping raw base64/UTF16 values.368 source streams are92 bodies
×2 quotes×2 ASCII/supplementary prefixes. They cover digit limits,08 (09 was absent from this original cohort), numeric
error spans/messages and identity BMP/supplementary characters, through EOF with
following tokens. This cohort does not cover numeric escapes at EOF, repeated
mixed escapes or all codepoints; older escape cohorts remain unchanged.

RED368fail/8181pass. After implementation all368 pass; three old own guards0/8/q
still expected unsupported. Inputs retained, expected outcomes changed to actual
Go syntax/acceptance. Four CLI rows add exact UTF16 JSON spans and exits0/2.
Final direct MTP8553/8553, Node69/69, frozen29/29, Release0 warnings/errors.
Golden repeats byte-identically. Independent review found no blocker, rebuilt
Release (0 warnings/errors), reran8553 tests and regenerated368 oracle vectors
identically. No second review; SDK orchestration NOT_RUN.

Source/lock/frozen/static inventory and prior full-profile acceptance states
remain unchanged outside this bounded implementation. No original compiler,
referenceCompiler, full config or AST assertions ran. All prior7 reference
fatals, domain/profile gaps and unknown case denominators remain. release=false.
Next: actual JSON parser/scanner mode and AST/config-host dependencies; do not
repeat these string matrices as new progress or use System.Text.Json to imply
TS7 JSONC compatibility.

## Explicit09 coverage correction — 2026-10-26

Review found the original368 cohort lacked09 despite the coverage description.
The generator now appends09 explicitly: four new quote/prefix combinations,
stable IDs0/92/0,0/92/1,1/92/0,1/92/1. All original368 rows remain identical
when filtered by ID and in their original order. Actual Go emits TS1487 over
only the consumed backslash+0 (length2), retains9 as the following character and
suggests backslash-x00; decoded values are NUL followed by9. ASCII/emoji offsets
are10/11. No product source changed and no new compiler capability is claimed.
Current golden372; direct MTP8557/8557, Node69/69, frozen29/29, Release0 warnings/
errors; golden regeneration byte-identical. [Correction evidence](../evidence/numeric-09.json).

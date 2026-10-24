# Source decoder — 2026-10-25

Base: 1d6ebec. Pinned spec: vfs/internal/internal.go decodeBytes/decodeUtf16.
User authorizes autonomous dependency-sized ports. Plan: raw byte API, fail xUnit
against stub, minimal BOM/UTF16 port, generate by actual pinned Go, compare entire
bytes, preserve original tests, independent review, persist source/evidence.

RED: 757 failing new cases, original60 pass. GREEN: original60 +756 boundary
vectors +8 full raw fixture vectors +1 no-alias test =825 passing. No claim that
adding the eight fixture vectors was itself RED. Node54/54, frozen29/29, build0/0.
Go vectors repeat byte-identically; byte fixtures are NOT compiler acceptance.
Existing PR5 remains pending remote review; this is a separate dependency-sized
branch rather than merging an unfinished PR or rewriting its scope again.

Independent reviewer regenerated all764 vectors and verified byte identity; no
important defects. Full verify.sh restored/built then SDK coordinator aborted134
on Unix pipe permission; direct825/825 separate. No corpus/compiler PASS promoted.

# Fixture directive byte boundary — 2026-10-25

Base328a369; spec pinned test_case_parser.go regexes, lineDelimiter and
parseSymlinkFromTest. Bounded slice: SourceDecoder integration, preserve bytes,
line offsets and ordered raw events. No file/global scoping or virtual units yet.

Plan: Go oracle golden generation, failing xUnit stub, C# ASCII byte projection,
Go whitespace trimming, repeat generation, fresh suites, independent review,
commit/persist on existing decoder branch without adding another pending PR.

RED535 failures/original825 passing; GREEN1361. Go535 vectors repeat identical,
including8 full raw fixture sources. Node54, frozen29, build0warnings/0errors.
All original scope records remain NOT_RUN. Next: use this lexical output to port
file state, empty/implicit files, symlink ownership and callback partial results;
scanner prelude validation and VFS/tsconfig are separate dependencies.

Review clean for correctness; independently regenerated original534 cases. The
backtracking observation was then reproduced: old regex on4096-space arrowless
link timed out after8s. Added the535th Go vector; non-backtracking engine passes
full1361 suite. No rereview claim. SDK verify still aborts before tests on pipe
permissions; direct MTP results separate.

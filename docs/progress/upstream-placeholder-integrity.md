# Generated TS7 configuration-target placeholder integrity — 2026-10-26

This task is a **separate post-generation verifier**, not another 15,523 compiler implementations and not a replacement for the frozen 29-case acceptance cohort. Its source is `tools/verify-upstream-placeholders.ts`; `tools/verify-upstream-placeholders.test.ts` contains eight test-first positive/fail-closed regression checks. The generator from the previous work package remains `tools/upstream-placeholders.ts`.

The immutable historical output archived as `libfile_00d083171c708191a3a2affd64acfeff` consists of 15,523 configuration-level xUnit `[Fact(Skip = "UNIMPLEMENTED: ...")]` methods split across 63 generated C# shards. No generated code is automatically included in the main `Tscs.slnx`. This verifier reads that output and refuses missing or modified files, changed SHA/length, missing/rehashed Facts, removed throwing bodies, invented PASS status, mismatched upstream paths/config IDs, upstream skip provenance or silent source/target count reductions. It independently recomputes the fixture/target/group/runner totals, and `--release` rejects all unresolved skips. A separate Python read-only audit verifies the historical bundle a second way; this is still **same-author**, not external review.

## How to verify the historical output

Materialize the existing Library ZIP, extract its flat files to a new directory, and run:

```sh
node tools/verify-upstream-placeholders.ts /path/to/extracted-generated-output
node tools/verify-upstream-placeholders.ts /path/to/extracted-generated-output --release  # must fail: unresolved skips
node --test tools/verify-upstream-placeholders.test.ts
```

Use **Node 24** for normal CI. The local fallback environment only had Node **22.16.0**, so direct execution used `--experimental-strip-types` with **RED 8/8 failures against a stub, GREEN 8/8 passed**, exact full bundle inspection and separate Python audit. The artifact metadata says 15,523 `SKIP(UNIMPLEMENTED)`, no end-to-end compiler passes; the exact archive and source hashes are in `docs/evidence/upstream-placeholder-integrity.json`. The full original 13,006 source identities and seven reference-fatals remain, as do 42 upstream-skipped sources and 39 per-target skip flags. That last skip category is upstream metadata, not an implementation waiver.

**Not executed here:** .NET 10 build, xUnit/MTP discovery of all generated Facts, full `scripts/verify.sh`, a fresh real upstream compiler/reference comparison and formal independent review. This verifier checks what the generator wrote, not that the generated code compiles or its tests are discovered. A later separate lane must compile/discover exactly the same 15,523 stable IDs and then run genuine pinned compiler assertions as implementations land, without weakening `--fail-skips on` for internal regressions. The remaining Fourslash/API/Go/JS/build/watch/profile domains still require expansion; the 15,523 records are not the complete runtime denominator.

Progress delta this package: **0 new product features, 0 accepted compiler cases, 0 resolved SKIPs**; **8 new Node integrity regressions**, confirmed full-target/release-blocking catalogue reconciliation. The branch remains unmerged pending CI and review conditions.

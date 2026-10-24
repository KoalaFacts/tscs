# Native TypeScript tool migration — 2026-10-25

Base: PR #5 head 55390c8ea07540e3da191e4b59ef1209a449a05a.
The owner's Node24/native .ts instruction applies to owned tooling, not upstream
assets. Keep every test and import/CLI entry, update shell/docs/current provenance,
and compare complete inventories before/after; no compiler compatibility credit.

Steps: add failing native-entry guard; rename own .mjs modules/tests and references;
run all Node/Go/C# and frozen checks; generate twice and compare baseline artifact
bytes except generator-path/hash provenance; independent review; commit/persist.
No new dependency or transpilation step. PR #5 reviews currently empty, Codex
summary still running; latest exact-head checks all success. Do not claim review
finished or merge while that state remains unresolved.

Completed: two native-entry guards reproduced RED then GREEN; Node54/54, direct
MTP60/60, frozen29/29, Go test/vet passed. All 8 payloads match baseline exactly;
repeat summary also matches. Independent review found no important issue. Existing
exact Node24 gate remains (no Node25 execution claim). SDK coordinator permission
abort is recorded separately. No upstream acceptance gain. Historical evidence and
plan filenames remain unchanged; upstream Herebyfile.mjs is not owned tooling.

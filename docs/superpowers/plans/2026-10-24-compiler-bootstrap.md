# Compiler Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Deliver the first bounded noEmit compiler slice and independently captured compatibility evidence.
**Architecture:** C# scanner/parser, lexical binder and primitive checker behind a thin CLI. A Node test harness builds/runs a pinned Go oracle adapter and compares serialized diagnostics and CLI output. Production checking never calls another compiler.
**Tech Stack:** .NET SDK 10.0.402, NUnit, Node built-in test runner, Go 1.27.3 for reference only.
**Spec:** docs/superpowers/specs/2026-10-24-tscs-compiler-design.md (approved by user).

## Global Constraints
- Oracle: microsoft/TypeScript 8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f; no floating baseline.
- Unsupported behavior is explicit and nonzero; no implicit successful fallback.
- All fixtures and results carry provenance; no performance or full conformance claim.
- Upstream compiler and regression inventory remains visible; filters do not change the denominator.
- C# production compiler; no JS runtime, IL backend or language server.

## Review Focus
- Unsupported constructs following valid statements must not be silently skipped (Task 2).
- Unicode and CRLF must preserve diagnostic coordinates (Task 2).
- Missing input/config, unsupported options and project-relative paths must fail or resolve predictably (Task 3).
- Empty selections, missing results, altered oracle evidence and subprocess failures must never pass (Task 1/4).
- Scope shadowing, incompatible assignments and const mutation need semantic tests (Task 2).

### Task 1: Freeze reference and fixtures
Files: upstream.lock.json, tools/oracle/main.go, tools/conformance.mjs, tools/conformance.test.mjs, fixtures/, tests/conformance/, docs/progress/.
Interfaces: oracle adapter accepts file paths and returns JSON {diagnostics,exitCode}; diagnostics have code,category,file,start,length,message,related,messageChain. JS classify(expected,actual) returns pass/mismatch/unsupported/crash/timeout/reference failure/not run.
- [ ] Build pinned reference; record binary and source hashes.
- [ ] Write harness tests for mismatch, absent results, timeout/crash, empty cohort and unsupported classification; run red.
- [ ] Implement runner/comparator and reference capture; run green.
- [ ] Freeze >=20 fixtures with >=5 unmodified upstream files; retain directive options and record deliberate noEmit/noLib isolation overrides.
- [ ] Inventory upstream files and directive option variants, classifying unhandled directives rather than inventing semantics. Commit.

### Task 2: C# compiler core
Files: src/Tscs.Compiler/{Diagnostic,Scanner,Syntax,Parser,Binder,TypeChecker,Compilation}.cs; tests/Tscs.Compiler.Tests/CompilationTests.cs and project files; global.json; Directory.Build.props; Tscs.slnx.
Interfaces: Compilation.Check(string file,string text) -> IReadOnlyList<Diagnostic>; diagnostic source positions use UTF-16 offsets, with explicit conversion in reference adapter. Compiler returns TSCS1001 for unsupported constructs. Scanner -> tokens -> syntax statements -> bound scopes -> primitive relations.
- [ ] Write NUnit cases from oracle outputs for valid declarations, TS2322, TS2304, const mutation, lexical scope, malformed input, Unicode/CRLF, unsupported trailing syntax; run red.
- [ ] Port supported scanner/parser and binder/checker behavior with upstream correspondence ledger. Keep unsupported constructs conservative; exclude unsupported declaration forms instead of silently widening to any.
- [ ] Run full NUnit suite and differential cohort; fix real mismatches; commit.

### Task 3: CLI/config boundary
Files: src/Tscs.Cli/{Program,CommandLine}.cs; tests/Tscs.Compiler.Tests/CommandLineTests.cs; fixtures/projects/.
Interfaces: CommandLine.Run(string[] args,TextWriter stdout,TextWriter stderr) -> int. Accept --noEmit, --noLib, --pretty false, --target es2015/es6, file input or -p config. JSON diagnostic mode is tscs-only tooling. Config subset files/noEmit/noLib/target; reject unknown keys and unsupported options.
- [ ] Write tests for file/config success, project-relative paths, missing files, malformed config, unknown options and no input; run red.
- [ ] Implement CLI and config, run green, compare text/exit status with reference; commit.

### Task 4: Reproducibility and handoff
Files: scripts/verify.sh, README.md, NOTICE, docs/PORTING.md, docs/STATUS.md, .github/workflows/ci.yml, checked-in conformance evidence.
- [ ] Test failure gates and evidence hash validation, then implement missing gates.
- [ ] Run clean Release build, all NUnit/Node tests, supported differential cohort twice, CLI checks; record results and remaining full-suite coverage honestly.
- [ ] Independent whole-branch review, fix important findings with regression tests; publish branch/PR with exact validation and limitations.

## Execution ruling
The user's “干” explicitly authorizes implementation after the approved design; proceed inline without another planning approval round. Later compiler milestones are not represented as complete by this bootstrap.

# Structural object data: one end-to-end work package

## Scope and ownership

Interactive execution after the user's 2026-10-28 instruction to execute. Base:
`79fdbaeeefc164d2345571738171baaf8e24f0d0` (PR #17 is already merged).
Continue this work package instead of opening overlapping object-feature PRs.
The bounded product implementation is now present; original compatibility acceptance is not claimed.

## Architecture and acceptance

Reuse the current Scanner, Parser/AST, Binder, shared semantic types,
ModuleLinker, TypeChecker and JavaScriptEmitter. Add required named structural
object members, inline shape annotations, object literals, property reads and
writes, structural assignment, and object parameters/returns across modules.
A single runtime example must create an object in one module, transform it via
a function in another, and read it in a third. Invalid member access and member
assignment must be rejected before output. No filename or input special cases.

Not included: index signatures, optional/readonly members, getters/setters,
methods, inheritance, generics, spread, computed keys or recursive type aliases.
Unsupported forms must fail explicitly, not become any or a successful skip.
Fresh-literal excess-property checks and non-fresh width assignment must remain
distinct. Mutation of a const/imported object's member is not rebinding it.

## Implementation checklist

- [x] Restore the existing verified tools/source into the isolated local workspace;
      execute focused tests there rather than repeatedly pushing RED microchanges.
- [x] Add positive/negative object-program tests and observe the missing support.
- [x] Represent type syntax and object/member expressions in the existing AST;
      preserve source tokens and bounded recursive parsing limits.
- [x] Connect named members, structural relations and declaration inference to
      Binder/TypeChecker, retaining symbol identity across modules.
- [x] Emit object values, member expressions and writes from that same checked AST.
- [x] Run focused regressions, executable cross-module JavaScript, and the normal
      delivery gate once the integrated package is ready; fix actual failures.
- [ ] Record exact-head evidence, request one whole-package independent review,
      and only merge when the existing gates/review are satisfied.

## Environment handoff

The read-only GitHub development-bundle workflow packages the existing installer’s
verified official downloads, the locked NuGet feed, and a Git bundle. It does not
include runner credentials, shell profiles or .git configuration, install system
software, alter versions, or publish a package. Artifacts expire after one day.
Re-run the installer locally in --offline mode to reverify archives and lockfile
hashes; a remote provisioning success is not a local build claim.

## Acceptance ledger

Existing original TS7 targets and SKIP reasons are untouched. Internal object
regressions/runtime examples are not original upstream acceptance. Full dynamic
scope remains unknown until independently expanded and validated.

## Actual implementation and local execution (2026-10-28)

`TypeSyntax` separates source annotations from semantic types. `ObjectTypeSymbol`
holds a read-only named member table. Binder prebinding infers closed object
literals (or uses explicit shapes); the checker retains unknown types rather
than turning failed inference into any. Required-member structural assignment
walks target members with visited type pairs. Contextual literal checks detect
excess properties separately, so storing a wider value does not preserve freshness.

The expression AST carries object/property nodes; property writes mutate values
without rebinding const/import symbols. The existing iterative checker/emitter
handles those nodes, and inline type annotations disappear from emitted output.
Literal/type nesting is bounded and shared type diagnostic display is bounded
rather than expanded exponentially. Prototype-setter literals and other declared
non-goals fail explicitly. Complete TS diagnostic text/span parity is not claimed.

Actual local SDK 10.0.402 build: 0 warnings/errors. Genuine initial object RED:
27 failures / 9 passes, then 36 passes. A later shared-graph display regression
failed (41 pass / 1 fail), then all 42 focused object tests passed after the fix.
The SDK `dotnet test --solution` coordinator actually ran: 15,788 internal tests
passed, zero skips; Node 157 passed and the unchanged frozen cohort was 29/29.
The complete verify log reached its final coverage check. Its container tool
wrapper timed out while the command was still running; no recorded wrapper exit
code is claimed. Stage outcomes and final completion lines are retained directly.

Real Node24 execution of the four emitted object modules returned result=42,
label=seed, enabled=false. An invalid member write returned TS2322 / exit2 and
wrote no JavaScript. The old `bootstrap:object-literal` probe moves from the
unsupported-guard theory to the positive theory with its original source and ID:
local registry 9 implemented / 5 unimplemented, not full upstream acceptance.

A 29-case bounded object semantic differential and the same runtime graph are
wired into the existing fixed-Go reference lane. Before that exact-head CI runs,
its result is NOT_RUN. It compares real exit codes, diagnostic code arrays and
runtime values, not full original runner acceptance or byte-identical formatting.

Local environment recovery additionally needed a user-owned XDG_RUNTIME_DIR,
unsetting inherited PLATFORM/Platform, and NUGET_CERT_REVOCATION_MODE=offline
for cached-only certificate revocation in the disconnected worker. Package
signatures and locked content hashes remain validated; versions/pins unchanged.
No future cache persistence is assumed.

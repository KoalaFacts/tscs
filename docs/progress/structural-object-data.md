# Structural object data: one end-to-end work package

## Scope and ownership

Interactive execution after the user's 2026-10-28 instruction to execute. Base:
`79fdbaeeefc164d2345571738171baaf8e24f0d0` (PR #17 is already merged).
Continue this work package instead of opening overlapping object-feature PRs.
No product implementation or compatibility acceptance is claimed by this plan.

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

- [ ] Restore the existing verified tools/source into the isolated local workspace;
      execute focused tests there rather than repeatedly pushing RED microchanges.
- [ ] Add positive/negative object-program tests and observe the missing support.
- [ ] Represent type syntax and object/member expressions in the existing AST;
      preserve source tokens and bounded recursive parsing limits.
- [ ] Connect named members, structural relations and declaration inference to
      Binder/TypeChecker, retaining symbol identity across modules.
- [ ] Emit object values, member expressions and writes from that same checked AST.
- [ ] Run focused regressions, executable cross-module JavaScript, and the normal
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

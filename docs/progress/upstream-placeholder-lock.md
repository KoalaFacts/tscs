# Locked xUnit/MTP standalone discovery canary — 2026-10-27

This work package addresses the *missing NuGet package lock* blocker for the already generated 15,523 source-configuration placeholders. It changes no compiler feature, never reduces the target denominator, and does not replace the real upstream reference assertion.

## Exact, hash-bound dependency graph

The archive's generated test project declares only xunit.v3.mtp-v2 4.0.2. Its separate reviewed lock is derived from the existing compiler-suite packages.lock.json (Git blob 5e455c0b5b4f10ce061de1f0706337fdf6296a4b; SHA-256 66df11cc3441bf528b1769abdc3dcc5db213d445e4872bc91fa8aee6b8f0f4a0), preserving all 16 pinned NuGet package versions, dependency edges and content hashes. It excludes precisely the three local Project entries. The resulting file has Git blob c3c5190bdded388f6eb8afc01be56936c733f657 and SHA-256 57f3ee5c212e2dd17dd07e73208aa230440091377e0a307772ffbff26b8d2fc4.

The new native .ts lock verifier rejects an altered source hash, added or missing package, changed version/content hash, unexpected framework or leaked project reference. This is a projection of already locked official packages, not an unreviewed upgrade; a real dotnet restore --locked-mode remains the runtime proof.

## Standalone real-discovery canary and full-corpus path

The separate Tscs.Upstream.DiscoveryProbe test project has the same framework, target, xUnit4/MTP2 package reference and reviewed NuGet graph as the external generated bundle. Its one deliberately skipped test binds a **real** upstream configuration from the frozen 15,523 corpus: tsc/testdata/tests/cases/compiler/2dArrays.ts with ID configuration:67cb1471604eb4443e5e229479f1458d2551e01344642b8b7d7e3795b626c824. It remains SKIP(reason=UNIMPLEMENTED); this is not a product capability or upstream acceptance result.

After the existing strict scripts/verify.sh, CI invokes scripts/verify-upstream-lock-probe.sh. That script performs locked restore, serial Release build and actual xUnit4/MTP2 CTRF reporting. It validates the real test name/ID/origin/status through the existing fail-closed verifier. Missing discovery records, unexpected PASS/FAIL, missing CTRF, restore/build failures and altered hashes must fail.

The original scripts/verify-upstream-placeholder-discovery.sh first verifies **all** source and C#-Fact artifacts, then verifies and copies the reviewed standalone lock to the **extracted working copy** of the external archive, refusing an arbitrary existing lock. It uses repository global.json, disabled DOTNET_CLI_TELEMETRY_OPTOUT / TESTINGPLATFORM_TELEMETRY_OPTOUT, locked restore, serial build and actual all-ID discovery. The archive itself is not overwritten. Only this separate lane uses --fail-skips off and only to expose registered SKIPs; the internal suite retains --fail-skips on with minimum 15,488.

## Evidence and disclosure

Local Node22.16.0 experimental loader: new tests first failed on missing code, then **5/5 PASS**; standalone CLI independently reconciled the 16 exact package versions and hashes against the existing locked suite. Both shell scripts passed bash -n. The original source lock bytes were reconstructed and independently SHA/Git-blob matched to the precise repository object; standalone lock bytes likewise match the published blob.

At commit author time, .NET 10 SDK is **absent locally**; no local restore/build/MTP canary or full-corpus MTP discovery is claimed. Each new hosted CI head must be checked independently; historical green jobs do not qualify. The external 15,523-test archive remains in Library libfile_00d083171c708191a3a2affd64acfeff, not in the repository CI checkout, so a one-test CI canary is not the full 15,523 discovery run.

Counts preserved: 13,006 source identities; 15,523 configuration targets (15,495 compiler, 28 transpile) remain source-bound and explicitly SKIP/UNIMPLEMENTED for the original end-to-end runner. Seven reference-fatal sources and 42 upstream-skipped source flags (39 generated target flags) remain separately recorded; original compiler and referenceCompiler acceptance NOT_RUN; full runtime case expansion UNEXPANDED/unknown. **This work package: +0 actual compiler feature; +0 original upstream accepted case; -0 missing-capability SKIP; releaseEligible=false.**

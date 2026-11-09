# Reproducible local development environment

From a **Linux x64 checkout** with Bash, Python 3, curl, tar/gzip, coreutils,
flock and Git, run:

```sh
bash scripts/setup-dev-environment.sh
source "${TSCS_DEV_ROOT:-${XDG_CACHE_HOME:-$HOME/.cache}/tscs/linux-x64}/env.sh"
```

This installs the exact SDK from `global.json`, Node 24.22.1 and Go 1.27.3
into a user-owned versioned cache. It does not use sudo, modify shell startup
files, replace system runtimes, upgrade `global.json`, or relax package locks.
Official HTTPS release metadata/checksums are retained and the archives are
verified before extraction. Both .NET/MTP telemetry opt-outs are set before
execution; the actual Go telemetry mode is set and checked to be `off`.

Exact NuGet packages are downloaded from NuGet's official flat-container
endpoint into a local feed. `dotnet restore --locked-mode` checks the existing
NuGet content hashes, then a serial Release build runs without restore.
Go auto-toolchain/module network access is disabled in the activated shell.
The Go reference module's dependencies are **not** provisioned by this script;
use the existing pinned reference provisioning lane when they are required.

For later shells, source the same `env.sh`. For a retained cache without network:

```sh
bash scripts/setup-dev-environment.sh --offline
```

`--tools-only` skips package restore/build. `--offline` fails on missing cache
entries instead of silently trying a newer SDK or a public package feed.
Network access is required for first-time downloads. A completely sealed runtime
with no cached binaries cannot be made ready by writing an activation file;
report that concrete blocker, not an installation success. Cache retention across
agent sessions is environment-dependent; the tracked installer is the recovery
path, not a promise that a temporary worker remains alive.

Development smoke checks (not full compiler acceptance):

```sh
dotnet src/Tscs.Cli/bin/Release/net10.0/tscs.dll --noEmit --noLib --target es2015 fixtures/local/number-ok.ts fixtures/minimal.d.ts
dotnet tests/Tscs.Compiler.Tests/bin/Release/net10.0/Tscs.Compiler.Tests.dll --filter-class Tscs.Tests.CompilerHostTests --fail-skips on --minimum-expected-tests 1
```

The `Development environment` workflow exercises a clean installation, locked
restore, build, native `.ts`, focused host tests and offline tool reuse. It leaves
the existing full CI/release gates unchanged. Logs and checksum provenance are
uploaded separately; no generated SKIP is counted as PASS. No tool binaries or
credentials are committed. Setup failures are not hidden or classified as SKIP.

Original upstream acceptance (needs a checkout of the pinned revision, such as
the one `./scripts/verify-original-emptyfile-go.sh` leaves in
`.artifacts/original-empty-go/TypeScript`):

```sh
node tools/materialize-upstream-placeholders.ts tests/Tscs.Upstream.Placeholders/packed /tmp/placeholders.zip
unzip -p /tmp/placeholders.zip targets.jsonl > /tmp/targets.jsonl
node tools/original-acceptance.ts --targets /tmp/targets.jsonl \
  --upstream .artifacts/original-empty-go/TypeScript \
  --tscs src/Tscs.Cli/bin/Release/net10.0/tscs.dll \
  --out /tmp/original-acceptance.json --expect-headline docs/STATUS.md
```

The report lists every target's status and reasons, plus the missing-capability
histograms summarized in [STATUS](STATUS.md#headline).

Standard library syntax and binding check against the same checkout (fails
when a file reachable from the default libs `lib.d.ts` or `lib.es6.d.ts` has a
syntax diagnostic, or binding those files together reports one; see
[STATUS](STATUS.md#standard-library)):

```sh
node tools/check-standard-library.ts --upstream .artifacts/original-empty-go/TypeScript \
  --harness tools/Tscs.FixtureHarness/bin/Release/net10.0/Tscs.FixtureHarness.dll \
  --out /tmp/standard-library.json
```

A2.3 progress: each top-level statement of the pinned `lib.es5.d.ts` checked
alone through the CLI, with a histogram of first diagnostics (a measure, not
library loading; `--lib` picks another pinned lib):

```sh
node tools/check-library-statements.ts --upstream .artifacts/original-empty-go/TypeScript \
  --tscs src/Tscs.Cli/bin/Release/net10.0/tscs.dll --out /tmp/library-statements.json
```

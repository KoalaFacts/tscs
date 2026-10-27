# Development environment

Before reporting a missing .NET SDK, Node, or Go as a development blocker, run
`bash scripts/setup-dev-environment.sh` from this checkout. On Linux x64 it
installs the exact repository SDK, Node 24.22.1 and Go 1.27.3, provisions the
locked NuGet feed, and builds. Activate the generated shell environment with:

```sh
source "${TSCS_DEV_ROOT:-${XDG_CACHE_HOME:-$HOME/.cache}/tscs/linux-x64}/env.sh"
```

Reuse this cache rather than reinstalling unchanged tools. For an already
provisioned disconnected worker, use `--offline`. Do not change `global.json`,
roll forward the SDK, relax locks, or turn telemetry back on to make setup pass.
A first-time worker with no network and no cached binaries is genuinely blocked;
record the actual download error and preserve useful work, never claim that a
script or a successful remote CI run installed binaries into that local worker.

See `docs/DEVELOPMENT.md` for focused build/test commands. Do not repeatedly run
the full suite just to prove the same environment is alive. Existing full CI and
release gates remain required at their normal delivery points.

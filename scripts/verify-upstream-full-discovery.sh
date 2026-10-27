#!/usr/bin/env bash
# Verify all 15,523 original source/configuration targets through a separate,
# real xUnit4/MTP2 SKIP-discovery lane. Never count SKIPs as compiler PASS.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd -P)"
cd "$root" # Honour the repository-pinned global.json and native Node 24.
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1
export GOPROXY=off GOSUMDB=off GOTOOLCHAIN=local
output="${TSCS_FULL_DISCOVERY_RESULTS:-$root/.artifacts/upstream-full-discovery}"
if [[ -e "$output" ]]; then
  echo "Refusing to overwrite existing discovery evidence: $output" >&2
  exit 2
fi
mkdir -p "$output"
output="$(cd "$output" && pwd -P)"
bundle="$output/bundle"
mkdir -p "$bundle"
# The exact historical archive is committed as twenty SHA-256-bound Git blobs.
# Assemble and verify before extraction; do not reach out to Library/network.
node "$root/tools/materialize-upstream-placeholders.ts" \
  "$root/tests/Tscs.Upstream.Placeholders/packed" "$output/upstream-placeholders.zip" \
  2>&1 | tee "$output/archive.log"
unzip -q "$output/upstream-placeholders.zip" -d "$bundle"
# The existing independent source/Fact verifier runs inside this locked runner
# before dotnet restore; it also checks every target's source/config binding.
"$root/scripts/verify-upstream-placeholder-discovery.sh" "$bundle" "$output/mtp" \
  2>&1 | tee "$output/runner.log"

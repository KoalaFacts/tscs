#!/usr/bin/env bash
# Separate opt-in lane for all 15,523 generated configuration targets.
# The strict internal test suite continues using --fail-skips on.
set -euo pipefail
if [[ $# -ne 2 ]]; then
  echo "Usage: scripts/verify-upstream-placeholder-discovery.sh EXTRACTED_BUNDLE NEW_RESULTS_DIRECTORY" >&2
  exit 2
fi
root="$(cd "$(dirname "$0")/.." && pwd -P)"
bundle="$(cd "$1" && pwd -P)"
results="$2"
if [[ -e "$results" ]]; then
  echo "Refusing to overwrite existing results directory: $results" >&2
  exit 2
fi
mkdir -p "$results"
results="$(cd "$results" && pwd -P)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1
export GOPROXY=off GOSUMDB=off GOTOOLCHAIN=local
node "$root/tools/verify-upstream-placeholders.ts" "$bundle" | tee "$results/source-ledger.json"
project="$bundle/Tscs.Upstream.Placeholders.csproj"
reviewed_lock="$root/tests/Tscs.Upstream.DiscoveryProbe/packages.lock.json"
source_lock="$root/tests/Tscs.Compiler.Tests/packages.lock.json"
if ! node "$root/tools/upstream-placeholder-lock.ts" "$source_lock" "$reviewed_lock" > "$results/lock-verification.json"; then
  echo "BLOCKED: reviewed xUnit4/MTP2 package graph differs from internal locked suite" | tee "$results/blocker.txt" >&2
  exit 3
fi
# Never accept an arbitrary pre-existing lock in the extracted evidence.
# The archive is immutable; only the extracted working copy gets the lock.
if [[ -e "$bundle/packages.lock.json" ]]; then
  if [[ ! -f "$bundle/packages.lock.json" ]] || ! cmp -s "$reviewed_lock" "$bundle/packages.lock.json"; then
    echo "BLOCKED: generated project's package lock differs from reviewed pinned lock" | tee "$results/blocker.txt" >&2
    exit 3
  fi
else
  cp "$reviewed_lock" "$bundle/packages.lock.json"
fi
cd "$root"  # Honour pinned global.json even when the caller starts elsewhere.
DOTNET="${DOTNET:-dotnet}"
"$DOTNET" restore "$project" --locked-mode --disable-parallel -m:1 -p:NuGetAudit=false 2>&1 | tee "$results/restore.log"
"$DOTNET" build "$project" -c Release --no-restore -m:1 -p:UseSharedCompilation=false 2>&1 | tee "$results/build.log"
"$DOTNET" run --project "$project" -c Release --no-build -- \
  --report-xunit-ctrf --report-xunit-ctrf-filename discovery.ctrf.json \
  --results-directory "$results" --fail-skips off 2>&1 | tee "$results/discovery.log"
report="$results/discovery.ctrf.json"
if [[ ! -f "$report" ]]; then
  echo "BLOCKED: xUnit/MTP did not produce the required CTRF report" >&2
  exit 3
fi
node "$root/tools/verify-upstream-discovery.ts" "$bundle" "$report" | tee "$results/reconciled.json"

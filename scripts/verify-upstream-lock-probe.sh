#!/usr/bin/env bash
# Real hosted .NET 10 / xUnit 4 / MTP2 canary for the standalone lock and
# skipped-target CTRF semantics. Separate from strict internal xUnit gate.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd -P)"
cd "$root"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1
export GOPROXY=off GOSUMDB=off GOTOOLCHAIN=local
project="tests/Tscs.Upstream.DiscoveryProbe/Tscs.Upstream.DiscoveryProbe.csproj"
node tools/upstream-placeholder-lock.ts tests/Tscs.Compiler.Tests/packages.lock.json tests/Tscs.Upstream.DiscoveryProbe/packages.lock.json
DOTNET="${DOTNET:-dotnet}"
"$DOTNET" restore "$project" --locked-mode --disable-parallel -m:1 -p:NuGetAudit=false
"$DOTNET" build "$project" -c Release --no-restore -m:1 -p:UseSharedCompilation=false
results="$(mktemp -d)"
"$DOTNET" run --project "$project" -c Release --no-build -- \
  --report-xunit-ctrf --report-xunit-ctrf-filename discovery.ctrf.json \
  --results-directory "$results" --fail-skips off
report="$results/discovery.ctrf.json"
if [[ ! -f "$report" ]]; then
  echo 'Canary failed: no real MTP CTRF report produced' >&2
  exit 1
fi
node --input-type=module - "$report" "$root/tests/Tscs.Upstream.DiscoveryProbe/expected.json" <<'JS'
import {readFileSync} from 'node:fs';
import {reconcileUpstreamDiscovery} from './tools/verify-upstream-discovery.ts';
const report=JSON.parse(readFileSync(process.argv[2],'utf8'));
const expected=JSON.parse(readFileSync(process.argv[3],'utf8'));
const result=reconcileUpstreamDiscovery([expected],report,{expected:1});
if(result.registered!==1||result.discovered!==1||result.passed!==0||result.skippedUnimplemented!==1) throw Error('Canary incorrectly classified discovery');
console.log(JSON.stringify({origin:'REAL_MTP_DISCOVERY_PROBE',...result}));
JS

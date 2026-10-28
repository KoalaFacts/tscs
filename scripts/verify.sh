#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET="${DOTNET:-dotnet}"
export DOTNET
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1
"$DOTNET" restore Tscs.slnx --locked-mode --disable-parallel -m:1
"$DOTNET" build Tscs.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
"$DOTNET" test --solution Tscs.slnx -c Release --no-build --no-restore -- --fail-skips on --minimum-expected-tests 15488
go telemetry off
if [[ "$(go env GOTELEMETRY)" != "off" ]]; then echo "Go telemetry must be disabled" >&2; exit 1; fi
export GOPROXY=off GOSUMDB=off GOTOOLCHAIN=local
go test tools/scope-go/main.go tools/scope-go/main_test.go
node --test tools/*.test.ts
node tools/verify-conformance.ts
node tools/verify-js-emit.ts
node tools/verify-named-types-emit.ts
node tools/verify-literal-types-emit.ts
node tools/verify-discriminant-emit.ts
node tools/verify-guard-emit.ts
node tools/implementation-coverage.ts --check

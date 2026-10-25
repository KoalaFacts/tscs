#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET="${DOTNET:-dotnet}"
export DOTNET
"$DOTNET" restore Tscs.slnx --locked-mode --disable-parallel -m:1
"$DOTNET" build Tscs.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
"$DOTNET" test --solution Tscs.slnx -c Release --no-build --no-restore -- --fail-skips on --minimum-expected-tests 7853
go test tools/scope-go/main.go tools/scope-go/main_test.go
node --test tools/*.test.ts
node tools/verify-conformance.ts

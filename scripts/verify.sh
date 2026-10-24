#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET="${DOTNET:-dotnet}"
export DOTNET
"$DOTNET" restore Tscs.slnx --locked-mode --disable-parallel -m:1
"$DOTNET" build Tscs.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
"$DOTNET" test Tscs.slnx -c Release --no-build --no-restore -m:1
node --test tools/*.test.mjs
node tools/verify-conformance.mjs

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
node tools/verify-readonly-emit.ts
node tools/verify-literal-types-emit.ts
node tools/verify-discriminant-emit.ts
node tools/verify-guard-emit.ts
node tools/verify-typeof-emit.ts
node tools/verify-conditional-emit.ts
node tools/verify-compound-guards-emit.ts
node tools/verify-bitwise-emit.ts
node tools/verify-division-emit.ts
node tools/verify-exponent-emit.ts
node tools/verify-numeric-literal-emit.ts
node tools/verify-string-add-emit.ts
node tools/verify-string-primitive-emit.ts
node tools/verify-type-operators-emit.ts
node tools/verify-switch-emit.ts
node tools/verify-switch-completion-emit.ts
node tools/verify-loop-control-emit.ts
node tools/verify-classic-for-emit.ts
node tools/verify-numeric-mutation-emit.ts
node tools/verify-prefix-update-emit.ts
node tools/verify-postfix-update-emit.ts
node tools/verify-throw-emit.ts
node tools/verify-try-finally-emit.ts
node tools/verify-try-catch-emit.ts
node tools/verify-void-return-emit.ts
node tools/verify-optional-parameters-emit.ts
node tools/verify-default-parameters-emit.ts
node tools/verify-rest-parameters-emit.ts
node tools/verify-spread-arguments-emit.ts
node tools/verify-array-spreads-emit.ts
node tools/verify-object-spreads-emit.ts
node tools/verify-object-shorthand-emit.ts
node tools/verify-object-index-emit.ts
node tools/verify-in-operator-emit.ts
node tools/verify-for-in-emit.ts
node tools/verify-numeric-object-keys-emit.ts
node tools/verify-keyof-object-emit.ts
node tools/verify-indexed-access-emit.ts
node tools/verify-object-intersection-emit.ts
node tools/verify-type-query-emit.ts
node tools/verify-function-types-emit.ts
node tools/verify-typed-arrow-emit.ts
node tools/verify-function-expression-emit.ts
node tools/verify-interface-inheritance-emit.ts
node tools/implementation-coverage.ts --check

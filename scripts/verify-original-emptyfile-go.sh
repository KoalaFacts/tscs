#!/usr/bin/env bash
# Execute the *actual* immutable Go TestLocal/emptyFile.ts runner, not a
# generated xUnit placeholder. A reference PASS is not yet tscs acceptance.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd -P)"
cd "$root"
pin="$(node -p 'JSON.parse(require("node:fs").readFileSync("upstream.lock.json","utf8")).commit')"
if [[ "$pin" != '8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f' ]]; then
  echo "Unexpected upstream pin: $pin" >&2; exit 1
fi
out="${TSCS_ORIGINAL_GO_RESULTS:-$root/.artifacts/original-empty-go}"
if [[ -e "$out" ]]; then
  echo "Refusing to overwrite existing evidence: $out" >&2; exit 1
fi
mkdir -p "$out"
out="$(cd "$out" && pwd -P)"
upstream="$out/TypeScript"
# The pinned upstream root go.work references ../tools, which is outside the
# purposely sparse tsc checkout. Test the pinned tsc module in isolation.
export GOTOOLCHAIN=local GOWORK=off
# The separate module-acquisition phase must validate the checked-in go.sum
# against the official module proxy/sumdb; actual Go test execution is offline.
if [[ "$(go version | awk '{print $3}')" != 'go1.27.3' ]]; then
  echo 'Pinned Go 1.27.3 required' >&2; exit 1
fi
go telemetry off
if [[ "$(go env GOTELEMETRY)" != 'off' ]]; then
  echo 'Actual Go telemetry mode must be off' >&2; exit 1
fi
# A fresh repository checkout prevents an altered local upstream file from
# silently changing oracle behavior. Do not change the immutable pin.
git init "$upstream"
git -C "$upstream" remote add origin https://github.com/microsoft/TypeScript.git
git -C "$upstream" sparse-checkout init --cone
git -C "$upstream" sparse-checkout set tsc
git -C "$upstream" -c protocol.version=2 fetch --depth=1 --filter=blob:none origin "$pin"
git -C "$upstream" checkout --detach "$pin"
if [[ "$(git -C "$upstream" rev-parse HEAD)" != "$pin" ]] ||
   [[ -n "$(git -C "$upstream" status --porcelain --untracked-files=no)" ]]; then
  echo 'Reference source SHA or tracked-tree integrity mismatch' >&2; exit 1
fi
printf 'commit=%s\ntree=%s\n' "$pin" "$(git -C "$upstream" rev-parse HEAD^{tree})" > "$out/upstream-ref.txt"
(
  cd "$upstream"
  sha256sum tsc/go.mod tsc/go.sum tsc/internal/testrunner/compiler_runner.go \
    tsc/internal/testrunner/compiler_runner_test.go \
    tsc/testdata/tests/cases/compiler/emptyFile.ts \
    tsc/testdata/baselines/reference/compiler/emptyFile.js
) > "$out/upstream-sources.sha256"
# This is dependency acquisition only (not an oracle/test outcome): every
# module is constrained by the exact pinned go.mod/go.sum; Go verifies hashes.
# It is intentionally separate from execution, which is strictly offline.
(
  cd "$upstream/tsc"
  env GOPROXY=https://proxy.golang.org GOSUMDB=sum.golang.org \
    GOTOOLCHAIN=local go mod download
)
export GOPROXY=off GOSUMDB=off GOTOOLCHAIN=local
if [[ "$(go env GOPROXY)" != 'off' || "$(go env GOSUMDB)" != 'off' || "$(go env GOTOOLCHAIN)" != 'local' || "$(go env GOWORK)" != 'off' || "$(go env GOTELEMETRY)" != 'off' ]]; then
  echo 'Go offline/telemetry execution policy not in effect' >&2; exit 1
fi
(
  cd "$upstream/tsc"
  go mod verify
) > "$out/modules-verify.log" 2>&1
printf '%s\n' 'go test -json -count=1 -run ^TestLocal/emptyFile[.]ts$ ./internal/testrunner' > "$out/command.txt"
set +e
(
  cd "$upstream/tsc"
  timeout --kill-after=30s 900s \
    go test -json -count=1 -run '^TestLocal/emptyFile[.]ts$' \
      ./internal/testrunner > "$out/reference.jsonl" 2> "$out/reference.stderr"
)
status=$?
set -e
printf '%s\n' "$status" > "$out/exit-code.txt"
# Even an apparently clean Go exit can mask a no-selection or t.Skip. Fail
# closed on any missing/duplicate nested test, skip, failure or truncated log.
node "$root/tools/verify-original-go-case.ts" \
  "$out/reference.jsonl" "$status" "$out/reference-summary.json"
# Go runner may generate untracked local baselines, but tracked reference
# source files are immutable and may not be rewritten during execution.
if [[ -n "$(git -C "$upstream" status --porcelain --untracked-files=no)" ]]; then
  echo 'Reference runner modified tracked source tree' >&2; exit 1
fi
printf '%s\n' 'Original pinned Go TestLocal/emptyFile.ts completed; tscs product-side original acceptance remains NOT_RUN.'

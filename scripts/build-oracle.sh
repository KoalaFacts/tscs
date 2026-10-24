#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
root="$PWD"
pin="$(node -p 'JSON.parse(require("node:fs").readFileSync("upstream.lock.json")).commit')"
upstream="${TSCS_UPSTREAM:-$root/.artifacts/TypeScript}"
mkdir -p .artifacts
if [ ! -d "$upstream/.git" ]; then
  git clone --no-checkout https://github.com/microsoft/TypeScript.git "$upstream"
  git -C "$upstream" checkout --detach "$pin"
fi
if [ "$(git -C "$upstream" rev-parse HEAD)" != "$pin" ]; then
  echo 'Upstream pin mismatch; use a dedicated checkout at the pinned commit.' >&2; exit 1
fi
if [ -n "$(git -C "$upstream" status --porcelain --untracked-files=no)" ]; then
  echo 'Upstream tracked sources are modified.' >&2; exit 1
fi
if [ "$(go version | cut -d ' ' -f 3)" != 'go1.27.3' ]; then
  echo 'Reference build requires Go 1.27.3.' >&2; exit 1
fi
# Adapter is test tooling inside the Go internal-package boundary; tracked sources remain unchanged.
mkdir -p "$upstream/tsc/cmd/tscs-oracle"
cp tools/oracle/main.go "$upstream/tsc/cmd/tscs-oracle/main.go"
(cd "$upstream/tsc"; go build -o "$root/.artifacts/tsgo" ./cmd/tsc; go build -o "$root/.artifacts/tscs-oracle" ./cmd/tscs-oracle)
node tools/check-oracle.mjs
node tools/inventory.mjs "$upstream"

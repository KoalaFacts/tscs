#!/usr/bin/env bash
# Linux x64, user-local, repeatable toolchain provisioning. No sudo or curl|sh.
set -euo pipefail
repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
offline=0
tools_only=0
for arg in "$@"; do
  case "$arg" in
    --offline) offline=1 ;;
    --tools-only) tools_only=1 ;;
    *) echo "Usage: bash scripts/setup-dev-environment.sh [--offline] [--tools-only]" >&2; exit 2 ;;
  esac
done
fail() { echo "tscs environment: $*" >&2; exit 1; }
[[ "$(uname -s)/$(uname -m)" == Linux/x86_64 ]] || fail 'This installer supports Linux x64 only.'
for tool in python3 curl tar sha256sum sha512sum flock git; do
  command -v "$tool" >/dev/null || fail "Missing bootstrap prerequisite: $tool"
done
cache="$(realpath -m "${TSCS_DEV_ROOT:-${XDG_CACHE_HOME:-$HOME/.cache}/tscs/linux-x64}")"
mkdir -p "$cache"/{downloads,tools,config/go/telemetry,nuget-feed,nuget-packages,logs}
exec 9>"$cache/.setup.lock"
flock -w 300 9 || fail 'Another setup process holds the environment lock.'
# Establish opt-outs before invoking any installed runtime.
export DOTNET_CLI_TELEMETRY_OPTOUT=1 TESTINGPLATFORM_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export XDG_CONFIG_HOME="$cache/config" GOTOOLCHAIN=local GOPROXY=off GOSUMDB=off GOWORK=off
printf 'off\n' > "$XDG_CONFIG_HOME/go/telemetry/mode"
sdk="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["sdk"]["version"])' "$repo/global.json")"
[[ "$sdk" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail 'global.json does not pin a stable SDK.'
node_version=24.22.1
go_version=1.27.3
dotnet_dir="$cache/tools/dotnet-$sdk"
node_dir="$cache/tools/node-$node_version"
go_dir="$cache/tools/go-$go_version"
fetch() {
  local url="$1" dest="$2"
  [[ -s "$dest" ]] && return 0
  [[ "$offline" == 0 ]] || fail "Offline cache is missing: $dest"
  echo "Downloading ${url%%\?*}"
  if ! curl --fail --location --proto '=https' --proto-redir '=https' --tlsv1.2 \
      --connect-timeout 15 --max-time 300 --retry 2 --output "$dest.part" "$url"; then
    rm -f -- "$dest.part"
    fail "Download failed: $url. No SDK/version fallback was applied."
  fi
  mv -- "$dest.part" "$dest"
}
install_archive() {
  local url="$1" digest="$2" bits="$3" dest="$4" strip="$5"
  local archive="$cache/downloads/${url##*/}" stage
  [[ "$bits" == 256 || "$bits" == 512 ]] || fail 'Invalid checksum algorithm.'
  [[ "$digest" =~ ^[0-9a-fA-F]+$ && "${#digest}" == "$((bits / 4))" ]] || fail 'Invalid official checksum.'
  if [[ -f "$dest/.verified-archive" ]]; then
    [[ "$(cat "$dest/.verified-archive")" == "$digest" ]] || fail "Installed tool checksum marker differs: $dest"
    return 0
  fi
  [[ ! -e "$dest" ]] || fail "Incomplete installation exists: $dest (inspect it before retrying)."
  fetch "$url" "$archive"
  printf '%s  %s\n' "$digest" "$archive" | "sha${bits}sum" --check --status || fail "Checksum mismatch: $archive"
  stage="$(mktemp -d "$cache/tools/.stage.XXXXXXXX")"
  if ! tar --extract --file "$archive" --directory "$stage" --strip-components="$strip" --no-same-owner; then
    rm -rf -- "$stage"
    fail "Cannot extract verified archive: $archive"
  fi
  printf '%s\n' "$digest" > "$stage/.verified-archive"
  mv -- "$stage" "$dest"
}
# Metadata is retained with the downloaded archives for disconnected reuse.
channel="${sdk%.*}"
dotnet_metadata="$cache/downloads/dotnet-$sdk-releases.json"
go_metadata="$cache/downloads/go-$go_version-releases.json"
node_metadata="$cache/downloads/node-$node_version-SHASUMS256.txt"
fetch "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/$channel/releases.json" "$dotnet_metadata"
fetch 'https://go.dev/dl/?mode=json&include=all' "$go_metadata"
fetch "https://nodejs.org/dist/v$node_version/SHASUMS256.txt" "$node_metadata"
python3 - "$sdk" "$go_version" "$node_version" "$dotnet_metadata" "$go_metadata" "$node_metadata" > "$cache/downloads/toolchain.tsv" <<'PY'
import json, re, sys
from urllib.parse import urlparse
sdk, go, node, dmeta, gmeta, nmeta = sys.argv[1:]
files = []
for release in json.load(open(dmeta))['releases']:
    for item in release.get('sdks', [release.get('sdk', {})]):
        if item.get('version') == sdk:
            files.extend(f for f in item['files'] if f['rid'] == 'linux-x64' and f['url'].endswith('.tar.gz'))
files = {f['url']: f for f in files}
if len(files) != 1:
    raise SystemExit(f'Official .NET metadata must contain exactly one SDK {sdk} linux-x64 archive.')
f = next(iter(files.values()))
u = urlparse(f['url'])
if u.scheme != 'https' or u.hostname not in {'builds.dotnet.microsoft.com', 'dotnetcli.blob.core.windows.net', 'download.visualstudio.microsoft.com'}:
    raise SystemExit('Unexpected .NET download origin.')
print('dotnet', f['url'], f['hash'], 512, sep='\t')
name = f'go{go}.linux-amd64.tar.gz'
gfiles = [f for r in json.load(open(gmeta)) if r['version'] == 'go'+go for f in r['files'] if f['filename'] == name]
if len(gfiles) != 1:
    raise SystemExit(f'Official Go metadata must contain exactly one {name}.')
print('go', 'https://go.dev/dl/'+name, gfiles[0]['sha256'], 256, sep='\t')
name = f'node-v{node}-linux-x64.tar.gz'
lines = [line.split() for line in open(nmeta) if line.split() and line.split()[-1] == name]
if len(lines) != 1 or not re.fullmatch('[0-9a-fA-F]{64}', lines[0][0]):
    raise SystemExit('Official Node checksum entry missing or ambiguous.')
print('node', f'https://nodejs.org/dist/v{node}/{name}', lines[0][0], 256, sep='\t')
PY
while IFS=$'\t' read -r tool url digest bits; do
  case "$tool" in
    dotnet) install_archive "$url" "$digest" "$bits" "$dotnet_dir" 0 ;;
    go) install_archive "$url" "$digest" "$bits" "$go_dir" 1 ;;
    node) install_archive "$url" "$digest" "$bits" "$node_dir" 1 ;;
    *) fail "Unexpected tool: $tool" ;;
  esac
done < "$cache/downloads/toolchain.tsv"
# An explicit activation file avoids modifying .bashrc or system-wide runtimes.
{
  printf 'export TSCS_DEV_ROOT=%q\n' "$cache"
  printf 'export DOTNET_ROOT=%q\n' "$dotnet_dir"
  printf 'export DOTNET=%q\n' "$dotnet_dir/dotnet"
  printf 'export DOTNET_HOST_PATH=%q\n' "$dotnet_dir/dotnet"
  printf 'export GO=%q\n' "$go_dir/bin/go"
  printf 'export PATH=%q:%q:%q:"$PATH"\n' "$dotnet_dir" "$node_dir/bin" "$go_dir/bin"
  printf 'export DOTNET_CLI_HOME=%q\n' "$cache/dotnet-home"
  printf 'export NUGET_PACKAGES=%q\n' "$cache/nuget-packages"
  printf 'export XDG_CONFIG_HOME=%q\n' "$cache/config"
  printf 'export RestoreSources=%q\n' "$cache/nuget-feed"
  printf '%s\n' 'export NuGetAudit=false DOTNET_CLI_TELEMETRY_OPTOUT=1 TESTINGPLATFORM_TELEMETRY_OPTOUT=1' \
    'export DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0' \
    'export GOTOOLCHAIN=local GOPROXY=off GOSUMDB=off GOWORK=off'
} > "$cache/env.sh"
source "$cache/env.sh"
cd -- "$repo"
[[ "$(dotnet --version)" == "$sdk" ]] || fail 'Wrong SDK selected.'
[[ "$(node --version)" == "v$node_version" ]] || fail 'Wrong Node selected.'
[[ "$(go env GOVERSION)" == "go$go_version" ]] || fail 'Wrong Go selected.'
go telemetry off
[[ "$(go env GOTELEMETRY)" == off ]] || fail 'Go telemetry is not off.'
printf '.NET %s; Node %s; Go %s; telemetry off\n' "$sdk" "$node_version" "$go_version" | tee "$cache/logs/versions.log"
if [[ "$tools_only" == 0 ]]; then
  # Exact packages only. NuGet's locked restore checks contentHash against the
  # existing lockfiles; a .nupkg archive hash is NOT a NuGet contentHash.
  python3 - "$repo" "$cache" > "$cache/downloads/packages.tsv" <<'PY'
import json, pathlib, re, subprocess, sys
import xml.etree.ElementTree as E
repo, cache = map(pathlib.Path, sys.argv[1:])
paths = subprocess.check_output(['git','-C',str(repo),'ls-files','-z','*packages.lock.json']).decode().split('\0')
packages = set()
for path in filter(None, paths):
    for framework in json.loads((repo/path).read_text())['dependencies'].values():
        for name, dep in framework.items():
            if dep['type'].lower() == 'project':
                continue
            version = dep['resolved']
            if not re.fullmatch(r'[A-Za-z0-9_.+-]+', name+version) or not dep.get('contentHash'):
                raise SystemExit(f'Invalid/unhashed lock entry: {name}')
            packages.add((name.lower(), version.lower()))
if not packages:
    raise SystemExit('No locked packages found; refusing an unlocked restore.')
for name, version in sorted(packages):
    print(name, version, sep='\t')
root = E.Element('configuration')
sources = E.SubElement(root,'packageSources')
E.SubElement(sources,'clear')
E.SubElement(sources,'add',key='tscs-local',value=str(cache/'nuget-feed'))
E.ElementTree(root).write(cache/'NuGet.Config',encoding='utf-8',xml_declaration=True)
PY
  while IFS=$'\t' read -r name version; do
    package="$name.$version.nupkg"
    fetch "https://api.nuget.org/v3-flatcontainer/$name/$version/$package" "$cache/nuget-feed/$package"
  done < "$cache/downloads/packages.tsv"
  dotnet restore Tscs.slnx --locked-mode --configfile "$cache/NuGet.Config" \
    --disable-parallel -m:1 -p:NuGetAudit=false | tee "$cache/logs/restore.log"
  dotnet build Tscs.slnx -c Release --no-restore -m:1 \
    -p:UseSharedCompilation=false -p:NuGetAudit=false | tee "$cache/logs/build.log"
fi
printf 'Ready. Activate this shell with: source %q\n' "$cache/env.sh"

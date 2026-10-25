# Native TypeScript tool migration — 2026-10-25

The migration converted all 13 owned tool and test modules from .mjs to Node 24
native .ts. Imports, shell commands, CLI entry points and generator provenance
were updated together. Upstream assets, including Herebyfile.mjs, and frozen
compiler inputs remain unchanged. No dependency, loader or transpilation step
was added.

Two native-entry guards reproduced a failure before migration and passed after
it. The recorded migration checks passed Node54/54, direct MTP60/60, frozen29/29,
Go tests/vet and a Release build with no warnings or errors. The SDK test
coordinator's Unix-pipe permission abort is recorded separately from direct MTP.

All eight complete inventory payloads matched the pre-migration baseline byte
for byte. Only the summary's generator path/hash provenance changed; repeated
native generation, including its summary, was byte-identical. The bounded
transpile inventory remains 25 inputs and 28 named configurations. These checks
do not award original upstream compiler or acceptance coverage. The existing
Node24-only inventory gate was retained; Node25 execution was not validated.

The migration evidence is in docs/evidence/native-typescript-tools.json.

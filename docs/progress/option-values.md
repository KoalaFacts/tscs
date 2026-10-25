# Canonical value splitting — 2026-10-25

Base PR7 head92464e4. Chosen compiler-mainline dependency: fixed harnessutil
splitOptionValues before full named products/effective config parsing. C# uses
Go-extracted fixed declaration metadata; actual Go function supplies expectations.

Initial1392 rows had36 duplicate xUnit identities from three duplicated Go
option declarations; minimum4456 gate caught4420 execution. Positional IDs
retain all130 records/127 names. Reverted implementation to stub after this
correction: RED1392fail/3064pass. GREEN4456. Independent review found U+0130
Go/.NET lowercase mismatch; added enum ordering/aliases/dotted-I vectors,
RED4fail/4474pass; one fix pass final4478/4478.1414 vectors retain1024 normal/nil,
186 expected panic and204 expected fatal outcomes. Go exit1 is intentional,
not a successful Go unit suite. -json failed test identities reconcile exactly;
raw output saved, all vector IDs unique. No second independent review.

Node65/65, frozen29/29, Release0/0, repeated golden byte-identical. SDK
coordinator NOT_RUN. Original scope/pin/frozen unchanged. Full source config
products/effective options, raw-invalid string inputs, config AST and compiler
acceptance remain NOT_RUN. Next: product computation, cap25 and names, then
fixed compilerVaryBy expansion across complete fixtures without promoting passes.

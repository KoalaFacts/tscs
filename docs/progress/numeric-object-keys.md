# Safe integer object member names

The C# parser and semantic checker support non-negative safe integer named properties
in object literals, object type annotations and interfaces. Static `obj[0]` reads,
writes, compound assignments, updates, readonly checks and structural relations
use the existing compiler pipeline and ES2015 JavaScriptEmitter.

Dynamic `object[number]` index signatures, negative/fractional/exponent keys and
general computed names remain unsupported. Unknown numeric members are not
fabricated; the prior fail-closed diagnostic is retained. Arrays continue to
use independent numeric indexing.

Acceptance uses positive/negative C# tests, a real multi-module Node runtime,
no-emit invalid programs and twelve new stable-ID pinned TypeScript Go cases.
This is a bounded reference cohort, not original TS7 upstream acceptance.

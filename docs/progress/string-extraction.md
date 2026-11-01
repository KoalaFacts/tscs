# Bounded string extraction and replacement

The C# compiler's existing native member resolver, ordinary checked calls and
ES2015 JavaScript emitter support `charAt`, `charCodeAt`, `substring`,
`indexOf`, `lastIndexOf`, string-to-string `replace`, string-separator
`split`, and `repeat` on known string receivers. `split` yields a fresh
writable `string[]` that reuses checked index and array-method operations.

This does not implement regular expressions, replacement callbacks, unrestricted
standard libraries, contextual inference, or every TypeScript overload.
Unsupported declarations fail closed under the strict finite String contract.

A separate frozen `fixtures/string-extraction/semantic-cases.json` has 28
stable positive/negative source IDs with matching ambient profile. The
`tools/verify-division-reference.ts` runner compares both compilers against
the pinned Go reference with complete per-case raw output, while
`tools/verify-string-extraction-emit.ts` compiles and actually executes four
linked ES2015 modules and verifies invalid programs emit no JavaScript.

These are bounded checks, **not** original full TS7 acceptance. The registered
original scope remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED with additional
dynamic scope UNEXPANDED. Original test IDs, profiles and gates are unchanged.

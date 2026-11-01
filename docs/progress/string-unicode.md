# Checked Unicode and string formatting

The C# native string resolver and ordinary checked-call path support
`trimStart`, `trimEnd`, `padStart`, `padEnd`, `normalize`,
`codePointAt`, and `localeCompare` on known strings. The result of
`codePointAt` is `number | undefined`, and optional formatting arguments
accept explicit `undefined`. Native JS calls preserve runtime semantics.
Padding methods require an ES2017-capable runtime, while normalization and
locale comparison depend on host Unicode/locale support. The ES2015 emitter
does not polyfill those methods.

A separate frozen 28-case source cohort, 28 source-backed xUnit checks,
four linked ES2015 modules executed in Node, 14 negative/no-JS checks and
three malformed ambient declarations provide bounded evidence. Pinned Go
comparisons preserve case/source/ambient hashes and exact exit/diagnostic
code arrays. This is **not** original TS7 acceptance: the registered original
scope remains 0 PASS / 15,523 SKIP_UNIMPLEMENTED and dynamic UNEXPANDED.
No source pin, CI minimum, release gate or protection changed.

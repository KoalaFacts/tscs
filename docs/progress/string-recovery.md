# String escape diagnostics and recovery — 2026-10-25

Port malformed hex/Unicode string branches from fixed scanEscapeSequence,
scanUnicodeEscape and scanString into the product scanner. Invalid escape values
retain the consumed raw spelling; scanning continues at exactly the consumed
boundary. Range overflow consumes all hex digits and reports the full digit
span. Empty/partial escapes, absent braces, EOF, trailing backslash and raw CR/LF
recover with the fixed error codes, messages and UTF16 spans. All errors remain
ordered in scanner.Diagnostics; Compilation uses its existing stable ordering.
StringTokenFlags records pinned quote/unterminated/hex/Unicode/invalid flags.

The actual Go Scanner.Scan/ErrorCallback oracle emits complete token streams,
all error records, raw Go base64 values and DecodeJSStringRune-derived UTF16
arrays. It never JSON-marshals lone-surrogate values directly. The bootstrap's
primitive type word `string` is explicitly projected from Go KindStringKeyword
to its existing Identifier token. Other unmapped token kinds fail generation.
323 source streams include both quotes, ASCII/emoji prefixes, terminated/EOF
forms, overflow/long digits, malformed low-surrogate lookahead, raw CR/LF/CRLF,
multiple errors, valid controls and all58 prior plain literal inputs.

Initial RED294fail/7882pass -> new323 vectors all pass;12 old own assertions still
expected the former unsupported/first-error behavior. Their inputs were retained
and assertions updated to the actual Go diagnostics, including both1002 errors
for newline-broken source. Three CLI JSON/range tests plus two integration Facts
bring the final suite to8181/8181. Node69/69, frozen29/29, Go scope tests pass,
Release zero warnings/errors, repeated323 golden byte-identical.
An earlier run before generated data existed failed enumeration/minimum count;
that is retained but excluded from RED. First Node run omitted Go's PATH and
failed7 tool tests (62pass); after supplying the same pinned Go and offline
settings, all69 pass. Neither environment failure is credited as successful.
SDK orchestration NOT_RUN.

Independent review audited the changes against fixed Go, regenerated323 golden
vectors byte-identically, reran the current built8181 suite, and ran diff-check.
It found no blocker; it did not independently rebuild or recreate historical
RED. Hash metadata is author-verified evidence, not an assertion of each C# row.
No second review is claimed.

Compilation returns lexical diagnostics before parser/checker work. Later
unsupported syntax cannot discard earlier lexical errors. This implements
scanner recovery, not full AST/parser diagnostic recovery. Numeric/octal/other
identity escape branches remain explicit TSCS1001; JSON/tsconfig and general
AST/parser are not complete. Original full source/compiler/referenceCompiler/
acceptance remain NOT_RUN; releaseEligible=false; unknown case denominators and
all prior domain/profile gaps remain. Next: remaining string escape branches and
actual JSON scanner/parser/config-host dependencies, then original config cases.

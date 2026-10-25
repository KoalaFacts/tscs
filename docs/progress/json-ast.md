# Bounded product JSON AST/config integration — 2026-10-26

The CLI Options.ReadConfig now consumes the product JsonSyntax tree rather than
System.Text.Json. JsonSyntax reuses Scanner(jsonMode:true), distinguishes JSON
brackets/minus/null and property identifiers without changing the TypeScript
mode. Structured objects/arrays preserve duplicate properties and ordering at
AST level; the existing bootstrap option reader still explicitly rejects
unsupported/duplicate effective settings. It retains its narrow files/noEmit/
noLib/target support. This is not makeUnitsFromTest/effective-options equivalence.

The selected parseJSONText/object/array/value branches support comments, trailing
commas, strings, booleans/null and decimal integer/minus values. Quote validation
runs recursively after tree construction, matching pinned validateJsonValue and
validateJsonObjectLiteral. Diagnostic spans skip leading trivia. Scanner errors
precede AST quote errors. ConfigSyntaxFailure transports these through -p JSON/
text output with the config filename/text, using exit2 for syntax,1 for unsupported.

22 actual Go ParseSourceFile(ScriptKindJSON) inputs compare the full selected
projection: kind, ordered property-name/value and array paths, UTF16 start/end,
string/identifier code units, number text and all diagnostic messages/ranges.
Go string values use DecodeJSStringRune/UTF16 arrays to preserve CESU8/WTF8.
Includes empty input, nested containers, duplicate properties, comments/trailing
commas, primitive roots, quote errors, malformed/legacy numeric string escapes,
Unicode/isolated surrogate values and a real supported bootstrap config shape.
Two CLI RED regressions require product identity-escape decoding and TS1327 for
single-quoted config. Initial RED23fail/8558pass ->8581/8581. Initial stub class
name JsonNode collided with System.Text.Json.Nodes in old tests; that build
failure is preserved separately and excluded from RED, then renamed JsonSyntaxNode.

Independent review regenerated all22 Go vectors identically and ran28 current
JsonAstTests. It found P2: host char.IsLetter accepts U+2E2F, whereas the fixed
Go parser reports invalid-character/recovery diagnostics. Four non-ASCII
unquoted-name policy regressions plus a CLI guard RED5fail/8587pass. Author fixed
JSON-mode names to explicit ASCII scope, avoiding false1327-only acceptance;
quoted Unicode stays supported and TS mode is unchanged. One corrective pass,
no second independent review. Raw U+2E2F Go request/recovery result is retained;
policy guards are UNSUPPORTED, not upstream-accepted equivalence.

Final Release0 warnings/errors, direct MTP8592/8592, Node69/69, frozen29/29.
22-vector golden repeated byte-identically. SDK orchestration NOT_RUN. Malformed
recovery, fractions/exponents/other numeric formats, non-ASCII unquoted names,
shorthand/computed properties, expression grammar, large/deep inputs (explicit
bootstrap limits), full config host and compiler/AST/runtime assertions remain
unimplemented or NOT_RUN. Four unsupported grammar guards, TS-mode isolation
and depth guard are own policy tests, not Go compatibility credit. No original
13006-source profiles rerun. All original scope counts, seven reference fatals,
unknown case denominators and compiler/referenceCompiler/acceptance NOT_RUN
remain; releaseEligible=false. Next: actual source-backed config AST profiles
and missing JSON numeric/recovery branches, then effective config/AST mainline.

## Integer value follow-up

The initial22-case tree projection did not expose large-integer canonical text.
That reviewed defect is fixed separately with140 additional actual-Go boundary
cases and an independent3845-value differential; see [number evidence](../evidence/json-numbers.json).
The original22 rows and historical evidence remain unchanged at their original tree.

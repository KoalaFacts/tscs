# JSON integer numeric values — 2026-10-26

Review found Number text diverged inside the documented decimal-integer subset:
9007199254740993 must round to9007199254740992, its separate-minus projection
must use the same rounded operand, and plain decimal10^21 must format1e+21.
Numeric property names and nested values had the same raw-spelling defect.

JsonNumbers.CanonicalInteger accepts only the unsigned decimal-digit spelling
already validated by the scanner. It parses with invariant binary64 semantics,
gets shortest round-tripping digits and applies the ECMAScript fixed/scientific
threshold instead of .NET's default threshold. Zero and positive infinity are
explicit; sign remains a separate unary-minus AST projection, including -0.
All three JsonSyntax number positions use this helper. Raw Token.Text/spans
stay unchanged. TS scanner behavior and supported input grammar do not expand.

Actual fixed ParseSourceFile(ScriptKindJSON) oracle now has162 cases: original22
unchanged plus35 integer spellings in four contexts (positive/negative roots,
nested arrays/objects, numeric property names). Covers2^53 neighbours, shortest
format ties,10^19/20/21/22/100/308/309 neighbours and400-digit overflow. Go probe
and expectations are unchanged; generator adds raw BigInt-derived spellings,
never a JS Number-rounded input. Metadata adds fixed jsnum/string.go SHA.
RED92fail/8640pass ->8732/8732; Node69/69, frozen29/29, Release0 warnings/errors;
162-case generation byte-identical on repeat. SDK orchestration NOT_RUN.

Independent review found no concrete correctness issue in the unsigned integer
domain. It compiled the actual helper independently, compared3845 spellings
against actual fixed Go ParseSourceFile with zero differences/diagnostics, and
ran172 existing Release JsonAstTests. It did not independently run the whole
suite or unsupported numeric grammar. Raw inputs/results/drivers/commands/logs
are preserved separately and indexed by evidence hashes; Git-backed source is
referenced by SHA/pin rather than duplicated in that archive. No second review.

This is a JSON AST value fix, not full scanner or checker numeric support.
Fractions/exponents/nondecimal input, arbitrary identifier/recovery grammar,
effective config, original source-backed config profiles and all original
compiler/referenceCompiler/acceptance assertions remain NOT_RUN/unsupported.
The seven prior reference fatals, original domains/profiles and unknown case
counts stay in the release gate. releaseEligible=false. Next: resume actual
source-backed config AST profile and expose remaining parser/config gaps.

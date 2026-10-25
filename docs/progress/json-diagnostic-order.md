# Mixed JSON diagnostic order — 2026-10-26

Review identified a supported combination incorrectly ordered by phase-wide
scanner-before-parser concatenation. A malformed following value must be reported
after the missing comma, while a malformed property-name token is reported before
the comma diagnostic. Sorting by source offset also gives the wrong result.

Scanner records cumulative lexical diagnostic counts at each emitted token,
including EOF. JsonSyntax Reader emits those events when reading/advancing the
current token, then records parser errors at their actual consumption point.
Lookahead checks the next token kind without publishing its lexical events.
Post-AST quote validation stays last. Original TS token/diagnostic data is unchanged.
General parser recovery and chronology after unsupported syntax are not claimed.

Added16 real Go ParseSourceFile regressions: both review examples, names/values,
nesting, repeated errors, quotes and supplementary UTF16 names. All original162
rows and metadata remain unchanged. Genuine RED12fail/8746pass; adding two CLI
cases and mutating back to the old merger gives RED14/8746. Final MTP8760/8760,
Node75/75, frozen29/29, Release0 warnings/errors. Independent review regenerated
178 golden vectors byte-identically and ran8758 full/188 focused cases before the
two CLI additions; it reviewed those additions without independently running8760.
No important findings, no second review claimed. SDK coordinator NOT_RUN.

[Evidence](../evidence/json-diagnostic-order.json) and all original raw logs are
committed. Prior full-source AST profile binds tree68d6ec65; it was not rerun here.
No effective-options feature/acceptance credit. All original domains, seven
reference fatals and unknown complete denominator stay in scope; releasefalse.
Next: config-host/AST-to-value conversion unless fresh CI/review requires fixes.

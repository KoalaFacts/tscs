# Config AST to values — 2026-10-26

JsonConfigValues converts the supported product JSON AST into a typed config
value tree. This follows fixed convertToObject/convertToJson with returnValue=true
and no option declarations/notifier callbacks. Objects retain first insertion
position but last value for duplicate keys. Empty keys are omitted after their
value is converted. Arrays filter scalar null values, preserve false/empty
strings and nested arrays, and distinguish allocated empty arrays from nil slices.
Strings/keys preserve UTF16 units; numeric values preserve binary64 bits, including
negative zero and overflow infinities. Empty source has its separate sentinel.

The CLI consumes this component for its supported booleans and target string.
Its existing file/duplicate restrictions remain enforced by the AST reader;
this is not the whole config host or option-declaration conversion pipeline.
The API accepts only supported ASTs; a null root means empty source, never a
successful replacement for unsupported parse failure. Other AST shapes, option
notifier validation, host/extends/globs and effective configuration remain unrun.

The actual Go overlay calls private convertToObject after real ParseSourceFile.
It projects OrderedMap iteration, UTF16 units, exact float64 bits and array nilness,
not encoding/json's lossy representation of surrogate values or numeric infinity.
189 vectors reuse178 parser sources plus11 value-specific inputs. Conversion
errors are empty for this supported AST domain; parser diagnostics are separate
and are not erased or relabelled as successful config parsing. Genuine stub
RED189fail/8760pass -> GREEN8949/8949. Node75/75, frozen29/29 and Release0 warnings/
errors; reference regeneration is byte-identical. SDK coordinator NOT_RUN.

Independent review found no important issue, regenerated189 Go vectors byte-identically,
rebuilt Release serially with0 warnings/errors and reran8949 tests. Its initial
default parallel build failed without diagnostic; that raw attempt is retained
and excluded, with no cause inferred. No second review.

[Evidence](../evidence/config-values.json) and complete goldens/raw logs are in
Git. No full source-backed13006/172 config profile rerun or compiler acceptance
credit. Original pin/static inventory/frozen scope, seven reference fatals and
unknown executable-case denominator remain; releaseEligible=false. Next: connect
source-backed config values and port the actual config host/option conversion
callbacks, preserving errors/defaults/paths instead of guessing effective options.

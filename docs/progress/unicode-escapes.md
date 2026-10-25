# Valid Unicode/hex string escapes — 2026-10-25

Port valid x2/u4/braced Unicode branches from fixed scanEscapeSequence into the
product scanner. Preserve UTF16 code units rather than treating lone surrogates
as replacement characters. Adjacent halves naturally retain JavaScript UTF16
semantics, including mixed fixed/braced forms. Bound accumulation at10FFFF before
another multiplication; arbitrarily many leading zeroes remain valid.
164 actual-Go vectors include control/ASCII/BMP/maximum Unicode, lone high/low
surrogates, mixed pairs and long-leading-zero forms, both quotes and ASCII/emoji
prefixes. Oracle calls actual DecodeJSStringRune, stores raw base64 plus numeric
UTF16 arrays; never marshals invalid Go string values as JSON text.
RED164fail/7686pass -> GREEN7850;3 CLI exit0 additions ->7853/7853.
Node69/frozen29/Release0 warnings/errors, repeated golden byte-identical.
Own previous unported-valid-u/x cases become malformed guards; valid forms are
covered by the actual-Go matrix, not removed/waived. Frozen29 stays unchanged.
Independent review found no blocker and audited all164 raw/UTF16 values, token
ends and provenance. It did not rerun builds/Go generation. No second review.
Malformed Unicode/hex/out-ofrange and other escape branches remain TSCS1001,
not faithful upstream diagnostics/recovery. General token flags not ported.
Next: malformed escape flags/diagnostics and JSON scanner/parser AST/config host.
No full source/whole compiler acceptance execution; releaseEligible=false.

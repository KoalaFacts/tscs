# Product simple string escapes — 2026-10-25

Implement the simple scanEscapeSequence branches required before JSON/config AST
work, while keeping all unported branches explicit. Add decoded Token.Value;
retain original spelling/source offsets. Friend test assembly reads internal
scanner tokens without creating a product test API.
60 initial real-Go vectors compare both quotes,15 escapes/continuations and
ASCII/supplementary prefixes: RED60fail/7613pass -> GREEN7673. Add2CLI exit cases.
Independent static review found no blocker and verified source/probe/60 identities;
reviewer did not rerun. Address coverage notes: following-token/preceding-trivia
LineBreak assertions,6 real-Go mixed/repeated examples, trailing-backslash guard.
Final7682/7682, Node69, frozen29, Release0 warnings/errors.66 golden repeat equal.
No second review claimed. Existing58 plain strings now assert decoded values.
Unicode/hex/numeric/other identity escapes and trailing backslash remain TSCS1001;
this is not full Go error recovery or full string/config/AST support.
Raw logs are committed under docs/evidence/string-escapes-run. Original compiler
acceptance and full source profiles were NOT_RUN. Next: Unicode/hex escape and
error flag/diagnostic dependencies, then faithful JSON scanner/parser config path.

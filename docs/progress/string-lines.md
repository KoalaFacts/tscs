# Product string scanner — 2026-10-25

Pinned scanString accepts raw LS/PS inside ordinary strings; the bootstrap used
its shared four-character IsLineBreak predicate and incorrectly rejected them.
Change only the string loop to CR/LF. Preserve trivia/comment/ASI handling.
58 real-Go lexical inputs cover both quotes, controls/Unicode spaces/LS/PS and
supplementary text. Four of those fail before the fix, alongside two CLI regressions.
Two outside-string ASI cases and two CR/LF CLI errors keep existing behavior.
Initial build had an unused-id analyzer error; old-DLL minimum gate correctly
rejected7544/7604. It is excluded. After fixing test identity use, genuine
RED6fail/7602pass -> GREEN7608/7608. Node69, frozen29, build zero warnings/errors.
Golden repeated identical. Independent static review found no blocker, did not
rerun tests. Accepted cases assert diagnostics rather than token value; CR/LF
location examples do not test supplementary text before the error.
Raw logs are committed under docs/evidence/string-lines-run. No full corpus
or complete compiler acceptance credit. Next: faithful JSON scanner/parser
and effective config host; do not substitute System.Text.Json for upstream JSONC.

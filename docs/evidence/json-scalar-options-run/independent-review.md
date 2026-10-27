# Independent scalar-option review

Reviewed base `55c3bb83039f8b0e035c73a8142724563da7895a` through head `12b041584a5d03a4b47fcaa208c15928d6140661` in `/workspace/scratch/daf3e6c2e318/tscs`.

Critical: none. Important: none.

Minor documentation finding: `docs/progress/json-option-types.md:13-17` still describes invalid noEmit/noLib types as preserving the bootstrap input-error policy. That was true of the previous step, but this step now deliberately reports TS5024 / exit 2. Add a superseded-by note linking to the scalar-options progress document, or update the current-tense statement.

The bounded conversion agrees with the pinned private Go primitive branch: command-line-only rejection occurs before null/type admission and points to the property name; type errors point to the value; accepted values are preserved; errors yield null; compiler diagnostics use start -1 and length 0. Source and node must both be present for a located diagnostic. The CLI changes only noEmit/noLib integration and retains null defaults, explicit CLI overrides, and bootstrap requirements. Actual Go declarations determine supported scope, with all 126 declarations retained and 100 supporting 3000 vectors. The probe calls actual private convertJsonOption and actual convertToObject; expected semantics are not handwritten.

Executed successfully with PATH=/tmp/tscs-go/go/bin:/tmp/tscs-dotnet:$PATH, DOTNET_CLI_TELEMETRY_OPTOUT=1, XDG_CONFIG_HOME=/tmp/tscs-offline-config, GOPROXY=off, GOSUMDB=off, GOTOOLCHAIN=local:

- `TSCS_STRING_LOG=/tmp/tscs-scalar-review/go-oracle.log node tools/generate-json-scalar-options.ts .artifacts/ts7-upstream /tmp/tscs-scalar-review/json-scalar-options.json`: exit 0; 126 declarations / 100 supported / 3000 vectors. Actual Go test passed.
- `cmp tests/Tscs.Compiler.Tests/TestData/json-scalar-options.json /tmp/tscs-scalar-review/json-scalar-options.json`: exit 0. Both SHA256 `79bfc0854897d5304048b80505adbac0e3c103710faf7a50b8d6405daf718da2`.
- `dotnet build Tscs.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false`: exit 0, zero warnings/errors.
- `dotnet tests/Tscs.Compiler.Tests/bin/Release/net10.0/Tscs.Compiler.Tests.dll --minimum-expected-tests 13946`: exit 0, 13946 passed / 0 failed / 0 skipped.
- `git diff --check 55c3bb83039f8b0e035c73a8142724563da7895a 12b041584a5d03a4b47fcaa208c15928d6140661`: exit 0.

Limits: no network restore, repository source edits, commits, merges, pushes, or third-party contact. Node and frozen suites were not independently rerun. No broader option/host/notifier/source-profile/compiler-acceptance claims. Golden covers paired absent and paired present source/node contexts; mixed contexts were inspected in code rather than independently exercised. The author changed README/status/verification files during this review; those uncommitted changes are outside the reviewed commit range. The standard verification test floor was observed updated to 13946 by the author.

# Named configuration products — 2026-10-25

Basea44bb53. Port GetFileBasedTestConfigurations after canonical value splitting.
Plan: actual Go helper oracle, failing xUnit vectors, products/cap25/names and nil
implementation, full suite and independent review, save source and raw evidence.
The helper returns nil when no varying/non-varying value remains; runner fallback
is separate. Effective options, raw source settings extraction and full fixture
configuration inventory remain NOT_RUN. Ordering is compared as a normalized
configuration multiset, not Go map/product enumeration order.

Stub RED1432fail/4478pass. This matrix retains1414 existing value inputs as
single-axis maps plus18 product/cap/nil/raw-scalar/case/description-tie cases.
Go fatal outcomes are expected failures and validated from -json test identities,
not a claim of a successful Go unit suite. Every request echo is checked.

Initial GREEN5910/5910. Independent review found55 Unicode lower mappings differ
between host .NET and pinned Go17. Added1488 actual-helper Unicode name pair
cases, RED55fail/7343pass. One corrective pass embedded the complete Go lowercase
mapping and changed normalized tie ordering to raw UTF8 key/value tuples on both
sides. Final7398/7398, Node65/65, frozen29/29, Release0/0.2920 helper inputs yield
2526 normal/nil,187panic,207fatal;4328 named records and262 normal nil results.
Go exit1 remains expected, all208 failing test names(root+207rows) verified.
Both golden/table byte-identical on regeneration. No second independent review.
Mixed fatal/panic inputs can have Go map-order-dependent failure precedence;
that broader error profile is not claimed deterministic or verified here.
Next: canonical source setting extraction and complete compilerVaryBy/profile
expansion, with compiler nil versus transpile original-settings fallback kept
separate. Effective config/AST and original compiler acceptance remain NOT_RUN.

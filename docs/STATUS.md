# tscs status

Current state only. Earlier dated status entries and work-package notes are in
git history (this page before the D1 rewrite: `git show 7edd7f5:docs/STATUS.md`);
validation logs stay in [evidence/](evidence/).

## Headline

Original upstream acceptance: **0 PASS / 2 FAIL / 15,521 SKIP**

Measured with the compiler at `7edd7f5` by `tools/original-acceptance.ts`,
which CI reruns on every pull request (job `original-go-reference`) and which
fails when this line disagrees with the measurement. Its denominator is the
15,523 source/configuration targets registered from the pinned revision
(`upstream.lock.json`, TS 7.1.0-dev, commit `8d9b6da`). A target runs
through tscs only when tscs supports everything it needs; otherwise it is
SKIP with each missing capability recorded. A run target is PASS only when
tscs's diagnostics equal the pinned reference's `.errors.txt` baseline and,
every emitted `.js` file equals the reference `.js` baseline's output
sections (none when there is no baseline; line endings normalised). Targets
whose configuration the reference itself never ran (no `.types`/`.symbols`
baseline) are SKIP, and each source must match its registered SHA-256.
`.types`/`.symbols` contents are not compared yet.

Why targets are skipped (each target counts under every capability it
lacks; "only" = targets blocked by that capability alone):

| Missing capability | Targets | Only this |
|---|---|---|
| Standard library (`lib.*.d.ts`) | 15,464 | 5,459 |
| `@module` option | 3,453 | 0 |
| `@target` other than es2015 | 3,172 | 0 |
| Multi-file tests | 2,830 | 0 |
| `@strict: false` | 2,825 | 0 |
| `@declaration` | 2,058 | 0 |
| JavaScript inputs / `allowJs` | 1,211 / 1,114 | 0 |
| Transpile runner, reference-fatal sources | 28, 19 | 28, 19 |

The two executed targets (`compiler/noCrashOnNoLib.ts`,
`conformance/parser/.../parser509698.ts`) FAIL with TSCS1001 unsupported
constructs. The xUnit placeholder suite (`tests/Tscs.Upstream.Placeholders`,
discovered by `./scripts/verify-upstream-full-discovery.sh`) still registers
all 15,523 targets as `SKIP_UNIMPLEMENTED`; its semantics are unchanged.
Dynamic test domains (fourslash, language service) are not expanded, so the
full case denominator is unknown. No release is claimed.

Separate from that headline:

- Internal regressions: 19,586 xUnit tests (Microsoft Testing Platform,
  `--fail-skips on`) plus about 170 Node tool tests and 76 executed-output
  checks (`tools/verify-*-emit.ts`) that run the emitted ES2015 modules.
- A frozen, separate 29-case isolated cohort (`tests/conformance`) is
  compared against the pinned Go oracle (`tools/verify-conformance.ts`). It
  earns no original credit.
- The pinned Go reference executes the original `TestLocal/emptyFile.ts`
  (`./scripts/verify-original-emptyfile-go.sh`, CI job
  `original-go-reference`). tscs emits the matching `"use strict";` in an
  xUnit test, but the acceptance runner counts `emptyFile.ts` as SKIP: its
  configuration loads the standard library.

## What works

Everything below is a bounded subset: constructs outside it report a TSCS
diagnostic and exit 1 rather than being compiled incorrectly.

### Project, host and modules

- CLI: `--noEmit`, `--emitJs`, `--noLib`, `--target es2015`,
  `--module es2015`, `-p`/`--project`, `--pretty false`, `--json`.
- `tsconfig.json` with `files`, explicit `include`/`exclude` globs, `noEmit`,
  `noLib`, `target: "es2015"`, `module: "es2015"`; explicit files win over
  excludes. All `compilerOptions` are parsed and typed per the pinned option
  declarations (scalar, enum, list, `lib`, paths), with JSON diagnostics in
  reference order.
- CompilerHost with real and virtual file systems; triple-slash path
  references; multi-file global scripts. A port of the pinned source decoder
  (UTF-8/UTF-16 BOMs) is tested but not yet wired into file loading.
- Named ES module imports/exports/re-exports, `import type` and type-only
  re-exports, with a deferred module linker and host-canonical diagnostics.

### Syntax

- Decimal, fractional, exponent and radix (`0x`/`0b`/`0o`, separators)
  numeric literals; string escapes, Unicode escapes, line terminators and
  recovery; untagged templates nested up to eight levels.
- Expressions: arithmetic, `**`, bitwise and shift operators, all compound
  assignments including logical (`&&=`, `||=`, `??=`), prefix/postfix
  updates, comparison, `typeof`, `void`, `delete` of optional members,
  `in`, conditional, `??`, value-producing `&&`/`||`, parenthesized comma
  sequences, optional chaining, non-null `!`, `as`, `satisfies`, `as const`,
  array and object spreads.
- Statements: `const`/`let`, finite object/array destructuring with
  defaults, braced or single-statement `if`/`while`/`do`/`for`/`for..of`/
  `for..in`, classic `for` with comma, call and multi-declaration headers,
  `break`/`continue`, `switch`, `throw`, `try`/`catch`/`finally`,
  leading `// @ts-ignore` on the next line.

### Types and checking

- Primitives, `any`, `unknown` (catch variables), `null`/`undefined`,
  numeric/string/boolean literal types and unions, `void`.
- Non-generic `type` aliases and interfaces (named, non-generic `extends`),
  structural object types with optional and `readonly` members, finite
  intersections, excess-property checks on fresh literals.
- Arrays (`T[]`, `Array<T>`, `readonly T[]`, `ReadonlyArray<T>`), fixed,
  labelled and optional tuples, tuple rest expansion.
- Bounded `keyof`, indexed access (`T["k"]`, `T[number]`), `typeof` queries,
  static computed keys, numeric key identity.
- Functions: declarations, typed arrows and function expressions, optional,
  default and rest parameters, typed callback contracts, method signatures,
  object literal methods, inferred braced returns, closures over lexical
  bindings.

### Control flow and narrowing

- Narrowing by `typeof`, truthiness, `===`/`!==` against literals, `null`
  and `undefined`, `in`, `Array.isArray`, discriminant properties, compound
  `&&`/`||`/`!` guards and returning guards; assignments invalidate facts.
- Definite assignment and return-path checks through loops, `switch`
  (including fall-through and exhaustive returns), `throw` and
  `try`/`finally`.
- `switch` case labels: literals, `const` identifiers, finite computed
  primitives, pure conditionals and short-circuits, checked direct, member
  and optional calls, static object data reads.

### Emit

ES2015 scripts and named ES modules are emitted from the checked AST; type
syntax is erased and runtime semantics are preserved (evaluation order,
single evaluation of receivers, native `for..of` Unicode iteration). Output
is executed by the `tools/verify-*-emit.ts` checks.

### Standard library

`lib.*.d.ts` files cannot be loaded. Programs use `--noLib` with
[`fixtures/minimal.d.ts`](../fixtures/minimal.d.ts) (empty global
interfaces). A fixed set of natives is modelled in the checker instead:
array `push`/`pop`/`shift`/`unshift`/`reverse`/`sort`/`fill`/`copyWithin`,
`map`/`filter`/`reduce`/`reduceRight`/`flat`/`flatMap`, queries such as
`indexOf`/`includes`/`find`/`findLast`/`some`/`every`, `Array.from`/`of`/
`isArray`; common `String` methods including `at`, `normalize`,
`replaceAll` and `isWellFormed`; `Object.keys`/`values`/`entries`/`assign`/
`freeze`/`is`/`hasOwn`.

### Conformance harness

- `tools/oracle` and `scripts/build-oracle.sh` build the pinned Go reference;
  `tools/check-oracle.ts` and `tools/verify-conformance.ts` compare
  diagnostics with tscs.
- `tools/original-acceptance.ts` runs the registered original targets
  against a pinned upstream checkout (see [Headline](#headline)).
- `tests/Tscs.Upstream.Placeholders` holds one generated Fact per discovered
  original target, each `SKIP_UNIMPLEMENTED`; release mode fails while any
  remain. The inventory and ledgers are described in
  [testing/ts7/README.md](testing/ts7/README.md).
- `tests/implementation-coverage` is a 14-probe local guard register, not an
  upstream percentage.

## What does not work

Generics (beyond the `Array<T>` spelling), classes, enums, namespaces,
declaration files as input beyond `minimal.d.ts`, declaration emit, source
maps, JSX/TSX, JavaScript inputs, decorators, async/await, generators,
iterators beyond arrays and strings, `this` typing, overloads, mapped and
conditional types, index signatures, module resolution through packages,
`extends` in `tsconfig.json`, default implicit includes, targets other than
ES2015, incremental and watch modes.

## Known gaps

- 15,464 of 15,523 original targets need the standard library; 5,459 need
  nothing else that the runner checks for (roadmap A2).
- The acceptance runner does not yet support multi-file tests, other
  targets or `@module`, and does not compare `.types`/`.symbols`.
- Many constructs were added as narrow, separately verified slices; the
  per-feature `tools/verify-*-emit.ts` scripts and checker paths overlap
  (roadmap C1).

## Roadmap

Ledger for the scheduled task. The headline above is the metric; each row
records the original case IDs it moves.

| ID | Item | Why it matters | Status | Evidence |
|---|---|---|---|---|
| D1 | Short README, current-state STATUS, `tools/check-docs.ts` in CI | Docs that are true and findable | done (PR #210) | at PR #210: README 30,505 → 4,906 bytes, STATUS 123,542 → 8,568 bytes; 15 checker tests |
| A1.1 | Acceptance runner over the 15,523 registered targets: SKIP with missing capabilities, run supported targets through tscs, compare diagnostics and `.js` emit with the pinned baselines, histogram, CI rerun with headline check | Turns the headline into a measured number | in progress | 0 PASS / 2 FAIL (`noCrashOnNoLib`, `parser509698`) / 15,521 SKIP; 6 runner tests |
| A1.2 | Runner support for multi-file tests and `@module`, and `.types`/`.symbols` comparison | Larger runnable set once the library loads | todo | — |
| A2 | Parse, bind and check `lib.es5.d.ts` and the es2015 libs; load them as the default lib | Almost every original case needs the standard library | todo | — |
| A3 | Close the cheapest family of failing original cases from A1's histogram, one family per PR | Moves original cases to PASS | todo | — |
| C1 | Remove duplicated checker/emitter paths and per-feature verify scripts where a shared test proves identical behaviour | Less code to maintain | todo | — |

Last docs housekeeping: 2026-11-06 (D1).

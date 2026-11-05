# tscs status

Current state only. Earlier dated status entries and work-package notes are in
git history (this page before the D1 rewrite: `git show 7edd7f5:docs/STATUS.md`);
validation logs stay in [evidence/](evidence/).

## Headline

Original upstream acceptance: **0 PASS / 0 FAIL / 15,523 SKIP**

Measured on `7edd7f5` by the generated upstream placeholder suite
(`tests/Tscs.Upstream.Placeholders`, materialized and discovered by
`./scripts/verify-upstream-full-discovery.sh` in CI). Every one of the 15,523
source/configuration targets discovered at the pinned revision
(`upstream.lock.json`, TS 7.1.0-dev, commit `8d9b6da`) is
`SKIP_UNIMPLEMENTED`: no original case is yet executed through tscs and
compared with the pinned reference. Dynamic test domains (fourslash,
language service, emit baselines) are not expanded, so the full case
denominator is unknown. No release is claimed.

Separate from that headline:

- Internal regressions: 19,586 xUnit tests on `7edd7f5` (`./scripts/verify.sh`) (Microsoft Testing Platform,
  `--fail-skips on`) plus about 170 Node tool tests and 76 executed-output
  checks (`tools/verify-*-emit.ts`) that run the emitted ES2015 modules.
- A frozen, separate 29-case isolated cohort (`tests/conformance`) is
  compared against the pinned Go oracle (`tools/verify-conformance.ts`). It
  earns no original credit.
- The pinned Go reference executes the original `TestLocal/emptyFile.ts`
  (`./scripts/verify-original-emptyfile-go.sh`, CI job
  `original-go-reference`); tscs emits the matching `"use strict";` for
  that file in an xUnit test, but this is not run through the original
  harness and is not counted as PASS.

## What works

Everything below is a bounded subset: constructs outside it report a TSCS
diagnostic and exit 1; they are intended never to be compiled silently.

### Project, host and modules

- CLI: `--noEmit`, `--emitJs`, `--noLib`, `--target es2015`,
  `--module es2015`, `-p`/`--project`, `--pretty false`, `--json`.
- `tsconfig.json` with `files`, explicit `include`/`exclude` globs, `noEmit`,
  `noLib`, `target: "es2015"`, `module: "es2015"`; explicit files win over
  excludes; any other option is rejected with TSCS1002. A port of the pinned
  `compilerOptions` JSON conversion (scalar, enum, list, `lib`, paths, with
  diagnostics in reference order) is tested against reference vectors but
  not wired into tsconfig loading.
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
ES2015, incremental and watch modes. Within the supported areas: no general
contextual typing or inference for untyped parameters, no overloaded
callbacks, no object-rest or computed binding patterns or general iterable
destructuring, no getters/setters, no `instanceof`, no labelled statements
and no `var`.

## Known gaps

- The original harness does not yet run any original case through tscs, so
  supported constructs carry no original credit (roadmap A1).
- Without the standard library, nearly every original case needs
  `lib.es5.d.ts` before it can be compared (roadmap A2).
- Many constructs were added as narrow, separately verified slices; the
  per-feature `tools/verify-*-emit.ts` scripts and checker paths overlap
  (roadmap C1).

## Roadmap

Ledger for the scheduled task. The headline above is the metric; each row
records the original case IDs it moves.

| ID | Item | Why it matters | Status | Evidence |
|---|---|---|---|---|
| D1 | Short README, current-state STATUS, `tools/check-docs.ts` in CI | Docs that are true and findable | in progress | README 30,505 → under 10,240 bytes; STATUS 123,542 → under 25,600 bytes |
| A1 | Original acceptance runner: run original compiler cases from the pinned revision through tscs, compare with the reference, PASS/FAIL/SKIP per case with a missing-feature histogram, in CI | Turns the headline into a measured number | todo | — |
| A2 | Parse, bind and check `lib.es5.d.ts` and the es2015 libs; load them as the default lib | Almost every original case needs the standard library | todo | — |
| A3 | Close the cheapest family of failing original cases from A1's histogram, one family per PR | Moves original cases to PASS | todo | — |
| C1 | Remove duplicated checker/emitter paths and per-feature verify scripts where a shared test proves identical behaviour | Less code to maintain | todo | — |

Last docs housekeeping: 2026-11-06 (D1).

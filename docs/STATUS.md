# tscs status

Current state only. Earlier dated status entries and work-package notes are in
git history (this page before the D1 rewrite: `git show 7edd7f5:docs/STATUS.md`);
validation logs stay in [evidence/](evidence/).

## Headline

Original upstream acceptance: **3 PASS / 0 FAIL / 15,520 SKIP**

Measured by `tools/original-acceptance.ts` against the frozen TS7 reference,
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
lacks; "only" = targets blocked by that capability alone). These branch counts
were measured from runner source `3ee09e65` by the
[original reference run](https://github.com/KoalaFacts/tscs/actions/runs/42336784587/job/128295378913).
The full 15,523-target report is unchanged by its prefix-collision fix;
a local real-compiler acceptance and standard-library run produced byte-identical
reports. The complete local test gate is still blocked by MTP IPC permissions:

| Missing capability | Targets | Only this |
|---|---|---|
| Standard library (`lib.*.d.ts`) | 15,464 | 5,667 |
| Unsupported `@module` transforms | 3,346 | 0 |
| Unsafe or conflicting virtual file paths | 859 | 0 |
| Virtual symlinks | 23 | 0 |
| `@target` other than es2015 | 3,172 | 2 |
| `@strict: false` | 2,825 | 0 |
| `@declaration` | 2,058 | 0 |
| JavaScript inputs / `allowJs` | 1,211 / 1,114 | 0 |
| Transpile runner, reference-fatal sources | 28, 19 | 28, 19 |

Three original targets execute, all PASS: `conformance/parser/.../parser509698.ts`
(ambient function signatures, style trivia and erased JS),
`compiler/noCrashOnNoLib.ts` (the ten TS2318 missing-global
diagnostics under `@noLib`, and the reference printer's four-space block
indentation and ungrouped spread operands), and
`compiler/arrayIterationLibES5TargetDifferent.ts` (`nolib=true,target=es2015`:
TS5053 for `@lib` with `@noLib`, ambient `declare const`, TS2495 hidden behind
the global diagnostics, and the script `"use strict";` prologue). `@lib`
without `@noLib` counts as needing the standard library.
Executed failures are never converted to SKIP. The xUnit placeholder suite (`tests/Tscs.Upstream.Placeholders`,
discovered by `./scripts/verify-upstream-full-discovery.sh`) still registers
all 15,523 targets as `SKIP_UNIMPLEMENTED`; its semantics are unchanged.
Dynamic test domains (fourslash, language service) are not expanded, so the
full case denominator is unknown. No release is claimed.

Separate from that headline:

- Internal regressions: 19,586 xUnit tests on `7edd7f5` (`./scripts/verify.sh`) (Microsoft Testing Platform,
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
diagnostic and exit 1; they are intended never to be compiled silently.

### Project, host and modules

- CLI: `--noEmit`, `--emitJs`, `--noLib`, `--target es2015`,
  `--module es2015`, `-p`/`--project`, `--pretty false`, `--json`;
  `--lib <names>` only with `--noLib`, reporting the reference's TS5053
  (the named libraries are not loaded).
- Like the pinned `tsc`, option and global diagnostics (TS5053, TS2318) are
  printed instead of semantic ones, and output is still emitted then; any
  other semantic diagnostic withholds output, and syntax errors or TSCS
  diagnostics always do.
- `tsconfig.json` with `files`, explicit `include`/`exclude` globs, `noEmit`,
  `noLib`, `target: "es2015"`, `module: "es2015"`; explicit files win over
  excludes; any other option is rejected with TSCS1002. A port of the pinned
  `compilerOptions` JSON conversion (scalar, enum, list, `lib`, paths, with
  diagnostics in reference order) is tested against reference vectors but
  not wired into tsconfig loading.
- CompilerHost with real and virtual file systems; triple-slash path
  references; multi-file global scripts. A port of the pinned source decoder
  (UTF-8/UTF-16 BOMs) is tested but now wired into filesystem source loading for UTF-8 BOM and UTF-16 LE/BE BOM inputs. Malformed UTF-8 fails closed instead of being accepted by replacement decoding; the full byte/VFS profile is still incomplete.
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
  intersections, excess-property checks on fresh literals; circular aliases
  report TS2456.
- Arrays (`T[]`, `Array<T>`, `readonly T[]`, `ReadonlyArray<T>`), fixed,
  labelled and optional tuples, tuple rest expansion.
- Bounded `keyof`, indexed access (`T["k"]`, `T[number]`), `typeof` queries,
  static computed keys, numeric key identity (non-negative integer member
  names; fractional keys and dynamic `obj[n]` reads of objects fail closed).
- Bitwise and shift operands must be `number` or `any` (others fail closed);
  `as` between unrelated types is TS2352, a `satisfies` mismatch TS1360;
  `satisfies` against a discriminated object union needs a fresh object
  literal with a unique string-literal discriminant (otherwise TSCS1001).
- Functions: declarations, typed arrows and function expressions, optional,
  source-file ambient `declare function` signatures and typed
  `declare let/const` variables (checked and erased),
  default and rest parameters, typed callback contracts, method signatures,
  object literal methods, inferred braced returns, closures over lexical
  bindings; a named function expression's name is an immutable binding
  visible only inside it.

### Control flow and narrowing

- Narrowing by `typeof`, truthiness, `===`/`!==` against literals, `null`
  and `undefined`, `in`, `Array.isArray`, discriminant properties, compound
  `&&`/`||`/`!` guards and returning guards; assignments invalidate facts.
- `for..of` over arrays and strings; with no global `Iterable` (no library
  is loaded) a named `number`, `boolean` or object source reports TS2495
  like the reference and its elements are `any`.
- Every file is strict, as in TS7: `eval`/`arguments` bindings, including
  script parameters, report TS1100.
- Definite assignment and return-path checks through loops, `switch`
  (including fall-through and exhaustive returns), `throw` and
  `try`/`finally`.
- `switch` case labels: literals, `const` identifiers, finite computed
  primitives, pure conditionals and short-circuits, checked direct, member
  and optional calls, static object data reads, and bounded pure numeric
  index reads of arrays, tuples and strings.

### Emit

ES2015 scripts and named ES modules are emitted from the checked AST; like
the pinned `useStrict` transform, every script (no import/export) starts with
`"use strict";` unless it already has that directive first. Type
syntax is erased and runtime semantics are preserved (evaluation order,
single evaluation of receivers, native `for..of` Unicode iteration). Like the
reference printer, statement blocks are indented four spaces per level and
spread operands get no added grouping. Output
is executed by the `tools/verify-*-emit.ts` checks.

### Standard library

`lib.*.d.ts` files cannot be loaded yet. `DeclarationParser` (A2.1) parses
ambient declaration syntax into a syntax tree: interfaces, type aliases,
`declare var/let/const/function`, ambient (and abstract) classes,
namespaces and `declare global`, call, construct, index, method, property,
accessor and constructor members, generics with constraints, defaults and
modifiers, union, intersection, conditional, `infer`, mapped, indexed,
tuple, template literal, function and constructor types, `keyof`, `unique`,
`readonly`, `typeof` queries, type predicates and binding-pattern
parameters. It stops at the first error (upstream recovers and reports
more), rejects reserved words as declaration names, and limits nesting to
200 levels; enums, imports, class initializers, private names outside ambient
classes and mapped types with extra members fail with TSCS1001. `tools/check-standard-library.ts`
(CI job `original-go-reference`) parses every pinned bundled lib:
**116 of 116** have zero diagnostics (ambient class `#private` names are parsed),
including all 20 files reachable from the default libs `lib.d.ts`
and `lib.es6.d.ts`, which the check requires. The check then binds those 20
files together with `DeclarationBinder` (A2.2a), in two stages like the
reference: each file binds on its own (an excluded redeclaration is TS2451
when the earlier symbol is block-scoped, else TS2300, and gets a fresh
symbol), then script files merge into the globals (TS2451 when either side
is block-scoped, else TS2300; TS2649 when the earlier symbol includes a
non-instantiated namespace). Interfaces, namespaces including dotted names,
function overloads, class + interface/namespace/function and `var` +
interface merge; a file with a top-level export is a module whose only
globals are its `declare global` blocks, and `declare global` in a script or
nested in a namespace is TS2669. Every segment of a dotted namespace name
counts toward the 200-level nesting limit, and stays counted for
everything nested in its body; so does each `[]`/`[K]` of a postfix chain
and each `keyof`/`unique`/`readonly` operator.
The pinned closure binds to 2,250 global symbols (791 with more than one
declaration) with zero diagnostics, which the check requires. Only the
excluded declaration is reported (the reference also reports the earlier
ones); members hidden by an `export {}` inside a namespace, separate export
and local tables in module files, qualified names in merge messages and the
body of an invalid (script or nested) `declare global` are not modelled, and
each file's global augmentations merge right after it rather than after all
scripts. `DeclarationResolver` (A2.2b) then resolves every entity name the
closure uses, in types, `typeof` queries, computed property names and
heritage clauses, with the pinned name resolver's scopes (type parameters,
`infer` only in its true branch, mapped-type parameters, signature
parameters, enclosing namespace exports, the globals and `globalThis`); a
failed lookup is TS2304, TS2503, TS2694, TS2693 or TS2749, and the check
requires zero for the pinned closure. Known gaps: property accesses after a
value (`typeof x.y`) are not checked; some wrong-meaning uses get TS2304 or
TS2694 where the reference reports TS2708, TS2709, TS2693 or TS2749 (a
namespace or type parameter in the wrong position); `infer` in a signature's
type-parameter constraints and index-signature parameters is not collected;
misplaced `infer` (TS1338) and unknown `intrinsic` aliases (TS2795) are not
reported. Module-local declarations are not resolved (the pinned libs have
none). Syntax trees are not yet compared with
the reference parser, types are not computed, and nothing is checked: the
CLI still accepts only the bounded globals below.

Programs use `--noLib` with
[`fixtures/minimal.d.ts`](../fixtures/minimal.d.ts) (empty global
interfaces). A fixed set of natives is modelled in the checker instead:
array `push`/`pop`/`shift`/`unshift`/`reverse`/`sort`/`fill`/`copyWithin`,
`map`/`filter`/`reduce`/`reduceRight`/`flat`/`flatMap`, queries such as
`indexOf`/`includes`/`find`/`findLast`/`some`/`every`, `Array.from`/`of`/
`isArray`; common `String` methods including `at`, `normalize`,
`replaceAll` and `isWellFormed` (`replace`/`replaceAll`/`split` take string
arguments only; newer methods such as `padStart` are emitted unpolyfilled);
`Object.keys`/`values`/`entries`/`assign`/
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
maps, JSX/TSX, JavaScript inputs, regular expression literals, decorators,
async/await, generators,
iterators beyond arrays and strings, `this` typing, overloads, mapped and
conditional types, index signatures, module resolution through packages,
`extends` in `tsconfig.json`, default implicit includes, targets other than
ES2015, incremental and watch modes. Within the supported areas: no general
contextual typing or inference for untyped parameters, no overloaded
callbacks, no object-rest or dynamic computed binding patterns or general iterable
destructuring, no getters/setters, no `instanceof`, no labelled statements
and no `var`.

## Known gaps

- 15,464 of 15,523 original targets need the standard library; 5,667 need
  nothing else that the runner checks for (roadmap A2).
- The acceptance runner now permits safe multi-source `.ts` inputs and
  explicit `@module: es2015`/`es6`; virtual symlinks, other module transforms,
  other targets, and `.types`/`.symbols` comparison remain unsupported.
- Emit parity gaps found in review of A3.1: arrow and function-expression
  block bodies restart indentation at column 0 instead of the enclosing
  depth, and conditional, `as` and `!` operands keep extra grouping
  (`...((c ? a : b))`, `...(a)` for `...a!`) where the reference prints none.
- Many constructs were added as narrow, separately verified slices; the
  per-feature `tools/verify-*-emit.ts` scripts and checker paths overlap
  (roadmap C1). 39 of the 78 scripts run the CLI through the shared
  `tools/tscs-process.ts`; the other 39 still carry their own copy.

## Roadmap

Ledger for the scheduled task. The headline above is the metric; each row
records the original case IDs it moves.

| ID | Item | Why it matters | Status | Evidence |
|---|---|---|---|---|
| D1 | Short README, current-state STATUS, `tools/check-docs.ts` in CI | Docs that are true and findable | done (PR #210, 355c914) | at merge: README 30,505 → 4,906 bytes, STATUS 123,542 → 8,980 bytes; docs 78 → 77 files; 16 checker tests |
| A1.1 | Acceptance runner over the 15,523 registered targets: SKIP with missing capabilities, run supported targets through tscs, compare diagnostics and `.js` emit with the pinned baselines, histogram, CI rerun with headline check | Turns the headline into a measured number | done (PR #211, df43af6) | 1 PASS (`parser509698`, since #214) / 1 FAIL (`noCrashOnNoLib`, PASS since A3.1) / 15,521 SKIP; 6 runner tests |
| A1.2 | Runner support for multi-file tests and `@module`, and `.types`/`.symbols` comparison | Larger runnable set once the library loads | in progress; A1.2a merged | `.types`/`.symbols` still not compared |
| A1.2a | Run safe multi-source original inputs as one program, support explicit ES2015 module configurations, and refuse unmodelled symlinks and unsafe paths | Allows real multi-file and module baseline comparison without silently dropping inputs | done (PR #228, 7166139) | Original acceptance: 3 PASS / 0 FAIL / 15,520 SKIP; 15 runner regressions include both prefix-collision orders. Exact-head hosted gate and post-merge main CI passed. Local full `verify.sh` blocked by MTP IPC permissions; no new original PASS credit |
| A2.1 | Parse the pinned libs with zero syntax errors (`DeclarationParser`, `tools/check-standard-library.ts` in CI) | First step to loading the standard library | done (PR #215, 68110ff) | at merge: 109/116 pinned libs clean, es5+es2015 closure 13/13; 63 xUnit + 4 node tests |
| A2.1b | Template literal types and ambient classes; require the default libs (`lib.d.ts`, `lib.es6.d.ts`) and everything they reference | The es2015 default lib includes `dom` and `scripthost` | done (PR #216, 8e8a68d) | 115/116 pinned libs clean at merge, default-lib closure 20/20 |
| A2.2a | Bind the parsed declarations into one global symbol table (cross-file merging, TS2300/TS2451 exclusions); the library check binds the default-lib closure | Library symbols become available | done (PR #220, 7b34604; review fixes PR #221, b5bd8e9) | pinned closure: 20 files, 2,250 global symbols (791 with more than one declaration), 0 diagnostics; 14 xUnit + 1 node test at merge, 16 with the review fixes |
| A2.2b | Resolve every name the bound library uses (type, value and namespace meanings, scoped type parameters and `infer`); the library check fails on any unresolved name | Every library reference has a target, the input to computing library types | done (PR #222, 61dfa09) | pinned closure: 0 unresolved names (6 `globalThis` references before modelling it); 18 resolver xUnit + 1 node test; review fixes: postfix/operator type nesting limit, parameters do not shadow namespaces |
| A2.2c | Compare the library syntax trees with the reference parser | Parser fidelity on the real libs, not only zero diagnostics | todo | — |
| A2.3 | Check enough of es5/es2015 to load them as the default lib instead of `fixtures/minimal.d.ts` | Unblocks 5,583 targets that need only the library | todo | — |
| A3.1 | `compiler/noCrashOnNoLib.ts` to PASS: four-space block indentation and no added grouping around spread operands in emit | The only executed original FAIL; every original `.js` baseline with a block depends on the indentation | done (PR #219, 28053fa) | 1 PASS / 1 FAIL → 2 PASS / 0 FAIL / 15,521 SKIP (CI original-acceptance on main); `JavaScriptEmitTests` 22 (+1) |
| A3.2 | `compiler/arrayIterationLibES5TargetDifferent.ts` (`nolib=true,target=es2015`) to PASS: runner passes `@lib`; CLI `--lib` with `--noLib` reports TS5053; ambient `declare let/const`; TS2495 for non-iterable `for..of` sources; script `"use strict";` prologue and strict script parameters; emit when only TS5053/TS2318 are printed | The only target whose sole gap was `@lib`; the prologue applies to every original script `.js` baseline | done (PR #224, cd1dea6) | 2 PASS / 0 FAIL / 15,521 SKIP → 3 PASS / 0 FAIL / 15,520 SKIP; standard-library-only 5,459 → 5,583 (the `@lib` targets now need only the library) |
| A3 | Close the cheapest family of failing original cases from A1's histogram, one family per PR | Moves original cases to PASS | todo | — |
| A3.3 | `compiler/arrayIterationLibES5TargetDifferent.ts` (`nolib=true,target=esnext`) to PASS: `--target esnext` with output identical to ES2015 where no lowering applies, failing closed where the ES2015 writer lowers (logical assignment, `**`, `??`, optional chaining, object spread, numeric separators) | The only target whose sole gap is `option:target=esnext`; its pinned `.errors.txt` and `.js` equal the `es2015` ones | todo | — |
| C1 | Remove duplicated checker/emitter paths and per-feature verify scripts where a shared test proves identical behaviour | Less code to maintain | todo | — |
| C1.1 | One shared `runTscs` helper (`tools/tscs-process.ts`, with a test) for the emit checks' copied spawn-and-assert function | Less duplicated harness code; one place to change how the checks run the CLI | done (PR #226, 00fde7b) | 31 of 78 `tools/verify-*-emit.ts` migrated mechanically (assertions and timeouts unchanged), scripts 311 lines removed / 93 added; all 31 pass; `tscs-process.test.ts` 2 tests |
| H1 | Agent verification skills (`.claude/skills/verification-first`, `.claude/skills/verify-tscs`) with a feature map run against the pinned reference | Every later PR is checked at the real boundary, not only by unit tests | done (PR #223, 2cfd095) | feature map rows run on 61dfa09: 2 PASS / 0 FAIL / 15,521 SKIP, library 0 unresolved names |

Last docs housekeeping: 2026-11-07 (Sydney date), the 2026-11-07 housekeeping PR.

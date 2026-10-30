# tscs

A TypeScript compiler written in C#.

**Experimental, bounded implementation — not a replacement for `tsc`.**
The goal is compatibility with the fixed TypeScript revision in
[`upstream.lock.json`](upstream.lock.json), with original test scope retained.
Internal regressions and local runtime examples are not original TS7 acceptance.
See [`docs/STATUS.md`](docs/STATUS.md) for current delivery and remaining scope.

## Develop

On Linux x64, provision the exact repository toolchain and locked local NuGet feed:

```sh
bash scripts/setup-dev-environment.sh
source "${TSCS_DEV_ROOT:-${XDG_CACHE_HOME:-$HOME/.cache}/tscs/linux-x64}/env.sh"
```

The installer uses .NET SDK 10.0.402, Node 24.22.1 and Go 1.27.3, verifies official
archives, disables telemetry, and supports offline cache reuse. It does not
modify system runtimes or loosen dependency locks. See
[`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) and [`AGENTS.md`](AGENTS.md).
The compiler itself is C#; Node and Go are development/reference tools only.

## Check or emit a supported program

```sh
dotnet build Tscs.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet src/Tscs.Cli/bin/Release/net10.0/tscs.dll \
  --noEmit --noLib --target es2015 \
  fixtures/local/number-ok.ts fixtures/minimal.d.ts
```

For checked ES2015 output, use `--emitJs --noLib --target es2015` instead of
`--noEmit`. Add `--module es2015` for the supported named-module subset.
Generated files must not already exist. The compiler does not execute user code.
`fixtures/minimal.d.ts` supplies empty interfaces for isolated tests, **not a
usable standard library**. Full standard-library loading remains unsupported.

A bounded `tsconfig.json` can specify `files`, explicit `include`/`exclude` globs,
`noEmit`, `noLib`, `target: "es2015"` and `module: "es2015"`:

```sh
dotnet src/Tscs.Cli/bin/Release/net10.0/tscs.dll --emitJs -p path/to/tsconfig.json
```

Project configuration and source discovery share CompilerHost/VFS. Explicit
files take precedence over excludes. Default implicit includes, `extends`,
package resolution, symlink traversal and TSX/JS inputs are not complete.

## Structural object data

Required named object members now use the same AST, Binder, semantic types,
module linker, checker and JavaScript emitter as other supported code:

```typescript
// data.ts
export const seed = { value: 21, label: "seed" };

// transform.ts
export function twice(data: { value: number }): { value: number } {
  return { value: data.value * 2 };
}

// entry.ts
import { seed } from "./data.js";
import { twice } from "./transform.js";
seed.value = 21;
export const result: number = twice(seed).value;
```

Nested member reads/writes, inline shape annotations and structural width
assignment are supported. Bounded `readonly` named properties in object types and
interfaces retain compile-time read-only write checks across module imports. Fresh object literals receive excess-property checks;
wider values already held in variables can satisfy narrower required shapes.
Writing a member of an imported or `const` object does not rebind that object.
Missing members and incompatible assignments are rejected before emission.

This does **not** implement optional members, readonly arrays, methods/accessors,
index signatures, general iterable spreads, computed keys, prototype-setter literals,
generic or unrestricted interface inheritance, or recursive type aliases. Named,
non-generic interfaces can extend finite object/interface shapes with checked
inherited required members. Such unimplemented forms fail explicitly.
Parser depth and diagnostic display are bounded. Runtime and validation details:
[`structural-object-data`](docs/progress/structural-object-data.md).

## Named aliases, interfaces and unions (bounded)

The compiler supports non-generic `type` aliases, required named-member
`interface` shapes, `number | string`-style unions, object unions, postfix
array union annotations and explicit `import type` between modules:

```typescript
// model.ts
export interface Reading { value: number; label: string }
export type Payload = Reading | number;

// logic.ts
import type { Reading } from "./model.js";
export function twice(x: Reading): Reading {
  return { value: x.value * 2, label: x.label };
}
```

These declarations are erased from emitted JavaScript. The runtime graph
and semantic limits are documented in
[`named-types-union`](docs/progress/named-types-union.md). This does not
implement general generic, recursive, narrowing, mapped or intersection types.

## Typed callback contracts (bounded)

Named function type annotations, such as `type Callback = (value: number) => number`,
are resolved through the same Binder, TypeResolver, function relations and module
linker as declared functions. Functions can be passed as annotated parameters,
assigned to function-typed variables and stored in required object members.
Finite trailing optional parameters and array rest parameters are supported.
Type annotations erase in ES2015 output. Explicitly typed arrow values accept
expression and braced bodies, lexical capture, checked local statements, and
bounded inferred braced returns (all value-returning paths must return).
Typed arrow values also support trailing optional typed parameters, defaults
and final array rest parameters (`...items: number[]`). Rest element types are
checked at calls and the braced body sees an ordinary array. Types are erased
to native ES2015 parameters. Untyped arrow parameters, mixed bare/value return
inference, overloads, generics,
`this` parameters and unrestricted callable properties remain unsupported.

Typed **anonymous function expressions** are checked callable values:
`const twice = function(n: number): number { return n + n; }`.
They reuse typed parameter grammar, lexical binding, return validation,
function signatures, modules, and ES2015 output. Trailing optional/default
and array-rest parameters are supported within the same checked subset.
Named function expressions with explicit return annotations also support
lexically scoped self-recursion; the self name does not leak to callers.
Nonrecursive named function expressions infer checked braced returns, but self
references during inference require an explicit return annotation. Generators,
implicit untyped parameters and `this` semantics remain unsupported. See
[`typed-function-expressions`](docs/progress/typed-function-expressions.md).

## Checked interface and object method signatures (bounded)

Required callable members accept TypeScript method spelling such as
`interface Op { double(n: number): number }` and
`type Box = { make(n: number): number }`. The method signature becomes
the existing checked function-type contract: a typed function/arrow value can
implement it, be extracted and called, and cross module boundaries without
emitting the interface or annotation. Optional trailing parameters and final
array-rest parameters use the same existing bounded semantics. This is a
function-valued structural member, not a method declaration or `this` binding;
method overload merging, general `this`, generics, optional methods and
readonly method modifiers are not implemented.

## Checked member and returned-function calls (bounded)

Function-valued object properties now support direct checked calls such as
`box.run(21)`, `box["run"](21)` and `nested.inner.run(21)`. A callee may
also be a parenthesized function expression or a returned callable.
The Parser retains the full callee expression through TypeChecker and the
ES2015 emitter; argument types, optional/default arity and final array-rest
spreads reuse existing signature validation. Runtime member access stays a
JavaScript member call instead of extracting the function. Dynamic object
index signatures, optional chaining, general `this` typing, generics and
overloads remain unsupported.

## Other implemented subsets

The current pipeline includes primitive/`any` variables, lexical scopes, selected
arithmetic/boolean/comparison expressions, braced `if`/`else`, explicit supported
function signatures, named imports/exports/re-exports, multi-file host-backed
programs, and ES2015 AST-backed output. Function return inference, overloads,
closures, general control flow, full type relations, declarations/maps,
standard libraries and incremental/watch compilation remain incomplete.

A bounded array and iteration family is being integrated: homogeneous array
literals, `T[]` annotations, numeric indexed reads/writes, `.length`, and braced
`for (const/let item of array)` with lexical iteration bindings. It also works
with named object members and across ES2015 modules through the same checked
AST and emitter. Empty evolving inference, heterogeneous unions, tuples,
non-array iterables, index signatures, classic `for`/`for..in`, destructuring,
and general array methods are **not** implemented. See
[`array-index-forof`](docs/progress/array-index-forof.md) for validation and
current merge status.

A leading whole-line `// @ts-ignore` suppresses semantic diagnostics only on the
immediately following physical line; scanner/parser errors are not suppressed.
Other directive forms are not generally supported. Intrinsic `globalThis` is
recognized but its type remains unsupported. Unsupported constructs/options
report a TSCS diagnostic and exit 1; ordinary TypeScript errors exit 2; successful
supported compilations exit 0. `--json` is a tscs diagnostic extension.

## Verification and honest scope

```sh
./scripts/verify.sh
```

The development gate builds with locked dependencies, runs xUnit 4/Microsoft
Testing.Platform and native Node `.ts` tests, checks the frozen 29-case isolated
cohort, and actually executes emitted JavaScript. Targeted development tests can
run the built MTP executable directly; report that separately from the SDK
`dotnet test` coordinator. Existing fail-skips and release gates remain strict.

The [local guard register](docs/testing/ts7/implementation-coverage.json) has
14 existing probes: 9 implemented and 5 explicitly unimplemented. Its static
execution fields remain NOT_RUN; actual results are evidence-bound separately.
This is not an upstream completion percentage. The independent generated suite
retains 15,523 configuration-level targets as SKIP/UNIMPLEMENTED, with 0 original
accepts. The full dynamic case denominator remains unknown; no release is claimed.

See the [full scope inventory](docs/testing/ts7/README.md),
[source correspondence](docs/PORTING.md), [design](docs/superpowers/specs/2026-10-24-tscs-compiler-design.md)
and [original evidence](docs/evidence/).
Apache-2.0; see [NOTICE](NOTICE) for upstream attribution.

# Array indexing and braced `for..of` — integrated language work package

## Dependency closure

This package builds on the merged structural-object compiler chain, reusing
`Syntax`, `Parser`, `Binder`, `CompilerTypes`, `TypeChecker`, `ModuleLinker`,
`JavaScriptEmitter`, `CompilerProgram`, `CompilerHost` and existing ES2015
module output. There is no second AST or interpreter.

Supported bounded family: homogeneous array literals, one or more postfix
`T[]` type annotations, `array[index]` read/write with number indices,
`array.length`, nested arrays and named object data in array elements, typed
parameters/returns, and braced `for (let/const x of array) { ... }` with
lexically scoped iteration binding. Mutable array elements are not a rebinding
of `const` or imported arrays. Loop bodies are checked but cannot establish
definite initialization after a possibly zero-iteration loop. The existing
bounded parser, semantic relation and iterative emitter protect deep inputs.

Explicit limitations: no heterogeneous union inference, tuple/sparse literals,
empty evolving array inference, generic `Array<T>` annotations, array methods,
string/object index signatures, non-array iterables, classic `for`, `for..in`,
while/do, destructuring, optional/readonly arrays, or full TS7 array semantics.
Such constructs remain unsupported instead of becoming implicit `any` or PASS.

## Actual local validation

- Exact restored SDK 10.0.402, Node 24.22.1, Go 1.27.3, Go telemetry off.
- Release build with offline locked restore cache, no restore during build,
  telemetry disabled: 0 warnings, 0 errors.
- Targeted MTP `ArrayIterationTests`: 31 pass, no skips/failures; current
  `ObjectDataTests`: 44 pass, no skips/failures.
- `node tools/verify-js-emit.ts` compiled an ESM four-module array graph,
  mutated the imported array, executed a typed `for..of` reducer and verified
  `result=42`, `first=21`, `length=2`. A negative indexed write exited 2,
  produced TS2322 and wrote no JS.
- These are internal developmental results, not the pinned original upstream
  acceptance suite. Exact source SHA and further CI evidence are required for
  integration/merge. No publication is claimed at initial source-edit stage.

## Pinned reference gate

`fixtures/array-iteration/semantic-cases.json` freezes 14 bounded positive and
negative sources with stable IDs and expected diagnostic codes. On an actual
CI build of pinned Go, `tools/verify-array-iteration-reference.ts` compares
fresh reference and product exit codes/diagnostic arrays and saves every raw
input, output and a byte-hash manifest. Both scripts are invoked within the
existing fixed-Go CI job, avoiding a redundant independent compiler download.
At initial implementation this comparison is **NOT_RUN**, not a PASS; the
original 15,523 SKIP_UNIMPLEMENTED configuration targets and unknown expanded
test denominator remain unchanged. Any mismatch is FAIL/MISMATCH, not SKIP.

## Follow-on dependency

After package integration, use the established array element type and lexical
scopes to build standard control-flow and union/interface/type-alias families,
by whole feature group rather than one operator or configuration diagnostic at
a time. Do not represent array and loop subsets as complete TypeScript support.

## Reference-profile correctness correction (2026-10-28)

The first real Go differential reported 6 mismatches: TS7053 for numeric
indexing in five sources and TS2339 for `array.length`. They came from the
shared `fixtures/minimal.d.ts` used for isolated early compiler smoke tests,
whose `interface Array<T> {}` intentionally declares neither numeric index nor
`length`. A production TypeScript Array interface does expose both.

The bounded array differential now uses its own explicitly recorded
`fixtures/array-iteration/minimal.d.ts`, adding `length: number` and
`[index: number]: T` with the same declarations supplied to **both** Go and
tscs. Original frozen sources, their expected diagnostics, stable IDs, and
`fixtures/minimal.d.ts` remain unchanged. The first Go run remains FAIL; the
corrected-profile result is NOT_RUN until a fresh fixed-Go CI completion.
This is a missing reference fixture capability, **not** a corrected product
semantic bug and not an original upstream acceptance PASS.

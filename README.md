# tscs

A TypeScript compiler written in C# (.NET 10).

**Experimental and bounded — not a replacement for `tsc`.** The target is
compatibility with the TypeScript revision pinned in
[`upstream.lock.json`](upstream.lock.json) (TS 7.1.0-dev, the Go
implementation at commit `8d9b6da`), measured by that revision's own original
compiler test cases.

## Where it stands

Original upstream acceptance: **2 PASS / 0 FAIL / 15,521 SKIP**

Of the 15,523 original source/configuration targets at the pinned revision,
`tools/original-acceptance.ts` runs the two that tscs can attempt and
compares them with the pinned reference baselines (both pass); the rest are
skipped, 15,464 of them because they need the standard library. The standard library
(`lib.es5.d.ts` and later) cannot be loaded; programs are checked with
`--noLib` against the empty interfaces in
[`fixtures/minimal.d.ts`](fixtures/minimal.d.ts). What does work is a
checked, bounded subset of the language with ES2015 output, covered by about
19,600 internal xUnit regressions and executed-output checks. Internal
regressions are not original acceptance. Details, gaps and the roadmap:
[docs/STATUS.md](docs/STATUS.md).

## Build and run

On Linux x64, provision the exact SDK (10.0.402), Node 24.22.1, Go 1.27.3 and
the locked NuGet feed, then build:

```sh
bash scripts/setup-dev-environment.sh
source "${TSCS_DEV_ROOT:-${XDG_CACHE_HOME:-$HOME/.cache}/tscs/linux-x64}/env.sh"
dotnet build Tscs.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
```

Check a program, or emit checked ES2015 JavaScript:

```sh
dotnet src/Tscs.Cli/bin/Release/net10.0/tscs.dll --noEmit --noLib --target es2015 \
  fixtures/local/number-ok.ts fixtures/minimal.d.ts
dotnet src/Tscs.Cli/bin/Release/net10.0/tscs.dll --emitJs --noLib --target es2015 \
  --module es2015 path/to/main.ts fixtures/minimal.d.ts
dotnet src/Tscs.Cli/bin/Release/net10.0/tscs.dll --emitJs -p path/to/tsconfig.json
```

Options: `--noEmit`, `--emitJs`, `--noLib`, `--target es2015`,
`--module es2015`, `-p`/`--project`, `--pretty false`, and the tscs extension
`--json`. Exit codes: 0 success, 2 TypeScript errors, 1 unsupported construct
or option (reported as a TSCS diagnostic). Output files must not already
exist. The compiler never executes user code.

Run the full development gate (what CI runs):

```sh
./scripts/verify.sh
```

## Capabilities by area

| Area | State | Detail |
|---|---|---|
| Project and host | Bounded `tsconfig.json` (`files`, explicit `include`/`exclude`, `noEmit`, `noLib`, `target`, `module`), CompilerHost/VFS, triple-slash path references | [STATUS](docs/STATUS.md#project-host-and-modules) |
| Scanner and parser | Most expression, statement and type syntax used by the supported subset; JSON config parsing checked against vectors generated from the pinned reference | [STATUS](docs/STATUS.md#syntax) |
| Types and checking | Primitives, literals, unions, named/structural objects, arrays, tuples, functions, bounded `keyof`/indexed access/`typeof`; no generics or classes | [STATUS](docs/STATUS.md#types-and-checking) |
| Control flow | `if`, loops, `switch`, `try`, narrowing guards, definite assignment for the supported subset | [STATUS](docs/STATUS.md#control-flow-and-narrowing) |
| Emit | ES2015 scripts and named ES modules from the checked AST | [STATUS](docs/STATUS.md#emit) |
| Standard library | Not loadable; a fixed set of native array/string/object members is modelled directly | [STATUS](docs/STATUS.md#standard-library) |
| Conformance harness | Pinned Go reference oracle, 15,523-target original inventory, frozen 29-case isolated cohort | [STATUS](docs/STATUS.md#conformance-harness) |

## Documentation

| Document | Purpose | Reader |
|---|---|---|
| [docs/STATUS.md](docs/STATUS.md) | Current state: headline metric, what works, gaps, roadmap | Everyone |
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | Toolchain provisioning, focused build and test commands | Contributors |
| [docs/PORTING.md](docs/PORTING.md) | C# module to upstream Go source correspondence | Compiler developers |
| [docs/testing/ts7/README.md](docs/testing/ts7/README.md) | Upstream test inventory, ledgers and how they are generated | Harness developers |
| [Design spec](docs/superpowers/specs/2026-10-24-tscs-compiler-design.md) and [bootstrap plan](docs/superpowers/plans/2026-10-24-compiler-bootstrap.md) | Original design and plan (historical) | Background |
| [docs/progress/](docs/progress/) | Per-work-package records (historical) | Background |
| [docs/evidence/](docs/evidence/) | Kept validation logs and receipts (historical, never rewritten) | Auditors |
| [AGENTS.md](AGENTS.md) | Binding instructions for automated contributors | Agents |

`node tools/check-docs.ts` (part of `./scripts/verify.sh`) checks links,
this index, size budgets and that the headline above matches STATUS.

The compiler is C#; Node and Go are development and reference tools only.
Apache-2.0; see [NOTICE](NOTICE) for upstream attribution.

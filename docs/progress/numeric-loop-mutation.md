# Bounded numeric mutations and do/while

Source-tree candidate on top of main `bd70282cd07ca66e10f14c0dddf9e46a60cd9fe3`.

- Parser/scanner/AST: single-statement numeric compound `+=`, `-=`, `*=` for identifiers, named properties and array indices; postfix `--` alongside `++`, including classic `for` update; braced `do { ... } while (condition);`.
- Binder/checker: reuse existing lexical declarations and imported/const assignment guards, numeric/any operand checks, array/property access checking, braced loop flow and nearest break/continue targets. Compound emit retains the operator and target (does not evaluate property or index twice).
- Flow: a `do` whose body always returns counts as definitely returning; breaks/continues produce normal possible completion. The conservative definite-initialization checker evaluates condition against entry facts to avoid treating a write bypassed by continue as established. That intentionally rejects some valid TS programs until full CFG.
- End-to-end: `NumericMutationLoopTests` local positive/negative compiler checks; `tools/verify-numeric-mutation-emit.ts` emits a four-module graph and executes Node expecting 42; actual TS2588 const-write rejection blocks output; 14 stable positive/negative reference cases in `fixtures/numeric-mutation/semantic-cases.json` compare actual code/exit against pinned Go oracle with identical bounded array declarations and immutable per-case artifacts.
- Intentional boundaries: no prefix updates, compound string concatenation, general assignment expressions, arbitrary lvalues, generic `Array<T>`, unbraced loop bodies or full TypeScript definite-assignment CFG. These remain unsupported, not PASS or SKIP-by-exception.

**At source creation:** build, reference differential, runtime and independent review are NOT_RUN. No original TS7 case is accepted by this bounded package; original 15,523 SKIP_UNIMPLEMENTED targets and UNEXPANDED dynamic scope are unchanged. Normal strict developer/release gates are neither removed nor relaxed.

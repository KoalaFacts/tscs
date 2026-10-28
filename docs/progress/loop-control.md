# Bounded while, continue and structured break flow

Base main: `8d595ca2cb20e5ce58e688e20f671385b7f36ec7` (switch completion merged).
One integrated next language-family package, not a separate configuration PR.

**Source flow:** scanner recognizes `while` and `continue`; AST/parser retain
braced while body and ContinueStatement. TypeChecker validates boolean/any
conditions, checks lexical bindings, and restores definite-initialisation facts
after zero-or-more iterations. Both while and for..of allow break/continue at
arbitrary positions in their braced bodies; outer switch allows continue only
when inside a loop. The shared FunctionControlFlow exit set now separates
Next/Return/Break/Continue, consuming break in the nearest switch or loop
and continue in the nearest loop. The same checked AST emits native ES2015.

**Acceptance criteria:** a real three-module executable program imports a type
and exercises while, for..of, a nested switch, continue in a switch/loop,
early break, property/element reads and addition. Node must produce
`result=42` and `alternate=5`. A TS2322 negative and an out-of-loop
continue must both block emitted JS. Product xUnit checks include nested
control-flow, parser depth limits and no fabricated definite assignment.
An independent pinned-Go run of 14 frozen bounded positive/negative sources
compares actual diagnostic code arrays and exits, saving raw inputs/results.
No fixture uses an expected failure as a SKIP.

**Non-goals:** unbraced while/switch/for..of bodies; classic `for`,
`do...while`, labeled break/continue, arbitrary expressions as switch
labels, loops with inferred tuple/destructuring iteration, general truthiness,
full reachability diagnostic parity and a complete TypeScript control-flow
graph. Conservative while return analysis requires a real trailing return.
Unsupported forms fail closed rather than silently emitting incorrect JS.
This is a bounded product capability, **not** full TypeScript compatibility.

The original immutable 15,523 configuration targets stay
SKIP_UNIMPLEMENTED, original accepted=0, dynamic runtime denominator
UNEXPANDED. No unrelated ledger/test minima or release gate was changed.
The existing completed switch cohorts and their IDs remain unchanged.

At initial candidate submission, current-head CI, pinned-Go differential and
independent whole-package review are NOT_RUN. Prior main results cannot
validate this candidate. Record actual head, logs and findings before merge.

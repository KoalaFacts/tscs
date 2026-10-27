# Structural object data: compiler implementation work package

Shared dependency: scanner dot and object grammar → AST type/expression nodes → binder prebound structural object members → checker structural assignment and member access → AST-backed ES2015 emitter → executed multi-module Node program.

Supported subset on this branch: required named fields of primitive or nested required-object type, object literals, named property reads and assignments, structural width compatibility, object parameters and returns, named module import/export. Pure literal object exports can be prebound. Rejections include missing members, incompatible assignments and arguments, duplicate object keys.

Not supported: optional/readonly members, computed/numeric keys, index signatures, getters/setters, object methods, type aliases, interfaces, unions, generic objects, method calls, dynamic bracket access.

New focused C# regression and existing `tools/verify-js-emit.ts` runtime verification extended. Exact-head CI and independent review still required, so changes are not claimed as run until verified. Frozen TS7 compatibility acceptance and SKIP entries are unchanged.

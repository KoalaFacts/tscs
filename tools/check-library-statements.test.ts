import assert from 'node:assert/strict';
import {test} from 'node:test';
import {firstDiagnostic, splitTopLevelStatements} from './check-library-statements.ts';

test('splits interfaces at their closing brace and other declarations at semicolons', () => {
  const text = [
    '/// <reference no-default-lib="true"/>',
    '/** NaN */',
    'declare var NaN: number;',
    'interface Box { value: number; f(x: { a: string; }): void; }',
    'declare function parse(text: string, reviver?: (this: any, key: string) => any): any;',
    'type Partial<T> = { [P in keyof T]?: T[P]; };',
    'type Pick<T, K extends keyof T> = {',
    '    [P in K]: T[P];',
    '}',
    ';',
    'declare namespace Intl { interface Collator { compare(x: string): number; } }',
  ].join('\n');
  assert.deepEqual(splitTopLevelStatements(text), [
    'declare var NaN: number;',
    'interface Box { value: number; f(x: { a: string; }): void; }',
    'declare function parse(text: string, reviver?: (this: any, key: string) => any): any;',
    'type Partial<T> = { [P in keyof T]?: T[P]; };',
    'type Pick<T, K extends keyof T> = {\n    [P in K]: T[P];\n}\n;',
    'declare namespace Intl { interface Collator { compare(x: string): number; } }',
  ]);
});

test('ignores braces and semicolons inside comments and strings', () => {
  const text = 'interface A { // };\n  s: "};"; /* } */ t: `;}`; }\ndeclare var a: A;';
  assert.deepEqual(splitTopLevelStatements(text), ['interface A { \n  s: "};";   t: `;}`; }', 'declare var a: A;']);
});

test('ends a statement written without a semicolon at the next declaration line', () => {
  assert.deepEqual(splitTopLevelStatements('declare var X: {\n a: number;\n}\ndeclare var Y: number;'),
    ['declare var X: {\n a: number;\n}', 'declare var Y: number;']);
  assert.deepEqual(splitTopLevelStatements('type T = { a: 1 }\ninterface I {}'), ['type T = { a: 1 }', 'interface I {}']);
  assert.deepEqual(splitTopLevelStatements('type U =\n  | A\n  | B;'), ['type U =\n  | A\n  | B;']);
});

test('fails on malformed input instead of miscounting', () => {
  assert.throws(() => splitTopLevelStatements('interface A {'), /unterminated bracket/);
  assert.throws(() => splitTopLevelStatements('declare var a: "x;'), /unterminated string/);
  assert.throws(() => splitTopLevelStatements('/* x'), /unterminated block comment/);
  assert.throws(() => splitTopLevelStatements('}'), /unbalanced/);
});

test('reports the first diagnostic without its position', () => {
  const output = "s.d.ts(3,5): error TSCS1001: Unsupported syntax 'new'.\ns.d.ts(4,1): error TS2304: Cannot find name 'X'.\n";
  assert.equal(firstDiagnostic(output), "TSCS1001: Unsupported syntax 'new'.");
  assert.equal(firstDiagnostic('error TSCS1002: The bootstrap requires a file.'), 'TSCS1002: The bootstrap requires a file.');
});

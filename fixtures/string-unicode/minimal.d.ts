interface Array<T> { length: number; [index: number]: T; }
interface Boolean {}
interface Function {}
interface IArguments {}
interface Number {}
interface Object {}
interface RegExp {}
interface String {
  readonly length: number;
  readonly [index: number]: string;
  trimStart(): string;
  trimEnd(): string;
  padStart(maxLength: number, fillString?: string): string;
  padEnd(maxLength: number, fillString?: string): string;
  normalize(form?: string): string;
  codePointAt(pos: number): number | undefined;
  localeCompare(that: string): number;
}
interface CallableFunction {}
interface NewableFunction {}

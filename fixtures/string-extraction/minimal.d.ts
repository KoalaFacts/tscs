interface Array<T> { length: number; [index: number]: T; join(separator?: string): string; }
interface Boolean {}
interface Function {}
interface IArguments {}
interface Number {}
interface Object {}
interface RegExp {}
interface String {
  readonly length: number;
  readonly [index: number]: string;
  charAt(pos: number): string;
  charCodeAt(index: number): number;
  substring(start: number, end?: number): string;
  indexOf(searchString: string, position?: number): number;
  lastIndexOf(searchString: string, position?: number): number;
  replace(searchValue: string, replaceValue: string): string;
  split(separator: string, limit?: number): string[];
  repeat(count: number): string;
}
interface CallableFunction {}
interface NewableFunction {}

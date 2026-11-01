interface Array<T> {
  length: number;
  [index: number]: T;
  includes(searchElement: T, fromIndex?: number): boolean;
  indexOf(searchElement: T, fromIndex?: number): number;
  lastIndexOf(searchElement: T, fromIndex?: number): number;
  join(separator?: string): string;
}
interface ReadonlyArray<T> {
  readonly length: number;
  readonly [index: number]: T;
  includes(searchElement: T, fromIndex?: number): boolean;
  indexOf(searchElement: T, fromIndex?: number): number;
  lastIndexOf(searchElement: T, fromIndex?: number): number;
  join(separator?: string): string;
}
interface Boolean {}
interface Function {}
interface IArguments {}
interface Number {}
interface Object {}
interface RegExp {}
interface String {}
interface CallableFunction {}
interface NewableFunction {}

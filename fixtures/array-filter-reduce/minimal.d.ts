interface Array<T> {
  length: number;
  [index: number]: T;
  filter(callback: (value: T) => boolean): T[];
  reduce(callback: (previous: T, current: T) => T, initialValue?: T): T;
  reduceRight(callback: (previous: T, current: T) => T, initialValue?: T): T;
}
interface ReadonlyArray<T> {
  readonly length: number;
  readonly [index: number]: T;
  filter(callback: (value: T) => boolean): T[];
  reduce(callback: (previous: T, current: T) => T, initialValue?: T): T;
  reduceRight(callback: (previous: T, current: T) => T, initialValue?: T): T;
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

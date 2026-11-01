interface Array<T> {
  length: number;
  [index: number]: T;
  reduce<U>(callback: (previous: U, current: T) => U, initialValue: U): U;
  reduceRight<U>(callback: (previous: U, current: T) => U, initialValue: U): U;
}
interface ReadonlyArray<T> {
  readonly length: number;
  readonly [index: number]: T;
  reduce<U>(callback: (previous: U, current: T) => U, initialValue: U): U;
  reduceRight<U>(callback: (previous: U, current: T) => U, initialValue: U): U;
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

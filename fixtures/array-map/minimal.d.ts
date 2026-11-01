interface Array<T> {
  length: number;
  [index: number]: T;
  map<U>(callback: (value: T) => U): U[];
}
interface ReadonlyArray<T> {
  readonly length: number;
  readonly [index: number]: T;
  map<U>(callback: (value: T) => U): U[];
}
interface Boolean {}
interface Function {}
interface IArguments {}
interface Number {}
interface Object {}
interface RegExp {}
interface String {
  readonly length: number;
  readonly [index: number]: string;
}
interface CallableFunction {}
interface NewableFunction {}

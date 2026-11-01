interface Array<T> { length: number; [index: number]: T;
  some(callback: (value: T) => boolean): boolean;
  every(callback: (value: T) => boolean): boolean;
  find(callback: (value: T) => boolean): T | undefined;
  findIndex(callback: (value: T) => boolean): number;
  findLast(callback: (value: T) => boolean): T | undefined;
  findLastIndex(callback: (value: T) => boolean): number;
  forEach(callback: (value: T) => void): void;
}
interface ReadonlyArray<T> { readonly length: number; readonly [index: number]: T;
  some(callback: (value: T) => boolean): boolean;
  every(callback: (value: T) => boolean): boolean;
  find(callback: (value: T) => boolean): T | undefined;
  findIndex(callback: (value: T) => boolean): number;
  findLast(callback: (value: T) => boolean): T | undefined;
  findLastIndex(callback: (value: T) => boolean): number;
  forEach(callback: (value: T) => void): void;
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

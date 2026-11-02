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
  toUpperCase(): string;
  toLowerCase(): string;
  toLocaleUpperCase(): string;
  toLocaleLowerCase(): string;
  trim(): string;
  trimLeft(): string;
  trimRight(): string;
  slice(start?: number, end?: number): string;
  includes(searchString: string, position?: number): boolean;
  startsWith(searchString: string, position?: number): boolean;
  endsWith(searchString: string, endPosition?: number): boolean;
  isWellFormed(): boolean;
  toWellFormed(): string;
  replaceAll(searchValue: string, replaceValue: string): string;
  at(index: number): string | undefined;
}
interface CallableFunction {}
interface NewableFunction {}

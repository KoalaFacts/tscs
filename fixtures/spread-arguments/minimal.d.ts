interface SymbolConstructor { readonly iterator: unique symbol; }
declare const Symbol: SymbolConstructor;
interface IteratorResult<T> { done?: boolean; value: T; }
interface Iterator<T> { next(): IteratorResult<T>; }
interface Array<T> { length: number; [index: number]: T; [Symbol.iterator](): Iterator<T>; }
interface Boolean {}
interface Function {}
interface IArguments {}
interface Number {}
interface Object {}
interface RegExp {}
interface String {}
interface CallableFunction {}
interface NewableFunction {}

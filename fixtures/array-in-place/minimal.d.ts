interface Array<T> {
  length: number;
  [index: number]: T;
  reverse(): T[];
  sort(compareFn?: (a:T,b:T)=>number): T[];
  fill(value:T,start?:number,end?:number): T[];
  copyWithin(target:number,start:number,end?:number): T[];
}
interface ReadonlyArray<T> { readonly length:number; readonly [index:number]:T; }
interface Boolean {}
interface Function {}
interface IArguments {}
interface Number {}
interface Object {}
interface RegExp {}
interface String {}
interface CallableFunction {}
interface NewableFunction {}

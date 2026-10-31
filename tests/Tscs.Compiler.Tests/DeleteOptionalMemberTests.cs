using Tscs;
using Xunit;

namespace Tscs.Tests;

// Real source-level product tests, not original TS7 harness acceptance.
public sealed class DeleteOptionalMemberTests
{
    [Theory]
    [InlineData("interface Box{value?:number;label:string}function clear(box:Box):boolean{return delete box.value;}")]
    [InlineData("type Box={value?:number};let box:Box={value:21};let cleared:boolean=delete box.value;")]
    [InlineData("let box:{value?:number}={value:21};let cleared:boolean=delete box[\"value\"];")]
    [InlineData("const box:{value?:number}={value:21};let cleared:boolean=delete box.value;")]
    [InlineData("interface Task{run?(n:number):number}let task:Task={};let ok:boolean=delete task.run;")]
    [InlineData("type Box={value?:number};function clear(box:Box):boolean{return delete (box.value);}")]
    [InlineData("let box:{delete?:number}={delete:21};let ok:boolean=delete box.delete;")]
    [InlineData("let box:{\"value\"?:number}={\"value\":21};let ok:boolean=delete box[\"value\"];")]
    [InlineData("interface Box{value?:number}function clear(box:Box|null):boolean{return delete box?.value;}")]
    [InlineData("interface Box{value?:number}function clear(box:Box|undefined):boolean{return delete box?.value;}")]
    [InlineData("interface Box{value?:number}function clear(box:Box|null|undefined):boolean{return delete box?.[\"value\"];}")]
    [InlineData("interface Box{value?:number}function clear(box:Box|null):boolean{return delete (box?.value);}")]
    [InlineData("interface Box{run?(n:number):number}function clear(box:Box|null):boolean{return delete box?.run;}")]
    [InlineData("interface Box{value?:number}interface Holder{inner:Box}function clear(h:Holder):boolean{return delete h.inner.value;}")]
    [InlineData("type Box={value?:number};function get():Box{return {value:21};}let removed:boolean=delete get().value;")]
    [InlineData("type Box={value?:number};let boxes:Box[]=[{value:21}];let removed:boolean=delete boxes[0].value;")]
    [InlineData("type Box={value?:number};let holder:{inner:Box}={inner:{value:21}};let removed:boolean=delete holder[\"inner\"][\"value\"];")]
    [InlineData("type Box={value?:number};function get():Box|null{return {value:21};}let removed:boolean=delete get()?.value;")]
    [InlineData("type Box={value?:number};function clear(h:{inner:Box|null}):boolean{return delete h.inner?.value;}")]
    [InlineData("let box:{value?:number}={value:21};let ok:boolean=delete ({value:box}).value.value;")]
    [InlineData("type Box={value?:number};function clear(h:{inner:Box}|null):boolean{return delete h?.inner.value;}")]
    [InlineData("type Box={value?:number};function clear(h:{inner:Box}|null):boolean{return delete h?.inner[\"value\"];}")]
    [InlineData("type Box={value?:number};function get():{inner:Box}|null{return {inner:{value:21}};}let ok:boolean=delete get()?.inner.value;")]
    [InlineData("type Box={value?:number};function clear(h:{nested:{inner:Box}}|null):boolean{return delete h?.nested.inner.value;}")]
    [InlineData("interface Box{run?(n:number):number}function clear(h:{inner:Box}|null):boolean{return delete h?.inner.run;}")]
    [InlineData("type Box={value?:number};function clear(h:{items:Box[]}|null):boolean{return delete h?.items[0].value;}")]
    public void MutableOptionalMembersCanBeDeleted(string source) =>
        Assert.Empty(Compilation.Check("delete-optional.ts",source));

    [Theory]
    [InlineData("let box:{required:number}={required:21};let ok:boolean=delete box.required;","TS2790")]
    [InlineData("let box:{readonly value?:number}={value:21};let ok:boolean=delete box.value;","TS2704")]
    [InlineData("let box:{required:number}={required:21};let ok:boolean=delete box[\"required\"];","TS2790")]
    [InlineData("let box:{value?:number}={value:21};let ok:boolean=delete box;","TSCS1001")]
    [InlineData("let ok:boolean=delete 21;","TSCS1001")]
    [InlineData("interface Box{value:number}function clear(box:Box|null):boolean{return delete box?.value;}","TS2790")]
    [InlineData("interface Box{readonly value?:number}function clear(box:Box|null):boolean{return delete box?.value;}","TS2704")]
    [InlineData("interface Box{value?:number}function clear(box:Box|null,key:string):boolean{return delete box?.[key];}","TSCS1001")]
    [InlineData("function clear(box:number[]|null):boolean{return delete box?.[0];}","TSCS1001")]
    [InlineData("let box:{value?:number}={value:21};let key:string=\"value\";let ok:boolean=delete box[key];","TSCS1001")]
    [InlineData("type Box={required:number};function get():Box{return {required:1};}let removed:boolean=delete get().required;","TS2790")]
    [InlineData("type Box={readonly value?:number};let holder:{inner:Box}={inner:{value:1}};let removed:boolean=delete holder.inner.value;","TS2704")]
    [InlineData("type Box={value?:number};let boxes:Box[]=[{value:1}];let key:string=\"value\";let removed:boolean=delete boxes[0][key];","TSCS1001")]
    [InlineData("function f(h:{inner:{value:number}}|null):boolean{return delete h?.inner.value;}","TS2790")]
    [InlineData("function f(h:{inner:{readonly value?:number}}|null):boolean{return delete h?.inner.value;}","TS2704")]
    [InlineData("function f(h:{inner:{value?:number}}|null,k:string):boolean{return delete h?.inner[k];}","TSCS1001")]
    [InlineData("function f(h:{items:number[]}|null):boolean{return delete h?.items[0];}","TSCS1001")]
    public void RequiredReadonlyAndUnsupportedDeletionFailsClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("delete-rejected.ts",source),diagnostic=>diagnostic.Code==code);
}

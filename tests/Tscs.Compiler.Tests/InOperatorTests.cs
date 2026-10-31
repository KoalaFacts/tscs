using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded checked property existence, separate from original TS7 acceptance.
public sealed class InOperatorTests
{
    [Theory]
    [InlineData("let obj={value:42};let present:boolean=\"value\" in obj;")]
    [InlineData("let obj={value:42};let missing:boolean=\"missing\" in obj;")]
    [InlineData("let obj={value:42};let key:string=\"value\";let result:boolean=key in obj;")]
    [InlineData("let obj={value:42};let result:number=(\"value\" in obj)?42:0;")]
    [InlineData("let obj={value:42};let result:boolean=(\"value\" in obj) && (\"missing\" in obj);")]
    [InlineData("let obj:any={value:42};let result:boolean=\"value\" in obj;")]
    [InlineData("interface Item {value:number} let obj:Item={value:42};let result:boolean=\"value\" in obj;")]
    [InlineData("let obj={in:42};let result:boolean=\"in\" in obj;")]
    [InlineData("let xs:number[]=[20,22];let present:boolean=1 in xs;")]
    [InlineData("function f():number{return 42;}let present:boolean=\"prototype\" in f;")]
    [InlineData("let obj={value:42};for(let present:boolean=(\"value\" in obj);false;){}")]
    [InlineData("function keep(x:boolean):boolean{return x;}let obj={value:42};for(let present:boolean=keep(\"value\" in obj);false;){}")]
    [InlineData("let obj={value:42};for(let flags=[\"value\" in obj];false;){}")]
    [InlineData("let obj={value:42};for(let config={present:\"value\" in obj};false;){}")]
    [InlineData("let obj={value:42};for(let present=true?\"value\" in obj:false;false;){}")]
    public void PropertyExistenceProducesBoolean(string source) =>
        Assert.Empty(Compilation.Check("in-operator.ts",source));

    [Theory]
    [InlineData("let obj={value:42};let result:boolean=true in obj;", "TS2322")]
    [InlineData("let x:number=42;let result:boolean=\"value\" in x;", "TS2322")]
    [InlineData("let obj={value:42};let result:number=\"value\" in obj;", "TS2322")]
    [InlineData("let obj={value:42};for(let present:boolean=\"value\" in obj;false;){}", "TSCS1001")]
    [InlineData("let obj={value:42};for(let present=false?false:\"value\" in obj;false;){}", "TSCS1001")]
    public void InvalidOperandOrResultIsRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("in-operator.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("type Entry={value:number}|{message:string};function read(e:Entry):number {if(\"value\" in e){return e.value;}return 0;}")]
    [InlineData("type Entry={value:number}|{message:string};function read(e:Entry):number {if(!(\"value\" in e)){return 0;}return e.value;}")]
    [InlineData("type Entry={value:number}|{message:string};function message(e:Entry):string {if(\"value\" in e){return \"ok\";}return e.message;}")]
    [InlineData("type Entry={value:number}|{message:string};function read(e:Entry):number {if(\"value\" in e && e.value>0){return e.value;}return 0;}")]
    [InlineData("type Entry={value:number}|{message:string};function message(e:Entry):string {if(\"message\" in e){return e.message;}return \"ok\";}")]
    public void RequiredPropertyExistenceNarrowingChecksBothBranches(string source) =>
        Assert.Empty(Compilation.Check("in-narrow.ts",source));

    [Theory]
    [InlineData("type Entry={value:number}|{message:string};function bad(e:Entry):number {return e.value;}", "TS2339")]
    [InlineData("type Entry={value:number}|{message:string};function bad(e:Entry):string {if(\"value\" in e){return e.message;}return \"ok\";}", "TS2339")]
    [InlineData("type Entry={value:number}|{message:string};function bad(e:Entry):number {if(\"value\" in e){e={message:\"changed\"};return e.value;}return 0;}", "TS2339")]
    [InlineData("type Entry={value:number}|{message:string};function bad(e:Entry,key:string):number {if(key in e){return e.value;}return 0;}", "TS2339")]
    [InlineData("type Entry={value?:number}|{message:string};function bad(e:Entry):number {if(\"value\" in e){return e.value;}return 0;}", "TS2322")]
    public void MissingDynamicOptionalOrReassignedMemberIsNotPromoted(string source,string code) =>
        Assert.Contains(Compilation.Check("in-narrow-negative.ts",source),d=>d.Code==code);

    // Optional key presence is a union filter, not proof that the value is defined.
    // The optional variant participates in both exits of the guard.
    [Theory]
    [InlineData("type Entry={value?:number}|{message:string};function read(e:Entry):number {if(\"value\" in e){return e.value??0;}return 0;}")]
    [InlineData("type Entry={value?:number}|{message:string};function read(e:Entry):number {if(!(\"value\" in e)){return 0;}return e.value??0;}")]
    [InlineData("type Entry={value?:number}|{message:string};function read(e:Entry):number {return \"value\" in e?(e.value??0):0;}")]
    [InlineData("type Entry={value?:number}|{value:number}|{message:string};function read(e:Entry):number {if(\"value\" in e){return e.value??0;}return 0;}")]
    [InlineData("type Entry={value?:number}|{message:string};function read(e:Entry):number {if(\"value\" in e && (e.value??0)>0){return e.value??0;}return 0;}")]
    public void OptionalKeyPresenceRefinesOnlyThePresentBranch(string source) =>
        Assert.Empty(Compilation.Check("in-optional-flow.ts",source));

    [Theory]
    [InlineData("type Entry={value?:number}|{message:string};function bad(e:Entry):string {if(!(\"value\" in e)){return e.message;}return \"ok\";}", "TS2339")]
    [InlineData("type Entry={value?:number}|{message:string};function bad(e:Entry,key:string):number {if(key in e){return e.value??0;}return 0;}", "TS2339")]
    public void OptionalKeyAbsenceOrDynamicKeyDoesNotProveMembers(string source,string code) =>
        Assert.Contains(Compilation.Check("in-optional-flow-negative.ts",source),d=>d.Code==code);

    // A static key is not a dynamic string lookup: only its proven value may
    // narrow a finite union. Errors must remain real compiler failures.
    [Theory]
    [InlineData("type E={value:number}|{message:string};function read(e:E):number{const key=\"value\";if(key in e){return e.value;}return 0;}")]
    [InlineData("type E={value:number}|{message:string};function read(e:E):number{const key=\"value\";if(!(key in e)){return 0;}return e.value;}")]
    [InlineData("type E={value:number}|{message:string};function read(e:E,key:\"value\"):number{if(key in e){return e.value;}return 0;}")]
    [InlineData("type E={value:number}|{message:string};function read(e:E):number{let key:\"value\"=\"value\";if(key in e){return e.value;}return 0;}")]
    [InlineData("type E={0:number}|{message:string};function read(e:E):number{if(0 in e){return e[0];}return 0;}")]
    [InlineData("type E={0:number}|{message:string};function read(e:E):number{const key=0;if(key in e){return e[0];}return 0;}")]
    [InlineData("type E={value?:number}|{message:string};function read(e:E):number{const key=\"value\";if(key in e){return e.value??0;}return 0;}")]
    [InlineData("type E={value:number}|{message:string};function read(e:E):number{const key=\"value\";return key in e?e.value:0;}")]
    public void FiniteStaticInKeysNarrowUnionAlternatives(string source) =>
        Assert.Empty(Compilation.Check("static-in-key.ts",source));

    [Theory]
    [InlineData("type E={value:number}|{message:string};function bad(e:E,key:string):number{if(key in e){return e.value;}return 0;}", "TS2339")]
    [InlineData("type E={value:number}|{message:string};function bad(e:E):number{const key:string=\"value\";if(key in e){return e.value;}return 0;}", "TS2339")]
    [InlineData("type E={value:number}|{message:string};function bad(e:E):number{let key=\"value\";if(key in e){return e.value;}return 0;}", "TS2339")]
    [InlineData("type E={value:number}|{message:string};function bad(e:E):string{const key=\"value\";if(key in e){return e.message;}return \"ok\";}", "TS2339")]
    public void DynamicKeysAndWrongMembersRemainRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("static-in-key-invalid.ts",source),d=>d.Code==code);
}

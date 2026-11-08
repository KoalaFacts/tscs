using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: an instantiated *declared alias* in generic interface heritage
// denotes its substituted body. An opaque type parameter remains opaque.
public sealed class GenericAliasHeritageIdentityTests
{
    [Theory]
    [InlineData("type Carrier<U>={payload:U;flag:number};interface Base<T>{meta:Carrier<T>}interface Box<V> extends Base<V>{meta:{flag:number;payload:V}}let b:Box<number>={meta:{payload:5,flag:3}};")]
    [InlineData("type Carrier<U>={payload:U;flag:number};interface Base<T>{meta:{flag:number;payload:T}}interface Box<V> extends Base<V>{meta:Carrier<V>}let b:Box<number>={meta:{payload:5,flag:3}};")]
    [InlineData("type Leaf<X>={payload:X;flag:number};type Carrier<U>=Leaf<U>;interface Base<T>{meta:Carrier<T>}interface Box<V> extends Base<V>{meta:{flag:number;payload:V}}let b:Box<number>={meta:{payload:5,flag:3}};")]
    [InlineData("type Leaf<X>={payload:X;flag:number};type Carrier<U>=Leaf<U>;interface Base<T>{meta:{flag:number;payload:T}}interface Box<V> extends Base<V>{meta:Carrier<V>}let b:Box<number>={meta:{payload:5,flag:3}};")]
    [InlineData("type Carrier<U>={payload:U;flag:number};interface Base<T>{meta:Carrier<T>}interface Box<V> extends Base<V>{meta:{payload:V;flag:number}}let b:Box<string>={meta:{payload:\"five\",flag:3}};")]
    public void EquivalentGenericAliasHeritageMembersCompile(string source)=>
        Assert.Empty(Compilation.Check("generic-alias-heritage.ts",source));

    [Theory]
    [InlineData("type Carrier<U>={payload:U;flag:number};interface Base<T>{meta:Carrier<T>}interface Box<V> extends Base<V>{meta:{payload:string;flag:number}}")]
    [InlineData("type Carrier<U>={payload:U;flag:number};interface Base<T>{meta:{payload:T;flag:number}}interface Box<V> extends Base<V>{meta:Carrier<string>}")]
    [InlineData("type Carrier<U>={payload:U;flag:number};interface Base<T>{meta:Carrier<T>}interface Box<V,W> extends Base<V>{meta:{payload:W;flag:number}}")]
    [InlineData("type Carrier<U>={payload:U;flag:number};interface Base<T>{meta:Carrier<T>}interface Box<V> extends Base<V>{meta:{payload:V;flag?:number}}")]
    [InlineData("type Carrier<U>={payload:U;flag:number};interface Base<T>{meta:Carrier<T>}interface Box<V> extends Base<V>{meta:{readonly payload:V;flag:number}}")]
    public void DifferentOrUnsafeGenericAliasHeritageMembersFailClosed(string source)=>
        Assert.Contains(Compilation.Check("generic-alias-heritage-conflict.ts",source),
            d=>d.Code=="TSCS1001");

    [Fact]
    public void ImportedBaseAndInstantiatedAliasMatchAcrossFiles()
    {
        var inputs=new[]
        {
            new SourceInput("base.ts",
                "export type Carrier<U>={payload:U;flag:number};"+
                "export interface Base<T>{value:T;meta:Carrier<T>}"),
            new SourceInput("box.ts",
                "import type {Base} from \"./base.js\";"+
                "export interface Box<V> extends Base<V>{meta:{flag:number;payload:V}}"),
            new SourceInput("entry.ts",
                "import type {Box} from \"./box.js\";"+
                "let result:Box<number>={value:5,meta:{payload:5,flag:3}};"+
                "let n:number=result.value+result.meta.flag;")
        };
        Assert.Empty(Compilation.CheckFiles(inputs));
    }

    [Fact]
    public void ImportedGenericAliasMismatchIsRejected()
    {
        var diagnostics=Compilation.CheckFiles([
            new SourceInput("base.ts",
                "export type Carrier<U>={payload:U;flag:number};"+
                "export interface Base<T>{meta:Carrier<T>}"),
            new SourceInput("box.ts",
                "import type {Base} from \"./base.js\";"+
                "export interface Box<V> extends Base<V>{meta:{flag:number;payload:string}}")
        ]);
        Assert.Contains(diagnostics,d=>d.Code=="TSCS1001");
    }
}

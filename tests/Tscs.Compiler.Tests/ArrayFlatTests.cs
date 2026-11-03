using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Checked source and native runtime regressions, NOT original TS7 acceptance.
public sealed class ArrayFlatTests
{
    [Theory]
    [InlineData("let rows:number[][]=[[1,2],[3]];let result:number[]=rows.flat();")]
    [InlineData("let rows:string[][]=[[\"a\"],[\"b\",\"c\"]];let result:string[]=rows.flat();")]
    [InlineData("let rows:number[][][]=[[[1]],[[2]]];let result:number[][]=rows.flat();")]
    [InlineData("type Row=readonly number[];let rows:ReadonlyArray<Row>=[[1,2],[3]];let result:number[]=rows.flat();result[0]=42;")]
    [InlineData("let rows:ReadonlyArray<number[]>=[[1],[2]];let result:number[]=rows.flat();result.push(3);")]
    [InlineData("type Matrix=number[][];function flatten(rows:Matrix):number[]{return rows.flat();}let result:number[]=flatten([[1],[2]]);")]
    [InlineData("let rows:number[][]=[[1],[2]];let result:number[]=rows.flat().map((n:number):number=>n*2);")]
    [InlineData("let rows:number[][]=[[1]];let result:readonly number[]=rows.flat();")]
    public void FlattensExactlyOneKnownNestedArrayLevel(string source) =>
        Assert.Empty(Compilation.Check("native-flat.ts",source));

    [Theory]
    [InlineData("let rows:number[][]=[[1]];let wrong:string[]=rows.flat();","TS2322")]
    [InlineData("let rows:number[][][]=[[[1]]];let wrong:number[]=rows.flat();","TS2322")]
    [InlineData("let rows:string[][]=[[\"a\"]];let wrong:number[]=rows.flat();","TS2322")]
    [InlineData("let rows:ReadonlyArray<number[]>=[[1]];let wrong:number=rows.flat();","TS2322")]
    [InlineData("let rows:number[][]=[[1]];let wrong:string[]=rows.flat().map((n:number):number=>n);","TS2322")]
    public void RejectsIncorrectFlatOutputTypes(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-native-flat.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; } = new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path) => Path.GetFullPath(path);
        public string ReadFile(string path) => Files[path];
    }

    [Fact]
    public void FourLinkedModulesExecuteOneLevelFlattenAndPreserveSources()
    {
        var root=Directory.CreateTempSubdirectory("tscs-native-flat-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                "export type Rows=ReadonlyArray<ReadonlyArray<number>>;"+
                "export const values:Rows=[[1,2],[3]];"+
                "export const labels:string[][]=[[\"a\",\"b\"],[\"c\"]];";
            host.Files[Path.Combine(root,"logic.ts")]=
                "import type {Rows} from \"./model.js\";"+
                "export function flatten(xs:Rows):number[]{return xs.flat();}"+
                "export function flattenLabels(xs:string[][]):string[]{return xs.flat();}";
            host.Files[Path.Combine(root,"bridge.ts")]=
                "export {values,labels} from \"./model.js\";"+
                "export {flatten,flattenLabels} from \"./logic.js\";";
            host.Files[Path.Combine(root,"entry.ts")]=
                "import {values,labels,flatten,flattenLabels} from \"./bridge.js\";"+
                "export const numbers:number[]=flatten(values);"+
                "export const words:string[]=flattenLabels(labels);"+
                "numbers[0]=99;export const original=values;";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {
                    NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
                });
            Assert.Empty(program.Analyze().Diagnostics);
            var outputs=program.EmitJavaScript();
            Assert.Equal(4,outputs.Count);
            Assert.Contains(outputs,js=>js.Text.Contains(".flat(",StringComparison.Ordinal));
            foreach(var output in outputs)
                File.WriteAllText(Path.ChangeExtension(output.SourceFile,".js"),output.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"{\"type\":\"module\"}");
            var info=new ProcessStartInfo("node") {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add(
                "import {numbers,words,original} from './entry.js';"+
                "console.log(numbers.join(',')+'|'+words.join(',')+'|'+original[0].join(',')+'|'+original.length);");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Node native flat execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("99,2,3|a,b,c|1,2|2",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedFlatOutputPreventsAllJavascriptEmission()
    {
        const string root="/virtual/native-flat-negative";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            "export const values:ReadonlyArray<ReadonlyArray<number>>=[[1,2],[3]];";
        host.Files[root+"/entry.ts"]=
            "import {values} from \"./model.js\";"+
            "export const wrong:string[]=values.flat();";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {
                NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
            });
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}

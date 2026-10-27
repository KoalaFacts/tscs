package tsoptions

import (
 "encoding/json"
 "fmt"
 "os"
 "strconv"
 "testing"
 "unicode/utf16"
 "github.com/microsoft/TypeScript/tsc/internal/ast"
 "github.com/microsoft/TypeScript/tsc/internal/core"
 "github.com/microsoft/TypeScript/tsc/internal/locale"
 "github.com/microsoft/TypeScript/tsc/internal/parser"
 "github.com/microsoft/TypeScript/tsc/internal/tspath"
)

func TestTscsJsonScalarOptionsOracle(t *testing.T) {
 b,e:=os.ReadFile(os.Getenv("TSCS_STRING_INPUT"));if e!=nil{t.Fatal(e)}
 var sources []string;if e=json.Unmarshal(b,&sources);e!=nil{t.Fatal(e)}
 rows:=[]any{};inventory:=[]any{}
 for pos,o:=range OptionsDeclarations {
  supported:=(o.Kind=="boolean"||o.Kind=="number"||o.Kind=="string"||o.Kind=="object")&&o.PathKind==CommandLineOptionPathKindNone&&o.extraValidation==""
  desc:=map[string]any{"name":o.Name,"kind":string(o.Kind),"disallowNull":o.DisallowNullOrUndefined(),"commandLineOnly":o.IsCommandLineOnly}
  inventory=append(inventory,map[string]any{"position":pos,"option":desc,"pathKind":int(o.PathKind),"extraValidation":string(o.extraValidation),"supported":supported})
  if !supported{continue}
  for i,literal:=range sources { for _,located:=range []bool{false,true} {
   source:="/*😀*/ {\"compilerOptions\": {"+strconv.Quote(o.Name)+": /*x*/ "+literal+"}}"
   sf:=parser.ParseSourceFile(ast.SourceFileParseOptions{FileName:"/tsconfig.json"},source,core.ScriptKindJSON)
   if len(sf.Diagnostics())!=0{t.Fatal("parser",source,sf.Diagnostics())}
   root:=sf.Statements.Nodes[0].Expression();assignment:=root.Properties()[0].Initializer().Properties()[0].AsPropertyAssignment()
   expression:=assignment.Initializer
   // Parse the literal alone for the exact convertToObject input semantics.
   literalFile:=parser.ParseSourceFile(ast.SourceFileParseOptions{FileName:"/value.json"},literal,core.ScriptKindJSON)
   input,errs:=convertToObject(literalFile);if len(errs)!=0||len(literalFile.Diagnostics())!=0{t.Fatal("literal",literal)}
   var file *ast.SourceFile;var property *ast.PropertyAssignment;var valueNode *ast.Node
   if located{file=sf;property=assignment;valueNode=expression}
   value,errors:=convertJsonOption(o,input,tspath.RootedDirectoryPathFromNormalized("/.src"),property,valueNode,file)
   ds:=[]any{};for _,d:=range errors {start,length:=-1,0;if located {start=len(utf16.Encode([]rune(source[:d.Pos()])));length=len(utf16.Encode([]rune(source[d.Pos():d.End()])))};ds=append(ds,map[string]any{"code":"TS"+strconv.Itoa(int(d.Code())),"message":d.Localize(locale.Default),"start":start,"length":length})}
   rows=append(rows,map[string]any{"id":fmt.Sprintf("%d/%s/%d/%t",pos,o.Name,i,located),"option":desc,"source":source,"literal":literal,"located":located,"value":configProjection(value),"diagnostics":ds})
  }}
 }
 b,e=json.Marshal(map[string]any{"inventory":inventory,"vectors":rows});if e!=nil{t.Fatal(e)};if e=os.WriteFile(os.Getenv("TSCS_STRING_OUTPUT"),b,0600);e!=nil{t.Fatal(e)}
}

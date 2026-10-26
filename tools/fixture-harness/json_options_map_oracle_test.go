package tsoptions
import (
 "encoding/json"
 "fmt"
 "os"
 "reflect"
 "strconv"
 "testing"
 "unicode/utf16"
 "github.com/microsoft/TypeScript/tsc/internal/ast"
 "github.com/microsoft/TypeScript/tsc/internal/collections"
 "github.com/microsoft/TypeScript/tsc/internal/core"
 "github.com/microsoft/TypeScript/tsc/internal/diagnostics"
 "github.com/microsoft/TypeScript/tsc/internal/locale"
 "github.com/microsoft/TypeScript/tsc/internal/parser"
 "github.com/microsoft/TypeScript/tsc/internal/tspath"
)
type tscsOptionRecorder struct{calls []any}
func(r *tscsOptionRecorder)ParseOption(k string,v any){r.calls=append(r.calls,map[string]any{"key":configUnits(k),"value":mapProjection(v)})}
func(r *tscsOptionRecorder)UnknownOptionDiagnostic()*diagnostics.Message{return diagnostics.Unknown_compiler_option_0}
func(r *tscsOptionRecorder)UnknownDidYouMeanDiagnostic()*diagnostics.Message{return diagnostics.Unknown_compiler_option_0_Did_you_mean_1}
func mapProjection(v any)map[string]any{
 if xs,ok:=v.([]any);ok{items:=[]any{};for _,x:=range xs{items=append(items,mapProjection(x))};r:=configProjection([]any{});r["elements"]=items;r["isNil"]=xs==nil;return r}
 if v!=nil {r:=reflect.ValueOf(v);switch r.Kind(){case reflect.Int,reflect.Int32,reflect.Int64:v=float64(r.Int())}}
 return configProjection(v)
}
func TestTscsJsonOptionsMapOracle(t *testing.T){
 b,e:=os.ReadFile(os.Getenv("TSCS_STRING_INPUT"));if e!=nil{t.Fatal(e)}
 var input []struct{ID string `json:"id"`;Source string `json:"source"`;BasePath string `json:"basePath"`};if e=json.Unmarshal(b,&input);e!=nil{t.Fatal(e)}
 var describe func(*CommandLineOption)any
 var supported func(*CommandLineOption)bool
 supported=func(o *CommandLineOption)bool{if o.PathKind!=CommandLineOptionPathKindNone||o.extraValidation!=""{return false};if o.Kind=="list"{return supported(o.Elements())};return o.Kind=="boolean"||o.Kind=="number"||o.Kind=="string"||o.Kind=="object"||o.Kind=="enum"}
 describe=func(o *CommandLineOption)any{d:=map[string]any{"name":o.Name,"kind":string(o.Kind),"disallowNull":o.DisallowNullOrUndefined(),"commandLineOnly":o.IsCommandLineOnly,"pathKind":int(o.PathKind),"extraValidation":string(o.extraValidation),"supported":supported(o)};if o.Kind=="list"{d["element"]=describe(o.Elements());d["preserveFalsy"]=o.listPreserveFalsyValues};if o.Kind=="enum"{es:=[]any{};for k,v:=range o.EnumMap().Entries(){es=append(es,map[string]any{"key":k,"value":mapProjection(v),"deprecated":o.DeprecatedKeys()!=nil&&o.DeprecatedKeys().Has(k)})};d["entries"]=es};return d}
 inventory:=[]any{};for _,o:=range OptionsDeclarations{inventory=append(inventory,describe(o))}
 rows:=[]any{}
 for _,r:=range input{
  sf:=parser.ParseSourceFile(ast.SourceFileParseOptions{FileName:"/tsconfig.json"},r.Source,core.ScriptKindJSON)
  root,conversionErrors:=convertToObject(sf)
  var options any;if m,ok:=root.(*collections.OrderedMap[string,any]);ok{options=m.GetOrZero("compilerOptions")}
  recorder:=&tscsOptionRecorder{calls:[]any{}}
  _,errs:=convertOptionsFromJson(CommandLineCompilerOptionsMap,options,tspath.RootedDirectoryPathFromNormalized(r.BasePath),recorder)
  ds:=[]any{};for _,d:=range errs{ds=append(ds,map[string]any{"code":"TS"+strconv.Itoa(int(d.Code())),"message":d.Localize(locale.Default),"start":-1,"length":0})}
  project:=func(es []*ast.Diagnostic)[]any{ds:=[]any{};for _,d:=range es{ds=append(ds,map[string]any{"code":"TS"+strconv.Itoa(int(d.Code())),"message":d.Localize(locale.Default),"start":len(utf16.Encode([]rune(r.Source[:d.Pos()]))),"length":len(utf16.Encode([]rune(r.Source[d.Pos():d.End()])))})};return ds}
  unsupported:=[]string{};if m,ok:=options.(*collections.OrderedMap[string,any]);ok{for k:=range m.Keys(){o:=CommandLineCompilerOptionsMap.Get(k);if o!=nil&&o.Name==k&&!supported(o){unsupported=append(unsupported,k)}}}
  rows=append(rows,map[string]any{"id":r.ID,"source":r.Source,"basePath":r.BasePath,"calls":recorder.calls,"diagnostics":ds,"parserDiagnostics":project(sf.Diagnostics()),"conversionDiagnostics":project(conversionErrors),"unsupported":unsupported})
 }
 b,e=json.Marshal(map[string]any{"inventory":inventory,"vectors":rows});if e!=nil{t.Fatal(fmt.Sprint(e))};if e=os.WriteFile(os.Getenv("TSCS_STRING_OUTPUT"),b,0600);e!=nil{t.Fatal(e)}
}

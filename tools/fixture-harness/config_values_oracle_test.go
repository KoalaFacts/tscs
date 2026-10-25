package tsoptions
import("encoding/json";"os";"testing";"fmt";"math";"unicode/utf16";"github.com/microsoft/TypeScript/tsc/internal/ast";"github.com/microsoft/TypeScript/tsc/internal/core";"github.com/microsoft/TypeScript/tsc/internal/parser";"github.com/microsoft/TypeScript/tsc/internal/stringutil";"github.com/microsoft/TypeScript/tsc/internal/collections";"github.com/microsoft/TypeScript/tsc/internal/locale")
func configUnits(s string)[]uint16{r:=[]uint16{};for len(s)>0{c,n:=stringutil.DecodeJSStringRune(s);s=s[n:];if c<=0xffff{r=append(r,uint16(c))}else{r=append(r,utf16.Encode([]rune{c})...)}};return r}
func configProjection(v any)map[string]any{
 kind,bits:="Null","";units:=[]uint16{};props:=[]any{};items:=[]any{};isNil:=false
 switch x:=v.(type){case struct{}:kind="EmptySource";case bool:if x{kind="True"}else{kind="False"};case string:kind="String";units=configUnits(x);case float64:kind="Number";bits=fmt.Sprintf("%016x",math.Float64bits(x));case *collections.OrderedMap[string,any]:kind="Object";for k,c:=range x.Entries(){props=append(props,map[string]any{"key":configUnits(k),"value":configProjection(c)})};case []any:kind="Array";isNil=x==nil;for _,c:=range x{items=append(items,configProjection(c))};case nil:default:panic(fmt.Sprintf("unmapped %T",v))}
 return map[string]any{"kind":kind,"bits":bits,"units":units,"properties":props,"elements":items,"isNil":isNil}
}
func TestTscsConfigValuesOracle(t *testing.T){b,e:=os.ReadFile(os.Getenv("TSCS_STRING_INPUT"));if e!=nil{t.Fatal(e)};var rows []struct{ID string `json:"id"`;Source string `json:"source"`};if e=json.Unmarshal(b,&rows);e!=nil{t.Fatal(e)};out:=[]any{}
 for _,r:=range rows{sf:=parser.ParseSourceFile(ast.SourceFileParseOptions{FileName:"/tsconfig.json"},r.Source,core.ScriptKindJSON);value,errs:=convertToObject(sf);ds:=[]any{};for _,d:=range errs{ds=append(ds,map[string]any{"code":d.Code(),"message":d.Localize(locale.Default),"start":len(utf16.Encode([]rune(r.Source[:d.Pos()]))),"length":len(utf16.Encode([]rune(r.Source[d.Pos():d.End()])))})};out=append(out,map[string]any{"id":r.ID,"source":r.Source,"value":configProjection(value),"diagnostics":ds})}
 b,e=json.MarshalIndent(out,"","  ");if e!=nil{t.Fatal(e)};if e=os.WriteFile(os.Getenv("TSCS_STRING_OUTPUT"),b,0600);e!=nil{t.Fatal(e)}
}

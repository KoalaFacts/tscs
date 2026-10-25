package scanner
import("encoding/json";"os";"testing";"unicode/utf16";"github.com/microsoft/TypeScript/tsc/internal/ast")
func TestTscsStringLinesOracle(t *testing.T){
 b,e:=os.ReadFile(os.Getenv("TSCS_STRING_INPUT"));if e!=nil{t.Fatal(e)}
 var rows []struct{ID string `json:"id"`;Literal string `json:"literal"`};if e=json.Unmarshal(b,&rows);e!=nil{t.Fatal(e)}
 out:=[]map[string]any{}
 for _,r:=range rows{s:=NewScanner();s.SetText(r.Literal);kind:=s.Scan();if kind!=ast.KindStringLiteral{t.Fatalf("%s is not string token",r.ID)};out=append(out,map[string]any{"id":r.ID,"literal":r.Literal,"unterminated":s.TokenFlags()&ast.TokenFlagsUnterminated!=0,"tokenEndUtf16":len(utf16.Encode([]rune(r.Literal[:s.TokenEnd()]))),"value":s.TokenValue()})}
 b,e=json.MarshalIndent(out,"","  ");if e!=nil{t.Fatal(e)};if e=os.WriteFile(os.Getenv("TSCS_STRING_OUTPUT"),b,0600);e!=nil{t.Fatal(e)}
}

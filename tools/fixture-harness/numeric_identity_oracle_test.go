package scanner

import (
 "encoding/base64"
 "encoding/json"
 "github.com/microsoft/TypeScript/tsc/internal/locale"
 "os"
 "testing"
 "unicode/utf16"
 "github.com/microsoft/TypeScript/tsc/internal/ast"
 "github.com/microsoft/TypeScript/tsc/internal/diagnostics"
 "github.com/microsoft/TypeScript/tsc/internal/stringutil"
)

func TestTscsNumericIdentityOracle(t *testing.T) {
 b,e:=os.ReadFile(os.Getenv("TSCS_STRING_INPUT"));if e!=nil{t.Fatal(e)}
 var rows []struct{ID string `json:"id"`;Source string `json:"source"`};if e=json.Unmarshal(b,&rows);e!=nil{t.Fatal(e)}
 out:=[]map[string]any{}
 for _,r:=range rows {
  offset:=func(p int)int{return len(utf16.Encode([]rune(r.Source[:p])))}
  ds:=[]map[string]any{};s:=NewScanner();s.SetText(r.Source)
  s.SetOnError(func(d *diagnostics.Message,start,length int,args ...any){ds=append(ds,map[string]any{"code":d.Code(),"start":offset(start),"length":offset(start+length)-offset(start),"message":d.Localize(locale.Default,args...)})})
  tokens:=[]map[string]any{}
  for {
   kind:=s.Scan();name:=""
   switch kind {case ast.KindStringLiteral:name="String";case ast.KindIdentifier,ast.KindStringKeyword:name="Identifier";case ast.KindNumericLiteral:name="Number";case ast.KindSemicolonToken:name="Semi";case ast.KindEqualsToken:name="Equal";case ast.KindLetKeyword:name="Let";case ast.KindColonToken:name="Colon";case ast.KindEndOfFile:name="End";default:t.Fatalf("unmapped kind %v for %s",kind,r.ID)}
   units:=[]uint16{};valueBytes:=[]byte{}
   if kind==ast.KindStringLiteral{value:=s.TokenValue();valueBytes=[]byte(value);for len(value)>0{ch,n:=stringutil.DecodeJSStringRune(value);value=value[n:];if ch<=0xFFFF{units=append(units,uint16(ch))}else{units=append(units,utf16.Encode([]rune{ch})...)}}}
   tokens=append(tokens,map[string]any{"kind":name,"start":offset(s.TokenStart()),"end":offset(s.TokenEnd()),"text":r.Source[s.TokenStart():s.TokenEnd()],"lineBreak":s.TokenFlags()&ast.TokenFlagsPrecedingLineBreak!=0,"flags":s.TokenFlags()&ast.TokenFlagsStringLiteralFlags,"valueUnits":units,"valueBytes":base64.StdEncoding.EncodeToString(valueBytes)})
   if kind==ast.KindEndOfFile{break}
  }
  out=append(out,map[string]any{"id":r.ID,"source":r.Source,"tokens":tokens,"diagnostics":ds})
 }
 b,e=json.MarshalIndent(out,"","  ");if e!=nil{t.Fatal(e)};if e=os.WriteFile(os.Getenv("TSCS_STRING_OUTPUT"),b,0600);e!=nil{t.Fatal(e)}
}

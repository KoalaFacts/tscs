package testrunner
import("encoding/json";"encoding/base64";"os";"testing";"github.com/microsoft/TypeScript/tsc/internal/testutil/harnessutil")
// Preparation fragment only: actual raw parser and config-name helper. This
// does not call makeUnitsFromTest's VFS factory or JSON/config parser.
func TestTscsConfigSelectionOracle(t *testing.T){
 b,e:=os.ReadFile(os.Getenv("TSCS_STATE_INPUT"));if e!=nil{t.Fatal(e)};var rows []stateRequest;if e=json.Unmarshal(b,&rows);e!=nil{t.Fatal(e)}
 out:=[]map[string]any{}
 for _,r:=range rows{
  p:=stateProbe(r);var expected map[string]any
  if p["panic"]!=nil{expected=map[string]any{"panic":p["panic"]}}else{
   b,_:=json.Marshal(p);var raw struct{Units []struct{Name string `json:"name"`;Content string `json:"content"`};Options map[string]string};if e=json.Unmarshal(b,&raw);e!=nil{t.Fatal(e)}
   units:=[]map[string]string{};var selected any;index:=-1
   for i,u:=range raw.Units {v:=map[string]string{"name":u.Name,"content":u.Content};name,_:=base64.StdEncoding.DecodeString(u.Name);if index<0&&harnessutil.GetConfigNameFromFileName(string(name))!=""{selected=v;index=i}else{units=append(units,v)}}
   external,_:=base64.StdEncoding.DecodeString(raw.Options["runexternalcode"])
   expected=map[string]any{"units":units,"configFile":selected,"configIndex":index,"runExternalCode":string(external)=="true","panic":nil}
  }
  out=append(out,map[string]any{"id":r.ID,"input":r.Input,"fileName":r.FileName,"expected":expected})
 }
 b,e=json.MarshalIndent(out,"","  ");if e!=nil{t.Fatal(e)};if e=os.WriteFile(os.Getenv("TSCS_STATE_OUTPUT"),b,0600);e!=nil{t.Fatal(e)}
}

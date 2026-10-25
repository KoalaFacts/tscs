// Read-only overlay: actual extraction/helper, with runner fallback mirrored
// from compiler_runner.go and transpile_runner.go; no compiler assertions.
package testrunner

import (
 "encoding/base64"
 "encoding/json"
 "fmt"
 "maps"
 "os"
 "slices"
 "sort"
 "strings"
 "testing"
 "unicode/utf8"
 "github.com/microsoft/TypeScript/tsc/internal/testutil/harnessutil"
 "github.com/microsoft/TypeScript/tsc/internal/tspath"
 "github.com/microsoft/TypeScript/tsc/internal/vfs/osvfs"
)
type sourceConfigRequest struct { ID string `json:"id"`; Input string `json:"input"`; Runner string `json:"runner"` }
func TestTscsSourceConfigurationsOracle(t *testing.T) {
 b,e:=os.ReadFile(os.Getenv("TSCS_SOURCE_CONFIG_INPUT"));if e!=nil {t.Fatal(e)}
 var requests []sourceConfigRequest;if e=json.Unmarshal(b,&requests);e!=nil {t.Fatal(e)}
 rows:=[]map[string]any{}
 for _,r:=range requests {
  expected:=map[string]any{"settings":nil,"helper":nil,"configurations":nil,"panic":nil,"fatal":false,"unsupported":nil}
  t.Run(r.ID,func(st *testing.T){
   complete:=false
   defer func(){if p:=recover();p!=nil {expected["panic"]=fmt.Sprint(p)} else if !complete {expected["fatal"]=true}}()
   raw,e:=base64.StdEncoding.DecodeString(r.Input);if e!=nil {st.Fatal(e)}
   f,e:=os.CreateTemp("","tscs-config-source-");if e!=nil {st.Fatal(e)};defer os.Remove(f.Name())
   if _,e=f.Write(raw);e!=nil {st.Fatal(e)};if e=f.Close();e!=nil {st.Fatal(e)}
   content,ok:=osvfs.FS().ReadFile(tspath.RootedFilePathFromNormalized(f.Name()));if !ok {st.Fatal("read")}
   settings:=extractCompilerSettings(content);bytes:=map[string]string{};invalid:=false
   for k,v:=range settings {bytes[k]=base64.StdEncoding.EncodeToString([]byte(v));invalid=invalid||!utf8.ValidString(v)}
   expected["settings"]=bytes
   if invalid {expected["unsupported"]="INVALID_UTF8_SETTING";complete=true;return}
   vary:=compilerVaryBy;if r.Runner=="transpile" {vary=transpileVaryBy} else if r.Runner!="compiler" {st.Fatal("unknown runner")}
   configs:=harnessutil.GetFileBasedTestConfigurations(st,settings,vary)
   sort.Slice(configs,func(i,j int)bool{
    a,b:=configs[i],configs[j];if a.Name!=b.Name{return a.Name<b.Name}
    ak,bk:=slices.Sorted(maps.Keys(a.Config)),slices.Sorted(maps.Keys(b.Config))
    for k:=0;k<min(len(ak),len(bk));k++{if c:=strings.Compare(ak[k],bk[k]);c!=0{return c<0};if c:=strings.Compare(a.Config[ak[k]],b.Config[bk[k]]);c!=0{return c<0}}
    return len(ak)<len(bk)
   })
   expected["helper"]=configs
   if len(configs)==0 {if r.Runner=="transpile" {configs=[]*harnessutil.NamedTestConfiguration{{Config:settings}}}else{configs=[]*harnessutil.NamedTestConfiguration{nil}}}
   expected["configurations"]=configs;complete=true
  })
  rows=append(rows,map[string]any{"id":r.ID,"input":r.Input,"runner":r.Runner,"expected":expected})
 }
 b,e=json.MarshalIndent(map[string]any{"vectors":rows,"compilerVaryBy":slices.Sorted(maps.Keys(compilerVaryBy.Keys())),"transpileVaryBy":slices.Sorted(maps.Keys(transpileVaryBy.Keys()))},"","  ");if e!=nil{t.Fatal(e)}
 if e=os.WriteFile(os.Getenv("TSCS_SOURCE_CONFIG_OUTPUT"),b,0600);e!=nil{t.Fatal(e)}
}

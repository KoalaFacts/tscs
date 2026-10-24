// Test-only adapter for pinned Microsoft TypeScript; not part of tscs production.
package main
import (
 "context"
 "fmt"
 "encoding/json"
 "os"
 "path/filepath"
 "unicode/utf16"
 "github.com/microsoft/TypeScript/tsc/internal/ast"
 "github.com/microsoft/TypeScript/tsc/internal/compiler"
 "github.com/microsoft/TypeScript/tsc/internal/core"
 "github.com/microsoft/TypeScript/tsc/internal/tsoptions"
 "github.com/microsoft/TypeScript/tsc/internal/tspath"
 "github.com/microsoft/TypeScript/tsc/internal/vfs/osvfs"
)
type diagnostic struct {Code string `json:"code"`; Category string `json:"category"`; File string `json:"file"`; Start int `json:"start"`; Length int `json:"length"`; Message string `json:"message"`; Related []diagnostic `json:"related"`; MessageChain []diagnostic `json:"messageChain"`}
func convert(d *ast.Diagnostic) diagnostic {
 r:=diagnostic{Code:fmt.Sprintf("TS%d", d.Code()),Category:d.Category().Name(),Start:d.Pos(),Length:d.Len(),Message:d.String(),Related:[]diagnostic{},MessageChain:[]diagnostic{}}
 if f:=d.File();f!=nil {r.File=filepath.Base(string(f.FileName()));t:=f.Text();r.Start=len(utf16.Encode([]rune(t[:d.Pos()])));r.Length=len(utf16.Encode([]rune(t[d.Pos():d.End()])))}
 for _,x:=range d.RelatedInformation(){r.Related=append(r.Related,convert(x))};for _,x:=range d.MessageChain(){r.MessageChain=append(r.MessageChain,convert(x))};return r
}
func main(){
 cwd,_:=os.Getwd(); roots:=[]tspath.RootedFilePath{};for _,a:=range os.Args[1:] {p,_:=filepath.Abs(a);roots=append(roots,tspath.RootedFilePath(filepath.ToSlash(p)))}
 opts:=&core.CompilerOptions{NoEmit:core.TSTrue,NoLib:core.TSTrue,Target:core.ScriptTargetES2015,Types:[]string{}}
 config:=tsoptions.NewParsedCommandLine(opts,roots,nil,tspath.RootedDirectoryPath(cwd),tspath.CaseSensitive)
 p:=compiler.NewProgram(compiler.ProgramOptions{ProgramConfig:compiler.ProgramConfig{Config:config,SingleThreaded:core.TSTrue},ProgramHosts:compiler.ProgramHosts{Host:compiler.NewCompilerHost(osvfs.FS(),"/",nil,nil,nil)}})
 ds:=compiler.GetDiagnosticsOfAnyProgram(context.Background(),p,nil,false,p.GetBindDiagnostics,p.GetSemanticDiagnostics)
 ds=compiler.SortAndDeduplicateDiagnostics(ds)
 out:=[]diagnostic{};for _,d:=range ds {out=append(out,convert(d))};code:=0;if len(out)>0{code=2}
 json.NewEncoder(os.Stdout).Encode(struct{Diagnostics []diagnostic `json:"diagnostics"`;ExitCode int `json:"exitCode"`}{out,code});os.Exit(code)
}

// Read-only Go overlay: actual case-sensitive vfstest/iovfs operations.
package testrunner

import (
	"encoding/base64"
	"encoding/json"
	"github.com/microsoft/TypeScript/tsc/internal/tspath"
	"github.com/microsoft/TypeScript/tsc/internal/vfs/vfstest"
	"os"
	"testing"
)

type vfsEntry struct {
	Path string `json:"path"`
	Data string `json:"data"`
	Link bool   `json:"link"`
}
type vfsRequest struct {
	ID      string     `json:"id"`
	Entries []vfsEntry `json:"entries"`
	Queries []string   `json:"queries"`
}

func vfsProbe(r vfsRequest) (out map[string]any) {
	defer func() {
		if e := recover(); e != nil {
			out = map[string]any{"panic": e}
		}
	}()
	dec := func(s string) string {
		b, e := base64.StdEncoding.DecodeString(s)
		if e != nil {
			panic(e)
		}
		return string(b)
	}
	entries := map[string]any{}
	for _, e := range r.Entries {
		if e.Link {
			entries[dec(e.Path)] = vfstest.Symlink(dec(e.Data))
		} else {
			entries[dec(e.Path)] = dec(e.Data)
		}
	}
	fs := vfstest.FromMap(entries, tspath.CaseSensitive)
	queries := []map[string]any{}
	for _, q := range r.Queries {
		p := tspath.RootedPathFromAbsolute(dec(q))
		content, ok := fs.ReadFile(tspath.RootedFilePathFromPath(p))
		queries = append(queries, map[string]any{"path": stateB64(p.AsString()), "content": stateB64(content), "readOk": ok, "fileExists": fs.FileExists(tspath.RootedFilePathFromPath(p)), "directoryExists": fs.DirectoryExists(tspath.RootedDirectoryPathFromPath(p)), "realpath": stateB64(fs.Realpath(p).AsString())})
	}
	return map[string]any{"queries": queries, "panic": nil}
}
func TestTscsVfsOracle(t *testing.T) {
	b, e := os.ReadFile(os.Getenv("TSCS_STATE_INPUT"))
	if e != nil {
		t.Fatal(e)
	}
	var rows []vfsRequest
	if e = json.Unmarshal(b, &rows); e != nil {
		t.Fatal(e)
	}
	out := []map[string]any{}
	for _, r := range rows {
		out = append(out, map[string]any{"id": r.ID, "entries": r.Entries, "queries": r.Queries, "expected": vfsProbe(r)})
	}
	b, e = json.MarshalIndent(out, "", "  ")
	if e != nil {
		t.Fatal(e)
	}
	if e = os.WriteFile(os.Getenv("TSCS_STATE_OUTPUT"), b, 0600); e != nil {
		t.Fatal(e)
	}
}

// Read-only overlay: executes the pinned tspath implementation.
package tspath

import (
	"encoding/base64"
	"encoding/json"
	"os"
	"testing"
)

type pathRequest struct {
	ID        string `json:"id"`
	Path      string `json:"path"`
	Directory string `json:"directory"`
}

func pathProbe(r pathRequest) (out map[string]any) {
	p, e := base64.StdEncoding.DecodeString(r.Path)
	if e != nil {
		panic(e)
	}
	d, e := base64.StdEncoding.DecodeString(r.Directory)
	if e != nil {
		panic(e)
	}
	b := func(s string) string { return base64.StdEncoding.EncodeToString([]byte(s)) }
	out = map[string]any{"encodedRoot": GetEncodedRootLength(string(p)), "normalize": b(NormalizePath(string(p))), "combine": b(CombinePaths(string(d), string(p))), "rooted": nil, "error": nil}
	defer func() {
		if e := recover(); e != nil {
			out["error"] = e
		}
	}()
	out["rooted"] = b(ToRootedPath(string(p), RootedDirectoryPath(string(d))).AsString())
	return
}
func TestTscsPathOracle(t *testing.T) {
	bytes, e := os.ReadFile(os.Getenv("TSCS_PATH_INPUT"))
	if e != nil {
		t.Fatal(e)
	}
	var rows []pathRequest
	if e = json.Unmarshal(bytes, &rows); e != nil {
		t.Fatal(e)
	}
	out := []map[string]any{}
	for _, r := range rows {
		out = append(out, map[string]any{"id": r.ID, "path": r.Path, "directory": r.Directory, "expected": pathProbe(r)})
	}
	bytes, e = json.MarshalIndent(out, "", "  ")
	if e != nil {
		t.Fatal(e)
	}
	if e = os.WriteFile(os.Getenv("TSCS_PATH_OUTPUT"), bytes, 0600); e != nil {
		t.Fatal(e)
	}
}

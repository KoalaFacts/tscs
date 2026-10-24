// Read-only overlay; raw parser and tspath mapping stage before config/VFS creation.
package testrunner

import (
	"encoding/base64"
	"encoding/json"
	"github.com/microsoft/TypeScript/tsc/internal/tspath"
	"os"
	"sort"
	"testing"
)

func mapProbe(row stateRequest) (out map[string]any) {
	defer func() {
		if e := recover(); e != nil {
			out = map[string]any{"panic": e}
		}
	}()
	raw := stateProbe(row)
	if p := raw["panic"]; p != nil {
		return map[string]any{"panic": p}
	}
	decode := func(v any) string {
		b, e := base64.StdEncoding.DecodeString(v.(string))
		if e != nil {
			panic(e)
		}
		return string(b)
	}
	directory := srcFolder
	if d := decode(raw["directory"]); d != "" {
		directory = tspath.ToRootedDirectoryPath(d, srcFolder)
	}
	files := map[string]string{}
	units := raw["units"].([]map[string]any)
	for _, u := range units {
		files[tspath.ToRootedFilePath(decode(u["name"]), directory).AsString()] = decode(u["content"])
	}
	keys := []string{}
	for p := range files {
		keys = append(keys, p)
	}
	sort.Strings(keys)
	entries := []map[string]string{}
	for _, p := range keys {
		entries = append(entries, map[string]string{"path": stateB64(p), "content": stateB64(files[p])})
	}
	links := []map[string]string{}
	for _, l := range raw["links"].([]map[string]string) {
		links = append(links, map[string]string{"rawAlias": l["alias"], "rawTarget": l["target"], "alias": stateB64(tspath.GetNormalizedAbsolutePath(decode(l["alias"]), directory)), "target": stateB64(tspath.GetNormalizedAbsolutePath(decode(l["target"]), directory))})
	}
	return map[string]any{"directory": stateB64(directory.AsString()), "files": entries, "links": links, "unitCount": len(units), "panic": nil}
}
func TestTscsFileMapOracle(t *testing.T) {
	b, e := os.ReadFile(os.Getenv("TSCS_STATE_INPUT"))
	if e != nil {
		t.Fatal(e)
	}
	var rows []stateRequest
	if e = json.Unmarshal(b, &rows); e != nil {
		t.Fatal(e)
	}
	out := []map[string]any{}
	for _, r := range rows {
		out = append(out, map[string]any{"id": r.ID, "input": r.Input, "fileName": r.FileName, "expected": mapProbe(r)})
	}
	b, e = json.MarshalIndent(out, "", "  ")
	if e != nil {
		t.Fatal(e)
	}
	if e = os.WriteFile(os.Getenv("TSCS_STATE_OUTPUT"), b, 0600); e != nil {
		t.Fatal(e)
	}
}

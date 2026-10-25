package testrunner

import (
	"encoding/base64"
	"encoding/json"
	"github.com/microsoft/TypeScript/tsc/internal/tsoptions/tsoptionstest"
	"github.com/microsoft/TypeScript/tsc/internal/tspath"
	"os"
	"sort"
	"testing"
)

func constructionProbe(r stateRequest) (out map[string]any) {
	defer func() {
		if e := recover(); e != nil {
			out = map[string]any{"panic": e}
		}
	}()
	m := mapProbe(r)
	if m["panic"] != nil {
		return m
	}
	dec := func(v string) string {
		b, e := base64.StdEncoding.DecodeString(v)
		if e != nil {
			panic(e)
		}
		return string(b)
	}
	files := map[string]string{}
	links := map[string]string{}
	seen := map[string]bool{}
	queries := map[string]bool{}
	directory := dec(m["directory"].(string))
	queries[directory] = true
	for _, e := range m["files"].([]map[string]string) {
		p := dec(e["path"])
		files[p] = dec(e["content"])
		queries[p] = true
	}
	for _, e := range m["links"].([]map[string]string) {
		a, t := dec(e["alias"]), dec(e["target"])
		if seen[a] {
			return map[string]any{"unsupported": "normalized link alias collision"}
		}
		seen[a] = true
		links[dec(e["rawAlias"])] = dec(e["rawTarget"])
		queries[a] = true
		queries[t] = true
	}
	fs := tsoptionstest.NewVFSWithSymlinks(files, links, tspath.RootedDirectoryPathFromAbsolute(directory), tspath.CaseSensitive)
	keys := []string{}
	for q := range queries {
		keys = append(keys, q)
	}
	sort.Strings(keys)
	results := []map[string]any{}
	encode := func(names []string) []string {
		if names == nil {
			return nil
		}
		out := make([]string, len(names))
		for i, s := range names {
			out[i] = stateB64(s)
		}
		return out
	}
	for _, q := range keys {
		p := tspath.RootedPathFromAbsolute(q)
		content, ok := fs.ReadFile(tspath.RootedFilePathFromPath(p))
		e := fs.GetAccessibleEntries(tspath.RootedDirectoryPathFromPath(p))
		syms := []string{}
		for n := range e.Symlinks {
			syms = append(syms, n)
		}
		sort.Strings(syms)
		results = append(results, map[string]any{"path": stateB64(p.AsString()), "content": stateB64(content), "readOk": ok, "fileExists": fs.FileExists(tspath.RootedFilePathFromPath(p)), "directoryExists": fs.DirectoryExists(tspath.RootedDirectoryPathFromPath(p)), "realpath": stateB64(fs.Realpath(p).AsString()), "files": encode(e.Files), "directories": encode(e.Directories), "symlinks": encode(syms)})
	}
	return map[string]any{"queries": results, "panic": nil}
}
func TestTscsConstructionOracle(t *testing.T) {
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
		out = append(out, map[string]any{"id": r.ID, "input": r.Input, "fileName": r.FileName, "expected": constructionProbe(r)})
	}
	b, e = json.MarshalIndent(out, "", "  ")
	if e != nil {
		t.Fatal(e)
	}
	if e = os.WriteFile(os.Getenv("TSCS_STATE_OUTPUT"), b, 0600); e != nil {
		t.Fatal(e)
	}
}

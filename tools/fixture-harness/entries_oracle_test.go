// Read-only overlay: executes the pinned Common.GetAccessibleEntries operation.
package testrunner

import (
	"encoding/base64"
	"encoding/json"
	"github.com/microsoft/TypeScript/tsc/internal/tspath"
	"github.com/microsoft/TypeScript/tsc/internal/vfs/vfstest"
	"os"
	"slices"
	"testing"
)

func entriesProbe(r vfsRequest) (out map[string]any) {
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
	encode := func(names []string) []string {
		if names == nil {
			return nil
		}
		result := make([]string, len(names))
		for i, s := range names {
			result[i] = stateB64(s)
		}
		return result
	}
	queries := []map[string]any{}
	for _, q := range r.Queries {
		p := tspath.RootedPathFromAbsolute(dec(q))
		e := fs.GetAccessibleEntries(tspath.RootedDirectoryPathFromPath(p))
		links := []string{}
		for name := range e.Symlinks {
			links = append(links, name)
		}
		slices.Sort(links)
		queries = append(queries, map[string]any{"path": stateB64(p.AsString()), "files": encode(e.Files), "directories": encode(e.Directories), "symlinks": encode(links)})
	}
	return map[string]any{"queries": queries, "panic": nil}
}
func TestTscsEntriesOracle(t *testing.T) {
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
		out = append(out, map[string]any{"id": r.ID, "entries": r.Entries, "queries": r.Queries, "expected": entriesProbe(r)})
	}
	b, e = json.MarshalIndent(out, "", "  ")
	if e != nil {
		t.Fatal(e)
	}
	if e = os.WriteFile(os.Getenv("TSCS_STATE_OUTPUT"), b, 0600); e != nil {
		t.Fatal(e)
	}
}

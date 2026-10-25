// Read-only overlay adapter: calls the actual pinned generic parser and VFS.
package testrunner

import (
	"encoding/base64"
	"encoding/json"
	"errors"
	"github.com/microsoft/TypeScript/tsc/internal/tspath"
	"github.com/microsoft/TypeScript/tsc/internal/vfs/osvfs"
	"os"
	"sort"
	"testing"
)

type stateRequest struct {
	ID        string `json:"id"`
	Input     string `json:"input"`
	FileName  string `json:"fileName"`
	Implicit  bool   `json:"implicit"`
	FailAt    int    `json:"failAt"`
	ReturnNil bool   `json:"returnNil"`
}

func stateB64(s string) string { return base64.StdEncoding.EncodeToString([]byte(s)) }
func stateOptions(m map[string]string) map[string]string {
	r := map[string]string{}
	for k, v := range m {
		r[k] = stateB64(v)
	}
	return r
}
func stateProbe(row stateRequest) (result map[string]any) {
	calls := 0
	defer func() {
		if e := recover(); e != nil {
			result = map[string]any{"panic": e, "calls": calls}
		}
	}()
	bytes, err := base64.StdEncoding.DecodeString(row.Input)
	if err != nil {
		panic(err.Error())
	}
	temp, err := os.CreateTemp("", "tscs-state-source-")
	if err != nil {
		panic(err.Error())
	}
	defer os.Remove(temp.Name())
	if _, err = temp.Write(bytes); err != nil {
		panic(err.Error())
	}
	temp.Close()
	text, ok := osvfs.FS().ReadFile(tspath.RootedFilePathFromNormalized(temp.Name()))
	if !ok {
		panic("read")
	}
	filename, err := base64.StdEncoding.DecodeString(row.FileName)
	if err != nil {
		panic(err.Error())
	}
	units, links, dir, opts, e := ParseTestFilesAndSymlinksWithOptions(text, string(filename), func(name, body string, options map[string]string) (map[string]any, error) {
		calls++
		unit := map[string]any{"name": stateB64(name), "content": stateB64(body), "options": stateOptions(options)}
		if row.ReturnNil {
			unit = nil
		}
		if row.FailAt == calls {
			return unit, errors.New("callback")
		}
		return unit, nil
	}, ParseTestFilesOptions{AllowImplicitFirstFile: row.Implicit})
	keys := []string{}
	for k := range links {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	linkData := []map[string]string{}
	for _, k := range keys {
		linkData = append(linkData, map[string]string{"alias": stateB64(k), "target": stateB64(links[k])})
	}
	var errorValue any
	if e != nil {
		errorValue = e.Error()
	}
	return map[string]any{"units": units, "links": linkData, "directory": stateB64(dir), "options": stateOptions(opts), "error": errorValue, "panic": nil, "calls": calls}
}
func TestTscsFixtureStateOracle(t *testing.T) {
	b, err := os.ReadFile(os.Getenv("TSCS_STATE_INPUT"))
	if err != nil {
		t.Fatal(err)
	}
	var rows []stateRequest
	if err = json.Unmarshal(b, &rows); err != nil {
		t.Fatal(err)
	}
	out := []map[string]any{}
	for _, row := range rows {
		out = append(out, map[string]any{"id": row.ID, "input": row.Input, "fileName": row.FileName, "implicit": row.Implicit, "failAt": row.FailAt, "returnNil": row.ReturnNil, "expected": stateProbe(row)})
	}
	b, err = json.MarshalIndent(out, "", "  ")
	if err != nil {
		t.Fatal(err)
	}
	if err = os.WriteFile(os.Getenv("TSCS_STATE_OUTPUT"), b, 0600); err != nil {
		t.Fatal(err)
	}
}

package testrunner

import (
	"encoding/base64"
	"encoding/json"
	"github.com/microsoft/TypeScript/tsc/internal/testutil/harnessutil"
	"github.com/microsoft/TypeScript/tsc/internal/tspath"
	"os"
	"testing"
)

// Preparation fragment only: actual raw parser and config-name helper. This
// does not call makeUnitsFromTest's VFS factory or JSON/config parser.
func TestTscsConfigAstSelectionOracle(t *testing.T) {
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
		p := stateProbe(r)
		var expected map[string]any
		var context any
		if p["panic"] != nil {
			expected = map[string]any{"panic": p["panic"]}
		} else {
			b, _ := json.Marshal(p)
			var raw struct {
				Units []struct {
					Name    string `json:"name"`
					Content string `json:"content"`
				}
				Directory string
				Options   map[string]string
			}
			if e = json.Unmarshal(b, &raw); e != nil {
				t.Fatal(e)
			}
			units := []map[string]string{}
			var selected any
			index := -1
			var configPath any
			rawDirectory, _ := base64.StdEncoding.DecodeString(raw.Directory)
			directory := srcFolder
			if len(rawDirectory) > 0 {
				directory = tspath.ToRootedDirectoryPath(string(rawDirectory), srcFolder)
			}
			for i, u := range raw.Units {
				v := map[string]string{"name": u.Name, "content": u.Content}
				name, _ := base64.StdEncoding.DecodeString(u.Name)
				if index < 0 && harnessutil.GetConfigNameFromFileName(string(name)) != "" {
					selected = v
					index = i
					configPath = stateB64(tspath.ToRootedFilePath(string(name), directory).AsString())
				} else {
					units = append(units, v)
				}
			}
			external, _ := base64.StdEncoding.DecodeString(raw.Options["runexternalcode"])
			context = map[string]any{"directory": stateB64(directory.AsString()), "configPath": configPath}
			expected = map[string]any{"units": units, "configFile": selected, "configIndex": index, "runExternalCode": string(external) == "true", "panic": nil}
		}
		out = append(out, map[string]any{"id": r.ID, "input": r.Input, "fileName": r.FileName, "expected": expected, "context": context})
	}
	b, e = json.MarshalIndent(out, "", "  ")
	if e != nil {
		t.Fatal(e)
	}
	if e = os.WriteFile(os.Getenv("TSCS_STATE_OUTPUT"), b, 0600); e != nil {
		t.Fatal(e)
	}
}

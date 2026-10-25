package testrunner

import (
	"encoding/json"
	"fmt"
	"github.com/microsoft/TypeScript/tsc/internal/collections"
	"github.com/microsoft/TypeScript/tsc/internal/testutil/harnessutil"
	"maps"
	"os"
	"slices"
	"sort"
	"strings"
	"testing"
	"unicode"
)

type configRequest struct {
	ID       string            `json:"id"`
	Settings map[string]string `json:"settings"`
	VaryBy   []string          `json:"varyBy"`
}

func TestTscsConfigurationsOracle(t *testing.T) {
	b, e := os.ReadFile(os.Getenv("TSCS_CONFIG_INPUT"))
	if e != nil {
		t.Fatal(e)
	}
	var requests []configRequest
	if e = json.Unmarshal(b, &requests); e != nil {
		t.Fatal(e)
	}
	lowercase := map[string]int{}
	for r := rune(0); r <= unicode.MaxRune; r++ {
		l := unicode.ToLower(r)
		if r != l {
			lowercase[fmt.Sprint(int(r))] = int(l)
			requests = append(requests, configRequest{ID: fmt.Sprintf("unicode/%06x", r), Settings: map[string]string{"outDir": string(r) + "," + string(l)}, VaryBy: []string{"outDir"}})
		}
	}
	rows := []map[string]any{}
	for _, r := range requests {
		expected := map[string]any{"configurations": nil, "panic": nil, "fatal": false}
		t.Run(r.ID, func(st *testing.T) {
			complete := false
			defer func() {
				if p := recover(); p != nil {
					expected["panic"] = fmt.Sprint(p)
				} else if !complete {
					expected["fatal"] = true
				}
			}()
			vary := collections.NewSetFromItems(r.VaryBy...)
			configs := harnessutil.GetFileBasedTestConfigurations(st, r.Settings, vary)
			sort.Slice(configs, func(i, j int) bool {
				a, b := configs[i], configs[j]
				if a.Name != b.Name {
					return a.Name < b.Name
				}
				ak, bk := slices.Sorted(maps.Keys(a.Config)), slices.Sorted(maps.Keys(b.Config))
				for k := 0; k < min(len(ak), len(bk)); k++ {
					if c := strings.Compare(ak[k], bk[k]); c != 0 {
						return c < 0
					}
					if c := strings.Compare(a.Config[ak[k]], b.Config[bk[k]]); c != 0 {
						return c < 0
					}
				}
				return len(ak) < len(bk)
			})
			expected["configurations"] = configs
			complete = true
		})
		rows = append(rows, map[string]any{"id": r.ID, "settings": r.Settings, "varyBy": r.VaryBy, "expected": expected})
	}
	b, e = json.MarshalIndent(map[string]any{"vectors": rows, "unicodeVersion": unicode.Version, "lowercase": lowercase}, "", "  ")
	if e != nil {
		t.Fatal(e)
	}
	if e = os.WriteFile(os.Getenv("TSCS_CONFIG_OUTPUT"), b, 0600); e != nil {
		t.Fatal(e)
	}
}

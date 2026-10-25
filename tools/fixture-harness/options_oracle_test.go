package harnessutil

import (
	"encoding/json"
	"fmt"
	"github.com/microsoft/TypeScript/tsc/internal/tsoptions"
	"os"
	"slices"
	"strings"
	"testing"
)

func TestTscsOptionValuesOracle(t *testing.T) {
	canonical := func(v any) string {
		b, e := json.Marshal(v)
		if e != nil {
			panic(e)
		}
		return fmt.Sprintf("%T:%s", v, b)
	}
	options := []map[string]any{}
	rows := []map[string]any{}
	for position, o := range compilerOptions {
		kind := "other"
		if o.Kind == tsoptions.CommandLineOptionTypeBoolean {
			kind = "boolean"
		}
		if o.Kind == tsoptions.CommandLineOptionTypeEnum {
			kind = "enum"
		}
		values := []map[string]string{}
		all := getAllValuesForOption(o.Name)
		for _, k := range all {
			v, ok := tryGetValueOfOptionString(o.Name, k)
			if !ok {
				t.Fatal(k)
			}
			values = append(values, map[string]string{"key": k, "canonical": canonical(v)})
		}
		options = append(options, map[string]any{"name": o.Name, "kind": kind, "values": values})
		inputs := []string{"", " ", "*", "*,!unknown", "unknown", "-unknown"}
		if len(all) > 0 {
			inputs = append(inputs, all[0], strings.ToUpper(all[0])+","+all[0], "*,!"+all[0], "\u0085"+all[0]+"\u0085", "\ufeff"+all[0])
			if len(all) > 1 {
				inputs = append(inputs, all[0]+","+all[1])
			}
		}
		if kind == "enum" {
			inputs = append(inputs, strings.Join(all, ","))
			reversed := slices.Clone(all)
			slices.Reverse(reversed)
			inputs = append(inputs, strings.Join(reversed, ","))
			if len(all) > 0 {
				inputs = append(inputs, all[len(all)-1]+",*")
			}
			for _, k := range all {
				if strings.Contains(k, "i") {
					dotted := strings.ReplaceAll(k, "i", "\u0130")
					inputs = append(inputs, dotted, dotted+","+k)
				}
			}
		}
		for i, input := range inputs {
			expected := map[string]any{"values": nil, "panic": nil, "fatal": false}
			t.Run(fmt.Sprintf("%d/%s/%d", position, o.Name, i), func(st *testing.T) {
				complete := false
				defer func() {
					if p := recover(); p != nil {
						expected["panic"] = fmt.Sprint(p)
					} else if !complete {
						expected["fatal"] = true
					}
				}()
				v := splitOptionValues(st, input, o.Name)
				slices.Sort(v)
				expected["values"] = v
				complete = true
			})
			rows = append(rows, map[string]any{"id": fmt.Sprintf("%d/%s/%d", position, o.Name, i), "option": o.Name, "value": input, "expected": expected})
		}
	}
	out := map[string]any{"options": options, "vectors": rows}
	b, e := json.MarshalIndent(out, "", "  ")
	if e != nil {
		t.Fatal(e)
	}
	if e = os.WriteFile(os.Getenv("TSCS_OPTIONS_OUTPUT"), b, 0600); e != nil {
		t.Fatal(e)
	}
}

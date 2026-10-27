package tsoptions

import (
	"encoding/json"
	"fmt"
	"github.com/microsoft/TypeScript/tsc/internal/ast"
	"github.com/microsoft/TypeScript/tsc/internal/core"
	"github.com/microsoft/TypeScript/tsc/internal/parser"
	"os"
	"testing"
)

func TestTscsJsonOptionTypesOracle(t *testing.T) {
	b, e := os.ReadFile(os.Getenv("TSCS_STRING_INPUT"))
	if e != nil {
		t.Fatal(e)
	}
	var sources []string
	if e = json.Unmarshal(b, &sources); e != nil {
		t.Fatal(e)
	}
	var describe func(*CommandLineOption) any
	describe = func(o *CommandLineOption) any {
		if o == nil {
			return nil
		}
		return map[string]any{"kind": string(o.Kind), "disallowNull": o.DisallowNullOrUndefined(), "element": describe(o.Elements())}
	}
	options := append([]*CommandLineOption{}, OptionsDeclarations...)
	options = append(options, extendsOptionDeclaration, nil, &CommandLineOption{Name: "missingElement", Kind: CommandLineOptionTypeListOrElement})
	rows := []any{}
	for pos, o := range options {
		for i, source := range sources {
			sf := parser.ParseSourceFile(ast.SourceFileParseOptions{FileName: "/tsconfig.json"}, source, core.ScriptKindJSON)
			if len(sf.Diagnostics()) != 0 {
				t.Fatal("invalid source", source)
			}
			value, errs := convertToObject(sf)
			if len(errs) != 0 {
				t.Fatal(errs)
			}
			name := "nil"
			if o != nil {
				name = o.Name
			}
			rows = append(rows, map[string]any{"id": fmt.Sprintf("%d/%s/%d", pos, name, i), "source": source, "option": describe(o), "accepted": isCompilerOptionsValue(o, value)})
		}
	}
	b, e = json.Marshal(rows)
	if e != nil {
		t.Fatal(e)
	}
	if e = os.WriteFile(os.Getenv("TSCS_STRING_OUTPUT"), b, 0600); e != nil {
		t.Fatal(e)
	}
}

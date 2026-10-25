package parser

import (
	"encoding/base64"
	"encoding/json"
	"fmt"
	"github.com/microsoft/TypeScript/tsc/internal/ast"
	"github.com/microsoft/TypeScript/tsc/internal/core"
	"github.com/microsoft/TypeScript/tsc/internal/locale"
	"github.com/microsoft/TypeScript/tsc/internal/stringutil"
	"github.com/microsoft/TypeScript/tsc/internal/tspath"
	"os"
	"strconv"
	"testing"
	"unicode/utf16"
)

func TestTscsSourceConfigAstOracle(t *testing.T) {
	b, e := os.ReadFile(os.Getenv("TSCS_STRING_INPUT"))
	if e != nil {
		t.Fatal(e)
	}
	var rows []struct {
		ID          string `json:"id"`
		SourceBytes string `json:"sourceBytes"`
		FileName    string `json:"fileName"`
	}
	if e = json.Unmarshal(b, &rows); e != nil {
		t.Fatal(e)
	}
	out := []map[string]any{}
	for _, r := range rows {
		func() {
			sourceBytes, e := base64.StdEncoding.DecodeString(r.SourceBytes)
			if e != nil {
				t.Fatal(e)
			}
			fileName, e := base64.StdEncoding.DecodeString(r.FileName)
			if e != nil {
				t.Fatal(e)
			}
			source := string(sourceBytes)
			var unsupported any
			var panicValue any
			nodes := []map[string]any{}
			ds := []map[string]any{}
			defer func() {
				if p := recover(); p != nil {
					panicValue = fmt.Sprint(p)
				}
				out = append(out, map[string]any{"id": r.ID, "sourceBytes": r.SourceBytes, "fileName": r.FileName, "nodes": nodes, "diagnostics": ds, "unsupported": unsupported, "panic": panicValue})
			}()
			sf := ParseSourceFile(ast.SourceFileParseOptions{FileName: tspath.RootedFilePathFromNormalized(string(fileName))}, source, core.ScriptKindJSON)
			offset := func(p int) int { return len(utf16.Encode([]rune(source[:p]))) }
			var walk func(*ast.Node, string)
			walk = func(n *ast.Node, path string) {
				if n == nil {
					return
				}
				kind := ""
				value := ""
				number := ""
				switch n.Kind {
				case ast.KindObjectLiteralExpression:
					kind = "Object"
				case ast.KindArrayLiteralExpression:
					kind = "Array"
				case ast.KindStringLiteral:
					kind = "String"
					value = n.Text()
				case ast.KindIdentifier:
					kind = "Identifier"
					value = n.Text()
				case ast.KindNumericLiteral:
					kind = "Number"
					number = n.Text()
				case ast.KindPrefixUnaryExpression:
					if n.AsPrefixUnaryExpression().Operator != ast.KindMinusToken || n.AsPrefixUnaryExpression().Operand.Kind != ast.KindNumericLiteral {
						unsupported = "UNMAPPED_REFERENCE_PREFIX at " + path
						return
					}
					kind = "Number"
					number = "-" + n.AsPrefixUnaryExpression().Operand.Text()
				case ast.KindTrueKeyword:
					kind = "True"
				case ast.KindFalseKeyword:
					kind = "False"
				case ast.KindNullKeyword:
					kind = "Null"
				default:
					unsupported = fmt.Sprintf("UNMAPPED_REFERENCE_AST_KIND:%v at %s", n.Kind, path)
					return
				}
				units := []uint16{}
				for len(value) > 0 {
					c, z := stringutil.DecodeJSStringRune(value)
					value = value[z:]
					if c <= 0xffff {
						units = append(units, uint16(c))
					} else {
						units = append(units, utf16.Encode([]rune{c})...)
					}
				}
				span := getErrorSpanForNode(source, n)
				nodes = append(nodes, map[string]any{"path": path, "kind": kind, "start": offset(span.Pos()), "end": offset(span.End()), "number": number, "units": units})
				if kind == "Object" {
					for i, p := range n.AsObjectLiteralExpression().Properties.Nodes {
						if p.Kind != ast.KindPropertyAssignment {
							unsupported = fmt.Sprintf("UNMAPPED_REFERENCE_PROPERTY:%v at %s/p%d", p.Kind, path, i)
							continue
						}
						walk(p.Name(), path+"/p"+strconv.Itoa(i)+"/name")
						walk(p.AsPropertyAssignment().Initializer, path+"/p"+strconv.Itoa(i)+"/value")
					}
				} else if kind == "Array" {
					for i, v := range n.Elements() {
						walk(v, path+"/e"+strconv.Itoa(i))
					}
				}
			}
			if len(sf.Statements.Nodes) > 0 {
				walk(sf.Statements.Nodes[0].Expression(), "$")
			}
			for _, d := range sf.Diagnostics() {
				ds = append(ds, map[string]any{"code": "TS" + strconv.Itoa(int(d.Code())), "start": offset(d.Pos()), "length": offset(d.End()) - offset(d.Pos()), "message": d.Localize(locale.Default)})
			}
		}()
	}
	b, e = json.MarshalIndent(out, "", "  ")
	if e != nil {
		t.Fatal(e)
	}
	if e = os.WriteFile(os.Getenv("TSCS_STRING_OUTPUT"), b, 0600); e != nil {
		t.Fatal(e)
	}
}

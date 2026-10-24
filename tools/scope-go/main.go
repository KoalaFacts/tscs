// Command scope-go inventories Go source syntax without compiling or executing it.
//
// Each stdin line is {"path":"relative/file.go","source":"Go source"}. Each stdout
// line is its corresponding inventory. Source parse errors are data, while invalid
// JSONL input is a fatal command error. Findings are syntactic, not runtime claims:
// selector receivers are not type-resolved, conditions are not evaluated, and
// examples without Output comments are still inventoried. signatureValid checks
// the shape accepted by Go's test loader, not import/type correctness. TestMain
// with a *T parameter is an ordinary test, matching Go's loader behavior.
package main

import (
	"bufio"
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"go/ast"
	"go/build/constraint"
	"go/format"
	"go/parser"
	"go/token"
	"io"
	"os"
	"strconv"
	"strings"
	"unicode"
	"unicode/utf8"
)

type inputRecord struct {
	Path   *string `json:"path"`
	Source *string `json:"source"`
}

type fileInventory struct {
	Path             string            `json:"path"`
	Package          string            `json:"package"`
	Declarations     []declaration     `json:"declarations"`
	SupportFunctions []supportFunction `json:"supportFunctions"`
	BuildConstraints []string          `json:"buildConstraints"`
	StringLists      []stringList      `json:"stringLists"`
	ParseError       *string           `json:"parseError"`
}

type declaration struct {
	Name           string     `json:"name"`
	Kind           string     `json:"kind"`
	Line           int        `json:"line"`
	EndLine        int        `json:"endLine"`
	SignatureValid bool       `json:"signatureValid"`
	RunCalls       []runCall  `json:"runCalls"`
	SkipCalls      []skipCall `json:"skipCalls"`
	HasLoop        bool       `json:"hasLoop"`
}

type supportFunction struct {
	Name string `json:"name"`
	Line int    `json:"line"`
}
type runCall struct {
	Line        int     `json:"line"`
	Expression  string  `json:"expression"`
	LiteralName *string `json:"literalName"`
}
type skipCall struct {
	Line       int    `json:"line"`
	Method     string `json:"method"`
	Expression string `json:"expression"`
}
type stringList struct {
	Name     string   `json:"name"`
	Line     int      `json:"line"`
	Values   []string `json:"values"`
	Complete bool     `json:"complete"`
}

func main() {
	if err := run(os.Stdin, os.Stdout); err != nil {
		fmt.Fprintln(os.Stderr, "scope-go:", err)
		os.Exit(1)
	}
}

func run(in io.Reader, out io.Writer) error {
	reader := bufio.NewReader(in)
	encoder := json.NewEncoder(out)
	encoder.SetEscapeHTML(false)
	seenPaths := make(map[string]bool)
	for lineNumber := 1; ; lineNumber++ {
		line, readErr := reader.ReadBytes('\n')
		if readErr != nil && !errors.Is(readErr, io.EOF) {
			return fmt.Errorf("input line %d: %w", lineNumber, readErr)
		}
		if len(line) == 0 && errors.Is(readErr, io.EOF) {
			return nil
		}
		var input inputRecord
		if err := json.Unmarshal(line, &input); err != nil {
			return fmt.Errorf("input line %d: %w", lineNumber, err)
		}
		if input.Path == nil || strings.TrimSpace(*input.Path) == "" {
			return fmt.Errorf("input line %d: path must be a nonempty string", lineNumber)
		}
		if input.Source == nil {
			return fmt.Errorf("input line %d: source must be a string", lineNumber)
		}
		if seenPaths[*input.Path] {
			return fmt.Errorf("input line %d: duplicate path %q", lineNumber, *input.Path)
		}
		seenPaths[*input.Path] = true
		if err := encoder.Encode(inventory(*input.Path, *input.Source)); err != nil {
			return fmt.Errorf("output line %d: %w", lineNumber, err)
		}
		if errors.Is(readErr, io.EOF) {
			return nil
		}
	}
}

func inventory(path, source string) fileInventory {
	result := fileInventory{Path: path, Declarations: []declaration{}, SupportFunctions: []supportFunction{}, BuildConstraints: []string{}, StringLists: []stringList{}}
	fset := token.NewFileSet()
	file, err := parser.ParseFile(fset, path, source, parser.ParseComments|parser.AllErrors|parser.SkipObjectResolution)
	if err != nil {
		message := err.Error()
		result.ParseError = &message
	}
	if file == nil {
		return result
	}
	if file.Name != nil {
		result.Package = file.Name.Name
	}
	physicalLine := func(pos token.Pos) int { return fset.PositionFor(pos, false).Line }
	for _, group := range file.Comments {
		for _, comment := range group.List {
			if comment.Pos() < file.Package && (constraint.IsGoBuild(comment.Text) || constraint.IsPlusBuild(comment.Text)) {
				result.BuildConstraints = append(result.BuildConstraints, comment.Text)
			}
		}
	}
	for _, node := range file.Decls {
		if group, ok := node.(*ast.GenDecl); ok && (group.Tok == token.VAR || group.Tok == token.CONST) {
			result.StringLists = append(result.StringLists, collectStringLists(group, physicalLine)...)
		}
		fn, ok := node.(*ast.FuncDecl)
		if !ok || fn.Name == nil {
			continue
		}
		kind, arg := functionKind(fn)
		if fn.Recv != nil || kind == "" {
			name := fn.Name.Name
			if fn.Recv != nil && len(fn.Recv.List) > 0 {
				name = "(" + expression(fset, fn.Recv.List[0].Type) + ")." + name
			}
			result.SupportFunctions = append(result.SupportFunctions, supportFunction{Name: name, Line: physicalLine(fn.Pos())})
			continue
		}
		d := declaration{Name: fn.Name.Name, Kind: kind, Line: physicalLine(fn.Pos()), EndLine: physicalLine(fn.End() - 1), SignatureValid: validSignature(fn, arg), RunCalls: []runCall{}, SkipCalls: []skipCall{}}
		if fn.Body != nil {
			ast.Inspect(fn.Body, func(node ast.Node) bool {
				switch n := node.(type) {
				case *ast.ForStmt, *ast.RangeStmt:
					d.HasLoop = true
				case *ast.CallExpr:
					selector, ok := unparen(n.Fun).(*ast.SelectorExpr)
					if !ok {
						break
					}
					switch selector.Sel.Name {
					case "Run":
						var literal *string
						if len(n.Args) > 0 {
							if value, ok := stringLiteral(n.Args[0]); ok {
								literal = &value
							}
						}
						d.RunCalls = append(d.RunCalls, runCall{Line: physicalLine(n.Pos()), Expression: expression(fset, n), LiteralName: literal})
					case "Skip", "Skipf", "SkipNow":
						d.SkipCalls = append(d.SkipCalls, skipCall{Line: physicalLine(n.Pos()), Method: selector.Sel.Name, Expression: expression(fset, n)})
					}
				}
				return true
			})
		}
		result.Declarations = append(result.Declarations, d)
	}
	return result
}

func functionKind(fn *ast.FuncDecl) (kind, arg string) {
	name := fn.Name.Name
	if name == "TestMain" {
		if validSignature(fn, "T") {
			return "test", "T"
		}
		return "testMain", "M"
	}
	for _, candidate := range []struct{ prefix, kind, arg string }{{"Test", "test", "T"}, {"Benchmark", "benchmark", "B"}, {"Fuzz", "fuzz", "F"}, {"Example", "example", ""}} {
		if !strings.HasPrefix(name, candidate.prefix) {
			continue
		}
		suffix := strings.TrimPrefix(name, candidate.prefix)
		first, _ := utf8.DecodeRuneInString(suffix)
		if suffix == "" || !unicode.IsLower(first) {
			return candidate.kind, candidate.arg
		}
	}
	return "", ""
}

func validSignature(fn *ast.FuncDecl, arg string) bool {
	typ := fn.Type
	if typ == nil || typ.TypeParams.NumFields() > 0 || typ.Results.NumFields() > 0 {
		return false
	}
	if arg == "" {
		return typ.Params.NumFields() == 0 && fn.Body != nil
	}
	if typ.Params == nil || len(typ.Params.List) != 1 || len(typ.Params.List[0].Names) > 1 {
		return false
	}
	ptr, ok := typ.Params.List[0].Type.(*ast.StarExpr)
	if !ok {
		return false
	}
	switch typ := ptr.X.(type) {
	case *ast.Ident:
		return typ.Name == arg
	case *ast.SelectorExpr:
		return typ.Sel.Name == arg
	}
	return false
}

func expression(fset *token.FileSet, node ast.Node) string {
	var b bytes.Buffer
	// All callers pass parser-produced expression nodes; bytes.Buffer cannot fail.
	if err := format.Node(&b, fset, node); err != nil {
		return "<unprintable expression>"
	}
	return b.String()
}

func unparen(expr ast.Expr) ast.Expr {
	for {
		p, ok := expr.(*ast.ParenExpr)
		if !ok {
			return expr
		}
		expr = p.X
	}
}

func stringLiteral(expr ast.Expr) (string, bool) {
	literal, ok := unparen(expr).(*ast.BasicLit)
	if !ok || literal.Kind != token.STRING {
		return "", false
	}
	value, err := strconv.Unquote(literal.Value)
	return value, err == nil
}

func collectStringLists(group *ast.GenDecl, line func(token.Pos) int) []stringList {
	var lists []stringList
	for _, spec := range group.Specs {
		values, ok := spec.(*ast.ValueSpec)
		if !ok {
			continue
		}
		for i, value := range values.Values {
			if i >= len(values.Names) {
				break
			}
			literal, ok := unparen(value).(*ast.CompositeLit)
			if !ok {
				continue
			}
			array, ok := literal.Type.(*ast.ArrayType)
			if !ok || array.Len != nil {
				continue
			}
			element, ok := array.Elt.(*ast.Ident)
			if !ok || element.Name != "string" {
				continue
			}
			list := stringList{Name: values.Names[i].Name, Line: line(values.Names[i].Pos()), Values: []string{}, Complete: true}
			for _, value := range literal.Elts {
				// Indexed literals may create implicit empty entries. Keep their
				// explicit strings but never claim the flat list is complete.
				if keyed, ok := value.(*ast.KeyValueExpr); ok {
					list.Complete = false
					value = keyed.Value
				}
				if text, ok := stringLiteral(value); ok {
					list.Values = append(list.Values, text)
				} else {
					list.Complete = false
				}
			}
			lists = append(lists, list)
		}
	}
	return lists
}

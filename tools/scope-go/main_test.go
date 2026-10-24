package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"io"
	"reflect"
	"strings"
	"testing"
)

// These tests exercise the serialized inventory boundary, not parser internals.
type inventoryDocument struct {
	Path             string                 `json:"path"`
	Package          string                 `json:"package"`
	Declarations     []inventoryDeclaration `json:"declarations"`
	SupportFunctions []struct {
		Name string `json:"name"`
		Line int    `json:"line"`
	} `json:"supportFunctions"`
	BuildConstraints []string `json:"buildConstraints"`
	StringLists      []struct {
		Name     string   `json:"name"`
		Line     int      `json:"line"`
		Values   []string `json:"values"`
		Complete bool     `json:"complete"`
	} `json:"stringLists"`
	ParseError *string `json:"parseError"`
}

type inventoryDeclaration struct {
	Name           string `json:"name"`
	Kind           string `json:"kind"`
	Line           int    `json:"line"`
	EndLine        int    `json:"endLine"`
	SignatureValid bool   `json:"signatureValid"`
	RunCalls       []struct {
		Line        int     `json:"line"`
		Expression  string  `json:"expression"`
		LiteralName *string `json:"literalName"`
	} `json:"runCalls"`
	SkipCalls []struct {
		Line       int    `json:"line"`
		Method     string `json:"method"`
		Expression string `json:"expression"`
	} `json:"skipCalls"`
	HasLoop bool `json:"hasLoop"`
}

func inventoryInput(path, source string) string {
	b, err := json.Marshal(map[string]string{"path": path, "source": source})
	if err != nil {
		panic(err)
	}
	return string(b) + "\n"
}

func inventoryOne(t *testing.T, source string) inventoryDocument {
	t.Helper()
	var out bytes.Buffer
	if err := run(strings.NewReader(inventoryInput("suite/sample_test.go", source)), &out); err != nil {
		t.Fatalf("inventory failed: %v", err)
	}
	dec := json.NewDecoder(&out)
	var got inventoryDocument
	if err := dec.Decode(&got); err != nil {
		t.Fatalf("decode inventory: %v; output=%q", err, out.String())
	}
	var extra any
	if err := dec.Decode(&extra); !errors.Is(err, io.EOF) {
		t.Fatalf("expected exactly one JSON record; got %v", err)
	}
	if got.Path != "suite/sample_test.go" {
		t.Errorf("path=%q", got.Path)
	}
	return got
}

func TestInventoryFindsRealTopLevelDeclarationsNotMisleadingText(t *testing.T) {
	got := inventoryOne(t, `package suite
import "testing"
// func TestComment(t *testing.T) { t.Skip("not real") }
var fake = "func TestString(t *testing.T) {}"
func TestReal(t *testing.T) {
  // t.Run("comment", nil)
  _ = "t.SkipNow()"
}
func helper() {}
func Testicular() {}
func Testé() {}
func TestÉ(t *testing.T) {}
func Test(t *testing.T) {}
func Test_underscore(t *testing.T) {}
type Suite struct{}
func (s *Suite) TestMethod(t *testing.T) { t.SkipNow() }
`)
	if got.ParseError != nil || got.Package != "suite" {
		t.Fatalf("package=%q parseError=%v", got.Package, got.ParseError)
	}
	var names []string
	for _, d := range got.Declarations {
		names = append(names, d.Name)
		if len(d.RunCalls) != 0 || len(d.SkipCalls) != 0 {
			t.Errorf("misleading text/method leaked into %s", d.Name)
		}
	}
	if want := []string{"TestReal", "TestÉ", "Test", "Test_underscore"}; !reflect.DeepEqual(names, want) {
		t.Fatalf("declarations=%v want=%v", names, want)
	}
	if got.Declarations[0].Line != 5 || got.Declarations[0].EndLine != 8 {
		t.Errorf("wrong real source lines: %+v", got.Declarations[0])
	}
	var support []string
	for _, f := range got.SupportFunctions {
		support = append(support, f.Name)
	}
	if want := []string{"helper", "Testicular", "Testé", "(*Suite).TestMethod"}; !reflect.DeepEqual(support, want) {
		t.Errorf("support=%v want=%v", support, want)
	}
}

func TestInventoryClassifiesKindsAndRejectsInvalidSignatures(t *testing.T) {
	for _, tt := range []struct {
		name, decl, kind string
		valid            bool
	}{
		{"plainTest", `func Test(t *testing.T) {}`, "test", true},
		{"aliasTest", `func TestAlias(t *alias.T) {}`, "test", true},
		{"dotImport", `func TestDot(*T) {}`, "test", true},
		{"noArgs", `func TestNoArgs() {}`, "test", false},
		{"wrongType", `func TestWrong(t *testing.B) {}`, "test", false},
		{"valueType", `func TestValue(t testing.T) {}`, "test", false},
		{"groupedArgs", `func TestTwo(a, b *testing.T) {}`, "test", false},
		{"separateArgs", `func TestTwo(a *testing.T, b *testing.T) {}`, "test", false},
		{"variadic", `func TestVariadic(t ...*testing.T) {}`, "test", false},
		{"result", `func TestResult(t *testing.T) int { return 0 }`, "test", false},
		{"generic", `func TestGeneric[T any](t *testing.T) {}`, "test", false},
		{"benchmark", `func BenchmarkWork(b *testing.B) {}`, "benchmark", true},
		{"badBenchmark", `func BenchmarkWork(t *testing.T) {}`, "benchmark", false},
		{"fuzz", `func FuzzWork(f *testing.F) {}`, "fuzz", true},
		{"badFuzz", `func FuzzWork() {}`, "fuzz", false},
		{"example", `func ExampleWork() {}`, "example", true},
		{"emptyExample", `func Example() {}`, "example", true},
		{"exampleSuffix", `func ExampleWork_suffix() {}`, "example", true},
		{"badExampleArgs", `func ExampleWork(t *testing.T) {}`, "example", false},
		{"badExampleResult", `func ExampleWork() int { return 1 }`, "example", false},
		{"genericExample", `func ExampleWork[T any]() {}`, "example", false},
		{"missingExampleBody", `func ExampleWork()`, "example", false},
		{"testMain", `func TestMain(m *testing.M) {}`, "testMain", true},
		{"ordinaryTestMain", `func TestMain(t *testing.T) {}`, "test", true},
		{"badTestMain", `func TestMain() {}`, "testMain", false},
	} {
		t.Run(tt.name, func(t *testing.T) {
			got := inventoryOne(t, "package suite\n"+tt.decl+"\n")
			if got.ParseError != nil || len(got.Declarations) != 1 {
				t.Fatalf("parseError=%v declarations=%+v", got.ParseError, got.Declarations)
			}
			d := got.Declarations[0]
			if d.Kind != tt.kind || d.SignatureValid != tt.valid {
				t.Errorf("kind=%q valid=%v; want %q %v", d.Kind, d.SignatureValid, tt.kind, tt.valid)
			}
		})
	}
}

func TestInventoryKeepsSyntacticRunSkipCallsAndLoops(t *testing.T) {
	got := inventoryOne(t, `package suite
func TestNested(t *testing.T) {
  t.Run("literal", func(t *testing.T) {
    t.Skip("conditional or nested")
  })
  t.Run(name, body)
  other.Run("not resolved", body)
  (t.Run)(("parenthesized"), body)
  t.Run("prefix" + suffix, body)
  t.Run(`+"`raw\\name`"+`, body)
  t.Skipf("%s", reason)
  t.SkipNow()
  other.Skip("also just syntax")
  Run("bare is not a selector", body)
  for _, name := range names { _ = name }
}
func TestLoop(t *testing.T) { for i:=0; i<2; i++ {} }
func TestNoLoop(t *testing.T) { _ = "for {}" }
`)
	if got.ParseError != nil || len(got.Declarations) != 3 {
		t.Fatalf("unexpected inventory: %+v", got)
	}
	d := got.Declarations[0]
	if !d.HasLoop || !got.Declarations[1].HasLoop || got.Declarations[2].HasLoop {
		t.Fatal("loop flags must derive from syntax")
	}
	if len(d.RunCalls) != 6 {
		t.Fatalf("Run calls=%+v", d.RunCalls)
	}
	wantNames := []*string{ptr("literal"), nil, ptr("not resolved"), ptr("parenthesized"), nil, ptr("raw\\name")}
	for i, c := range d.RunCalls {
		if !reflect.DeepEqual(c.LiteralName, wantNames[i]) {
			t.Errorf("run %d literal=%v want=%v", i, c.LiteralName, wantNames[i])
		}
	}
	if d.RunCalls[0].Line != 3 || !strings.HasPrefix(d.RunCalls[0].Expression, `t.Run("literal",`) || d.RunCalls[2].Expression != `other.Run("not resolved", body)` {
		t.Errorf("Run expressions/lines=%+v", d.RunCalls)
	}
	if len(d.SkipCalls) != 4 {
		t.Fatalf("Skip calls=%+v", d.SkipCalls)
	}
	for i, want := range []string{"Skip", "Skipf", "SkipNow", "Skip"} {
		if d.SkipCalls[i].Method != want {
			t.Errorf("skip %d method=%q want=%q", i, d.SkipCalls[i].Method, want)
		}
	}
	if d.SkipCalls[0].Line != 4 || d.SkipCalls[3].Expression != `other.Skip("also just syntax")` {
		t.Errorf("Skip expressions/lines=%+v", d.SkipCalls)
	}
}

func ptr(s string) *string { return &s }

func TestInventoryPreservesLeadingBuildConstraintsAndPhysicalLines(t *testing.T) {
	got := inventoryOne(t, `//go:build linux && amd64
// +build linux,amd64

package suite
//line invented.go:900
func TestPhysical(t *testing.T) {}
//go:build ignored_late
var s = "//go:build not_real"
`)
	if want := []string{"//go:build linux && amd64", "// +build linux,amd64"}; !reflect.DeepEqual(got.BuildConstraints, want) {
		t.Errorf("build constraints=%v want=%v", got.BuildConstraints, want)
	}
	if len(got.Declarations) != 1 || got.Declarations[0].Line != 6 || got.Declarations[0].EndLine != 6 {
		t.Errorf("physical declaration lines=%+v", got.Declarations)
	}
}

func TestInventoryReportsParseErrorsAndKeepsRecoverableDeclarations(t *testing.T) {
	got := inventoryOne(t, "package suite\nfunc TestGood(t *testing.T) {}\nfunc TestBroken(\n")
	if got.ParseError == nil || *got.ParseError == "" {
		t.Fatal("invalid syntax must report parseError")
	}
	if len(got.Declarations) < 1 || got.Declarations[0].Name != "TestGood" {
		t.Errorf("lost recoverable declarations: %+v", got.Declarations)
	}
	got = inventoryOne(t, "")
	if got.ParseError == nil {
		t.Fatal("empty Go source must report parseError")
	}
}

func TestInventoryEmitsRequiredEmptyArraysAndNull(t *testing.T) {
	var out bytes.Buffer
	if err := run(strings.NewReader(inventoryInput("empty.go", "package empty\n")), &out); err != nil {
		t.Fatal(err)
	}
	var got map[string]json.RawMessage
	if err := json.Unmarshal(out.Bytes(), &got); err != nil {
		t.Fatal(err)
	}
	for _, key := range []string{"declarations", "supportFunctions", "buildConstraints", "stringLists"} {
		if string(got[key]) != "[]" {
			t.Errorf("%s=%s want []", key, got[key])
		}
	}
	if string(got["parseError"]) != "null" {
		t.Errorf("parseError=%s want null", got["parseError"])
	}
}

func TestInventoryFindsTopLevelStringListLiteralsOnly(t *testing.T) {
	got := inventoryOne(t, `package suite
// var skippedTests = []string{"not real"}
var skippedTests = []string{"one", `+"`two`"+`, dynamic, "four"}
var complete = []string{"a", "b"}
var empty = []string{}
var number = []int{1}
var left, right = []string{"left"}, []string{"right"}
var indexed = []string{2: "index"}
var parenthesized = ([]string{"paren"})
func helper() { local := []string{"local"}; _ = local }
`)
	if got.ParseError != nil {
		t.Fatalf("parse error=%v", got.ParseError)
	}
	if len(got.StringLists) != 7 {
		t.Fatalf("string lists=%+v", got.StringLists)
	}
	for i, tt := range []struct {
		name     string
		line     int
		values   []string
		complete bool
	}{
		{"skippedTests", 3, []string{"one", "two", "four"}, false},
		{"complete", 4, []string{"a", "b"}, true},
		{"empty", 5, []string{}, true},
		{"left", 7, []string{"left"}, true},
		{"right", 7, []string{"right"}, true},
		{"indexed", 8, []string{"index"}, false},
		{"parenthesized", 9, []string{"paren"}, true},
	} {
		g := got.StringLists[i]
		if g.Name != tt.name || g.Line != tt.line || g.Complete != tt.complete || !reflect.DeepEqual(g.Values, tt.values) {
			t.Errorf("list %d=%+v want %+v", i, g, tt)
		}
	}
}

func TestInventoryJSONLRejectsMalformedMissingAndDuplicateInputs(t *testing.T) {
	for _, tt := range []struct{ name, input string }{
		{"malformed", "{bad}\n"},
		{"emptyPath", `{"path":"","source":"package p"}` + "\n"},
		{"whitespacePath", `{"path":"  ","source":"package p"}` + "\n"},
		{"missingPath", `{"source":"package p"}` + "\n"},
		{"missingSource", `{"path":"p.go"}` + "\n"},
		{"wrongSourceType", `{"path":"p.go","source":3}` + "\n"},
		{"nullRecord", "null\n"},
		{"nullSource", `{"path":"p.go","source":null}` + "\n"},
		{"twoRecordsOneLine", `{"path":"a","source":"package p"} {"path":"b","source":"package p"}` + "\n"},
		{"blankLine", "\n"},
		{"duplicatePath", inventoryInput("a.go", "package p") + inventoryInput("a.go", "package p")},
	} {
		t.Run(tt.name, func(t *testing.T) {
			var out bytes.Buffer
			if err := run(strings.NewReader(tt.input), &out); err == nil {
				t.Fatalf("expected input error; output=%q", out.String())
			}
		})
	}
}

func TestInventoryJSONLPreservesOrderAndAcceptsLongLinesWithoutFinalNewline(t *testing.T) {
	input := inventoryInput("b.go", "package b\n//"+strings.Repeat("x", 100000)+"\n") + strings.TrimSuffix(inventoryInput("a.go", "package a"), "\n")
	var out bytes.Buffer
	if err := run(strings.NewReader(input), &out); err != nil {
		t.Fatal(err)
	}
	dec := json.NewDecoder(&out)
	for _, want := range []string{"b.go", "a.go"} {
		var got inventoryDocument
		if err := dec.Decode(&got); err != nil {
			t.Fatal(err)
		}
		if got.Path != want {
			t.Errorf("path=%q want=%q", got.Path, want)
		}
	}
	var extra any
	if err := dec.Decode(&extra); !errors.Is(err, io.EOF) {
		t.Errorf("extra result: %v", err)
	}
}

func TestInventoryPropagatesReadAndWriteErrors(t *testing.T) {
	if err := run(errorReader{}, io.Discard); err == nil {
		t.Fatal("reader error swallowed")
	}
	if err := run(strings.NewReader(inventoryInput("p.go", "package p")), errorWriter{}); err == nil {
		t.Fatal("writer error swallowed")
	}
}

type errorReader struct{}

func (errorReader) Read([]byte) (int, error) { return 0, errors.New("fixture read failure") }

type errorWriter struct{}

func (errorWriter) Write([]byte) (int, error) { return 0, errors.New("fixture write failure") }

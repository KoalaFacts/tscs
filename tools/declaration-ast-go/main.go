// Command declaration-ast-go parses declaration files with the pinned reference
// parser and prints a statement inventory for comparison with tscs's
// DeclarationParser (roadmap A2.2c). Each stdin line is
// {"fileName":"lib.es5.d.ts","text":"..."}; each stdout line is
// {"fileName":...,"diagnostics":n,"statements":[{"kind":...,"names":[...],"members":n}]}.
// It is copied into the pinned checkout's tsc/cmd the way tools/oracle is and
// never modifies tracked upstream sources.
package main

import (
	"bufio"
	"encoding/json"
	"os"

	"github.com/microsoft/TypeScript/tsc/internal/ast"
	"github.com/microsoft/TypeScript/tsc/internal/core"
	"github.com/microsoft/TypeScript/tsc/internal/parser"
	"github.com/microsoft/TypeScript/tsc/internal/tspath"
)

type input struct {
	FileName string `json:"fileName"`
	Text     string `json:"text"`
}

type statement struct {
	Kind    string   `json:"kind"`
	Names   []string `json:"names"`
	Members int      `json:"members"`
}

type output struct {
	FileName    string      `json:"fileName"`
	Diagnostics int         `json:"diagnostics"`
	Statements  []statement `json:"statements"`
}

func inventory(node *ast.Node) statement {
	s := statement{Kind: node.Kind.String(), Names: []string{}}
	switch node.Kind {
	case ast.KindVariableStatement:
		for _, d := range node.AsVariableStatement().DeclarationList.AsVariableDeclarationList().Declarations.Nodes {
			s.Names = append(s.Names, ast.GetDeclarationName(d))
		}
	case ast.KindInterfaceDeclaration, ast.KindClassDeclaration:
		s.Names = append(s.Names, ast.GetDeclarationName(node))
		s.Members = len(node.Members())
	case ast.KindModuleDeclaration:
		s.Names = append(s.Names, node.Name().Text())
		if body := node.Body(); body != nil && body.Kind == ast.KindModuleBlock {
			s.Members = len(body.Statements())
		}
	default:
		if name := ast.GetDeclarationName(node); name != "" {
			s.Names = append(s.Names, name)
		}
	}
	return s
}

func main() {
	scanner := bufio.NewScanner(os.Stdin)
	scanner.Buffer(make([]byte, 64<<20), 64<<20)
	writer := json.NewEncoder(os.Stdout)
	for scanner.Scan() {
		var in input
		if err := json.Unmarshal(scanner.Bytes(), &in); err != nil {
			os.Exit(2)
		}
		file := parser.ParseSourceFile(ast.SourceFileParseOptions{FileName: tspath.RootedFilePathFromAbsolute("/" + in.FileName)}, in.Text, core.ScriptKindTS)
		out := output{FileName: in.FileName, Diagnostics: len(file.Diagnostics()), Statements: []statement{}}
		for _, node := range file.Statements.Nodes {
			out.Statements = append(out.Statements, inventory(node))
		}
		if err := writer.Encode(out); err != nil {
			os.Exit(3)
		}
	}
	if scanner.Err() != nil {
		os.Exit(2)
	}
}

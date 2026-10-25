package tsoptions

import (
	"encoding/base64"
	"encoding/json"
	"github.com/microsoft/TypeScript/tsc/internal/ast"
	"github.com/microsoft/TypeScript/tsc/internal/core"
	"github.com/microsoft/TypeScript/tsc/internal/locale"
	"github.com/microsoft/TypeScript/tsc/internal/parser"
	"github.com/microsoft/TypeScript/tsc/internal/tspath"
	"os"
	"strconv"
	"testing"
	"unicode/utf16"
)

// Real parser + private convertToObject, no option notifier or filesystem host.
// configProjection is shared with the existing synthetic oracle overlay.
func TestTscsSourceConfigValuesOracle(t *testing.T) {
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
	out := []any{}
	for _, r := range rows {
		bytes, e := base64.StdEncoding.DecodeString(r.SourceBytes)
		if e != nil {
			t.Fatal(e)
		}
		name, e := base64.StdEncoding.DecodeString(r.FileName)
		if e != nil {
			t.Fatal(e)
		}
		source := string(bytes)
		sf := parser.ParseSourceFile(ast.SourceFileParseOptions{FileName: tspath.RootedFilePathFromNormalized(string(name))}, source, core.ScriptKindJSON)
		project := func(ds []*ast.Diagnostic) []any {
			out := []any{}
			for _, d := range ds {
				out = append(out, map[string]any{"code": "TS" + strconv.Itoa(int(d.Code())), "message": d.Localize(locale.Default), "start": len(utf16.Encode([]rune(source[:d.Pos()]))), "length": len(utf16.Encode([]rune(source[d.Pos():d.End()])))})
			}
			return out
		}
		value, errors := convertToObject(sf)
		out = append(out, map[string]any{"id": r.ID, "sourceBytes": r.SourceBytes, "fileName": r.FileName, "value": configProjection(value), "parserDiagnostics": project(sf.Diagnostics()), "conversionDiagnostics": project(errors)})
	}
	b, e = json.MarshalIndent(out, "", "  ")
	if e != nil {
		t.Fatal(e)
	}
	if e = os.WriteFile(os.Getenv("TSCS_STRING_OUTPUT"), b, 0600); e != nil {
		t.Fatal(e)
	}
}

import test from 'node:test';
import assert from 'node:assert/strict';
import {inspectFixture,stableId} from './ts7-scope.ts';
import {expandTranspileConfigurations,inventoryConfigurations,transpileConfigurationPolicy} from './ts7-configurations.ts';

const directives=rows=>rows.map(([name,value])=>({name,value}));
const expand=rows=>expandTranspileConfigurations(directives(rows));
const expanded=configurations=>({status:'EXPANDED',configurations,blockers:[]});
const fixture=(source='',domain='fixture:transpile')=>inspectFixture({id:stableId('a'.repeat(40),'asset','test.ts'),path:'test.ts',domain},Buffer.isBuffer(source)?source:Buffer.from(source));
const assets=()=>transpileConfigurationPolicy.sources.map(x=>({...x,id:`asset:${x.path}`}));

test('no settings gives the transpile fallback configuration, not zero tests',()=>{
  assert.deepEqual(expand([]),expanded([{name:'',settings:{}}]));
});

test('non-varying commas remain scalar and singleton variations have no suffix',()=>{
  assert.deepEqual(expand([['target','es5, esnext'],['lib','es5,dom'],['sourcemap','TRUE']]),expanded([
    {name:'',settings:{lib:'es5,dom',sourcemap:'TRUE',target:'es5, esnext'}},
  ]));
});

test('settings use last declaration, Go whitespace, and remove only one final semicolon',()=>{
  assert.deepEqual(expand([['sourcemap','false'],['sourcemap','\u0085TRUE;\u0085'],['lib','\uFEFFes5;;'],['__proto__','retained']]),expanded([
    {name:'',settings:JSON.parse('{"__proto__":"retained","lib":"﻿es5;","sourcemap":"TRUE"}')},
  ]));
});

test('boolean products get canonical sorted names while preserving first spelling',()=>{
  assert.deepEqual(expand([['sourcemap','TRUE, true, false'],['declarationmap','false, true'],['target','esnext']]),expanded([
    {name:'declarationmap=false,sourcemap=false',settings:{declarationmap:'false',sourcemap:'false',target:'esnext'}},
    {name:'declarationmap=false,sourcemap=true',settings:{declarationmap:'false',sourcemap:'TRUE',target:'esnext'}},
    {name:'declarationmap=true,sourcemap=false',settings:{declarationmap:'true',sourcemap:'false',target:'esnext'}},
    {name:'declarationmap=true,sourcemap=true',settings:{declarationmap:'true',sourcemap:'TRUE',target:'esnext'}},
  ]));
});

test('wildcards add missing boolean values and exclusions ignore unknown values',()=>{
  assert.deepEqual(expand([['sourcemap','FALSE, *, !obsolete, -TRUE']]),expanded([{name:'',settings:{sourcemap:'FALSE'}}]));
  assert.deepEqual(expand([['sourcemap','*, !false']]),expanded([{name:'',settings:{sourcemap:'true'}}]));
});

test('empty token lists disappear except when transpile fallback restores raw settings',()=>{
  assert.deepEqual(expand([['sourcemap',' , '],['lib','es5']]),expanded([{name:'',settings:{lib:'es5'}}]));
  assert.deepEqual(expand([['sourcemap',' , ']]),expanded([{name:'',settings:{sourcemap:','}}]));
  assert.deepEqual(expand([['sourcemap','']]),expanded([{name:'',settings:{sourcemap:''}}]));
});

test('invalid included values and emptied sets are blockers with no partial configurations',()=>{
  for(const value of ['maybe','true,maybe','*, -true, !false','-true','!obsolete','true;;','\uFEFFtrue']) {
    const result=expand([['sourcemap',value],['declarationmap','*']]);
    assert.equal(result.status,'BLOCKED',value);
    assert.equal(result.configurations,null);
    assert.equal(result.blockers.length,1);
    assert.match(result.blockers[0],/UNKNOWN_BOOLEAN_VALUE|EMPTY_VARIATION_SET/);
  }
});

test('all three boolean axes have eight distinct names, not repeated aliases',()=>{
  const result=expand([['sourcemap','*,TRUE'],['inlinesourcemap','*'],['declarationmap','*']]);
  assert.equal(result.configurations.length,8);
  assert.equal(new Set(result.configurations.map(x=>x.name)).size,8);
  assert.equal(result.configurations[0].name,'declarationmap=false,inlinesourcemap=false,sourcemap=false');
  assert.equal(result.configurations.at(-1).name,'declarationmap=true,inlinesourcemap=true,sourcemap=true');
});

test('inventory expands only source-verified UTF8 transpile configurations with stable IDs and NOT_RUN',()=>{
  const fixtures=[fixture('// @sourceMap: true, false\n'),fixture('// @target: esnext','fixture:compiler'),fixture(Buffer.from([255,254,47,0,47,0]))];
  const rows=inventoryConfigurations(fixtures,transpileConfigurationPolicy.upstreamCommit,assets());
  assert.equal(rows.length,2);
  assert.equal(new Set(rows.map(x=>x.id)).size,2);
  assert.deepEqual(rows.map(x=>x.name),['sourcemap=false','sourcemap=true']);
  assert.equal(fixtures[0].configurationExpansion.status,'EXPANDED');
  assert.equal(fixtures[0].configurationExpansion.configurations,2);
  assert.equal(fixtures[0].expansion.concreteCases,null);
  assert.equal(fixtures[0].expansion.status,'UNEXPANDED');
  assert.equal(fixtures[0].progress.acceptance,'NOT_RUN');
  for(const row of rows) { assert.equal(row.fixtureId,fixtures[0].id);assert.equal(row.progress.acceptance,'NOT_RUN'); }
  assert.equal(fixtures[1].configurationExpansion.status,'UNEXPANDED');
  assert.equal(fixtures[2].configurationExpansion.status,'UNEXPANDED');
  assert.deepEqual(Buffer.from(fixtures[2].rawBytesBase64,'base64'),Buffer.from([255,254,47,0,47,0]));
  assert.deepEqual(inventoryConfigurations([fixture('// @sourceMap: true, false\n')],transpileConfigurationPolicy.upstreamCommit,assets()),rows);
});

test('unverified pin, missing policy bytes or changed policy never gain expansion credit',()=>{
  for(const [pin,sources] of [
    ['b'.repeat(40),assets()],
    [transpileConfigurationPolicy.upstreamCommit,assets().slice(1)],
    [transpileConfigurationPolicy.upstreamCommit,assets().map((x,i)=>i?x:{...x,sha256:'0'.repeat(64)})],
  ]) {
    const f=fixture('// @sourceMap: true, false\n');
    assert.deepEqual(inventoryConfigurations([f],pin,sources),[]);
    assert.equal(f.configurationExpansion.status,'UNEXPANDED');
    assert.equal(f.configurationExpansion.configurations,null);
    assert.ok(f.configurationExpansion.blockers.includes('CONFIGURATION_POLICY_UNVERIFIED'));
  }
});

test('full-source extraction retains canonical newline consumption and trailing-space settings',()=>{
  const f=fixture('// @empty:\n// @sourceMap: true,false\n// @target: es6 ; \n// @SourceMap: false\n// @sourceMap: TRUE,true\n// @declaration: true,false\n');
  assert.deepEqual(expandTranspileConfigurations(f.directives),expanded([{name:'',settings:{
    declaration:'true,false',empty:'// @sourceMap: true,false',sourcemap:'TRUE',target:'es6 ',
  }}]));
  assert.deepEqual(expand([['sourcemap','true,! true']]),expanded([{name:'',settings:{sourcemap:'true'}}]));
});

test('a blocked fixture retains its evidence without removing successful neighboring records',()=>{
  const good=fixture('// @sourceMap: *\n'), bad=fixture('// @sourceMap: true,garbage,!garbage\n');
  bad.id='fixture:distinct';bad.path='bad.ts';
  const rows=inventoryConfigurations([good,bad],transpileConfigurationPolicy.upstreamCommit,assets());
  assert.equal(rows.length,2);
  assert.equal(bad.configurationExpansion.status,'BLOCKED');
  assert.equal(bad.configurationExpansion.configurations,null);
  assert.equal(bad.progress.acceptance,'NOT_RUN');
  assert.equal(bad.directives[0].value,'true,garbage,!garbage');
  assert.ok(bad.expansion.blockers.includes('CANONICAL_CONFIGURATION_EXPANSION'));
});

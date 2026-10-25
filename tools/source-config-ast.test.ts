import {test} from 'node:test';import assert from 'node:assert/strict';
import {reconcileConfigAst,validateConfigAst,verifyConfigAstArtifacts} from './source-config-ast.ts';
const q={id:'profile',input:'',fileName:'YS50cw==',implicit:false,failAt:0,returnNil:false,profile:'config-ast'};
const selection=(has=true)=>({units:[],configFile:has?{name:'dHNjb25maWcuanNvbg==',content:'e30='}:null,configIndex:has?0:-1,runExternalCode:false,panic:null});
const context=(has=true)=>({directory:"Ly5zcmM=",configPath:has?"Ly5zcmMvdHNjb25maWcuanNvbg==":null});
const ast=()=>({nodes:[],diagnostics:[],unsupported:null,panic:null});
const source={sourceId:'fixture',path:'a.ts',rawSha256:'source',upstreamSelection:{status:'UPSTREAM_SKIPPED'}};
function run(p,a=ast(),s=selection()){p={context:context(s.configFile!==null),...p};return reconcileConfigAst([source],[q],[{id:q.id,expected:p}],[{id:q.id,input:q.input,fileName:q.fileName,expected:s,context:context(s.configFile!==null)}],s.configFile?[{id:q.id,sourceBytes:s.configFile.content,fileName:context().configPath,...a}]:[]);}
test('no config is NOT_APPLICABLE and never compiler acceptance',()=>{
 const s=selection(false),r=run({selection:s,ast:null,unsupported:null},ast(),s)[0];assert.equal(r.status,'NOT_APPLICABLE');assert.equal(r.compiler,'NOT_RUN');assert.equal(r.acceptance,'NOT_RUN');
});
test('syntax errors and unsupported grammar cannot earn clean MATCH',()=>{
 const a=ast();a.diagnostics=[{code:'TS1327',start:0,length:1,message:'quote'}];
 assert.equal(run({selection:selection(),ast:a,unsupported:null},a)[0].status,'MATCH_DIAGNOSTICS');
 assert.equal(run({selection:selection(),ast:null,unsupported:'INVALID_UTF8_CONFIG'})[0].status,'UNSUPPORTED');
 const unsupported={...ast(),unsupported:'UNMAPPED_REFERENCE_AST'};assert.equal(run({selection:selection(),ast:ast(),unsupported:null},unsupported)[0].status,'UNSUPPORTED');
});
test('AST differences and source/selection bindings remain visible',()=>{
 const a=ast();a.nodes=[{path:'$',kind:'Null',start:0,end:4,number:'',units:[]}];
 assert.equal(run({selection:selection(),ast:a,unsupported:null})[0].status,'MISMATCH');
 assert.throws(()=>run({selection:selection(false),context:context(false),ast:null,unsupported:null}),/selection/);
 const r={id:q.id,sourceBytes:'W10=',fileName:context().configPath,...ast()};
 assert.throws(()=>reconcileConfigAst([source],[q],[{id:q.id,expected:{selection:selection(),ast:ast(),unsupported:null,context:context()}}],[{id:q.id,input:q.input,fileName:q.fileName,expected:selection(),context:context()}],[r]),/binding/);
});
test('strict schema rejects malformed bytes, ranges and extra fields',()=>{
 validateConfigAst({selection:selection(),context:context(),ast:ast(),unsupported:null});
 for(const p of [{selection:{...selection(),configFile:{name:'!',content:''}},ast:null,unsupported:null},{selection:selection(),ast:{...ast(),nodes:[{path:'$',kind:'String',start:2,end:1,number:'',units:[]}]},unsupported:null},{selection:selection(),ast:ast(),unsupported:null,extra:1}])assert.throws(()=>validateConfigAst(p));
});
test('malformed envelope and duplicate identities cannot produce accepted evidence',()=>{
 const p={selection:selection(),context:context(),ast:ast(),unsupported:null},s={id:q.id,input:q.input,fileName:q.fileName,expected:selection(),context:context()},r={id:q.id,sourceBytes:selection().configFile.content,fileName:context().configPath,...ast()};
 assert.throws(()=>reconcileConfigAst([source],[q],[{id:q.id,expected:p,extra:true}],[s],[r]),/envelope/);
 assert.throws(()=>reconcileConfigAst([source],[q],[{id:q.id,expected:p},{id:q.id,expected:p}],[s],[r]),/identities/);
});

// Catch evidence whose streamed output was truncated after the tool returned.
import {mkdtempSync,writeFileSync,rmSync} from 'node:fs';import {tmpdir} from 'node:os';import {join} from 'node:path';import {sha256} from './ts7-scope.ts';
test('post-completion truncation cannot reuse a previously valid summary',()=>{
 const dir=mkdtempSync(join(tmpdir(),'tscs-artifact-check-'));
 try{
  const bytes=Buffer.from('{"id":"a"}\n{"id":"b"}\n');writeFileSync(join(dir,'actual-000.jsonl'),bytes);
  writeFileSync(join(dir,'summary.json'),JSON.stringify({counts:{sources:2},artifacts:[{path:'actual-000.jsonl',bytes:bytes.length,sha256:sha256(bytes)}]}));
  assert.equal(verifyConfigAstArtifacts(dir).counts.sources,2);
  writeFileSync(join(dir,'actual-000.jsonl'),'{"id":"a"}\n');assert.throws(()=>verifyConfigAstArtifacts(dir),/artifact/i);
 }finally{rmSync(dir,{recursive:true,force:true});}
});

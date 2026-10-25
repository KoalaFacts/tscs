// Strict selected-config syntax evidence. No compiler/effective-option credit.
import {isDeepStrictEqual} from 'node:util';
import {readFileSync} from 'node:fs';import {join} from 'node:path';
import {sha256,stableId} from './ts7-scope.ts';
import {canonicalJson} from './fixture-corpus.ts';
const object=x=>x!==null&&typeof x==='object'&&!Array.isArray(x);
const keys=(x,want)=>object(x)&&Object.keys(x).sort().join(',')===[...want].sort().join(',');
const bytes=x=>typeof x==='string'&&Buffer.from(x,'base64').toString('base64')===x;
const integer=x=>Number.isSafeInteger(x)&&x>=0;
function validateSelection(p){
 if(keys(p,['panic'])&&typeof p.panic==='string')return;
 if(!keys(p,['units','configFile','configIndex','runExternalCode','panic'])||p.panic!==null||typeof p.runExternalCode!=='boolean'||!Array.isArray(p.units)||!p.units.every(u=>keys(u,['name','content'])&&bytes(u.name)&&bytes(u.content))||!(p.configFile===null? p.configIndex===-1:keys(p.configFile,['name','content'])&&bytes(p.configFile.name)&&bytes(p.configFile.content)&&integer(p.configIndex)&&p.configIndex<=p.units.length))throw Error('Malformed selection payload');
}
function validateAst(p){
 if(!keys(p,['nodes','diagnostics','unsupported','panic'])||!(p.unsupported===null||typeof p.unsupported==='string')||!(p.panic===null||typeof p.panic==='string')||!Array.isArray(p.nodes)||!Array.isArray(p.diagnostics)||!p.nodes.every(n=>keys(n,['path','kind','start','end','number','units'])&&typeof n.path==='string'&&/^\$(?:\/(?:p\d+\/(?:name|value)|e\d+))*$/.test(n.path)&&['Object','Array','String','Identifier','Number','True','False','Null'].includes(n.kind)&&integer(n.start)&&integer(n.end)&&n.end>=n.start&&typeof n.number==='string'&&Array.isArray(n.units)&&n.units.every(v=>integer(v)&&v<=65535))||!p.diagnostics.every(d=>keys(d,['code','start','length','message'])&&typeof d.code==='string'&&/^(?:TS|TSCS)\d+$/.test(d.code)&&integer(d.start)&&integer(d.length)&&typeof d.message==='string'))throw Error('Malformed AST payload');
 if(p.nodes.length&&p.nodes[0].path!=='$'||new Set(p.nodes.map(n=>n.path)).size!==p.nodes.length)throw Error('Invalid AST node identities');
}
export function validateConfigAst(p){
 if(!keys(p,['selection','context','ast','unsupported'])||!(p.unsupported===null||typeof p.unsupported==='string'))throw Error('Malformed config AST payload');
 validateSelection(p.selection);
 if(p.ast!==null)validateAst(p.ast);
 const hasConfig=p.selection.panic===null&&p.selection.configFile!==null;
 if(p.selection.panic!==null?p.context!==null:!keys(p.context,['directory','configPath'])||!bytes(p.context.directory)||(hasConfig?!bytes(p.context.configPath):p.context.configPath!==null))throw Error('Malformed config context');
 if(hasConfig?(p.ast===null)===(p.unsupported===null):p.ast!==null||p.unsupported!==null)throw Error('Inconsistent config AST state');
}
function index(rows,label){const m=new Map();for(const r of rows){if(typeof r.id!=='string'||!r.id||m.has(r.id))throw Error('Duplicate '+label+' identities');m.set(r.id,r);}return m;}
export function reconcileConfigAst(sources,requests,actual,selection,reference){
 if(actual.some(row=>!keys(row,['id','expected']))||selection.some(row=>!keys(row,['id','input','fileName','expected','context'])))throw Error('Malformed result envelope');
 const q=index(requests,'request'),a=index(actual,'actual'),s=index(selection,'selection'),r=index(reference,'parser');
 for(const m of [a,s])if(m.size!==q.size||[...m.keys()].some(id=>!q.has(id)))throw Error('Missing/unexpected identities');
 if(sources.length!==requests.length)throw Error('Source identities mismatch');
 let configCount=0;
 const results=requests.map((request,i)=>{
  if(!bytes(request.input)||!bytes(request.fileName)||request.profile!=='config-ast'||request.implicit!==false||request.failAt!==0||request.returnNil!==false)throw Error('Invalid profile request');
  const expected=s.get(request.id),p=a.get(request.id).expected;
  if(expected.input!==request.input||expected.fileName!==request.fileName)throw Error('Selection input binding mismatch');
  validateConfigAst(p);validateSelection(expected.expected);
  if(!isDeepStrictEqual(p.selection,expected.expected)||!isDeepStrictEqual(p.context,expected.context))throw Error('C#/Go selection binding mismatch at '+request.id);
  const config=p.selection.configFile;let status='NOT_APPLICABLE',equal=null,referenceOutcome='NOT_APPLICABLE',actualOutcome='NOT_APPLICABLE',ref=null;
  if(p.selection.panic!==null){status='REFERENCE_PANIC';referenceOutcome='PANIC';actualOutcome='PANIC';}
  else if(config!==null){
   configCount++;const row=r.get(request.id);
   if(!row||!keys(row,['id','sourceBytes','fileName','nodes','diagnostics','unsupported','panic'])||row.sourceBytes!==config.content||row.fileName!==p.context.configPath)throw Error('Parser source/name binding mismatch');
   ref={nodes:row.nodes,diagnostics:row.diagnostics,unsupported:row.unsupported,panic:row.panic};validateAst(ref);
   referenceOutcome=ref.panic!==null?'PANIC':ref.unsupported!==null?'UNSUPPORTED':ref.diagnostics.length?'DIAGNOSTICS':'RESULT';
   actualOutcome=p.unsupported!==null||p.ast?.unsupported!==null?'UNSUPPORTED':p.ast.panic!==null?'CRASH':p.ast.diagnostics.length?'DIAGNOSTICS':'RESULT';
   if(ref.panic!==null)status='REFERENCE_PANIC';else if(p.ast?.panic)status='CRASH';else if(actualOutcome==='UNSUPPORTED'||referenceOutcome==='UNSUPPORTED')status='UNSUPPORTED';
   else{equal=isDeepStrictEqual(p.ast,ref);status=!equal?'MISMATCH':ref.diagnostics.length?'MATCH_DIAGNOSTICS':'MATCH';}
  }
  return {...sources[i],id:request.id,profile:'pre-vfs/selected-config/json-ast',status,equal,selectionEqual:true,actualOutcome,referenceOutcome,config:config?{id:stableId(request.id,'config-ast',canonicalJson(config)),name:config.name,contentSha256:sha256(Buffer.from(config.content,'base64')),bytes:Buffer.from(config.content,'base64').length,index:p.selection.configIndex,directory:p.context.directory,configPath:p.context.configPath}:null,actualSha256:sha256(canonicalJson(p)),referenceSha256:ref?sha256(canonicalJson(ref)):null,compiler:'NOT_RUN',referenceCompiler:'NOT_RUN',acceptance:'NOT_RUN'};
 });
 if(r.size!==configCount||[...r.keys()].some(id=>!q.has(id)||s.get(id).expected.configFile===null))throw Error('Unexpected parser identities');
 return results;
}

export function verifyConfigAstArtifacts(directory){
 const summary=JSON.parse(readFileSync(join(directory,'summary.json'),'utf8'));
 if(!Array.isArray(summary.artifacts)||!summary.artifacts.length)throw Error('Missing artifact manifest');
 const seen=new Set();for(const a of summary.artifacts){
  if(!keys(a,['path','bytes','sha256'])||typeof a.path!=='string'||!/^[-a-zA-Z0-9.]+$/.test(a.path)||seen.has(a.path)||!integer(a.bytes)||!/^[a-f0-9]{64}$/.test(a.sha256))throw Error('Malformed artifact manifest');seen.add(a.path);
  const bytes=readFileSync(join(directory,a.path));if(bytes.length!==a.bytes||sha256(bytes)!==a.sha256)throw Error('Profile artifact mismatch: '+a.path);
 }
 return summary;
}

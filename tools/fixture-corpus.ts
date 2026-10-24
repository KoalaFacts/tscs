import {isDeepStrictEqual} from 'node:util';
import {sha256} from './ts7-scope.ts';
import {spawnSync} from 'node:child_process';
import {writeFileSync} from 'node:fs';
export function buildFixtureHarness(dotnet,project,logPath) {
 const run=spawnSync(dotnet,['build',project,'-c','Release','--no-restore','-m:1','-p:UseSharedCompilation=false'],{encoding:'utf8',timeout:180000});
 writeFileSync(logPath,(run.stdout??'')+(run.stderr??''),{flag:'wx'});
 if(run.error||run.status!==0||run.signal)throw Error('Harness build failed: '+(run.error??run.stderr));
}

function validatePayload(p) {
 const object=x=>x!==null&&typeof x==='object'&&!Array.isArray(x);
 const keys=(x,want)=>object(x)&&Object.keys(x).sort().join(',')===[...want].sort().join(',');
 const bytes=x=>typeof x==='string'&&Buffer.from(x,'base64').toString('base64')===x;
 const options=x=>object(x)&&Object.values(x).every(bytes);
 const calls=x=>Number.isSafeInteger(x)&&x>=0;
 if(keys(p,['panic','calls'])&&typeof p.panic==='string'&&calls(p.calls))return;
 if(!keys(p,['units','links','directory','options','error','panic','calls'])||p.panic!==null||!calls(p.calls)||
  !(p.error===null||typeof p.error==='string')||!bytes(p.directory)||!options(p.options)||!Array.isArray(p.links)||
  !p.links.every(l=>keys(l,['alias','target'])&&bytes(l.alias)&&bytes(l.target))||
  !(p.units===null||Array.isArray(p.units)&&p.units.every(u=>u===null||keys(u,['name','content','options'])&&bytes(u.name)&&bytes(u.content)&&options(u.options))))
  throw Error('Malformed result payload');
}

export function canonicalJson(value) {
 if(Array.isArray(value))return '['+value.map(canonicalJson).join(',')+']';
 if(value&&typeof value==='object')return '{'+Object.keys(value).sort().map(k=>JSON.stringify(k)+':'+canonicalJson(value[k])).join(',')+'}';
 return JSON.stringify(value);
}
export function reconcileResults(sources,actual,reference) {
 const index=(rows,label)=>{
  const m=new Map();for(const row of rows){if(typeof row.id!=='string'||!row.id||m.has(row.id))throw Error(`Duplicate or invalid ${label} identity`);m.set(row.id,row);}
  return m;
 };
 const sourceIndex=index(sources,'source'),a=index(actual,'actual'),r=index(reference,'reference');
 for(const [label,m] of [['actual',a],['reference',r]])
  if(m.size!==sourceIndex.size||[...m.keys()].some(id=>!sourceIndex.has(id)))throw Error(`Missing or unexpected ${label} result identities`);
 return sources.map(source=>{
  const av=a.get(source.id).expected,rv=r.get(source.id).expected;
  validatePayload(av);validatePayload(rv);
  const same=isDeepStrictEqual(av,rv);
  const loading=rv.panic!==null&&rv.panic!==undefined?'REFERENCE_PANIC':rv.error!==null?'REFERENCE_ERROR':av.panic!==null?'CRASH':same?'MATCH':'MISMATCH';
  return {...source,loading,equal:same,actualSha256:sha256(canonicalJson(av)),referenceSha256:sha256(canonicalJson(rv)),
   progress:{compiler:'NOT_RUN',reference:'NOT_RUN',acceptance:'NOT_RUN'}};
 });
}

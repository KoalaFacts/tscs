import {readFileSync,writeFileSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {execFileSync,spawnSync} from 'node:child_process';
import {isDeepStrictEqual} from 'node:util';
import {sha256,stableId,classifyAsset} from './ts7-scope.ts';
import {canonicalJson} from './fixture-corpus.ts';
export function sourceConfigurationRunner(path,domain){
 const selected=classifyAsset(path);
 if(selected.role!=='fixture-input'||selected.domain!==domain)throw Error('Fixture path/domain mismatch');
 return selected.domain==='fixture:transpile'?'transpile':'compiler';
}
export const sourceConfigPin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';
export const sourceConfigReferences=['tsc/internal/testrunner/test_case_parser.go','tsc/internal/testrunner/compiler_runner.go','tsc/internal/testrunner/transpile_runner.go','tsc/internal/testrunner/options_generated.go','tsc/internal/testutil/harnessutil/harnessutil.go','tsc/internal/tsoptions/declarations_generated.go','tsc/internal/vfs/internal/internal.go'];
export function sourceConfigOracle(upstream,requests,output){
 const git=(...args)=>execFileSync('git',['-C',upstream,...args],{env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
 if(git('rev-parse','HEAD').toString().trim()!==sourceConfigPin||git('status','--porcelain','--untracked-files=no').length||JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==sourceConfigPin)throw Error('Exact clean pin required');
 const go=process.env.GO??'go',goVersion=execFileSync(go,['version'],{encoding:'utf8'}).trim();if(!goVersion.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
 const input=join(output,'requests.json'),result=join(output,'reference.json'),probe=resolve('tools/fixture-harness/source_configurations_oracle_test.go'),overlay=join(output,'overlay.json');
 writeFileSync(input,JSON.stringify(requests),{flag:'wx'});
 writeFileSync(overlay,JSON.stringify({Replace:{[join(upstream,'tsc/internal/testrunner/tscs_source_configurations_test.go')]:probe}}),{flag:'wx'});
 const run=spawnSync(go,['test','-json','-overlay',overlay,'-run','^TestTscsSourceConfigurationsOracle$','-count=1','./internal/testrunner'],{cwd:join(upstream,'tsc'),encoding:'utf8',timeout:180000,maxBuffer:64*1024*1024,env:{...process.env,GOWORK:'off',GOTOOLCHAIN:'local',TSCS_SOURCE_CONFIG_INPUT:input,TSCS_SOURCE_CONFIG_OUTPUT:result}});
 writeFileSync(join(output,'reference.log'),(run.stdout??'')+(run.stderr??''),{flag:'wx'});
 if(run.error||run.signal||![0,1].includes(run.status))throw Error('Go oracle failed: '+(run.error??run.stderr));
 const data=JSON.parse(readFileSync(result,'utf8'));bindSourceConfigurations(requests,data.vectors);
 const failed=new Set(run.stdout.trim().split('\n').filter(Boolean).map(l=>JSON.parse(l)).filter(e=>e.Action==='fail'&&e.Test).map(e=>e.Test));
 const expected=new Set(data.vectors.filter(r=>r.expected.fatal).map(r=>'TestTscsSourceConfigurationsOracle/'+r.id));if(expected.size)expected.add('TestTscsSourceConfigurationsOracle');
 if((run.status===1)!==(expected.size>0)||!isDeepStrictEqual(failed,expected))throw Error('Unexpected Go failed tests or exit');
 return {...data,upstreamCommit:sourceConfigPin,goVersion,referenceExit:run.status,expectedGoFailuresVerified:true,probeSha256:sha256(readFileSync(probe)),sourceHashes:Object.fromEntries(sourceConfigReferences.map(p=>[p,sha256(git('show',`${sourceConfigPin}:${p}`))]))};
}
const object=x=>x!==null&&typeof x==='object'&&!Array.isArray(x);
const keys=(x,names)=>object(x)&&Object.keys(x).sort().join(',')===[...names].sort().join(',');
const bytes=x=>typeof x==='string'&&Buffer.from(x,'base64').toString('base64')===x;
export function validateSourceConfiguration(p){
 const config=c=>keys(c,['Name','Config'])&&typeof c.Name==='string'&&object(c.Config)&&Object.values(c.Config).every(v=>typeof v==='string');
 if(!keys(p,['settings','helper','configurations','panic','fatal','unsupported'])||!object(p.settings)||!Object.keys(p.settings).every(k=>/^[a-z0-9_]+$/.test(k))||!Object.values(p.settings).every(bytes)||
 !(p.helper===null||Array.isArray(p.helper)&&p.helper.length>0&&p.helper.every(config))||
 !(p.configurations===null||Array.isArray(p.configurations)&&p.configurations.length>0&&p.configurations.every(c=>c===null||config(c)))||typeof p.fatal!=='boolean'||
 !(p.panic===null||typeof p.panic==='string')||!(p.unsupported===null||p.unsupported==='INVALID_UTF8_SETTING'))throw Error('Malformed source configuration payload');
 const errors=Number(p.fatal)+Number(p.panic!==null)+Number(p.unsupported!==null);
 if(errors>1||errors&&(p.helper!==null||p.configurations!==null)||!errors&&p.configurations===null)throw Error('Inconsistent source configuration outcome');
 if(!errors&&p.helper!==null&&!isDeepStrictEqual(p.helper,p.configurations))throw Error('Helper/runner configuration disagreement');
}
export function bindSourceConfigurations(requests,rows){
 if(!Array.isArray(rows)||rows.length!==requests.length||new Set(requests.map(r=>r.id)).size!==requests.length||new Set(rows.map(r=>r.id)).size!==rows.length)throw Error('Lost or duplicate configuration identities');
 const byID=new Map(rows.map(r=>[r.id,r]));for(const r of requests){const row=byID.get(r.id);if(!row||row.input!==r.input||row.runner!==r.runner)throw Error('Source configuration input binding mismatch');validateSourceConfiguration(row.expected);
  const p=row.expected;if(p.helper===null&&!p.fatal&&p.panic===null&&p.unsupported===null){
   const decoder=new TextDecoder('utf-8',{fatal:true,ignoreBOM:true});
   const fallback=r.runner==='compiler'?[null]:r.runner==='transpile'?[{Name:'',Config:Object.fromEntries(Object.entries(p.settings).map(([k,v])=>[k,decoder.decode(Buffer.from(v,'base64'))]))}]:null;
   if(fallback===null||!isDeepStrictEqual(p.configurations,fallback))throw Error('Invalid runner fallback');
  }
 }
}
export function reconcileSourceConfigurations(sources,requests,actual,reference){
 bindSourceConfigurations(requests,reference);
 if(actual.length!==requests.length||new Set(actual.map(r=>r.id)).size!==actual.length)throw Error('Lost or duplicate actual identities');
 const a=new Map(actual.map(r=>[r.id,r.expected])),ref=new Map(reference.map(r=>[r.id,r.expected]));
 bindSourceConfigurations(requests,requests.map(r=>({...r,expected:a.get(r.id)})));
 return requests.map((r,i)=>{
  const av=a.get(r.id),rv=ref.get(r.id);validateSourceConfiguration(av);const equal=isDeepStrictEqual(av,rv);
  const outcome=p=>p.panic!==null?'PANIC':p.fatal?'FATAL':p.unsupported!==null?'UNSUPPORTED':'RESULT';
  const status=rv.panic!==null?'REFERENCE_PANIC':rv.fatal?'REFERENCE_FATAL':av.panic!==null||av.fatal?'CRASH':av.unsupported!==null||rv.unsupported!==null?'UNSUPPORTED':equal?'MATCH':'MISMATCH';
  return {...sources[i],id:r.id,profile:r.runner+'/source-settings/configurations',status,equal,actualOutcome:outcome(av),referenceOutcome:outcome(rv),configurations:status==='MATCH'?av.configurations.map((c,index)=>({id:stableId(r.id,'configuration',canonicalJson(c)),index,configuration:c,compiler:'NOT_RUN',referenceCompiler:'NOT_RUN',acceptance:'NOT_RUN'})):null,compiler:'NOT_RUN',referenceCompiler:'NOT_RUN',acceptance:'NOT_RUN'};
 });
}

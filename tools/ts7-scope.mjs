import { createHash } from 'node:crypto';

const utf8 = new TextDecoder('utf-8', {fatal:true, ignoreBOM:true});
export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
export const stableId = (pin, kind, identity) => `${kind}:${sha256(JSON.stringify([pin,kind,identity]))}`;
export const initialProgress = () => ({implementation:'UNIMPLEMENTED',compiler:'NOT_RUN',reference:'NOT_RUN',acceptance:'NOT_RUN',evidence:[]});

export function parseTree(buffer) {
  const entries=[], paths=new Set();
  let start=0;
  while (start<buffer.length) {
    const end=buffer.indexOf(0,start);
    if (end<0) throw Error('Unterminated Git tree entry');
    const row=utf8.decode(buffer.subarray(start,end)); start=end+1;
    const match=/^(100644|100755|120000|160000) (blob|commit) ([a-f0-9]{40})\t([\s\S]+)$/.exec(row);
    if (!match) throw Error('Unsupported or malformed Git tree entry');
    const [,mode,type,oid,path]=match;
    if ((mode==='160000')!==(type==='commit')) throw Error(`Git tree mode/type mismatch: ${path}`);
    if (path.startsWith('/') || path.split('/').some(x=>!x || x==='.' || x==='..')) throw Error(`Unsafe Git tree path: ${path}`);
    if (paths.has(path)) throw Error(`Duplicate Git tree path: ${path}`);
    paths.add(path); entries.push({mode,type,oid,path});
  }
  return entries;
}

export function classifyAsset(path) {
  let m=/^tsc\/testdata\/tests\/cases\/([^/]+)\//.exec(path);
  if (m) {
    const known=['compiler','conformance','transpile'].includes(m[1]);
    const selected=m[1]==='transpile' ? /\.[cm]?[tj]sx?$/.test(path) : /\.tsx?$/.test(path);
    return {role:known&&selected?'fixture-input':'fixture-support',domain:`fixture:${m[1]}`};
  }
  m=/^tsc\/testdata\/baselines\/reference\/([^/]+)\//.exec(path);
  if (m) return {role:'baseline',domain:`baseline:${m[1]}`};
  if (path.endsWith('_test.go')) {
    const testdata=path.indexOf('/testdata/');
    return testdata>=0 ? {role:'go-test-fixture',domain:`support:${path.slice(0,testdata)}/testdata`} : {role:'go-test-source',domain:`go:${path.slice(0,path.lastIndexOf('/'))}`};
  }
  if (/\.(?:test|bench)\.[cm]?[jt]sx?$/.test(path)) {
    const prefix=path.startsWith('packages/')?path.split('/').slice(0,2).join('/'):'tools/scripts';
    return {role:/\.bench\./.test(path)?'js-benchmark-source':'js-test-source',domain:`js:${prefix}`};
  }
  if (path.startsWith('tsc/testdata/')) return {role:'test-support',domain:`support:${path.split('/').slice(0,3).join('/')}`};
  return {role:'upstream-support',domain:`support:${path.split('/')[0]}`};
}

export function inspectFixture(asset,bytes) {
  const result={id:stableId(asset.id,'fixture',asset.path),assetId:asset.id,path:asset.path,domain:asset.domain,
    rawSha256:sha256(bytes),decoding:'UTF8',bom:bytes.subarray(0,3).equals(Buffer.from([0xef,0xbb,0xbf]))?'UTF8':bytes.subarray(0,2).equals(Buffer.from([0xff,0xfe]))?'UTF16LE':bytes.subarray(0,2).equals(Buffer.from([0xfe,0xff]))?'UTF16BE':null,
    hasNul:bytes.includes(0),directives:[],virtualPathDeclarations:0,symlinkDeclarations:0,
    upstreamSelection:{status:'SELECTED_BY_EXTENSION',policy:null},
    expansion:{status:'UNEXPANDED',concreteCases:null,blockers:['CANONICAL_CONFIGURATION_EXPANSION','VIRTUAL_FILE_AND_EFFECTIVE_OPTION_EXPANSION','RUNTIME_SUBTEST_EXPANSION']},progress:initialProgress()};
  let source;
  try { source=utf8.decode(bytes); } catch {
    result.decoding='UTF8_INVALID'; result.rawBytesBase64=bytes.toString('base64');
    result.expansion.blockers.unshift('BYTE_DECODING_UNIMPLEMENTED'); return result;
  }
  // This records lexical declarations only. It intentionally does not implement
  // extractCompilerSettings, virtual files, option defaults or canonical variations.
  if(result.bom==='UTF8') source=source.slice(1);
  for (const match of source.matchAll(/(?:^|(?<=\n))\/\/[\t\n\f\r ]*@(\w+)[\t\n\f\r ]*:[\t\n\f\r ]*([^\r\n]*)/g)) {
    const name=match[1].toLowerCase();
    result.directives.push({name,value:match[2],line:source.slice(0,match.index).split('\n').length});
    if(name==='filename') result.virtualPathDeclarations++;
    if(name==='link'||name==='symlink') result.symlinkDeclarations++;
  }
  return result;
}

export function validateProgress(progress) {
  if (!progress || !['UNIMPLEMENTED','IN_PROGRESS','IMPLEMENTED'].includes(progress.implementation)) throw Error('Invalid implementation status');
  const states=['NOT_RUN','BLOCKED','UNSUPPORTED','PASS','FAIL'];
  for(const key of ['compiler','reference','acceptance']) if(!states.includes(progress[key])) throw Error(`Invalid ${key} status`);
  if(!Array.isArray(progress.evidence)) throw Error('Invalid progress evidence');
  for(const item of progress.evidence) {
    if(!item || !['compiler','reference','acceptance'].includes(item.kind) || typeof item.uri!=='string' || !item.uri || !/^[a-f0-9]{64}$/.test(item.sha256??'') || !/^[a-f0-9]{40}$/.test(item.upstreamCommit??'')) throw Error('Invalid progress evidence');
  }
  if(progress.acceptance==='PASS' && (progress.compiler!=='PASS'||progress.reference!=='PASS'||progress.implementation!=='IMPLEMENTED')) throw Error('Acceptance PASS requires implemented compiler/reference PASS');
  for(const key of ['compiler','reference','acceptance']) if(['PASS','FAIL'].includes(progress[key])&&!progress.evidence.some(x=>x.kind===key)) throw Error(`${key} result requires evidence`);
  return true;
}

export function summarize(assets,fixtures,declarations) {
  const unique=(rows,label)=>{const ids=new Set();for(const row of rows){if(ids.has(row.id))throw Error(`Duplicate ${label} ID`);ids.add(row.id);}return ids;};
  const assetsById=new Map(assets.map(x=>[x.id,x]));
  const assetIds=unique(assets,'asset'); unique(fixtures,'fixture'); unique(declarations,'declaration');
  const fixtureSources=new Set(fixtures.map(x=>x.assetId));
  for(const row of [...fixtures,...declarations]) if(!assetIds.has(row.assetId)) throw Error('Dangling source reference');
  for(const row of assets) if(row.role==='fixture-input'&&!fixtureSources.has(row.id))throw Error(`Missing fixture metadata: ${row.path}`);
  if(fixtures.length!==assets.filter(x=>x.role==='fixture-input').length)throw Error('Fixture inventory reconciliation failed');
  const groups=new Map();
  for(const row of assets) { if(!groups.has(row.domain)) groups.set(row.domain,{id:row.domain,assets:0,roles:{},fixtureInputs:0,goDeclarations:0}); const group=groups.get(row.domain);group.assets++;group.roles[row.role]=(group.roles[row.role]??0)+1; }
  for(const row of fixtures) groups.get(row.domain).fixtureInputs++;
  for(const row of declarations) { const source=assetsById.get(row.assetId); groups.get(source.domain).goDeclarations++; }
  return {trackedAssets:assets.length,fixtureInputs:fixtures.length,goDeclarations:declarations.length,
    concreteCases:null,sourceScopeComplete:false,releaseEligible:false,
    invalidUtf8Fixtures:fixtures.filter(x=>x.decoding==='UTF8_INVALID').length,
    upstreamSkippedFixtures:fixtures.filter(x=>x.upstreamSelection.status==='UPSTREAM_SKIPPED').length,
    virtualPathDeclarations:fixtures.reduce((n,x)=>n+x.virtualPathDeclarations,0),
    virtualSymlinkDeclarations:fixtures.reduce((n,x)=>n+x.symlinkDeclarations,0),
    domains:[...groups.values()].sort((a,b)=>a.id<b.id?-1:a.id>b.id?1:0)};
}

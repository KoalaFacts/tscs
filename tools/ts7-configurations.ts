import {initialProgress,stableId} from './ts7-scope.ts';

// These committed sources jointly define this deliberately narrow policy.
// Re-pinning, changing a selector, or extending to compiler enums needs review.
export const transpileConfigurationPolicy = {
  upstreamCommit:'8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f',
  sources:[
    {path:'tsc/internal/vfs/internal/internal.go',sha256:'2d641c466808f1417e4e20d46aa4beb7f9dcc1bc5fd872a5d134fb3ae56d1def'},
    {path:'tsc/internal/testrunner/transpile_runner.go',sha256:'d273f976c9a84a7796d227ba98f75745ce71530a0262dc6885ea442cfafdaae2'},
    {path:'tsc/internal/testrunner/test_case_parser.go',sha256:'03578c4d35165ee461f4e0488855ffb534eea9a5c598887b255f60817c07d34f'},
    {path:'tsc/internal/testutil/harnessutil/harnessutil.go',sha256:'170d8cf5fdd5e146113411c4526a2c409a28e78a17ac23912feae581af50271d'},
    {path:'tsc/internal/tsoptions/declarations_generated.go',sha256:'8f414711280dc2e88abdb480bf6d823beae435eedb9161199fdf3b08a3ea33ff'},
  ],
};
const varying = new Set(['declarationmap','sourcemap','inlinesourcemap']);
// Go strings.TrimSpace uses Unicode White_Space: unlike JS trim it includes
// U+0085 and excludes U+FEFF. Directive matching itself stays in inspectFixture.
const whitespace = '[\\t\\n\\v\\f\\r \\u0085\\u00a0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000]';
const trim = value => value.replace(new RegExp(`^${whitespace}+|${whitespace}+$`,'g'),'');
const sortedObject = entries => Object.fromEntries([...entries].sort(([a],[b])=>a<b?-1:a>b?1:0));
const booleanKey = value => /^(true|false)$/i.test(value) ? value.toLowerCase() : null;

function splitBooleanValues(value,option) {
  const includes=[],excludes=[];
  let star=false;
  for(const raw of value.split(',')) {
    const token=trim(raw);
    if(!token)continue;
    if(token==='*')star=true;
    else if(token.startsWith('-')||token.startsWith('!'))excludes.push(token.slice(1));
    else includes.push(token);
  }
  if(!includes.length&&!excludes.length&&!star)return {values:[]};
  const values=new Map();
  for(const include of includes) {
    const key=booleanKey(include);
    if(key===null)return {blocker:`UNKNOWN_BOOLEAN_VALUE:${option}`};
    if(!values.has(key))values.set(key,include);
  }
  if(star)for(const key of ['true','false'])if(!values.has(key))values.set(key,key);
  for(const exclude of excludes)values.delete(booleanKey(exclude));
  if(!values.size)return {blocker:`EMPTY_VARIATION_SET:${option}`};
  return {values:[...values.values()]};
}

// Implements only the pinned transpile configuration layer. It does not parse
// effective compiler options, virtual files, diagnostics or runtime runKind cases.
export function expandTranspileConfigurations(directives) {
  const settings=new Map();
  for(const {name,value} of directives)settings.set(name,trim(value).replace(/;$/,''));
  const nonVarying=new Map(),axes=[];
  for(const [option,value] of [...settings].sort(([a],[b])=>a<b?-1:a>b?1:0)) {
    if(!varying.has(option)) { nonVarying.set(option,value);continue; }
    const result=splitBooleanValues(value,option);
    if(result.blocker)return {status:'BLOCKED',configurations:null,blockers:[result.blocker]};
    if(result.values.length>1)axes.push([option,result.values]);
    else if(result.values.length===1)nonVarying.set(option,result.values[0]);
  }
  // Three boolean axes have at most eight members, below the upstream cap of 25.
  let products=[new Map()];
  for(const [option,values] of axes)products=products.flatMap(config=>values.map(value=>new Map([...config,[option,value]])));
  const configurations=products.map(config=>({
    name:[...config].map(([key,value])=>`${key}=${value.toLowerCase()}`).join(','),
    settings:sortedObject([...config,...nonVarying]),
  }));
  if(!axes.length&&!nonVarying.size)configurations[0].settings=sortedObject(settings);
  configurations.sort((a,b)=>a.name<b.name?-1:a.name>b.name?1:0);
  return {status:'EXPANDED',configurations,blockers:[]};
}

export function inventoryConfigurations(fixtures,pin,assets) {
  const sources=transpileConfigurationPolicy.sources;
  const verified=pin===transpileConfigurationPolicy.upstreamCommit && sources.every(expected=>assets.some(actual=>actual.path===expected.path&&actual.sha256===expected.sha256));
  const configurations=[];
  for(const fixture of fixtures) {
    const unresolved=reason=>({status:'UNEXPANDED',configurations:null,blockers:[reason]});
    if(fixture.domain!=='fixture:transpile')fixture.configurationExpansion=unresolved('COMPILER_CONFIGURATION_EXPANSION_UNIMPLEMENTED');
    else if(fixture.decoding!=='UTF8')fixture.configurationExpansion=unresolved('BYTE_DECODING_UNIMPLEMENTED');
    else if(!verified)fixture.configurationExpansion=unresolved('CONFIGURATION_POLICY_UNVERIFIED');
    else {
      const result=expandTranspileConfigurations(fixture.directives);
      fixture.configurationExpansion={status:result.status,configurations:result.configurations?.length??null,blockers:result.blockers,
        policySources:sources.map(({path})=>assets.find(x=>x.path===path).id)};
      if(result.status!=='EXPANDED')continue;
      fixture.expansion.blockers=fixture.expansion.blockers.filter(x=>x!=='CANONICAL_CONFIGURATION_EXPANSION');
      for(const config of result.configurations)configurations.push({
        id:stableId(pin,'fixture-configuration',[fixture.id,config.name,Object.entries(config.settings)]),
        fixtureId:fixture.id,assetId:fixture.assetId,path:fixture.path,domain:fixture.domain,
        kind:'TRANSPILE_NAMED_CONFIGURATION',...config,progress:initialProgress(),
      });
    }
  }
  return configurations;
}

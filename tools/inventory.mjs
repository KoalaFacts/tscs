import {readdirSync,readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {join,relative} from 'node:path';
import {createHash} from 'node:crypto';
const root=process.argv[2];if(!root)throw Error('usage: node tools/inventory.mjs UPSTREAM');
const variants=new Set(['target','module','moduleresolution','strict','strictnullchecks','noimplicitany','usedefineforclassfields','declaration','verbatimmodulesyntax','jsx','noemit','isolatedmodules','esmoduleinterop','allowjs','checkjs','exactoptionalpropertytypes','noindexedaccess','noemitonerror']);
const list=new Set(['lib','types','typeroots','rootdirs','customconditions']);
function walk(p){return readdirSync(p,{withFileTypes:true}).flatMap(d=>d.isDirectory()?walk(join(p,d.name)):[join(p,d.name)]);}
const entries=[];
for(const suite of ['compiler','conformance'])for(const file of walk(join(root,'tsc/testdata/tests/cases',suite)).sort()){
 if(!/\.tsx?$/.test(file))continue;
 const bytes=readFileSync(file);const directives=[...bytes.toString('utf8').matchAll(/^\s*\/\/\s*@(\w+)\s*:\s*([^\r\n]*)/gm)].map(m=>[m[1].toLowerCase(),m[2].trim()]);
 const options=new Map();let unhandled=[];
 for(const [key,value]of directives){if(variants.has(key)) options.set(key,value.split(',').map(v=>v.trim()));else if(value.includes(',')&&!list.has(key))unhandled.push(key);}
 const variantCount=unhandled.length?null:[...options.values()].reduce((n,a)=>n*a.length,1);
 entries.push({path:relative(root,file).replaceAll('\\','/'),sha256:createHash('sha256').update(bytes).digest('hex'),directives,variantCount,unhandled});
}
const serialized=JSON.stringify(entries);mkdirSync('.artifacts',{recursive:true});writeFileSync('.artifacts/upstream-inventory.json',serialized+'\n');
const summary={commit:'8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f',files:entries.length,knownVariants:entries.reduce((n,e)=>n+(e.variantCount??0),0),unresolvedVariantFiles:entries.filter(e=>e.variantCount===null).length,inventorySha256:createHash('sha256').update(serialized+'\n').digest('hex'),note:'Discovery only; variant counts are provisional where unhandled directives are present. Full upstream harness is not implemented.'};
writeFileSync('tests/conformance/inventory-summary.json',JSON.stringify(summary,null,2)+'\n');console.log(summary);

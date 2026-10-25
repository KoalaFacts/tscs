// Preserve one actual-Go source-backed regression from a completed full profile.
import {readFileSync,writeFileSync} from 'node:fs';import {join} from 'node:path';
import {verifyConfigAstArtifacts} from './source-config-ast.ts';
import {sha256} from './ts7-scope.ts';
const [dir,path,out]=process.argv.slice(2);if(!dir||!path||!out||process.argv.length!==5)throw Error('Usage: node tools/select-config-ast-regression.ts PROFILE SOURCE_PATH NEW_OUTPUT');
const summary=verifyConfigAstArtifacts(dir);
if(summary.upstreamCommit!==JSON.parse(readFileSync('upstream.lock.json','utf8')).commit)throw Error('Pin mismatch');
const result=readFileSync(join(dir,'results.jsonl'),'utf8').trimEnd().split('\n').map(l=>JSON.parse(l)).filter(r=>r.path===path);if(result.length!==1||!result[0].config)throw Error('Expected one selected config');
const row=result[0],requests=JSON.parse(readFileSync(join(dir,'requests.json'),'utf8')),selection=JSON.parse(readFileSync(join(dir,'selection.json'),'utf8')),reference=JSON.parse(readFileSync(join(dir,'reference.json'),'utf8'));
const q=requests.find(r=>r.id===row.id),s=selection.find(r=>r.id===row.id),r=reference.find(r=>r.id===row.id);if(!q||!s||!r||q.input!==s.input||q.fileName!==s.fileName||sha256(Buffer.from(q.input,'base64'))!==row.rawSha256||r.sourceBytes!==s.expected.configFile.content||r.fileName!==s.context.configPath||r.panic!==null||r.unsupported!==null)throw Error('Source/reference binding mismatch');
const ast={nodes:r.nodes,diagnostics:r.diagnostics,unsupported:r.unsupported,panic:r.panic};
writeFileSync(out,JSON.stringify({upstreamCommit:summary.upstreamCommit,path,sourceId:row.sourceId,rawSha256:row.rawSha256,request:q,expected:{selection:s.expected,context:s.context,ast,unsupported:null},referenceHashes:summary.referenceHashes,probeHashes:Object.fromEntries(Object.entries(summary.sourceHashes).filter(([p])=>p.endsWith('_test.go')))},null,2)+'\n',{flag:'wx'});

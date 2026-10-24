import {execFileSync,spawnSync} from 'node:child_process';
import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
import {createHash} from 'node:crypto';

const pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';
const upstream=process.argv[2],output=process.argv[3];
if(!upstream||!output)throw Error('Usage: node tools/generate-source-decoding-vectors.ts UPSTREAM OUTPUT');
const lock=JSON.parse(readFileSync(new URL('../upstream.lock.json',import.meta.url),'utf8'));
if(lock.commit!==pin)throw Error('Pin change requires separate decoder review');
const version=execFileSync('go',['version'],{encoding:'utf8'}).trim();
if(!version.startsWith('go version go1.27.3 '))throw Error('Pinned Go1.27.3 required');
const path='tsc/internal/vfs/internal/internal.go';
const source=execFileSync('git',['-C',upstream,'show',`${pin}:${path}`],{encoding:'utf8',env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
const functions=source.slice(source.indexOf('func decodeBytes('));
if(!functions.startsWith('func decodeBytes(')||!functions.trimEnd().endsWith('return string(utf16.Decode(ints))\n}'))throw Error('Pinned decoder function boundary changed');
const rows=[];
const add=(id,input)=>rows.push({id,input:Buffer.from(input).toString('base64')});
add('empty',[]);
for(let n=0;n<256;n++)add(`single-byte-${n.toString(16).padStart(2,'0')}`,[n]);
for(const hex of ['80','c0af','e282','eda080','f4908080','f09f9880','efbfbd','efbbbf','efbbbf80efbbbf','fffe','feff','fffe00','feff00','fffe00d800dc','feffd800dc00'])add(`utf8-bom-and-boundary-${hex}`,Buffer.from(hex,'hex'));
const units=[0,0x41,0x85,0xd7ff,0xd800,0xdbff,0xdc00,0xdfff,0xe000,0xfeff,0xffff];
for(const a of units)for(const b of units)for(const little of [true,false])for(const odd of [false,true]){
 const bytes=Buffer.alloc(6+(odd?1:0));if(little){bytes[0]=255;bytes[1]=254;bytes.writeUInt16LE(a,2);bytes.writeUInt16LE(b,4);}else{bytes[0]=254;bytes[1]=255;bytes.writeUInt16BE(a,2);bytes.writeUInt16BE(b,4);}if(odd)bytes[6]=0x80;
 add(`utf16-${little?'le':'be'}-${a.toString(16)}-${b.toString(16)}-${odd?'odd':'even'}`,bytes);
}
// These are the eight raw non-UTF8 fixture blobs in the pinned source inventory.
// Read immutable Git bytes, not decoded working-copy text or historical results.
for(const name of ['bom-utf16be.ts','bom-utf16le.ts','collisionCodeGenModuleWithUnicodeNames.ts','instanceofOperator.ts','promiseTest.ts','regexInvalidUtf8WithUnicodeFlag.ts','targetTypeBaseCalls.ts','unicodeIdentifierNames.ts']){
 const fixture=`tsc/testdata/tests/cases/compiler/${name}`;
 const bytes=execFileSync('git',['-C',upstream,'show',`${pin}:${fixture}`],{env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
 add(`fixture:${fixture}`,bytes);
}
const goSource=`package main
import("encoding/binary";"strings";"unicode/utf16";"encoding/json";"encoding/base64";"os")
${functions}
func main(){var rows []struct{ID string \u0060json:"id"\u0060;Input string \u0060json:"input"\u0060};if err:=json.NewDecoder(os.Stdin).Decode(&rows);err!=nil{panic(err)};result:=[]map[string]string{};for _,row:=range rows{b,err:=base64.StdEncoding.DecodeString(row.Input);if err!=nil{panic(err)};s,ok:=decodeBytes(string(b));if !ok{panic("decode failed")};result=append(result,map[string]string{"id":row.ID,"input":row.Input,"expected":base64.StdEncoding.EncodeToString([]byte(s))})};if err:=json.NewEncoder(os.Stdout).Encode(result);err!=nil{panic(err)}}
`;
const temporary=mkdtempSync(join(tmpdir(),'tscs-decoder-'));
try{
 const file=join(temporary,'main.go');writeFileSync(file,goSource);
 const result=spawnSync('go',['run',file],{encoding:'utf8',input:JSON.stringify(rows),maxBuffer:8*1024*1024,env:{...process.env,GOTOOLCHAIN:'local',GOWORK:'off'}});
 if(result.status!==0)throw Error(result.stderr||String(result.error));
 const vectors=JSON.parse(result.stdout);
 if(vectors.length!==rows.length||new Set(vectors.map(x=>x.id)).size!==rows.length)throw Error('Vector reconciliation failed');
 writeFileSync(resolve(output),JSON.stringify({upstreamCommit:pin,sourcePath:path,sourceSha256:createHash('sha256').update(source).digest('hex'),functionSourceSha256:createHash('sha256').update(functions).digest('hex'),goVersion:version,vectors},null,2)+'\n');
 console.log(`Generated ${vectors.length} byte-exact vectors by executing pinned Go functions`);
}finally{rmSync(temporary,{recursive:true,force:true});}

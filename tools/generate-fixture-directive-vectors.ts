import {execFileSync,spawnSync} from 'node:child_process';
import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
import {createHash} from 'node:crypto';

const pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';
const upstream=process.argv[2],output=process.argv[3];
if(!upstream||!output)throw Error('Usage: node tools/generate-fixture-directive-vectors.ts UPSTREAM OUTPUT');
const lock=JSON.parse(readFileSync(new URL('../upstream.lock.json',import.meta.url),'utf8'));
if(lock.commit!==pin)throw Error('Pin change requires separate decoder review');
const version=execFileSync('go',['version'],{encoding:'utf8'}).trim();
if(!version.startsWith('go version go1.27.3 '))throw Error('Pinned Go1.27.3 required');
const path='tsc/internal/vfs/internal/internal.go';
const source=execFileSync('git',['-C',upstream,'show',`${pin}:${path}`],{encoding:'utf8',env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
const functions=source.slice(source.indexOf('func decodeBytes('));
if(!functions.startsWith('func decodeBytes(')||!functions.trimEnd().endsWith('return string(utf16.Decode(ints))\n}'))throw Error('Pinned decoder function boundary changed');
const parserPath='tsc/internal/testrunner/test_case_parser.go';
const parser=execFileSync('git',['-C',upstream,'show',`${pin}:${parserPath}`],{encoding:'utf8',env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
const expressions=['lineDelimiter','optionRegex','linkRegex'].map(name=>parser.split('\n').find(line=>line.startsWith(`var ${name} = regexp.MustCompile(`))).join('\n');
const symlink=parser.slice(parser.indexOf('func parseSymlinkFromTest('));
if(expressions.split('\n').length!==3||!symlink.startsWith('func parseSymlinkFromTest('))throw Error('Missing pinned parser functions');
const rows=[];const add=(id,bytes)=>rows.push({id,input:Buffer.from(bytes).toString('base64')});
for(const [id,text] of Object.entries({empty:'',lines:'\n\r\nbody;\r\n',barecr:'a\rb\n',duplicate:'// @strict: false\n// @strict: true;',indented:' // @filename: a.ts',vertical:'//\v@strict: true',case:'// @LINK: target -> alias',link:'// @link: real -> alias',greedy:'// @link: first -> second -> third',emptylink:'// @link: ->',filenames:'// @Filename: a.ts\n// @noOpen: true\n// @symlink: b.ts, c.ts\n// @currentDirectory: /project',unicode:'// @x: \u0085\u00a0value\u200b\ufeff\u2029',nul:'// @x: a\0b',unknown:'// @123_: a;'}))add(id,Buffer.from(text));
add('long-arrowless-link',Buffer.from('// @link: '+ ' '.repeat(4096)+'no arrow'));
for(let n=0;n<256;n++){
 add(`raw-value-${n}`,Buffer.concat([Buffer.from('// @value: '),Buffer.from([n]),Buffer.from(' \r\n')]));
 add(`raw-link-${n}`,Buffer.concat([Buffer.from('// @link: /'),Buffer.from([n]),Buffer.from(' -> /'),Buffer.from([255-n])]));
}
const decoderVectors=JSON.parse(readFileSync(new URL('../tests/Tscs.Compiler.Tests/TestData/source-decoding.json',import.meta.url),'utf8'));
if(decoderVectors.upstreamCommit!==pin)throw Error('Fixture source pin mismatch');
const fixtures=decoderVectors.vectors.filter(row=>row.id.startsWith('fixture:'));
if(fixtures.length!==8)throw Error('Expected eight pinned raw non-UTF8 fixture sources');
for(const row of fixtures){
 const bytes=execFileSync('git',['-C',upstream,'show',`${pin}:${row.id.slice(8)}`],{env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
 add(row.id,bytes);
}
const goSource=`package main
import("encoding/binary";"strings";"unicode/utf16";"encoding/json";"encoding/base64";"os";"regexp")
${functions}
${expressions}
${symlink}
func b64(s string)string{return base64.StdEncoding.EncodeToString([]byte(s))}
func main(){var rows []struct{ID string;Input string};if err:=json.NewDecoder(os.Stdin).Decode(&rows);err!=nil{panic(err)};result:=[]map[string]any{};for _,row:=range rows{b,err:=base64.StdEncoding.DecodeString(row.Input);if err!=nil{panic(err)};text,_:=decodeBytes(string(b));lines:=[]map[string]any{};start:=0;parts:=lineDelimiter.Split(text,-1);separators:=lineDelimiter.FindAllStringIndex(text,-1);for i,line:=range parts{var directive any;links:=map[string]string{};if parseSymlinkFromTest(line,links){for alias,target:=range links{directive=map[string]any{"name":"link","value":b64(alias),"linkTarget":b64(target)}}}else if m:=optionRegex.FindStringSubmatch(line);m!=nil{directive=map[string]any{"name":strings.ToLower(m[1]),"value":b64(strings.TrimSpace(m[2])),"linkTarget":nil}};lines=append(lines,map[string]any{"offset":start,"bytes":b64(line),"directive":directive});if i<len(separators){start=separators[i][1]}};result=append(result,map[string]any{"id":row.ID,"input":row.Input,"decoded":b64(text),"lines":lines})};if err:=json.NewEncoder(os.Stdout).Encode(result);err!=nil{panic(err)}}
`;
const temporary=mkdtempSync(join(tmpdir(),'tscs-decoder-'));
try{
 const file=join(temporary,'main.go');writeFileSync(file,goSource);
 const result=spawnSync('go',['run',file],{encoding:'utf8',input:JSON.stringify(rows),maxBuffer:8*1024*1024,env:{...process.env,GOTOOLCHAIN:'local',GOWORK:'off'}});
 if(result.status!==0)throw Error(result.stderr||String(result.error));
 const vectors=JSON.parse(result.stdout);
 if(vectors.length!==rows.length||new Set(vectors.map(x=>x.id)).size!==rows.length)throw Error('Vector reconciliation failed');
 writeFileSync(resolve(output),JSON.stringify({upstreamCommit:pin,parserPath,parserSha256:createHash('sha256').update(parser).digest('hex'),sourcePath:path,sourceSha256:createHash('sha256').update(source).digest('hex'),functionSourceSha256:createHash('sha256').update(functions).digest('hex'),goVersion:version,vectors},null,2)+'\n');
 console.log(`Generated ${vectors.length} directive vectors by executing pinned Go regex and link functions`);
}finally{rmSync(temporary,{recursive:true,force:true});}

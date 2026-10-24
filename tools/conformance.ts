import {spawnSync} from 'node:child_process';
import {isDeepStrictEqual} from 'node:util';
export function classify(expected,actual){
 if(!expected) return 'reference failure';
 if(!actual) return 'not run';
 if(actual.failure) return actual.failure;
 if(actual.diagnostics?.some(d=>String(d.code).startsWith('TSCS'))) return 'unsupported';
 return isDeepStrictEqual(expected,actual)?'pass':'mismatch';
}
export function validateSelection(cases){
 if(!cases.length) throw Error('empty selection');
 if(new Set(cases.map(c=>c.id)).size!==cases.length) throw Error('duplicate case');
}
export function execute(command,args,timeout=10000){
 const p=spawnSync(command,args,{encoding:'utf8',timeout,maxBuffer:16*1024*1024});
 if(p.error?.code==='ETIMEDOUT') return {failure:'timeout'};
 if(p.error||p.signal) return {failure:'crash'};
 try {const r=JSON.parse(p.stdout);if(!Array.isArray(r.diagnostics)||!r.diagnostics.every(validDiagnostic)||!Number.isInteger(r.exitCode)||![0,1,2].includes(r.exitCode)||p.status!==r.exitCode)throw Error();return r;}
 catch{return {failure:'crash'};}
}

function validDiagnostic(d){return d&&typeof d.code==='string'&&typeof d.category==='string'&&typeof d.file==='string'&&Number.isInteger(d.start)&&Number.isInteger(d.length)&&d.start>=0&&d.length>=0&&typeof d.message==='string'&&Array.isArray(d.related)&&d.related.every(validDiagnostic)&&Array.isArray(d.messageChain)&&d.messageChain.every(validDiagnostic);}

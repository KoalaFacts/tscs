import {test} from 'node:test';
import assert from 'node:assert/strict';
import {validateSourceConfiguration,bindSourceConfigurations,reconcileSourceConfigurations,sourceConfigurationRunner} from './source-configurations.ts';
const request={id:'fixture',input:'',runner:'compiler'};
const normal=()=>({settings:{},helper:null,configurations:[null],panic:null,fatal:false,unsupported:null});
test('runner policy is bound to pinned path rather than a mutable inventory label',()=>{
 assert.throws(()=>sourceConfigurationRunner('tsc/testdata/tests/cases/compiler/a.ts','fixture:transpile'),/domain/);
 assert.equal(sourceConfigurationRunner('tsc/testdata/tests/cases/transpile/a.ts','fixture:transpile'),'transpile');
});
test('configuration evidence rejects a helper and runner configuration disagreement',()=>{
 const p=normal();p.helper=[{Name:'strict=true',Config:{strict:'true'}}];
 assert.throws(()=>validateSourceConfiguration(p),/configuration/);
});
test('runner fallback evidence preserves compiler nil and transpile original settings',()=>{
 const p=normal();p.configurations=[{Name:'',Config:{}}];
 assert.throws(()=>bindSourceConfigurations([request],[{...request,expected:p}]),/fallback/);
 const t={...request,runner:'transpile'};
 assert.throws(()=>bindSourceConfigurations([t],[{...t,expected:normal()}]),/fallback/);
});
test('malformed outputs, missing IDs and swapped input cannot earn MATCH',()=>{
 for(const p of [null,{...normal(),settings:{a:'!'}},{...normal(),configurations:null},{...normal(),extra:true}])assert.throws(()=>validateSourceConfiguration(p));
 assert.throws(()=>bindSourceConfigurations([request],[{...request,input:'YQ==',expected:normal()}]),/binding/);
 assert.throws(()=>reconcileSourceConfigurations([{}],[request],[],[{...request,expected:normal()}]),/identities/);
 const p={...normal(),configurations:null,unsupported:'INVALID_UTF8_SETTING'};
 const r=reconcileSourceConfigurations([{}],[request],[{id:request.id,expected:p}],[{...request,expected:p}]);assert.equal(r[0].status,'UNSUPPORTED');assert.equal(r[0].equal,true);
});

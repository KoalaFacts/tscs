import {verifyConfigAstArtifacts} from './source-config-ast.ts';
if(process.argv.length!==3)throw Error('Usage: node tools/verify-source-config-ast-artifacts.ts PROFILE');
console.log(JSON.stringify(verifyConfigAstArtifacts(process.argv[2]).counts));

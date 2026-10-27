import {isDeepStrictEqual} from 'node:util';
// A match requires complete identity, parser, option and outcome agreement.
// Unsupported policy outputs retain partial calls/diagnostics but earn no match.
export function reconcileSourceOptions(binding:any,reference:any,actual:any):string {
 if(!actual||!isDeepStrictEqual(binding.selection,actual.preparation?.selection)||!isDeepStrictEqual(binding.context,actual.preparation?.context))throw Error('preparation binding mismatch');
 if(actual.preparation.unsupported!==null||actual.preparation.ast?.unsupported!==null||actual.preparation.ast?.panic!==null)throw Error('unsupported parser/preparation');
 if(!isDeepStrictEqual(reference.parserDiagnostics,actual.preparation.ast.diagnostics))throw Error('parser diagnostics mismatch');
 if(!isDeepStrictEqual(reference.unsupported,actual.unsupportedOptions))throw Error('unsupported option binding mismatch');
 if(reference.unsupported.length){if(actual.outcome!=='UNSUPPORTED'||actual.unsupported!=='OPTION_POLICY')throw Error('unsupported outcome mismatch');return 'UNSUPPORTED';}
 if(!isDeepStrictEqual(reference.calls,actual.calls))throw Error('option calls mismatch');
 if(!isDeepStrictEqual(reference.diagnostics,actual.optionDiagnostics))throw Error('option diagnostics mismatch');
 const diagnosed=reference.parserDiagnostics.length+reference.diagnostics.length>0;
 if(actual.unsupported!==null||actual.outcome!==(diagnosed?'OPTIONS_DIAGNOSTICS':'OPTIONS'))throw Error('option outcome mismatch');
 return diagnosed?'MATCH_DIAGNOSTICS':'MATCH';
}

import {createSeed,getSupport} from '../src/generated/schema.mjs';
const state=createSeed({type:'doc',content:[{type:'paragraph',content:[{type:'text',text:Array.from({length:5000},(_,i)=>'word'+i%100).join(' ')}]}]});
console.log(JSON.stringify({support:await getSupport(),state:Buffer.from(state).toString('base64')}));

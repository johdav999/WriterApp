import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';

const source=await readFile(new URL('../WriterApp.Client/wwwroot/js/full-editor-collaboration.js',import.meta.url),'utf8');
const bridge=await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);

function fixture(t){
 const original=Object.fromEntries(['window','document','location','fetch','setInterval','clearInterval'].map(k=>[k,globalThis[k]]));
 t.after(()=>{for(const[k,v]of Object.entries(original))globalThis[k]=v;});
 let generation='generation-one',tick,late;const statuses=[];let closed=0;
 const editor={setEditable(){}};
 const api={getSupport:async()=>({protocolVersion:1,schemaFingerprint:'schema'}),
  IndexedDbJournal:class {async close(){closed++;}},CollaborationAccountFence:class {activate(){}capture(){return {};}close(){}},
  webTicketSource:()=>()=>{},createJournaledEditor:async(host,options)=>{
   late=options.onStatus;
   return {editor,invalidate(){late({accountChanged:true});},session:{flush:async()=>{}},async destroy(){late({connected:false});}};
  }};
 globalThis.window={tiptapEditor:{collaboration:api},addEventListener(){},removeEventListener(){}};
 globalThis.document={getElementById:()=>({isConnected:true})};globalThis.location={origin:'https://prosa.test'};
 globalThis.setInterval=fn=>{tick=fn;return 1;};globalThis.clearInterval=()=>{};
 globalThis.fetch=async url=>({ok:true,json:async()=>url==='/api/auth/me'?{isAuthenticated:true,userId:'actor'}:
  url.includes('/capabilities?')?{canWriteCollaboratively:true,membershipRevision:1,pages:[{pageId:'page',generation}]}:
  {structureRevision:1,pages:[]}});
 return {editor,statuses,get closed(){return closed;},changeGeneration(){generation='generation-two';},
  tick:()=>tick(),emit:value=>late(value),callbacks:{async invokeMethodAsync(method,value){if(method==='OnCollaborationStatus')statuses.push(value);}}};
}

test('generation replacement emits one remount and ignores late notifications from its retired binding',async t=>{
 const f=fixture(t);await bridge.create('host','document','page',f.callbacks);
 f.emit({connected:true});f.changeGeneration();await f.tick();
 assert.equal(f.statuses.filter(s=>s.reloadRequired).length,1);
 f.emit({connected:false});f.emit({pending:0});await bridge.close(f.editor);
 assert.equal(f.statuses.length,2);assert.equal(f.statuses[0].reloadRequired,undefined);
 assert.equal(f.closed,1);
});

test('replacement with pending private writing retains recovery instead of remounting',async t=>{
 const f=fixture(t);await bridge.create('host','document','page',f.callbacks);
 f.emit({pending:1});f.changeGeneration();await f.tick();
 assert.equal(f.statuses.at(-1).pending,1);assert.match(f.statuses.at(-1).recoveryReason,/private local draft is retained/);
 assert.equal(f.statuses.some(s=>s.reloadRequired),false);
 const count=f.statuses.length;f.emit({connected:false});await bridge.close(f.editor);assert.equal(f.statuses.length,count);
});

test('ordinary close ignores draining status notifications and leaves prior snapshots immutable',async t=>{
 const f=fixture(t);await bridge.create('host','document','page',f.callbacks);
 f.emit({connected:true});f.emit({connected:false});assert.equal(f.statuses[0].connected,true);
 await bridge.close(f.editor);assert.equal(f.statuses.length,2);
});

test('storage failures are not misreported as revoked access and flush remains blocked',async t=>{
 const f=fixture(t);await bridge.create('host','document','page',f.callbacks);
 f.emit({error:'Local journal storage unavailable',localDurable:false});
 await assert.rejects(()=>bridge.flush(f.editor),error=>{
  assert.match(error.message,/Local journal storage unavailable/);assert.doesNotMatch(error.message,/Access changed/);return true;
 });
 await bridge.close(f.editor);
});

test('actual account revocation still blocks a shared flush',async t=>{
 const f=fixture(t);await bridge.create('host','document','page',f.callbacks);
 f.emit({accountChanged:true});await assert.rejects(()=>bridge.flush(f.editor),/Access changed/);
 await bridge.close(f.editor);
});

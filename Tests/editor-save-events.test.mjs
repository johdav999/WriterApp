import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
const source=await readFile(new URL('../WriterApp.Client/wwwroot/js/editor-save-events.js',import.meta.url),'utf8');
const {registerEditorSaveEvents:register,unregisterEditorSaveEvents:unregister}=await import('data:text/javascript;base64,'+Buffer.from(source).toString('base64'));
const windowTarget=new EventTarget(),documentTarget=new EventTarget();
globalThis.window=windowTarget;globalThis.document=documentTarget;documentTarget.visibilityState='visible';
const settle=()=>new Promise(resolve=>setImmediate(resolve));
test('Lifecycle blur is deferred, coalesced and does not call .NET while a render holds the heap',async()=>{
 let rendering=true;const calls=[];register('editor',{invokeMethodAsync:async method=>{assert.equal(rendering,false);calls.push(method);}});
 windowTarget.dispatchEvent(new Event('blur'));windowTarget.dispatchEvent(new Event('blur'));assert.deepEqual(calls,[]);rendering=false;await settle();assert.deepEqual(calls,['OnWindowBlurred']);unregister('editor');
});
test('Replacing or unregistering an editor cancels its queued callback',async()=>{
 const calls=[];register('editor',{invokeMethodAsync:async()=>calls.push('old')});windowTarget.dispatchEvent(new Event('blur'));
 register('editor',{invokeMethodAsync:async()=>calls.push('new')});await settle();assert.deepEqual(calls,[]);windowTarget.dispatchEvent(new Event('blur'));unregister('editor');await settle();assert.deepEqual(calls,[]);
 register('editor',{invokeMethodAsync:async()=>calls.push('old-after-first-microtask')});windowTarget.dispatchEvent(new Event('blur'));
 queueMicrotask(()=>unregister('editor'));await settle();assert.deepEqual(calls,[]);
});
test('Hidden and pagehide still request a save and rejected callbacks are observed',async()=>{
 const calls=[];register('editor',{invokeMethodAsync:async method=>calls.push(method)});documentTarget.visibilityState='hidden';documentTarget.dispatchEvent(new Event('visibilitychange'));windowTarget.dispatchEvent(new Event('pagehide'));await settle();assert.deepEqual(calls,['OnDocumentHidden','OnPageHide']);unregister('editor');
 const warnings=[];const previous=console.warn;console.warn=(...args)=>warnings.push(args);try{register('editor',{invokeMethodAsync:()=>{throw new Error('Disposed');}});windowTarget.dispatchEvent(new Event('blur'));await settle();assert.equal(warnings.length,1);}finally{unregister('editor');console.warn=previous;}
});

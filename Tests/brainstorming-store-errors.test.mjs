import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';

const source=await readFile(new URL('../WriterApp.Client/wwwroot/js/brainstorming-store.js',import.meta.url),'utf8');
const helper=await readFile(new URL('../WriterApp.Client/wwwroot/js/indexeddb-connection.js',import.meta.url),'utf8');
const helperUrl=`data:text/javascript;base64,${Buffer.from(helper).toString('base64')}`;
const store=await import(`data:text/javascript;base64,${Buffer.from(source.replace("'./indexeddb-connection.js'",JSON.stringify(helperUrl))).toString('base64')}`);
const args=['a'.repeat(64),'11111111-1111-1111-1111-111111111111','22222222-2222-2222-2222-222222222222'];

test('storage open failures retain the actual error category without replacing stored data',async t=>{
 const previous=globalThis.indexedDB;t.after(()=>globalThis.indexedDB=previous);
 globalThis.indexedDB={open(){const request={error:{name:'QuotaExceededError'}};queueMicrotask(()=>request.onerror());return request;}};
 await assert.rejects(()=>store.read(...args),/QuotaExceededError.*Existing data was retained/);
});

test('a blocked open closes a late connection and never starts a write transaction',async t=>{
 const previous=globalThis.indexedDB;t.after(()=>globalThis.indexedDB=previous);let request,closed=0;
 globalThis.indexedDB={open(){request={result:{close(){closed++;},transaction(){assert.fail('A rejected open must not write.');}}};queueMicrotask(()=>request.onblocked());return request;}};
 await assert.rejects(()=>store.read(...args),/Another tab is blocking/);
 request.onsuccess();assert.equal(closed,1);
});

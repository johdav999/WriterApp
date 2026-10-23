import {test} from 'node:test';
import assert from 'node:assert/strict';
import {indexedDbConnection} from '../WriterApp.Client/wwwroot/js/indexeddb-connection.js';

function fixture(t, errors=[]) {
 const original=globalThis.indexedDB;t.after(()=>globalThis.indexedDB=original);
 const databases=[];let opens=0,transactions=0;
 globalThis.indexedDB={open(){
  opens++;const error=errors.shift(),db={closed:false,close(){this.closed=true;},transaction(){
   if(this.closed)throw new DOMException('Connection closed','InvalidStateError');transactions++;return {id:transactions};
  }};
  const request={result:db,error:error?new DOMException('Synthetic open failure',error):null};
  queueMicrotask(()=>{if(error)request.onerror();else{databases.push(db);request.onsuccess();}});return request;
 }};
 const connection=indexedDbConnection('retained',1,()=>assert.fail('No schema reset'));
 t.after(()=>connection.close());
 return {connection,databases,get opens(){return opens;},get transactions(){return transactions;}};
}

test('concurrent conversation and identity work reuses one successful connection',async t=>{
 const f=fixture(t);await Promise.all(Array.from({length:100},()=>f.connection.transaction('retained','readwrite')));
 assert.equal(f.opens,1);assert.equal(f.transactions,100);
});
test('transient browser open failure recovers before the first transaction',async t=>{
 const f=fixture(t,['UnknownError','AbortError']);await f.connection.transaction('retained','readwrite');
 assert.equal(f.opens,3);assert.equal(f.transactions,1);
});
test('exhausted opening failure does not poison the next explicit attempt',async t=>{
 const f=fixture(t,['AbortError','AbortError','AbortError']);await assert.rejects(()=>f.connection.get(),{name:'AbortError'});
 await f.connection.transaction('retained');assert.equal(f.opens,4);assert.equal(f.transactions,1);
});
test('forced browser closure reopens on the next operation',async t=>{
 const f=fixture(t);await f.connection.get();f.databases[0].closed=true;f.databases[0].onclose();
 await f.connection.transaction('retained');assert.equal(f.opens,2);assert.equal(f.transactions,1);
});
test('closed connection race is retried only before a transaction exists',async t=>{
 const f=fixture(t);await f.connection.get();f.databases[0].closed=true;
 await f.connection.transaction('retained','readwrite');assert.equal(f.opens,2);assert.equal(f.transactions,1);
});
for(const name of ['QuotaExceededError','SecurityError','VersionError'])test(`${name} is not automatically retried`,async t=>{
 const f=fixture(t,[name]);await assert.rejects(()=>f.connection.transaction('retained','readwrite'),{name});
 assert.equal(f.opens,1);assert.equal(f.transactions,0);
});
test('a started transaction is returned once; its later failure cannot replay a write',async t=>{
 const f=fixture(t);const tx=await f.connection.transaction('retained','readwrite');
 tx.error=new DOMException('Commit outcome uncertain','AbortError');
 assert.equal(f.opens,1);assert.equal(f.transactions,1);assert.equal(tx.id,1);
});
test('version change releases the old connection; disposed journals never reopen',async t=>{
 const f=fixture(t);await f.connection.get();f.databases[0].onversionchange();assert.equal(f.databases[0].closed,true);
 await f.connection.transaction('retained');assert.equal(f.opens,2);await f.connection.close();
 await assert.rejects(()=>f.connection.transaction('retained'),{name:'InvalidStateError'});assert.equal(f.opens,2);
});

// Isolated browser acceptance; never attaches to a user's browser/profile/database.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,mkdir,writeFile} from 'node:fs/promises';
import {createRequire} from 'node:module';
import {resolve} from 'node:path';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const directory=resolve(process.env.BRAINSTORMING_EVIDENCE??'artifacts/chrome-storage-5399');await mkdir(directory,{recursive:true});
const helper=await readFile('WriterApp.Client/wwwroot/js/indexeddb-connection.js');
const server=createServer((req,res)=>{res.setHeader('Cache-Control','no-store');res.setHeader('Content-Type',req.url==='/helper.js'?'text/javascript':'text/html');res.end(req.url==='/helper.js'?helper:'<!doctype html><title>IndexedDB connection recovery acceptance</title>');});
await new Promise(done=>server.listen(0,'127.0.0.1',done));let browser;
try {
 browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage();await page.goto(`http://127.0.0.1:${server.address().port}/`);
 const results=await page.evaluate(async()=>{
  const {indexedDbConnection}=await import('/helper.js');const nativeOpen=indexedDB.open.bind(indexedDB);let failures=2,opens=0,transactions=0;
  indexedDB.open=(...args)=>{opens++;if(!failures--)return nativeOpen(...args);if(failures<0)return nativeOpen(...args);
   const request={error:new DOMException('Injected transient browser backing-store open failure','UnknownError')};queueMicrotask(()=>request.onerror());return request;};
  const connection=indexedDbConnection('isolated-recovery-test',1,db=>db.createObjectStore('retained'));
  const checks=[];
  async function put(key,value,abort=false){const tx=await connection.transaction('retained','readwrite');transactions++;
   return new Promise((ok,no)=>{tx.oncomplete=ok;tx.onabort=()=>no(tx.error??new DOMException('Explicit abort','AbortError'));tx.onerror=()=>{};tx.objectStore('retained').put(value,key);if(abort)tx.abort();});}
  async function read(key){const tx=await connection.transaction('retained');return new Promise((ok,no)=>{const r=tx.objectStore('retained').get(key);tx.oncomplete=()=>ok(r.result);tx.onabort=()=>no(tx.error);});}
  await put('authored','Original authored text');if(opens!==3||transactions!==1)throw Error('Open recovery duplicated a transaction');checks.push('two transient open failures recover before exactly one durable write');
  const first=await connection.get();first.close();await put('after-close','Saved once');if(opens!==4||transactions!==2)throw Error('Closed connection did not recover');checks.push('closed browser connection reopens without a page reload');
  await Promise.all(Array.from({length:200},(_,i)=>put('iteration-'+i,i)));if(opens!==4)throw Error('Connection churn remains');checks.push('200 concurrent transactions reuse the connection');
  const before=transactions;try{await put('uncertain','Must remain absent',true);throw Error('Expected abort');}catch(e){if(e.name!=='AbortError')throw e;}
  if(transactions!==before+1||await read('uncertain')!==undefined)throw Error('Aborted write was replayed');checks.push('started write abort remains explicit and is never replayed');
  if(await read('authored')!=='Original authored text')throw Error('Authored text changed');checks.push('authored data survives all connection failures');
  await connection.close();const reopened=indexedDbConnection('isolated-recovery-test',1,()=>{throw Error('Existing schema was replaced');});
  const tx=await reopened.transaction('retained');const restored=await new Promise((ok,no)=>{const r=tx.objectStore('retained').get('authored');tx.oncomplete=()=>ok(r.result);tx.onabort=()=>no(tx.error);});
  if(restored!=='Original authored text')throw Error('Reopen lost data');await reopened.close();checks.push('reopening preserves the original database and schema');
  return {checks,opens,transactions};
 });
 assert.equal(results.checks.length,6);await writeFile(resolve(directory,'browser-storage-result.json'),JSON.stringify(results,null,2));console.log(JSON.stringify(results,null,2));
} finally {await browser?.close();await new Promise(done=>server.close(done));}

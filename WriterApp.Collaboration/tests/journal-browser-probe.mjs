import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, mkdtemp, rm, mkdir, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve, join } from 'node:path';
import { createRequire } from 'node:module';
import * as Y from 'yjs';
import * as decoding from 'lib0/decoding';
import { createCollaborationServer } from '../src/server.mjs';

const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const request = JSON.parse(process.env.PROBE_SESSION_REQUEST), backend = process.env.PROBE_BACKEND_URL;
const sessionPath = process.env.PROBE_SESSION_PATH, serviceKey = process.env.COLLABORATION_SERVICE_KEY;
let transport, context; let profileOwner = 'owner', stockWrites = 0; const results = [], errors = [];let expectedPending;
const directory = await mkdtemp(join(tmpdir(),'WriterApp_CollaborationBrowser_'));
async function api(path, body, internal = false) {
 const response = await fetch(new URL(path, backend), {method:'POST',headers:{'Content-Type':'application/json','X-Test-User':'owner',...(internal?{Authorization:`Bearer ${serviceKey}`}:{})},body:JSON.stringify(body)});
 assert.equal(response.status,200);return response.json();
}
const adminTicket = await api(sessionPath,request), connectionId=crypto.randomUUID();
const adminScope={ticket:adminTicket.ticket,roomName:adminTicket.roomName,connectionId,acquire:true};
await api('/api/internal/collaboration/authorize',adminScope,true);
const baseline=await api('/api/internal/collaboration/bootstrap',{...adminScope,acquire:false},true);
const doc = new Y.Doc();for(const bytes of baseline.updates)Y.applyUpdate(doc,Buffer.from(bytes,'base64'));const state=Array.from(Y.encodeStateAsUpdate(doc));doc.destroy();
const routes=new Map([['/',['../../WriterApp.Client/tests/journal.html','text/html']],['/web.js',['../../WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js','text/javascript']],['/device.js',['../../WriterApp.Device.Shared/wwwroot/editor/device-editor.js','text/javascript']]]);
const http=createServer(async(req,res)=>{
 try{
  const path=new URL(req.url,'http://localhost').pathname;
  res.setHeader('Cache-Control','no-store');
  if(routes.has(path)){const[file,type]=routes.get(path);res.setHeader('Content-Type',type);res.end(await readFile(new URL(file,import.meta.url)));return;}
  if(path==='/seed'){res.setHeader('Content-Type','application/json');res.end(JSON.stringify({state,documentId:request.documentId??sessionPath.split('/')[3],pageId:sessionPath.split('/')[5],generation:request.generation}));return;}
  if(path==='/api/auth/me'){res.setHeader('Content-Type','application/json');res.end(JSON.stringify({isAuthenticated:true,isPaidAccessActive:true,userId:profileOwner}));return;}
  if(path.startsWith('/api/documents/')){
   const chunks=[];for await(const chunk of req)chunks.push(chunk);
   const response=await fetch(new URL(path,backend),{method:'POST',headers:{'Content-Type':'application/json','X-Test-User':profileOwner},body:Buffer.concat(chunks)});
   const value=await response.json();if(response.ok)value.webSocketUrl=`ws://127.0.0.1:${transport.address.port}`;
   res.writeHead(response.status,{'Content-Type':'application/json'});res.end(JSON.stringify(value));return;
  }
  res.writeHead(404);res.end();
 }catch(error){res.writeHead(500);res.end('Owned test request failed');}
});
await new Promise(resolve=>http.listen(0,'127.0.0.1',resolve));const origin=`http://127.0.0.1:${http.address().port}`;
transport=createCollaborationServer({backendUrl:backend,serviceKey,environment:'test',allowedOrigins:[origin],port:0,commitsEnabled:true,checkpointEvery:2,revalidateMilliseconds:100});
transport.hocuspocus.configuration.extensions.unshift({async beforeHandleMessage({update}){const d=decoding.createDecoder(update);decoding.readVarString(d);const type=decoding.readVarUint(d);if((type===0||type===4)&&decoding.readVarUint(d)===2)stockWrites++;}});
await transport.listen();
async function launch(){
 for(let attempt=0;;attempt++){
  try{context=await chromium.launchPersistentContext(directory,{channel:'msedge',headless:true});return context;}
  catch(error){if(attempt>=8||!String(error).includes('ProcessSingleton'))throw error;await new Promise(resolve=>setTimeout(resolve,1000));}
 }
}
async function page(host='web'){const p=await context.newPage();p.on('pageerror',e=>errors.push(e.message));await p.goto(`${origin}/?host=${host}`);await p.waitForFunction(()=>window.ready);return p;}
async function check(name,run){
 await mkdir(resolve('artifacts/realtime-p6-browser'),{recursive:true});
 await writeFile(resolve('artifacts/realtime-p6-browser/progress.json'),JSON.stringify({running:name,passed:results.length}));
 await run();results.push(name);console.log(`PASS ${name}`);
}
async function saved(p,pending=undefined){await p.waitForFunction(pending=>session.status.localDurable&&(pending===undefined||session.status.pending===pending),pending);}
let a,b;
try{
 await launch();a=await page();b=await page('device');
 await check('web/device journal APIs share the schema and restore a known committed binary seed',async()=>{
  assert.deepEqual(await a.evaluate(()=>api.getSupport()),await b.evaluate(()=>api.getSupport()));
  await a.evaluate(()=>start());assert.match(await a.evaluate(()=>text()),/Seed/);
 });
 await check('rapid offline transactions atomically persist state and immutable pending identities',async()=>{
  await a.evaluate(async()=>{insert(' offline-one');insert(' offline-two');await session.flush();});await saved(a,2);
  const record=await a.evaluate(()=>store.load());assert.equal(record.pending.length,2);assert.notEqual(record.pending[0].updateId,record.pending[1].updateId);
  assert.equal(record.pending[0].installationId,record.installationId);assert.equal(record.receipts.length,0);assert.equal(await a.evaluate(()=>session.status.remoteDurable),false);
 });
 const pendingBefore=await a.evaluate(()=>session.pending);
 await check('a new page in the same origin recovers offline state and exactly the same envelopes',async()=>{
  await a.evaluate(()=>stop());await a.reload();await a.waitForFunction(()=>window.ready);await a.evaluate(()=>start());
  assert.match(await a.evaluate(()=>text()),/offline-one/);assert.deepEqual(await a.evaluate(()=>session.pending),pendingBefore);
 });
 await check('two independent tabs merge concurrent journal writers without losing pending updates',async()=>{
  await b.evaluate(()=>start());await Promise.all([a.evaluate(async()=>{insert(' tab-A');await session.flush();}),b.evaluate(async()=>{insert(' tab-B');await session.flush();})]);
  await a.waitForFunction(()=>text().includes('tab-A')&&text().includes('tab-B'));await b.waitForFunction(()=>text().includes('tab-A')&&text().includes('tab-B'));
  assert.equal((await a.evaluate(()=>store.load())).pending.length,4);
  expectedPending=await a.evaluate(()=>session.pending);
 });
 await check('backend/account/environment/generation isolation and private recovery enumeration',async()=>{
  const isolation=await a.evaluate(async()=>{const values=[];for(const change of [{ownerId:'other'},{backend:'http://localhost:9999/'},{environment:'other'},{generation:crypto.randomUUID()}]){const other=new api.IndexedDbJournal({...scope,...change},'journal-main');values.push(await other.load());await other.close();}return {values,recovery:await store.listRecovery()};});
  assert.ok(isolation.values.every(value=>value===null));assert.equal(isolation.recovery.length,1);
 });
 await check('a schema mismatch cannot overwrite the existing checkpoint',async()=>{
  assert.match(await a.evaluate(async()=>{const other=new api.IndexedDbJournal({...scope,support:{...scope.support,schemaFingerprint:'f'.repeat(64)}},'journal-main');try{await other.load();return 'unexpected';}catch(e){return String(e);}finally{await other.close();}}),/journal_invalid/);
  assert.equal((await a.evaluate(()=>store.load())).pending.length,4);
 });
 await check('real Hocuspocus reconnect commits persisted envelopes and stores exact durable receipts',async()=>{
  await a.evaluate(()=>session.connect());await a.waitForFunction(()=>session.status.remoteDurable&&session.status.pending===0);
  const record=await a.evaluate(()=>store.load());assert.equal(record.receipts.length,4);assert.deepEqual(record.receipts.map(r=>r.updateId).sort(),expectedPending.map(e=>e.updateId).sort());
  assert.equal(await a.evaluate(()=>statuses.some(value=>value.connected&&value.pending>0&&!value.remoteDurable)),true);
  const stored=await api('/api/internal/collaboration/bootstrap',{...adminScope,acquire:false},true);assert.equal(stored.committedSequence,4);assert.equal(stockWrites,0);
 });
 await check('device bundle uses an empty committed-only transport document with an offline journal',async()=>{
  // A distinct database models a separate installation; the existing tab is closed before reopening it.
  await b.evaluate(()=>stop());await b.evaluate(()=>start('journal-device'));await b.evaluate(async()=>{insert(' device-offline');await session.flush();});await saved(b,1);
  await b.evaluate(()=>session.connect());await b.waitForFunction(()=>session.status.remoteDurable&&session.status.pending===0);
  await a.waitForFunction(()=>text().includes('device-offline'));assert.equal(stockWrites,0);
 });
 await check('a fresh installation bootstraps through authenticated binary sync without an HTML seed',async()=>{
  const value=await a.evaluate(async()=>{const disk=new api.IndexedDbJournal(scope,'journal-bootstrap'), owner=new api.CollaborationAccountFence(null);owner.activate(scope,1);
   const fresh=await api.JournaledSession.open({store:disk,account:owner.capture(scope),getTicket:api.webTicketSource(scope),autoReconnect:false});
   const result={text:fresh.doc.getXmlFragment('default').toString(),page:await disk.load()};await fresh.dispose();await disk.close();owner.close();return result;
  });assert.match(value.text,/device-offline/);assert.equal(value.page.pending.length,0);assert.equal(stockWrites,0);
 });
 await check('receipt mismatch never clears a pending envelope',async()=>{
  await b.evaluate(async()=>{session.disconnect();insert(' receipt-pending');await session.flush();});
  assert.match(await b.evaluate(async()=>{const page=await store.load(), e=page.pending[0];try{await store.acknowledge({pageId:e.pageId,generation:e.generation,installationId:e.installationId,updateId:e.updateId,sha256:'f'.repeat(64),commitSequence:999,committedAtUnixMilliseconds:Date.now()});return 'unexpected';}catch(e){return String(e);}}),/receipt_invalid/);
  assert.equal((await b.evaluate(()=>store.load())).pending.length,1);
 });
 await check('an unavailable generation stays a private recovery copy and does not retry delivery automatically',async()=>{
  const value=await a.evaluate(async()=>{
   const savedScope={...scope,generation:crypto.randomUUID()}, disk=new api.IndexedDbJournal(savedScope,'journal-revoked');
   const owner=new api.CollaborationAccountFence(null);owner.activate(savedScope,1);
   const privateSession=await api.JournaledSession.open({store:disk,account:owner.capture(savedScope),committedSeed:new Uint8Array(seed.state),autoReconnect:false,getTicket:api.webTicketSource(savedScope)});
   privateSession.doc.getXmlFragment('default').get(0).get(0).insert(0,'private recovery ');await privateSession.flush();const before=privateSession.pending;
   await privateSession.connect().catch(()=>{});await privateSession.flush();const after=await disk.load();const error=await privateSession.connect().then(()=>'',e=>String(e));
   await privateSession.dispose();await disk.close();owner.close();return {before,after,error};
  });assert.equal(value.after.recoveryReason,'access_unavailable');assert.deepEqual(value.after.pending,value.before);assert.match(value.error,/private_recovery_required/);
 });
 await check('corrupt IndexedDB source is refused rather than reseeded or overwritten',async()=>{
  const value=await a.evaluate(async()=>{
   const chosen={...scope,pageId:crypto.randomUUID()}, disk=new api.IndexedDbJournal(chosen,'journal-corrupt');await disk.initialize(new Uint8Array(seed.state));
   const raw=await new Promise((resolve,reject)=>{const open=indexedDB.open('journal-corrupt',1);open.onsuccess=()=>resolve(open.result);open.onerror=()=>reject(open.error);});
   await new Promise((resolve,reject)=>{const tx=raw.transaction('pages','readwrite'),store=tx.objectStore('pages'),get=store.getAll();get.onsuccess=()=>store.put({...get.result[0],sha256:'f'.repeat(64)});tx.oncomplete=resolve;tx.onabort=()=>reject(tx.error);});
   const error=await disk.initialize(new Uint8Array(seed.state)).then(()=>'',e=>String(e));
   const unchanged=await new Promise(resolve=>{const get=raw.transaction('pages').objectStore('pages').getAll();get.onsuccess=()=>resolve(get.result[0].sha256);});
   raw.close();await disk.close();return {error,unchanged};
  });assert.match(value.error,/journal_invalid/);assert.equal(value.unchanged,'f'.repeat(64));
 });
 await check('account generation changes stop transport and fence a late ticket callback',async()=>{
  await a.evaluate(async()=>{session.disconnect();window.resolveTicket=null;const base=await store.load();window.lateStore=new api.IndexedDbJournal({...scope,pageId:crypto.randomUUID()},'journal-late');window.lateFence=new api.CollaborationAccountFence(null);lateFence.activate(lateStore.scope,1);window.late=await api.JournaledSession.open({store:lateStore,account:lateFence.capture(lateStore.scope),committedSeed:new Uint8Array(seed.state),autoReconnect:false,getTicket:()=>new Promise(resolve=>resolveTicket=resolve)});window.lateConnect=late.connect().catch(e=>String(e));});
  await a.waitForFunction(()=>!!resolveTicket);await a.evaluate(()=>{lateFence.activate(null,2);resolveTicket({});});assert.match(await a.evaluate(()=>lateConnect),/account_changed/);
  assert.equal(await a.evaluate(()=>late.status.accountChanged),true);await a.evaluate(async()=>{await late.dispose();await lateStore.close();});
 });
 await check('denied access keeps a durable private recovery copy and prevents automatic delivery',async()=>{
  profileOwner='other';await b.evaluate(()=>session.connect().catch(e=>String(e)));profileOwner='owner';
  // Profile/account mismatch is fenced immediately, preserving its old account record.
  assert.equal(await b.evaluate(()=>session.status.accountChanged),true);assert.equal((await b.evaluate(()=>store.load())).pending.length,1);
  await a.waitForFunction(()=>session.status.accountChanged);await a.evaluate(async()=>{await stop();await start();});
 });
 await check('aborted IndexedDB writes and injected quota errors preserve the old atomic checkpoint and unsaved bytes',async()=>{
  const prior=await a.evaluate(()=>store.load());
  await a.evaluate(()=>{window.originalJournalPut=IDBObjectStore.prototype.put;window.abortJournalWrite=true;IDBObjectStore.prototype.put=function(...args){const result=originalJournalPut.apply(this,args);if(this.name==='pages'&&abortJournalWrite){abortJournalWrite=false;this.transaction.abort();}return result;};insert(' quota-recovery '.repeat(5000));});
  await a.waitForFunction(()=>session.status.error==='collaboration_local_save_failed');assert.equal(await a.evaluate(()=>session.status.localDurable),false);
  assert.deepEqual(await a.evaluate(()=>store.load()),prior);
  assert.match(await a.evaluate(()=>session.exportRecovery().state),/^[A-Za-z0-9+/]+=*$/);
  await a.evaluate(async()=>{IDBObjectStore.prototype.put=originalJournalPut;window.originalJournalSave=store.save.bind(store);store.save=async()=>{throw new DOMException('Owned quota fault injection','QuotaExceededError');};await session.retryLocalPersistence().catch(()=>{});});
  assert.equal(await a.evaluate(()=>session.status.localDurable),false);assert.deepEqual(await a.evaluate(()=>store.load()),prior);
  assert.match(await a.evaluate(()=>session.dispose().then(()=>'',e=>String(e))),/local_save_failed/);assert.match(await a.evaluate(()=>text()),/quota-recovery/);
  await a.evaluate(async()=>{store.save=originalJournalSave;await session.retryLocalPersistence();});await saved(a,1);assert.match(await a.evaluate(()=>text()),/quota-recovery/);
 });
 const crashBefore=await a.evaluate(()=>session.pending);
 await check('forced browser-process death restores the exact durable checkpoint/outbox without a lifecycle save',async()=>{
  const cdp=await context.browser().newBrowserCDPSession();
  const {processInfo}=await cdp.send('SystemInfo.getProcessInfo');const owned=processInfo.find(info=>info.type==='browser');
  assert.ok(owned&&Number.isInteger(Number(owned.id))&&Number(owned.id)>0);
  // The PID comes from THIS owned persistent browser's CDP session; never kill by executable name.
  process.kill(Number(owned.id),'SIGKILL');
  await Promise.race([context.close().catch(()=>{}),new Promise(resolve=>setTimeout(resolve,2000))]);
  await launch();a=await page();await a.evaluate(()=>start());assert.deepEqual(await a.evaluate(()=>session.pending),crashBefore);assert.match(await a.evaluate(()=>text()),/quota-recovery/);
 });
 await check('fresh-process reconnect replays original IDs and local acknowledgement survives another restart',async()=>{
  await a.evaluate(()=>session.connect());await a.waitForFunction(()=>session.status.remoteDurable&&session.status.pending===0);
  const final=await a.evaluate(()=>store.load());assert.ok(final.receipts.some(r=>r.updateId===crashBefore[0].updateId));assert.equal(stockWrites,0);
  await a.evaluate(()=>stop());await a.reload();await a.waitForFunction(()=>window.ready);await a.evaluate(()=>start());assert.equal((await a.evaluate(()=>store.load())).pending.length,0);
 });
 assert.deepEqual(errors,[]);
 const out=resolve('artifacts/realtime-p6-browser');await mkdir(out,{recursive:true});await writeFile(join(out,'results.json'),JSON.stringify({passed:results.length,results,stockWrites,errors},null,2));
 console.log(`PASS: ${results.length} browser journal, real-provider, quota and forced-process recovery checks`);
}finally{
 await context?.close().catch(()=>{});await transport.destroy();http.closeAllConnections();await new Promise(resolve=>http.close(resolve));
 // Only this mkdtemp-created browser profile is owned by the probe.
 await rm(directory,{recursive:true,force:true});
}

import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,mkdir,writeFile} from 'node:fs/promises';
import {createRequire} from 'node:module';
import * as Y from 'yjs';
import * as decoding from 'lib0/decoding';
import {createCollaborationServer} from '../src/server.mjs';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const backend=process.env.PROBE_BACKEND_URL,path=process.env.PROBE_SESSION_PATH,request=JSON.parse(process.env.PROBE_SESSION_REQUEST),serviceKey=process.env.COLLABORATION_SERVICE_KEY;
let transport,browser,stockWrites=0;const errors=[],checks=[];
async function post(actor,url,body,internal=false){const r=await fetch(new URL(url,backend),{method:'POST',headers:{'Content-Type':'application/json','X-Test-User':actor,...internal?{Authorization:`Bearer ${serviceKey}`} : {}},body:JSON.stringify(body)});assert.equal(r.status,200);return r.json();}
const ticket=await post('owner',path,request),scope={ticket:ticket.ticket,roomName:ticket.roomName,connectionId:crypto.randomUUID(),acquire:true};
await post('owner','/api/internal/collaboration/authorize',scope,true);const bootstrap=await post('owner','/api/internal/collaboration/bootstrap',{...scope,acquire:false},true);
const doc=new Y.Doc();for(const u of bootstrap.updates)Y.applyUpdate(doc,Buffer.from(u,'base64'));const seed=Array.from(Y.encodeStateAsUpdate(doc));doc.destroy();
const ids={documentId:path.split('/')[3],pageId:path.split('/')[5],generation:request.generation};
const html=`<!doctype html><meta charset="utf-8"><title>Owned interaction probe</title><div id="editor"></div><script type="module">
const host=new URLSearchParams(location.search).get('host'),actor=new URLSearchParams(location.search).get('actor');
if(host==='web'){await new Promise(r=>{const s=document.createElement('script');s.src='/web.js';s.onload=r;document.head.append(s);});window.api=tiptapEditor.collaboration;}else window.api=(await import('/device.js')).collaboration;
const config=await(await fetch('/seed')).json(),support=await api.getSupport();window.scope={...config.ids,environment:'test',backend:location.origin+'/',ownerId:actor,support,membershipRevision:actor==='owner'?0:1};
window.store=new api.IndexedDbJournal(scope,'p9-real-'+actor);window.accountFence=new api.CollaborationAccountFence(null);accountFence.activate(scope,1);
window.binding=await api.createJournaledEditor(document.getElementById('editor'),{store,account:accountFence.capture(scope),committedSeed:new Uint8Array(config.seed),getTicket:api.webTicketSource(scope),interactionsEnabled:true},
{documentId:scope.documentId,mode:2,featureAvailable:true,canRead:true,canWriteCollaboratively:true,membershipRevision:scope.membershipRevision,pages:[{...support,pageId:scope.pageId,generation:scope.generation}]});
window.commentPath='api/documents/'+scope.documentId+'/pages/'+scope.pageId+'/collaboration/comments';
window.call=async(url,body)=>{const r=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});return {status:r.status,value:await r.json()};};
window.comments=async()=>{const r=await call(commentPath+'/read',{generation:scope.generation});if(r.status!==200)throw Error('comments unavailable');binding.interactions.setThreads(r.value.threads);return r.value;};
window.ready=true;</script>`;
const http=createServer(async(req,res)=>{
 try{const url=new URL(req.url,'http://localhost'),actor=req.headers['x-test-actor']||'owner';res.setHeader('Cache-Control','no-store');
 if(url.pathname==='/'){res.setHeader('Content-Type','text/html');res.end(html);return;}
 const bundle=url.pathname==='/web.js'?'../../WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js':url.pathname==='/device.js'?'../../WriterApp.Device.Shared/wwwroot/editor/device-editor.js':null;
 if(bundle){res.setHeader('Content-Type','text/javascript');res.end(await readFile(new URL(bundle,import.meta.url)));return;}
 res.setHeader('Content-Type','application/json');if(url.pathname==='/seed'){res.end(JSON.stringify({seed,ids}));return;}
 if(url.pathname==='/api/auth/me'){res.end(JSON.stringify({isAuthenticated:true,userId:actor}));return;}
 if(url.pathname.startsWith('/api/')){const chunks=[];for await(const chunk of req)chunks.push(chunk);const response=await fetch(new URL(url.pathname,backend),{method:req.method,headers:{'Content-Type':'application/json','X-Test-User':actor},...req.method==='POST'?{body:Buffer.concat(chunks)}:{}});const value=await response.json();if(value.webSocketUrl)value.webSocketUrl='ws://127.0.0.1:'+transport.address.port;res.writeHead(response.status);res.end(JSON.stringify(value));return;}
 res.writeHead(404);res.end('{}');
 }catch{res.writeHead(500);res.end('{}');}
});await new Promise(r=>http.listen(0,'127.0.0.1',r));const origin='http://127.0.0.1:'+http.address().port;
transport=createCollaborationServer({backendUrl:backend,serviceKey,environment:'test',allowedOrigins:[origin],port:0,commitsEnabled:true,interactionsEnabled:true,revalidateMilliseconds:100});
transport.hocuspocus.configuration.extensions.unshift({async beforeHandleMessage({update}){const d=decoding.createDecoder(update);decoding.readVarString(d);const t=decoding.readVarUint(d);if((t===0||t===4)&&decoding.readVarUint(d)===2)stockWrites++;}});await transport.listen();
const check=async(name,run)=>{await run();checks.push(name);};const durable=p=>p.waitForFunction(()=>binding.session.status.remoteDurable&&binding.session.status.pending===0);
try{
 browser=await chromium.launch({channel:'msedge',headless:true});const a=await browser.newPage({extraHTTPHeaders:{'X-Test-Actor':'owner'}}),b=await browser.newPage({extraHTTPHeaders:{'X-Test-Actor':'editor'}});
 for(const p of[a,b])p.on('pageerror',e=>{errors.push(e.message);console.error('Owned browser probe:',e.message);});await Promise.all([a.goto(origin+'/?host=web&actor=owner'),b.goto(origin+'/?host=device&actor=editor')]);for(const p of[a,b]){await p.waitForFunction(()=>window.ready);await durable(p);}
 await check('real web/device journals show authenticated display names in rosters and remote carets',async()=>{
  await new Promise(r=>setTimeout(r,250));for(const p of[a,b])await p.evaluate(()=>binding.session.publishPresence(binding.interactions.selectionAnchor()));
  for(const [page,remoteName] of [[a,'Sam Editor'],[b,'Alex Writer']]) {
   await page.waitForFunction(name=>document.querySelector('.collaboration-presence').textContent.includes('Alex Writer')&&document.querySelector('.collaboration-presence').textContent.includes('Sam Editor')&&document.querySelector('.collaboration-remote-caret')?.textContent===name,remoteName);
   assert.equal(await page.locator('.collaboration-remote-caret').getAttribute('aria-label'),remoteName+' cursor');
   assert.deepEqual((await page.locator('.collaboration-person-name').allTextContents()).sort(),['Alex Writer','Sam Editor']);
   assert.equal(await page.locator('.collaboration-presence').getAttribute('data-state'),'live');
   assert.equal(await page.locator('.collaboration-people-count').innerText(),'2 people');
  }
 });
 await check('participant popover is keyboard accessible and does not change writing or selection',async()=>{
  for(const p of[a,b]) {
   const before=await p.evaluate(()=>({html:binding.editor.getHTML(),selection:binding.editor.state.selection.toJSON()}));
   const button=p.getByRole('button',{name:'Show 2 people on this page'});
   await button.focus();await button.press('Enter');
   assert.equal(await button.getAttribute('aria-expanded'),'true');
   assert.equal(await p.getByRole('region',{name:'People on this page'}).isVisible(),true);
   assert.equal(await p.locator('.collaboration-person-meta').filter({hasText:'You'}).count(),1);
   const color=await p.locator('.collaboration-remote-caret').evaluate(e=>e.style.getPropertyValue('--participant-color'));
   assert.equal(await p.locator('.collaboration-person').filter({hasText:p===a?'Sam Editor':'Alex Writer'}).locator('.collaboration-avatar').evaluate(e=>e.style.getPropertyValue('--participant-color')),color);
   await button.press('Escape');assert.equal(await button.getAttribute('aria-expanded'),'false');
   assert.equal(await button.evaluate(e=>e===document.activeElement),true);
   assert.deepEqual(await p.evaluate(()=>({html:binding.editor.getHTML(),selection:binding.editor.state.selection.toJSON()})),before);
  }
 });
 await check('duplicate account connections do not duplicate people or remote cursors',async()=>{
  const extra=await browser.newPage({extraHTTPHeaders:{'X-Test-Actor':'editor'}});
  try {
   await extra.goto(origin+'/?host=device&actor=editor');await extra.waitForFunction(()=>window.ready);await durable(extra);
   await extra.evaluate(()=>binding.session.publishPresence(binding.interactions.selectionAnchor()));
   await a.waitForFunction(()=>binding.session.presenceMembers.length===3);
   assert.equal(await a.locator('.collaboration-person').count(),2);
   assert.equal(await a.locator('.collaboration-remote-caret').count(),1);
  } finally { await extra.close(); }
 });
 await check('participant bar and popover fit narrow screens and are excluded from print',async()=>{
  await mkdir('artifacts/collaborator-ui',{recursive:true});
  for(const [p,host] of [[a,'web'],[b,'device']]) {
   await p.setViewportSize({width:360,height:740});
   await p.getByRole('button',{name:'Show 2 people on this page'}).click();
   assert.equal(await p.locator('.collaboration-presence').evaluate(e=>{const r=e.getBoundingClientRect(),panel=e.querySelector('.collaboration-people-panel').getBoundingClientRect();return e.scrollWidth<=e.clientWidth&&r.left>=0&&r.right<=innerWidth&&panel.left>=0&&panel.right<=innerWidth;}),true);
   await p.waitForFunction(()=>{const badge=document.querySelector('.collaboration-remote-caret-label'),editor=document.querySelector('.ProseMirror');if(!badge||!editor)return false;const r=badge.getBoundingClientRect(),bounds=editor.getBoundingClientRect();return r.top>=bounds.top&&r.left>=0&&r.right<=innerWidth;});
   await p.screenshot({path:`artifacts/collaborator-ui/${host}-narrow.png`,fullPage:true});
   await p.emulateMedia({media:'print'});assert.equal(await p.locator('.collaboration-presence').isVisible(),false);assert.equal(await p.locator('.collaboration-remote-caret').isVisible(),false);
   await p.emulateMedia({media:'screen'});await p.setViewportSize({width:1280,height:900});
   await p.screenshot({path:`artifacts/collaborator-ui/${host}-participants.png`,fullPage:true});
   await p.locator('.ProseMirror').click();assert.equal(await p.locator('.collaboration-people-button').getAttribute('aria-expanded'),'false');
  }
 });
 await check('disconnect removes stale people and shows connection feedback before reconnecting',async()=>{
  await a.getByRole('button',{name:'Show 2 people on this page'}).click();
  await a.evaluate(()=>binding.session.disconnect());
  assert.equal(await a.locator('.collaboration-presence').getAttribute('data-state'),'offline');
  assert.equal(await a.locator('.collaboration-presence-status').innerText(),'Not connected');
  assert.equal(await a.locator('.collaboration-person').count(),0);
  assert.equal(await a.locator('.collaboration-people-button').isDisabled(),true);
  assert.equal(await a.locator('.collaboration-people-panel').isVisible(),false);
  await a.evaluate(()=>binding.session.connect());await durable(a);
  await a.waitForFunction(()=>document.querySelector('.collaboration-people-count').textContent==='2 people');
 });
 const threadId=crypto.randomUUID();
 await check('owner creates a committed relative comment that the device editor can read and highlight',async()=>{
  const result=await a.evaluate(async id=>{binding.editor.commands.setTextSelection({from:1,to:5});const current=await comments();return call(commentPath,{operationId:id,generation:scope.generation,membershipRevision:0,expectedSequence:current.sequence,anchor:binding.interactions.selectionAnchor(),body:'Shared passage comment'});},threadId);assert.equal(result.status,200);
  await b.evaluate(()=>comments());assert.equal(await b.locator('.collaboration-comment-anchor').count(),1);
 });
 await check('a durable remote insertion keeps the comment on its original passage',async()=>{
  await b.evaluate(()=>binding.editor.commands.insertContentAt(1,'Other '));await durable(b);await a.waitForFunction(()=>binding.editor.getText().includes('Other'));
  await a.evaluate(()=>comments());assert.equal(await a.evaluate(id=>{binding.interactions.focusThread(id);return binding.editor.state.doc.textBetween(binding.editor.state.selection.from,binding.editor.state.selection.to);},threadId),'Seed');
 });
 await check('editor reply and owner resolution use revisioned HTTP authority',async()=>{
  assert.equal((await b.evaluate(async id=>{const c=await comments(),t=c.threads[0];return call(commentPath+'/'+id,{operationId:crypto.randomUUID(),generation:scope.generation,membershipRevision:1,expectedRevision:t.revision,action:'reply',body:'Device reply'});},threadId)).status,200);
  for(const action of['resolve','reopen'])assert.equal((await a.evaluate(async({id,action})=>{const c=await comments();return call(commentPath+'/'+id,{operationId:crypto.randomUUID(),generation:scope.generation,membershipRevision:0,expectedRevision:c.threads[0].revision,action});},{id:threadId,action})).status,200);
 });
 await check('undo and redo of local writing preserve the other author durable insertion',async()=>{
  await a.evaluate(()=>binding.editor.commands.insertContentAt(binding.editor.state.doc.content.size-1,' Mine'));await durable(a);await a.evaluate(()=>binding.editor.commands.undo());await durable(a);assert.equal(await a.evaluate(()=>binding.editor.getText()),'Other Seed');
  await a.evaluate(()=>binding.editor.commands.redo());await durable(a);assert.equal(await a.evaluate(()=>binding.editor.getText()),'Other Seed Mine');
 });
 await check('deleting the passage detaches the server comment and removes both client highlights',async()=>{
  await b.evaluate(async id=>{await comments();binding.interactions.focusThread(id);binding.editor.commands.deleteSelection();},threadId);await durable(b);await a.waitForFunction(()=>!binding.editor.getText().includes('Seed'));
  for(const p of[a,b]){const c=await p.evaluate(()=>comments());assert.equal(c.threads[0].detached,true);assert.equal(await p.locator('.collaboration-comment-anchor').count(),0);}
 });
 await check('revocation removes participant state, blocks comments and retains private journal data',async()=>{
  await post('owner',`/api/collaboration/manuscripts/${ids.documentId}/members/editor/change`,{expectedRevision:1,role:1,revoke:true});
  await b.waitForFunction(()=>binding.session.status.recoveryReason==='access_unavailable');await a.waitForFunction(()=>!document.querySelector('.collaboration-presence').textContent.includes('Sam Editor'));
  assert.equal(await b.locator('.collaboration-presence').getAttribute('data-state'),'unavailable');
  assert.equal((await b.evaluate(()=>call(commentPath+'/read',{generation:scope.generation}))).status,403);assert.ok(await b.evaluate(async()=>Boolean(await store.load())));
 });
 await check('account invalidation freezes the old editor and standard provider text uploads stay zero',async()=>{await a.evaluate(()=>accountFence.activate(null,2));assert.equal(await a.evaluate(()=>binding.editor.isEditable),false);assert.equal(stockWrites,0);assert.deepEqual(errors,[]);});
 await mkdir('artifacts/realtime-p9-interactions',{recursive:true});await writeFile('artifacts/realtime-p9-interactions/browser-results.json',JSON.stringify({passed:checks.length,checks,stockWrites,errors,scope:'Real Kestrel/Hocuspocus plus rebuilt web/device bundles in desktop Edge. Synthetic authenticated accounts and capability activation. No packaged/native acceptance.'},null,2));
 console.log(`PASS: ${checks.length} real cross-bundle presence, comments, committed anchors, undo and revocation journeys`);
}finally{await browser?.close();await transport.destroy();await new Promise(r=>http.close(r));}

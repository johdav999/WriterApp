import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { randomUUID } from 'node:crypto';
import * as Y from '../node_modules/yjs/dist/yjs.mjs';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const output = resolve(process.env.EDITOR_EVIDENCE_DIR || 'artifacts/realtime-p3');
await mkdir(output,{recursive:true});
const routes=new Map([
 ['/',['./collaboration.html','text/html']],
 ['/web.js',['../wwwroot/js/tiptap-editor.bundle.js','text/javascript']],
 ['/device.js',['../../WriterApp.Device.Shared/wwwroot/editor/device-editor.js','text/javascript']]
]);
const server=createServer(async(req,res)=>{
 const route=routes.get(new URL(req.url,'http://localhost').pathname);
 if(!route){res.writeHead(404);res.end();return;}
 try{res.setHeader('Content-Type',route[1]);res.setHeader('Cache-Control','no-store');res.end(await readFile(new URL(route[0],import.meta.url)));}
 catch{res.writeHead(500);res.end();}
});
await new Promise(done=>server.listen(0,'127.0.0.1',done));
let browser;const checks=[],errors=[],networkWrites=[];
const check=async(name,run)=>{if(process.env.EDITOR_CHECK_FILTER&&!name.includes(process.env.EDITOR_CHECK_FILTER))return;await run();checks.push(name);};
try {
 browser=await chromium.launch({channel:process.env.EDITOR_BROWSER_CHANNEL || 'msedge',headless:true});
 const a=await browser.newPage(),b=await browser.newPage();
 for(const page of [a,b]){
  page.on('pageerror',error=>errors.push(error.message));
  page.on('request',request=>{if(['POST','PUT','PATCH','DELETE'].includes(request.method()))networkWrites.push(request.url());});
 }
 const base=`http://127.0.0.1:${server.address().port}`;
 await Promise.all([a.goto(base+'/?host=web'),b.goto(base+'/?host=device')]);
 await Promise.all([a.waitForFunction(()=>window.ready),b.waitForFunction(()=>window.ready)]);
 const identity={environment:'isolated',documentId:randomUUID(),pageId:randomUUID(),generation:randomUUID()};
 const seed=await a.evaluate(async()=>{const seed=await api.createIsolatedSeed('<p>Base text.</p>');return {...seed,state:Array.from(seed.state)};});
 const mount=async(page,room=identity,initial=seed)=>page.evaluate(async({room,initial})=>{updates.length=0;projections.length=0;await mount(room,initial);},{room,initial});
 const drain=page=>page.evaluate(()=>updates.splice(0));
 const deliver=async(page,updates)=>{for(const update of updates)await page.evaluate(update=>receive(update),update);};
 const html=page=>page.evaluate(()=>session.editor.getHTML());
 const converge=async()=>{
  for(let i=0;i<5;i++){const au=await drain(a),bu=await drain(b);if(!au.length&&!bu.length)return;await deliver(b,au);await deliver(a,bu);}
  assert.fail('Remote application echoed more local updates');
 };
 await check('Both shipped bundles expose the identical contract/schema fingerprint',async()=>assert.deepEqual(await a.evaluate(()=>api.getSupport()),await b.evaluate(()=>api.getSupport())));
 await Promise.all([mount(a),mount(b)]);
 await check('Simultaneous real keyboard typing converges across independent web/device editors',async()=>{
  await Promise.all([a.evaluate(()=>session.editor.commands.setTextSelection(1)),b.evaluate(()=>session.editor.commands.setTextSelection(1))]);
  await a.locator('.ProseMirror').pressSequentially('Alice ');await b.locator('.ProseMirror').pressSequentially('Bob ');
  await converge();assert.equal(await html(a),await html(b));assert.match(await html(a),/Alice/);assert.match(await html(a),/Bob/);
 });
 await check('Remote application emits no local transport update',async()=>{
  await a.evaluate(()=>session.editor.commands.insertContentAt(1,'Remote '));const outgoing=await drain(a);assert.ok(outgoing.length);
  await deliver(b,outgoing);assert.equal((await drain(b)).length,0);assert.equal(await html(a),await html(b));
 });
 await check('A remote insertion preserves the other editor’s selected passage',async()=>{
  const selected=await b.evaluate(()=>{session.editor.commands.setTextSelection({from:3,to:7});const {from,to}=session.editor.state.selection;return session.editor.state.doc.textBetween(from,to);});
  await a.evaluate(()=>session.editor.commands.insertContentAt(1,'prefix '));await converge();
  assert.equal(await b.evaluate(()=>{const {from,to}=session.editor.state.selection;return session.editor.state.doc.textBetween(from,to);}),selected);
 });
 await check('Local undo and redo retain the other author’s independent text',async()=>{
  await a.evaluate(()=>{session.stopUndoCapture();session.editor.commands.insertContentAt(1,'LOCAL ');session.stopUndoCapture();});await converge();
  await b.evaluate(()=>{session.stopUndoCapture();session.editor.commands.insertContentAt(session.editor.state.doc.content.size-1,' PEER');session.stopUndoCapture();});await converge();
  await a.evaluate(()=>session.editor.commands.undo());await converge();assert.doesNotMatch(await html(a),/LOCAL/);assert.match(await html(a),/PEER/);
  await a.evaluate(()=>session.editor.commands.redo());await converge();assert.match(await html(a),/LOCAL/);assert.match(await html(a),/PEER/);
 });
 await check('Reordered and duplicated updates converge after offline edits',async()=>{
  for(const [sender,receiver] of [[a,b],[b,a]]){
   await sender.evaluate(()=>{session.editor.commands.insertContentAt(1,'one ');session.editor.commands.insertContentAt(1,'two ');session.editor.commands.deleteRange({from:1,to:3});});
   const outgoing=await drain(sender);assert.ok(outgoing.length>=3);
   const before=await html(receiver),snapshot=await receiver.evaluate(()=>Array.from(session.snapshot()));
   await deliver(receiver,[...outgoing.slice(1)].reverse());await deliver(receiver,[outgoing.at(-1)]);
   assert.equal(await html(receiver),before,'Incomplete deltas must not change visible text');
   assert.deepEqual(await receiver.evaluate(()=>Array.from(session.snapshot())),snapshot,'Snapshots must remain complete');
   assert.equal((await drain(receiver)).length,0,'Deferred remote updates must not echo');
   await receiver.evaluate(()=>session.editor.commands.insertContentAt(1,'While waiting '));
   await deliver(sender,await drain(receiver));
   await deliver(receiver,[outgoing[0]]);await deliver(receiver,outgoing);
   assert.equal(await html(sender),await html(receiver));assert.equal((await drain(receiver)).length,0);
  }
 });
 await check('Deferred unsupported content is rejected without poisoning subsequent valid updates',async()=>{
  for(const receiver of [a,b]){
   const before=await html(receiver),doc=new Y.Doc(),updates=[];
   Y.applyUpdate(doc,new Uint8Array(await receiver.evaluate(()=>Array.from(session.snapshot()))));
   doc.on('update',bytes=>updates.push({identity,support:seed.support,bytes:Array.from(bytes)}));
   const element=new Y.XmlElement('futureNode'),text=new Y.XmlText('Unknown');element.insert(0,[text]);
   doc.getXmlFragment('default').insert(0,[element]);text.insert(text.length,' deferred');
   await deliver(receiver,[updates[1]]);assert.equal(await html(receiver),before);
   assert.equal(await receiver.evaluate(update=>{try{receive(update);return false;}catch{return true;}},updates[0]),true);
   assert.equal(await html(receiver),before);doc.destroy();
  }
  await a.evaluate(()=>session.editor.commands.insertContentAt(1,'After rejected update '));
  await converge();assert.equal(await html(a),await html(b));
 });
 await check('Concurrent deletion and formatting converge without replacing HTML',async()=>{
  await a.evaluate(()=>session.editor.chain().setTextSelection({from:1,to:4}).toggleBold().run());
  await b.evaluate(()=>session.editor.commands.deleteRange({from:5,to:8}));await converge();assert.equal(await html(a),await html(b));
 });
 await check('Full HTML replacement APIs refuse collaborative editors',async()=>{
  const before=await html(a);
  const failure=await a.evaluate(()=>{try{tiptapEditor.setContent(session.editor,'<p>overwrite</p>');}catch(e){return e.message;}});
  assert.match(failure,/collaboration_legacy_write_blocked/);assert.equal(await html(a),before);
  assert.match(await b.evaluate(()=>{try{session.replaceContent('<p>overwrite</p>');}catch(e){return e.message;}}),/collaboration_legacy_write_blocked/);
  assert.match(await b.evaluate(()=>{try{session.editor.commands.setContent('<p>overwrite</p>');}catch(e){return e.message;}}),/collaboration_legacy_write_blocked/);
 });
 await check('Binary snapshots with unknown nodes or marks are refused without disposing the active editor',async()=>{
  const before=await html(a);
  for(const unknown of ['node','mark','attribute']){
   const doc=new Y.Doc(),element=new Y.XmlElement(unknown==='node'?'futureNode':'paragraph'),text=new Y.XmlText('Unknown source');
   if(unknown==='attribute')element.setAttribute('futureAttribute','retain');
   element.insert(0,[text]);doc.getXmlFragment('default').insert(0,[element]);
   if(unknown==='mark')text.format(0,text.length,{futureMark:{}});
   const state=Array.from(Y.encodeStateAsUpdate(doc));doc.destroy();
   assert.ok(await a.evaluate(async({identity,seed,state})=>{try{await api.createIsolatedEditor(document.getElementById('editor'),identity,{support:seed.support,state:new Uint8Array(state)});return false;}catch{return true;}},{identity,seed,state}),unknown);
   assert.equal(await html(a),before);
  }
 });
 await check('Wrong page/generation/schema and malformed bytes are rejected before changing state',async()=>{
  await a.evaluate(()=>session.editor.commands.insertContentAt(1,'valid '));const update=(await drain(a)).at(-1),before=await html(b);
  for(const bad of [{...update,identity:{...update.identity,generation:randomUUID()}},{...update,support:{...update.support,schemaFingerprint:'unknown'}},{...update,bytes:[255,255,255]}])
   assert.ok(await b.evaluate(bad=>{try{receive(bad);return false;}catch{return true;}},bad));
  assert.equal(await html(b),before);await deliver(b,[update]);assert.equal(await html(a),await html(b));
 });
 await check('Unknown JSON nodes and HTML formatting are rejected before seeding',async()=>{
  for(const [content,format] of [['{"type":"doc","content":[{"type":"futureNode"}]}','LegacyJson'],['<p data-future="keep">Never strip</p>','Html']])
   assert.ok(await a.evaluate(async({content,format})=>{try{await api.createIsolatedSeed(content,format);return false;}catch{return true;}},{content,format}));
 });
 await check('Rich-content seed retains headings, lists, tables, links, image identity, alignment and indentation',async()=>{
  const fixture='<h2 style="text-align:center" data-indent-level="2">Heading 日本語 🧭</h2><p><strong>Bold</strong> <em>italic</em> <u>underline</u> <s>strike</s> <a href="https://example.com">link</a></p><ol start="3"><li><p>Numbered</p></li></ol><blockquote><p>Quote</p></blockquote><pre><code class="language-js">let x = 1;</code></pre><table><tbody><tr><th align="right"><p>Header</p></th><td colspan="2" colwidth="120,160"><p>Cell</p></td></tr></tbody></table><img src="/missing-image.png" data-asset-id="asset-123" data-asset-url="/missing-image.png" width="80" alt="Reference"><p>Tail</p>';
  const rich=await a.evaluate(async fixture=>{const seed=await api.createIsolatedSeed(fixture);return {...seed,state:Array.from(seed.state)};},fixture);
  await Promise.all([mount(a,identity,rich),mount(b,identity,rich)]);assert.equal(await html(a),await html(b));
  const json=await a.evaluate(()=>JSON.stringify(session.editor.getJSON()));
  for(const value of ['heading','orderedList','table','tableHeader','colwidth','asset-123','indentLevel','center','right','language','underline'])assert.ok(json.includes(value),value);
  await a.screenshot({path:resolve(output,'web-editor.png')});await b.screenshot({path:resolve(output,'device-editor.png')});
 });
 await check('The P2 disabled capability and the P3 live gate cannot be bypassed',async()=>{
  const message=await a.evaluate(async identity=>{const support=await api.getSupport();const caps={documentId:identity.documentId,mode:2,featureAvailable:false,canRead:true,canWriteCollaboratively:false,pages:[{...support,pageId:identity.pageId,generation:identity.generation}]};
   let blocked=0;for(const grant of [false,true]){caps.featureAvailable=grant;caps.canWriteCollaboratively=grant;try{await api.createLiveEditor(document.getElementById('editor'),identity,caps);}catch{blocked++;}}return blocked;},identity);
  assert.equal(message,2);
 });
 await check('Live entry reads the capability endpoint and fails closed on unavailable access',async()=>{
  let lookups=0;
  await a.route('**/api/documents/*/collaboration/capabilities?*',async route=>{
   const url=new URL(route.request().url());lookups++;assert.equal(route.request().method(),'GET');
   assert.equal(url.searchParams.get('pageId'),identity.pageId);assert.equal(url.searchParams.get('generation'),identity.generation);
   assert.equal(url.searchParams.get('schemaFingerprint'),seed.support.schemaFingerprint);
   if(lookups===2){await route.fulfill({status:503,body:'Unavailable'});return;}
   await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({documentId:identity.documentId,mode:2,featureAvailable:false,canRead:true,canWriteCollaboratively:false,pages:[{...seed.support,pageId:identity.pageId,generation:identity.generation}]})});
  });
  const before=await html(a);
  for(let i=0;i<2;i++)assert.ok(await a.evaluate(async identity=>{try{await api.createLiveEditor(document.getElementById('editor'),identity);return false;}catch{return true;}},identity));
  assert.equal(lookups,2);assert.equal(await html(a),before);await a.unroute('**/api/documents/*/collaboration/capabilities?*');
 });
 await check('Page switching disposes the prior binding and double-destroy is safe',async()=>{
  const result=await a.evaluate(async({identity,seed})=>{const previous=session;await mount({...identity,pageId:crypto.randomUUID()},seed);let refused=false;try{previous.receive({identity,support:seed.support,bytes:new Uint8Array(seed.state)});}catch{refused=true;}previous.destroy();return {refused,count:document.querySelectorAll('.ProseMirror').length};},{identity,seed});
  assert.deepEqual(result,{refused:true,count:1});
  await a.evaluate(()=>{session.destroy();session.destroy();});assert.equal(await a.locator('.ProseMirror').count(),0);
 });
 await check('Both bundles bind real editors to atomic journals and preserve pending edits through reopen',async()=>{
  for(const page of [a,b]){
   const result=await page.evaluate(async({identity,seed})=>{
    try{window.session?.destroy();}catch{}
    const scope={...identity,backend:location.origin+'/',ownerId:'synthetic-owner',support:seed.support};
    const store=new api.IndexedDbJournal(scope,'p7-binding-'+crypto.randomUUID());
    const fence=new api.CollaborationAccountFence(null);fence.activate(scope,1);
    const caps={documentId:identity.documentId,mode:2,canRead:true,featureAvailable:true,canWriteCollaboratively:true,pages:[{pageId:identity.pageId,generation:identity.generation,...seed.support}]};
    const options={store,account:fence.capture(scope),committedSeed:new Uint8Array(seed.state),autoReconnect:false};
    let binding=await api.createJournaledEditor(document.getElementById('editor'),options,caps);
    binding.editor.commands.insertContentAt(1,'Journaled fixture ');await binding.session.flush();
    const pending=binding.session.pending.map(p=>p.updateId),html=binding.editor.getHTML();
    let blocked=false;try{binding.editor.commands.setContent('<p>forbidden</p>');}catch{blocked=true;}
    const local=binding.session.status.localDurable;
    const previous=binding;await previous.destroy();await previous.destroy();
    binding=await api.createJournaledEditor(document.getElementById('editor'),options,caps);
    await previous.destroy();
    const same=html===binding.editor.getHTML()&&JSON.stringify(pending)===JSON.stringify(binding.session.pending.map(p=>p.updateId));
    fence.activate(null,2);const fenced=!binding.editor.isEditable&&binding.session.status.accountChanged;
    await binding.destroy();await store.close();fence.close();
    return {blocked,local,same,fenced,pending:pending.length};
   },{identity,seed});
   assert.deepEqual(result,{blocked:true,local:true,same:true,fenced:true,pending:1});
  }
 });
 await check('Re-invitation isolates both bundle journals while preserving prior pending work',async()=>{
  for(const page of [a,b]){
   const result=await page.evaluate(async({identity,seed})=>{
    const scope={...identity,backend:location.origin+'/',ownerId:'synthetic-member',support:seed.support,membershipRevision:1};
    const database='p8-membership-'+crypto.randomUUID(),old=new api.IndexedDbJournal(scope,database);
    const fence=new api.CollaborationAccountFence(null);fence.activate(scope,1);
    const caps={documentId:identity.documentId,mode:2,canRead:true,featureAvailable:true,canWriteCollaboratively:true,membershipRevision:1,pages:[{pageId:identity.pageId,generation:identity.generation,...seed.support}]};
    const binding=await api.createJournaledEditor(document.getElementById('editor'),{store:old,account:fence.capture(scope),committedSeed:new Uint8Array(seed.state),autoReconnect:false},caps);
    binding.editor.commands.insertContentAt(1,'Private old grant ');await binding.session.flush();await binding.destroy();
    const next=new api.IndexedDbJournal({...scope,membershipRevision:3},database);
    const empty=await next.load()===null;await next.initialize(new Uint8Array(seed.state));
    const preserved=(await old.load()).pending.length===1,isolated=(await next.load()).pending.length===0;
    await old.close();await next.close();fence.close();return {empty,preserved,isolated};
   },{identity,seed});assert.deepEqual(result,{empty:true,preserved:true,isolated:true});
  }
 });
 await check('Both bundle comment anchors follow remote inserts, detach after deletion and preserve local-only undo',async()=>{
  for(const page of [a,b]){
   const result=await page.evaluate(async({identity,seed})=>{
    const scope={...identity,backend:location.origin+'/',ownerId:'synthetic-commenter',support:seed.support};
    const store=new api.IndexedDbJournal(scope,'p9-anchors-'+crypto.randomUUID()),fence=new api.CollaborationAccountFence(null);fence.activate(scope,1);
    const caps={documentId:identity.documentId,mode:2,canRead:true,featureAvailable:true,canWriteCollaboratively:true,pages:[{pageId:identity.pageId,generation:identity.generation,...seed.support}]};
    const binding=await api.createJournaledEditor(document.getElementById('editor'),{store,account:fence.capture(scope),committedSeed:new Uint8Array(seed.state),autoReconnect:false,interactionsEnabled:true},caps);
    binding.editor.commands.setTextSelection({from:1,to:5});const anchor=binding.interactions.selectionAnchor();
    const thread={id:crypto.randomUUID(),pageId:identity.pageId,generation:identity.generation,detached:false,resolved:false,anchor};binding.interactions.setThreads([thread]);
    const highlighted=document.querySelectorAll('.collaboration-comment-anchor').length>0;
    // Synthetic authoritative frame tests rendering only; the real socket probe verifies its identity source.
    await binding.session.receiveReceipt(JSON.stringify({type:'presenceSnapshot',members:[{connectionId:'other-connection',actorId:'<remote member>',cursor:anchor}]}),binding.session.wireGeneration);
    const caret=document.querySelectorAll('.collaboration-remote-caret').length===1&&document.querySelector('.collaboration-remote-caret').textContent==='<remote member>'&&document.querySelector('.collaboration-presence').textContent.includes('<remote member>');
    const host=document.createElement('div');document.body.append(host);const remote=await api.createIsolatedEditor(host,identity,{...seed,state:new Uint8Array(seed.state)});
    remote.editor.commands.insertContentAt(1,'Remote ');await binding.session.persist(remote.snapshot());
    const focused=binding.interactions.focusThread(thread.id),selected=binding.editor.state.doc.textBetween(binding.editor.state.selection.from,binding.editor.state.selection.to);
    const followed=focused&&selected==='Base';
    binding.editor.commands.insertContentAt(binding.editor.state.doc.content.size-1,' Local');await binding.session.flush();binding.editor.commands.undo();await binding.session.flush();
    const undo=binding.editor.getText().includes('Remote')&&!binding.editor.getText().includes('Local');binding.editor.commands.redo();await binding.session.flush();
    const redo=binding.editor.getText().includes('Remote')&&binding.editor.getText().includes('Local');
    remote.editor.commands.deleteRange({from:8,to:12});await binding.session.persist(remote.snapshot());
    const detached=!binding.interactions.focusThread(thread.id);
    let wrongGeneration=false;try{binding.interactions.setThreads([{...thread,generation:crypto.randomUUID()}]);}catch{wrongGeneration=true;}
    fence.activate(null,2);const cleared=document.querySelectorAll('.collaboration-comment-anchor,.collaboration-remote-caret').length===0&&!binding.editor.isEditable;
    remote.destroy();host.remove();await binding.destroy();await store.close();fence.close();if(!followed||!detached)throw Error(JSON.stringify({focused,selected,anchor,followed,detached}));return {highlighted,caret,followed,undo,redo,detached,wrongGeneration,cleared};
   },{identity,seed});assert.deepEqual(result,{highlighted:true,caret:true,followed:true,undo:true,redo:true,detached:true,wrongGeneration:true,cleared:true});
  }
 });
 await check('Restored environments isolate prior receipts and private recovery never reports a cloud save',async()=>{
  for(const page of[a,b]){
   const result=await page.evaluate(async({identity,seed})=>{
    const scope={...identity,backend:location.origin+'/',ownerId:'restore-fixture',support:seed.support},database='p10-restore-'+crypto.randomUUID(),store=new api.IndexedDbJournal(scope,database),account=new api.CollaborationAccountFence(null);account.activate(scope,1);
    const caps={documentId:scope.documentId,mode:2,featureAvailable:true,canRead:true,canWriteCollaboratively:true,pages:[{...scope.support,pageId:scope.pageId,generation:scope.generation}]};
    const binding=await api.createJournaledEditor(document.getElementById('editor'),{store,account:account.capture(scope),committedSeed:new Uint8Array(seed.state),autoReconnect:false},caps);
    binding.editor.commands.insertContentAt(1,'Previously acknowledged ');await binding.session.flush();const envelope=binding.session.pending[0];
    await store.acknowledge({pageId:scope.pageId,generation:scope.generation,installationId:envelope.installationId,updateId:envelope.updateId,sha256:envelope.sha256,commitSequence:1,committedAtUnixMilliseconds:Date.now()});await binding.session.reload();
    const previouslySaved=binding.session.status.remoteDurable,nextEnvironment='restore-'+crypto.randomUUID().replaceAll('-','');let fenced=false;
    try{await api.createJournaledEditor(document.getElementById('editor'),{store,account:account.capture(scope),committedSeed:new Uint8Array(seed.state),autoReconnect:false},{...caps,environment:nextEnvironment});}catch(error){fenced=String(error).includes('environment_changed');}
    await binding.session.block('access_unavailable');const privateOnly=binding.session.status.localDurable&&!binding.session.status.remoteDurable&&binding.editor.getText().includes('Previously acknowledged');
    const next=new api.IndexedDbJournal({...scope,environment:nextEnvironment},database),isolated=await next.load()===null,preserved=(await store.load()).receipts.length===1;
    await binding.destroy();await store.close();await next.close();account.close();return {previouslySaved,fenced,privateOnly,isolated,preserved};
   },{identity,seed});assert.deepEqual(result,{previouslySaved:true,fenced:true,privateOnly:true,isolated:true,preserved:true});
  }
 });
 await check('Durability and connection status do not synthesize content edits; authority loss still disables typing',async()=>{
  for(const page of[a,b]){
   const result=await page.evaluate(async({identity,seed})=>{
    const scope={...identity,backend:location.origin+'/',ownerId:'status-fixture',support:seed.support},store=new api.IndexedDbJournal(scope,'status-'+crypto.randomUUID()),account=new api.CollaborationAccountFence(null);account.activate(scope,1);
    const caps={documentId:scope.documentId,mode:2,featureAvailable:true,canRead:true,canWriteCollaboratively:true,pages:[{...scope.support,pageId:scope.pageId,generation:scope.generation}]};
    const binding=await api.createJournaledEditor(document.getElementById('editor'),{store,account:account.capture(scope),committedSeed:new Uint8Array(seed.state),autoReconnect:false},caps);
    let edits=0;binding.editor.on('update',()=>edits++);
    binding.session.disconnect();binding.session.disconnect();const statusOnly=edits===0&&binding.editor.isEditable;
    binding.editor.commands.insertContentAt(1,'Durable ');await binding.session.flush();const oneEdit=edits===1&&binding.session.pending.length===1&&binding.session.status.localDurable;
    account.activate(null,2);const disabled=!binding.editor.isEditable&&edits===1;
    await binding.destroy();await store.close();account.close();return {statusOnly,oneEdit,disabled};
   },{identity,seed});assert.deepEqual(result,{statusOnly:true,oneEdit:true,disabled:true});
  }
 });
 await check('Reopening a committed checkpoint rechecks access; unsent drafts and changed schemas stay fenced',async()=>{
  for(const page of[a,b]){
   const result=await page.evaluate(async({identity,seed})=>{
    window.session?.destroy();
    const results=[];
    for(const kind of ['committed','pending','schema']){
     const scope={...identity,backend:location.origin+'/',ownerId:'reconnect-fixture',support:seed.support};
     const store=new api.IndexedDbJournal(scope,'reconnect-'+crypto.randomUUID()),account=new api.CollaborationAccountFence(null);account.activate(scope,1);
     const caps={documentId:scope.documentId,mode:2,featureAvailable:true,canRead:true,canWriteCollaboratively:true,pages:[{...scope.support,pageId:scope.pageId,generation:scope.generation}]};
     const binding=await api.createJournaledEditor(document.getElementById('editor'),{store,account:account.capture(scope),committedSeed:new Uint8Array(seed.state),autoReconnect:false},caps);
     if(kind==='pending'){binding.editor.commands.insertContentAt(1,'Private draft ');await binding.session.flush();}
     await binding.session.block(kind==='schema'?'schema_changed':'access_unavailable');
     const before=await store.load();await binding.destroy();
     let tickets=0;
     const reopened=await api.JournaledSession.open({store,account:account.capture(scope),autoReconnect:false,getTicket:async()=>{tickets++;throw Error('collaboration_transport_unavailable');}});
     let error='';try{await reopened.connect();}catch(e){error=String(e);}
     const after=await store.load();
     results.push({kind,tickets,privateFence:error.includes('private_recovery_required'),preserved:JSON.stringify(before)===JSON.stringify(after),saved:reopened.status.remoteDurable});
     await reopened.dispose();
     const view=await api.createJournaledEditor(document.getElementById('editor'),{store,account:account.capture(scope),autoReconnect:false},caps);
     const retained=view.editor.getText();
     if(!retained.includes('Base text.') || (kind==='pending'&&!retained.includes('Private draft ')))throw Error('Recovery view lost retained text');
     view.editor.commands.insertContentAt(1,'Forbidden ');
     if(view.editor.isEditable || view.editor.getText()!==retained)throw Error('Recovery view allowed a local edit');
     await view.destroy();await store.close();account.close();
    }
    return results;
   },{identity,seed});
   assert.deepEqual(result,[
    {kind:'committed',tickets:1,privateFence:false,preserved:true,saved:false},
    {kind:'pending',tickets:0,privateFence:true,preserved:true,saved:false},
    {kind:'schema',tickets:0,privateFence:true,preserved:true,saved:false}
   ]);
  }
 });
 assert.deepEqual(errors,[]);assert.deepEqual(networkWrites,[]);
 await writeFile(resolve(output,'browser-results.json'),JSON.stringify({passed:checks.length,checks,errors,networkWrites,scope:'Shipped web/device bundles in headless Edge; isolated binary/editor and atomic IndexedDB journal binding. Synthetic capability grants only; no packaged/native or production activation acceptance.'},null,2));
 console.log(`${checks.length} collaboration browser checks passed`);
} catch(error) {
 await writeFile(resolve(output,'browser-results.json'),JSON.stringify({passed:checks.length,checks,errors,failure:error.stack},null,2));throw error;
} finally {await browser?.close();await new Promise(done=>server.close(done));}

import assert from 'node:assert/strict';
import {Agent,createServer,request as httpRequest} from 'node:http';
import {mkdir,writeFile} from 'node:fs/promises';
import {createRequire} from 'node:module';
import {createCollaborationServer} from '../src/server.mjs';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const backend=process.env.PROBE_BACKEND_URL,client=process.env.PROBE_CLIENT_URL||'http://localhost:5398';
const evidence=process.env.PROBE_EVIDENCE_DIR||'artifacts/brainstorm-flow',requests=[],errors=[];let browser,service,owner,guest,guestConversationId;
const backendAgent=new Agent({keepAlive:true});
const http=createServer(async(req,res)=>{
 try{
  const url=new URL(req.url,'http://localhost'),actor=req.headers['x-test-user']??'owner';
  const json=value=>{res.setHeader('Content-Type','application/json');res.end(JSON.stringify(value));};
  if(url.pathname==='/api/auth/me')return json({isAuthenticated:true,userId:actor,name:actor,email:actor+'@fixture.test',roles:[],planKey:'standard',effectivePlanKey:'standard',isPaidAccessActive:true});
  if(url.pathname==='/api/ai/status')return json({planKey:'standard',aiEnabled:true,uiEnabled:true,supportsWebCheckedSources:true,quotaRemaining:250000,quotaTotal:250000});
  if(url.pathname==='/api/ai/actions')return json([]);
  if(url.pathname==='/api/onboarding/state')return json({hasCompletedOnboarding:true});
  if(!url.pathname.startsWith('/api/')){const result=await fetch(new URL(req.url,client));res.writeHead(result.status,{'Content-Type':result.headers.get('content-type')??'application/octet-stream'});res.end(Buffer.from(await result.arrayBuffer()));return;}
  if(!url.pathname.includes('/collaboration')&&!url.pathname.startsWith('/api/brainstorming')&&!url.pathname.startsWith('/api/ai/actions/web-source/')&&!url.pathname.startsWith('/api/ai/actions/writing-outline/')){res.statusCode=404;return json({});}
  const chunks=[];for await(const chunk of req)chunks.push(chunk);
  // Preserve the browser origin as a real reverse proxy does. Node fetch rewrites
  // Host, which would correctly fail Brainstorm's backend-bound session identity.
  const result=await new Promise((resolve,reject)=>{const upstream=httpRequest(new URL(req.url,backend),{method:req.method,agent:backendAgent,headers:{'Content-Type':'application/json','X-Test-User':actor,Host:req.headers.host}},response=>{const parts=[];response.on('data',p=>parts.push(p));response.on('end',()=>resolve({status:response.statusCode,headers:response.headers,body:Buffer.concat(parts).toString()}));});upstream.on('error',reject);if(chunks.length)upstream.write(Buffer.concat(chunks));upstream.end();});
  let body=result.body;
  if(result.status<300&&url.pathname.endsWith('/collaboration/sessions')){const value=JSON.parse(body);value.webSocketUrl=`ws://127.0.0.1:${service.address.port}/`;body=JSON.stringify(value);}
  requests.push({actor,method:req.method,path:req.url,status:result.status,...(result.status>=400?{body,responseHost:result.headers['x-probe-host']}:{} )});
  res.writeHead(result.status,{'Content-Type':result.headers['content-type']??'application/json'});res.end(body);
 }catch(e){errors.push(e.stack);res.writeHead(500);res.end(JSON.stringify({error:e.message}));}
});
await mkdir(evidence,{recursive:true});await writeFile(evidence+'/result.json',JSON.stringify({passed:false,state:'running',startedAt:new Date().toISOString()}));
await new Promise(r=>http.listen(0,'127.0.0.1',r));const origin=`http://127.0.0.1:${http.address().port}`;
try{
 service=createCollaborationServer({backendUrl:backend,serviceKey:process.env.COLLABORATION_SERVICE_KEY,environment:'test',port:0,commitsEnabled:true,interactionsEnabled:true,allowedOrigins:[origin]});await service.listen();
 browser=await chromium.launch({channel:'msedge',headless:true});
 owner=await browser.newPage({viewport:{width:1500,height:1000},extraHTTPHeaders:{'X-Test-User':'owner'}});
 guest=await browser.newPage({viewport:{width:1500,height:1000},extraHTTPHeaders:{'X-Test-User':'editor'}});
 for(const page of[owner,guest]) {
  page.on('pageerror',e=>errors.push(e.stack||e.message));
  // WASM renderer exceptions are console errors, not browser pageerror events.
  page.on('console',message=>{const text=message.text();if(message.type()==='error' && /Unhandled exception|crit: Microsoft\.AspNetCore\.Components/.test(text))errors.push(text);});
 }
 const documentId='1396210e-fbac-4714-bb20-37560d6b40bf';
 await Promise.all([owner.goto(origin+`/app/documents/${documentId}/sharing`),guest.goto(origin+'/app/collaboration')]);
 await owner.getByRole('checkbox',{name:'My changes are synced',exact:false}).check();await owner.getByRole('button',{name:'Enable sharing and live editing',exact:true}).click();
 await owner.getByLabel('Email address',{exact:true}).fill('editor@fixture.test');await owner.getByRole('button',{name:'Create invitation',exact:true}).click();await owner.getByText('Invitation ready.',{exact:false}).waitFor();
 await guest.getByRole('button',{name:'Refresh access',exact:true}).click();await guest.getByRole('button',{name:'Accept',exact:true}).click();await guest.locator('.ProseMirror[contenteditable=true]').waitFor();
 await owner.getByRole('link',{name:'Back to manuscript',exact:true}).click();await owner.locator('.ProseMirror[contenteditable=true]').waitFor();
 const manuscriptRoute=owner.url();
 async function openBrainstorm(page,ready=true){await page.getByRole('button',{name:'Brainstorm',exact:true}).click();await page.getByRole('textbox',{name:'Your message',exact:true}).waitFor();if(ready)await page.getByText('Context ready',{exact:true}).waitFor();}
 async function resumeConversation(page){
  if(await page.getByRole('button',{name:'History',exact:true}).isVisible()){
   await page.getByRole('button',{name:'History',exact:true}).click();
   const select=page.getByRole('combobox',{name:'Saved conversation',exact:true});
   if(await select.count()){assert.ok(guestConversationId,'The original conversation ID must be captured before reload.');await select.selectOption(guestConversationId);await page.getByRole('button',{name:'Open saved conversation',exact:true}).click();}
  }
 }
 await openBrainstorm(owner);
 await owner.getByRole('textbox',{name:'Your message',exact:true}).fill('Private unsent author idea.');
 await openBrainstorm(guest,false);
 guestConversationId=await guest.locator('.brainstorm-history select').inputValue();assert.ok(guestConversationId);
 await guest.getByRole('textbox',{name:'Your message',exact:true}).fill('Draft a quiet meeting between two hesitant people.');await guest.getByRole('button',{name:'Send message',exact:true}).click();
 await guest.getByRole('button',{name:'View working draft',exact:false}).waitFor();
 const shared=owner.getByRole('region',{name:"Collaborator's Brainstorm draft",exact:true});
 await shared.getByText('Hon tvivlade. 日本語 🧭',{exact:true}).waitFor();
 assert.equal(await owner.getByRole('textbox',{name:'Your message',exact:true}).inputValue(),'Private unsent author idea.');
 assert.equal(await owner.getByText('Draft a quiet meeting between two hesitant people.',{exact:true}).count(),0);
 await owner.screenshot({path:evidence+'/collaborator-draft.png',fullPage:true});
 // Reverse the publisher/recipient roles before changing the manuscript source.
 // The editor must see the owner's draft while retaining its own private composer.
 await guest.getByRole('textbox',{name:'Your message',exact:true}).fill('Private unsent collaborator idea.');
 await owner.getByRole('textbox',{name:'Your message',exact:true}).fill('Draft an owner-only alternative meeting.');
 await owner.getByRole('button',{name:'Send message',exact:true}).click();
 await owner.getByRole('button',{name:'View working draft',exact:false}).waitFor();
 const reverseShared=guest.getByRole('region',{name:"Collaborator's Brainstorm draft",exact:true});
 await reverseShared.getByText('Hon tvivlade. 日本語 🧭',{exact:true}).waitFor();
 assert.equal(await guest.getByRole('textbox',{name:'Your message',exact:true}).inputValue(),'Private unsent collaborator idea.');
 assert.equal(await guest.getByText('Draft an owner-only alternative meeting.',{exact:true}).count(),0);
 await guest.screenshot({path:evidence+'/reverse-collaborator-draft.png',fullPage:true});
 await owner.getByRole('button',{name:'Back to writing',exact:true}).click();
 const proposal=guest.getByRole('region',{name:'Proposed prose review',exact:true});
 await proposal.getByRole('button',{name:'Review & keep',exact:false}).click();await guest.getByRole('heading',{name:'Where should this text go?'}).waitFor();
 await guest.screenshot({path:evidence+'/placement.png',fullPage:true});
 await proposal.getByRole('button',{name:'Add here',exact:true}).click();await guest.getByText('Added to your manuscript',{exact:true}).waitFor();
 await guest.screenshot({path:evidence+'/kept.png',fullPage:true});
 // Already-open collaborator reconnects to a replacement generation without a reload.
 await owner.waitForFunction(()=>document.querySelector('.editor-content .ProseMirror')?.textContent.includes('Hon tvivlade.'));
  // Repeated replacements exercise late callbacks from retired editor mounts.
  for(let cycle=0;cycle<3;cycle++){
   await proposal.getByRole('button',{name:'Details',exact:true}).click();await proposal.getByRole('button',{name:'Undo kept version',exact:true}).click();
   await proposal.locator('.proposal-status').filter({hasText:'Undone'}).waitFor();
   await owner.waitForFunction(()=>{const node=document.querySelector('.editor-content .ProseMirror');if(!node)return false;const prose=node.cloneNode(true);prose.querySelectorAll('.collaboration-remote-caret').forEach(caret=>caret.remove());return prose.textContent==='Existing writing retained.';});
   await proposal.getByRole('button',{name:'Details',exact:true}).click();await proposal.getByRole('button',{name:'Redo kept version',exact:true}).click();
   await guest.getByText('Added to your manuscript',{exact:true}).waitFor();
   await owner.waitForFunction(()=>document.querySelector('.editor-content .ProseMirror')?.textContent.includes('Hon tvivlade.'));
  }
 await guest.getByRole('button',{name:'Back to writing',exact:true}).click();await guest.waitForFunction(()=>document.querySelector('.editor-content .ProseMirror')?.textContent.includes('Hon tvivlade.'));
 await guest.reload();await guest.waitForFunction(()=>document.querySelector('.editor-content .ProseMirror')?.textContent.includes('Hon tvivlade.'));
 await openBrainstorm(guest);
 // Resume the retained private conversation through its visible History controls.
 await resumeConversation(guest);
 await guest.locator('.brainstorm-planning-review > summary').click();
 const planning=guest.getByRole('region',{name:'Save brainstorming decisions into planning',exact:true});
 await planning.getByLabel('Decision',{exact:true}).fill('They conceal their affection until chapter four.');
 await planning.getByRole('button',{name:'Choose destination / refresh planning',exact:true}).click();await planning.getByRole('button',{name:'Review selected fields',exact:true}).click();
 await planning.getByRole('button',{name:'Approve selected planning fields',exact:true}).click();
 await planning.locator('summary').filter({hasText:'Planning operation'}).last().waitFor();await planning.locator('summary').filter({hasText:'Planning operation'}).last().click();
 await planning.getByRole('button',{name:'Undo approved planning fields',exact:true}).waitFor();await guest.screenshot({path:evidence+'/planning-saved.png',fullPage:true});
 await guest.reload();await guest.locator('.ProseMirror[contenteditable=true]').waitFor();await openBrainstorm(guest);await resumeConversation(guest);
 await guest.locator('.brainstorm-planning-review > summary').click();
 const retainedPlanning=planning.locator('summary').filter({hasText:'Planning operation · StoryNote · Committed'});
 await retainedPlanning.waitFor();await retainedPlanning.click();
 await guest.screenshot({path:evidence+'/planning-reloaded.png',fullPage:true});
 await planning.getByRole('button',{name:'Inspect planning save',exact:true}).click();await planning.getByRole('button',{name:'Undo approved planning fields',exact:true}).click();
 await planning.locator('summary').filter({hasText:'Planning operation'}).filter({hasText:'Undone'}).waitFor();
 for(const kind of['SceneIdea','SynopsisField','CharacterNote']){
  await planning.getByRole('button',{name:'Start a new typed decision',exact:true}).click();
  await planning.getByLabel('Decision',{exact:true}).fill(`Reviewed ${kind} decision: they meet again in chapter four.`);
  await planning.getByLabel('Destination').selectOption(kind);
  if(kind==='CharacterNote')await planning.getByLabel('Character name',{exact:true}).fill('Åsa');
  await planning.getByRole('button',{name:'Choose destination / refresh planning',exact:true}).click();
  if(kind==='SceneIdea'){const chapters=planning.getByLabel('Chapter');await chapters.selectOption(await chapters.locator('option').nth(1).getAttribute('value'));}
  await planning.getByRole('button',{name:'Review selected fields',exact:true}).click();
  await planning.getByRole('button',{name:'Approve selected planning fields',exact:true}).click();
  const saved=planning.locator('summary').filter({hasText:`Planning operation · ${kind} · Committed`});await saved.waitFor();await saved.click();
  await planning.getByRole('button',{name:'Inspect planning save',exact:true}).last().click();
  await planning.getByRole('button',{name:'Undo approved planning fields',exact:true}).click();
  await planning.locator('summary').filter({hasText:`Planning operation · ${kind} · Undone`}).waitFor();
 }
 // Keep undo/redo is in the retained draft's Details tab.
 await guest.reload();await guest.locator('.ProseMirror[contenteditable=true]').waitFor();await openBrainstorm(guest);await resumeConversation(guest);
 await guest.getByRole('button',{name:'View working draft',exact:false}).first().click();await proposal.getByRole('button',{name:'Details',exact:true}).click();
 await proposal.getByRole('button',{name:'Undo kept version',exact:true}).click();
 await proposal.locator('.proposal-status').filter({hasText:'Undone'}).waitFor();await proposal.getByRole('button',{name:'Details',exact:true}).click();
 await proposal.getByRole('button',{name:'Redo kept version',exact:true}).waitFor();
 await owner.waitForFunction(()=>{const node=document.querySelector('.editor-content .ProseMirror');if(!node)return false;const prose=node.cloneNode(true);prose.querySelectorAll('.collaboration-remote-caret').forEach(caret=>caret.remove());return prose.textContent==='Existing writing retained.';});
 await proposal.getByRole('button',{name:'Redo kept version',exact:true}).click();await guest.getByText('Added to your manuscript',{exact:true}).waitFor();
 await owner.waitForFunction(()=>document.querySelector('.editor-content .ProseMirror')?.textContent.includes('Hon tvivlade.'));
 await owner.screenshot({path:evidence+'/owner-manuscript.png',fullPage:true});await guest.screenshot({path:evidence+'/editor-redone.png',fullPage:true});
 assert.equal(await owner.locator('.editor-content .ProseMirror').innerText().then(t=>t.includes('Existing writing retained.')),true);
 assert.deepEqual(errors,[]);
 await writeFile(evidence+'/result.json',JSON.stringify({passed:true,documentId,manuscriptRoute,clientAssetOrigin:client,checks:['owner and editor join same manuscript','typing immediately while context prepares','editor and owner context and deterministic generation','review exact placement','Keep publishes CRDT generation','both accounts see each other generated draft without reload and retain private composers','other account sees Keep without reload','editor reload retains prose','private conversation resumes','planning receipt survives reload and can be inspected and undone','review and save all four planning destinations','inspect and undo all four planning destinations','Keep undo and redo visible to owner without reload','original prose retained'],scope:'Actual WASM UI, real isolated Kestrel/SQLite/Node collaboration, synthetic accounts and deterministic external AI provider; no paid generation. Client assets loaded from clientAssetOrigin; manuscriptRoute identifies the isolated test origin.'},null,2));
 console.log('PASS: full shared Brainstorm visible flow, Keep, planning, undo/redo, second account and reload');
}catch(e){await writeFile(evidence+'/result.json',JSON.stringify({passed:false,state:'failed',clientAssetOrigin:client,error:e.stack||String(e)},null,2));if(owner){await owner.screenshot({path:evidence+'/owner-failure.png',fullPage:true});await writeFile(evidence+'/owner-failure.html',await owner.content());}if(guest){await guest.screenshot({path:evidence+'/failure.png',fullPage:true});await writeFile(evidence+'/failure.html',await guest.content());}throw e;}
finally{
 await writeFile(evidence+'/requests.json',JSON.stringify(requests,null,2));await writeFile(evidence+'/errors.json',JSON.stringify(errors,null,2));
 await browser?.close();
 console.log('Browser closed; stopping disposable collaboration rooms.');
 if(service){
  const closing=service.destroy();
  // Replacement generations can leave already-idle rooms with no future
  // disconnect/store event. Unload them through Hocuspocus's normal safety
  // checks so shutdown observes the last afterUnloadDocument event.
  for(const document of [...service.hocuspocus.documents.values()])await service.hocuspocus.unloadDocument(document);
  await closing;
 }
 http.closeAllConnections();await new Promise(r=>http.close(r));
 backendAgent.destroy();
 await writeFile(evidence+'/cleanup-resources.json',JSON.stringify(process.getActiveResourcesInfo()));
 console.log('Disposable browser services stopped.');
}

import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {mkdir,writeFile} from 'node:fs/promises';
import {createRequire} from 'node:module';
import {createCollaborationServer} from '../src/server.mjs';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const backend=process.env.PROBE_BACKEND_URL,client=process.env.PROBE_CLIENT_URL||'http://localhost:5398';
let service,browser,pending=0;const errors=[];
// Presence widgets are presentation, not manuscript HTML; each browser renders the other author's cursor.
const manuscriptHtml=page=>page.locator('.ProseMirror').evaluate(element=>{const copy=element.cloneNode(true);copy.querySelectorAll('.ProseMirror-widget,.ProseMirror-separator,.ProseMirror-trailingBreak').forEach(node=>node.remove());return copy.innerHTML;});
const http=createServer(async(req,res)=>{
 try {
  const url=new URL(req.url,'http://localhost'),actor=req.headers['x-test-user']??'owner';
  const json=value=>{res.setHeader('Content-Type','application/json');res.end(JSON.stringify(value));};
  if(url.pathname==='/api/auth/me')return json({isAuthenticated:true,userId:actor,name:actor,email:actor+'@fixture.test',roles:[],planKey:'professional',effectivePlanKey:'professional',isPaidAccessActive:true});
  if(url.pathname==='/api/ai/status')return json({planKey:'professional',aiEnabled:true,uiEnabled:true,supportsWebCheckedSources:true,quotaRemaining:10000,quotaTotal:10000});
  if(url.pathname==='/api/ai/actions')return json([{actionKey:'rewrite.selection',displayName:'Rewrite selection',requiresSelection:true,modalities:['Text'],requiredInputs:[]},{actionKey:'expand.section',displayName:'Expand section',requiresSelection:false,modalities:['Text'],requiredInputs:[]}]);
  if(url.pathname==='/api/onboarding/state')return json({hasCompletedOnboarding:true});
  if(url.pathname==='/__probe/pending'){pending=3;res.writeHead(204);res.end();return;}
  if(actor==='editor'&&url.pathname.endsWith('/collaboration/read')&&pending-->0){res.statusCode=409;return json({code:'collaboration_projection_pending'});}
  if(!url.pathname.startsWith('/api/')){
   const upstream=await fetch(new URL(req.url,client));res.writeHead(upstream.status,{'Content-Type':upstream.headers.get('content-type')??'application/octet-stream'});res.end(Buffer.from(await upstream.arrayBuffer()));return;
  }
  // Non-collaboration shell endpoints are fixture-only; collaboration routes use the real backend.
  if(!url.pathname.includes('/collaboration')&&!url.pathname.startsWith('/api/ai/actions/web-source/')&&!url.pathname.startsWith('/api/ai/actions/writing-outline/')&&!url.pathname.startsWith('/api/ai/actions/history')&&!url.pathname.endsWith('/rewrite.selection/execute')&&!url.pathname.endsWith('/expand.section/execute')&&!url.pathname.includes('/structured-translations')){res.statusCode=404;return json({});}
  const chunks=[];for await(const chunk of req)chunks.push(chunk);
  const result=await fetch(new URL(url.pathname.startsWith('/api/ai/actions/writing-outline/')?'/__probe/outline/'+url.pathname.split('/').at(-1):url.pathname.endsWith('/execute')?'/__probe/ai?action='+url.pathname.split('/').at(-2):url.pathname==='/api/ai/actions/history'?'/__probe/history'+url.search:req.url,backend),{method:req.method,headers:{'Content-Type':'application/json','X-Test-User':actor},body:req.method==='GET'?undefined:Buffer.concat(chunks)});
  let body=await result.text();
  if(result.ok&&url.pathname.includes('/sessions')){const value=JSON.parse(body);value.webSocketUrl=`ws://127.0.0.1:${service.address.port}/`;body=JSON.stringify(value);}
  res.writeHead(result.status,{'Content-Type':'application/json',...Object.fromEntries([...result.headers].filter(([key])=>key.startsWith('x-writerapp-')))});res.end(body);
 }catch(error){res.writeHead(500);res.end(JSON.stringify({error:error.message}));}
});
await new Promise(r=>http.listen(0,'127.0.0.1',r));const origin=`http://127.0.0.1:${http.address().port}`;
try{
 service=createCollaborationServer({backendUrl:backend,serviceKey:process.env.COLLABORATION_SERVICE_KEY,environment:'test',port:0,commitsEnabled:true,interactionsEnabled:true,allowedOrigins:[origin]});await service.listen();
 browser=await chromium.launch({channel:'msedge',headless:true});
 const owner=await browser.newPage({extraHTTPHeaders:{'X-Test-User':'owner'}}),guest=await browser.newPage({extraHTTPHeaders:{'X-Test-User':'editor'}});
 for(const page of[owner,guest]){page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if((m.type()==='error'&&!m.text().startsWith('Failed to load resource:'))||m.text().includes('[collaboration] local persistence failed'))errors.push(m.text());});}
 const documentId='1396210e-fbac-4714-bb20-37560d6b40bf';
 await Promise.all([owner.goto(origin+`/app/documents/${documentId}/sharing`),guest.goto(origin+'/app/collaboration')]);
 await owner.getByRole('checkbox',{name:'My changes are synced',exact:false}).check();
 await owner.getByRole('button',{name:'Enable sharing and live editing',exact:true}).click();
 await owner.getByLabel('Email address',{exact:true}).fill('editor@fixture.test');
 await owner.getByRole('button',{name:'Create invitation',exact:true}).click();
 await owner.getByText('Invitation ready.',{exact:false}).waitFor();
 assert.equal(await owner.getByLabel('One-time code',{exact:true}).count(),0);
 await guest.getByRole('button',{name:'Refresh access',exact:true}).click();
 await guest.getByRole('button',{name:'Accept',exact:true}).click();
 await guest.locator('.ProseMirror[contenteditable=true]').waitFor();
 await owner.getByRole('link',{name:'Back to manuscript',exact:true}).click();
 await owner.locator('.ProseMirror[contenteditable=true]').waitFor();
 await owner.locator('.ProseMirror').press('End');await owner.locator('.ProseMirror').pressSequentially(' NORMAL-OWNER');
 await guest.waitForFunction(()=>document.querySelector('.ProseMirror')?.textContent.includes('NORMAL-OWNER'));
 await guest.locator('.ProseMirror').press('End');await guest.locator('.ProseMirror').pressSequentially(' NORMAL-GUEST');
 await owner.waitForFunction(()=>document.querySelector('.ProseMirror')?.textContent.includes('NORMAL-GUEST'));
 await Promise.all([owner.getByText('Saved to shared manuscript',{exact:true}).waitFor(),guest.getByText('Saved to shared manuscript',{exact:true}).waitFor()]);
 for(const page of[owner,guest]){
  await page.getByRole('button',{name:'Show 2 people on this page'}).waitFor();
  assert.equal(await page.locator('.collaboration-presence').count(),1);
  assert.equal(await page.locator('.collaboration-presence').evaluate(e=>!e.closest('.editor-content')&&!e.closest('.ProseMirror')),true);
  await page.getByRole('button',{name:'Show 2 people on this page'}).click();
  assert.equal(await page.getByRole('region',{name:'People on this page'}).isVisible(),true);
  assert.equal(await page.locator('.collaboration-person').count(),2);
  assert.equal(await page.locator('.collaboration-person-meta').filter({hasText:'You'}).count(),1);
  await page.getByRole('button',{name:'Show 2 people on this page'}).press('Escape');
 }
 await guest.reload();await guest.waitForFunction(()=>document.querySelector('.ProseMirror')?.textContent.includes('NORMAL-GUEST'));
 assert.equal(new URL(owner.url()).pathname,new URL(guest.url()).pathname,'Both accounts must open the same manuscript and section');
 assert.equal(await manuscriptHtml(owner),await manuscriptHtml(guest));
 await Promise.all([owner.locator('.ProseMirror').press('Control+End'),guest.locator('.ProseMirror').press('Control+End')]);
 await Promise.all([owner.locator('.ProseMirror').pressSequentially(' CONCURRENT-OWNER'),guest.locator('.ProseMirror').pressSequentially(' CONCURRENT-EDITOR')]);
 for(const page of[owner,guest])await page.waitForFunction(()=>{const text=document.querySelector('.ProseMirror')?.textContent??'';return text.includes('CONCURRENT-OWNER')&&text.includes('CONCURRENT-EDITOR');});
 await Promise.all([owner.getByText('Saved to shared manuscript',{exact:true}).waitFor(),guest.getByText('Saved to shared manuscript',{exact:true}).waitFor()]);
 const synchronizedHtml=await manuscriptHtml(owner);
 assert.equal(synchronizedHtml,await manuscriptHtml(guest));
 await Promise.all([owner.reload(),guest.reload()]);
 for(const page of[owner,guest]){
  await page.locator('.ProseMirror[contenteditable=true]').waitFor();
  await page.getByText('Saved to shared manuscript',{exact:true}).waitFor();
  assert.equal(await manuscriptHtml(page),synchronizedHtml,'Both accounts must reload the exact committed content');
 }
 await mkdir('artifacts/two-account-sync',{recursive:true});
 await owner.screenshot({path:'artifacts/two-account-sync/owner.png',fullPage:true});
 await guest.screenshot({path:'artifacts/two-account-sync/editor.png',fullPage:true});
 await writeFile('artifacts/two-account-sync/result.json',JSON.stringify({passed:true,documentId,checks:['distinct owner/editor identities','same manuscript and section','owner typing reaches editor','editor typing reaches owner','concurrent typing converges','identical full HTML','both report durable save','both reload identical committed content'],scope:'Normal Prosa WASM editors with real Kestrel/SQLite/Node services and isolated synthetic account authentication; not the user\'s provider sign-in sessions.'},null,2));
 if(process.env.PROBE_SYNC_ONLY==='1'){
  assert.deepEqual(errors.filter(e=>e.includes('Unhandled')||e.includes('crit:')||e.includes('Error:')),[]);
  console.log('PASS: two-account full-editor typing, concurrent convergence, durable saves and reload');
 }else{
 const firstPage=await owner.getByLabel('Manuscript page',{exact:true}).inputValue();
 await owner.getByLabel('Manuscript page',{exact:true}).selectOption('8567c507-26f0-4e69-81c5-6b7676fe11cc');
 await owner.waitForFunction(()=>document.querySelector('.ProseMirror')?.textContent.includes('Second page only.'));
 await owner.locator('.ProseMirror').press('End');await owner.locator('.ProseMirror').pressSequentially(' SECOND-ONLY');
 await owner.getByText('Saved to shared manuscript',{exact:true}).waitFor();
 assert.equal(await guest.locator('.ProseMirror').innerText().then(t=>t.includes('SECOND-ONLY')),false);
 await owner.getByLabel('Manuscript page',{exact:true}).selectOption(firstPage);
 await owner.waitForFunction(()=>document.querySelector('.ProseMirror')?.textContent.includes('NORMAL-GUEST'));
 assert.equal(await owner.locator('.ProseMirror').innerText().then(t=>t.includes('SECOND-ONLY')),false);
 // Exercise the existing formatting toolbar, rather than a separate test editor.
 await owner.locator('.ProseMirror').press('Control+a');
 await owner.getByRole('button',{name:'Bold',exact:true}).click();
 await guest.locator('.ProseMirror strong').waitFor();
 await owner.waitForTimeout(600);
 await owner.getByRole('combobox',{name:'Text style',exact:true}).selectOption('heading:2');
 await guest.locator('.ProseMirror h2').waitFor();
 await owner.getByRole('button',{name:'Undo',exact:true}).click();
 await guest.locator('.ProseMirror h2').waitFor({state:'detached'});
 await owner.getByRole('button',{name:'Redo',exact:true}).click();
 await guest.locator('.ProseMirror h2').waitFor();
 await owner.locator('.ProseMirror').press('Control+End');await owner.locator('.ProseMirror').press('Enter');
 await owner.getByRole('combobox',{name:'Text style',exact:true}).selectOption('paragraph');
 await owner.locator('.ProseMirror').pressSequentially('LIST-ITEM');
 await owner.getByRole('button',{name:'Bulleted list',exact:true}).click();
 await guest.locator('.ProseMirror li').filter({hasText:'LIST-ITEM'}).waitFor();
 await owner.locator('.ProseMirror').press('End');await owner.locator('.ProseMirror').press('Enter');await owner.locator('.ProseMirror').press('Enter');
 await owner.getByRole('button',{name:'Insert table',exact:true}).click();
 await guest.locator('.ProseMirror table').waitFor();
 await owner.locator('.ProseMirror table th').first().click();await owner.keyboard.type('SHARED-CELL');
 await guest.locator('.ProseMirror table').filter({hasText:'SHARED-CELL'}).waitFor();
 await Promise.all([owner.getByText('Saved to shared manuscript',{exact:true}).waitFor(),guest.getByText('Saved to shared manuscript',{exact:true}).waitFor()]);
 // Both normal editors must remount a checked replacement without losing sign-in.
 const pageId=await owner.getByLabel('Manuscript page',{exact:true}).inputValue();
 const sectionId=new URL(owner.url()).pathname.split('/').at(-1);
 const checkedJson=async send=>{for(let i=0;i<30;i++){const response=await send();const value=await response.json();if(response.ok())return value;if(response.status()!==409||value.code!=='collaboration_projection_pending')throw Error(JSON.stringify(value));await new Promise(r=>setTimeout(r,200));}throw Error('Projection did not become current');};
 const source=await checkedJson(()=>owner.request.get(origin+`/api/ai/actions/web-source/${documentId}?sectionId=${sectionId}&pageId=${pageId}`));
 const snapshot=await checkedJson(()=>owner.request.post(origin+`/api/documents/${documentId}/collaboration/read`,{data:{}}));
 const original=snapshot.pages.find(p=>p.pageId===pageId).html;
 const replaced=await owner.request.post(origin+`/api/documents/${documentId}/collaboration/tools/replace`,{data:{operationId:crypto.randomUUID(),source,beforeContent:original,afterContent:'<p>CHECKED-REPLACEMENT</p>'}});
 assert.equal(replaced.status(),200,await replaced.text());
 for(const page of [owner,guest])await page.locator('.ProseMirror[contenteditable=true]').filter({hasText:'CHECKED-REPLACEMENT'}).waitFor();
 await guest.getByRole('tablist',{name:'Right panel categories'}).getByRole('tab',{name:'History',exact:true}).click();
 await guest.getByRole('button',{name:'Restore',exact:true}).first().click();
 await guest.getByRole('dialog',{name:'Restore this version?'}).getByRole('button',{name:'Restore',exact:true}).click();
 for(const page of [owner,guest])await page.locator('.ProseMirror table').filter({hasText:'SHARED-CELL'}).waitFor();
 // Exercise the actual normal-editor Apply UI; only generation is deterministic.
 await owner.locator('.ProseMirror table th').first().click();await owner.keyboard.press('Home');await owner.keyboard.press('Shift+End');
 await owner.getByRole('button',{name:'Fix grammar (Neutral)',exact:true}).click();
 await owner.getByText('REVIEWED-TEXT',{exact:true}).first().waitFor();
 await owner.getByRole('button',{name:'Apply',exact:true}).waitFor();
 assert.equal(await guest.locator('.ProseMirror').filter({hasText:'REVIEWED-TEXT'}).count(),0,'A proposal must not enter the shared document');
 await owner.getByRole('button',{name:'Apply',exact:true}).click();
 for(const page of [owner,guest])await page.locator('.ProseMirror table').filter({hasText:'REVIEWED-TEXT'}).waitFor();
 await owner.getByRole('tablist',{name:'Right panel categories'}).getByRole('tab',{name:'History',exact:true}).click();
 await owner.getByRole('button',{name:'AI Undo',exact:true}).click();
 for(const page of [owner,guest])await page.locator('.ProseMirror table').filter({hasText:'SHARED-CELL'}).waitFor();
 await guest.getByRole('tablist',{name:'Right panel categories'}).getByRole('tab',{name:'Writing',exact:true}).click();
 await guest.getByRole('button',{name:'Expand section',exact:true}).click();
 await guest.getByText('SECTION-APPROVAL',{exact:false}).first().waitFor();
 await guest.getByRole('button',{name:'Apply',exact:true}).click();
 for(const page of [owner,guest])await page.locator('.ProseMirror table').filter({hasText:'SECTION-APPROVAL'}).waitFor();
 await guest.getByRole('button',{name:'Undo change',exact:true}).click();
 for(const page of [owner,guest])await page.waitForFunction(()=>document.querySelector('.ProseMirror')?.textContent.includes('SHARED-CELL')&&!document.querySelector('.ProseMirror')?.textContent.includes('SECTION-APPROVAL'));
 await owner.getByText('Organize manuscript',{exact:true}).click();
 await owner.getByRole('button',{name:'New section',exact:true}).click();
 await owner.locator('.editor-section-title').filter({hasText:'New section'}).waitFor();
 await guest.getByRole('navigation',{name:'Manuscript sections'}).getByRole('button',{name:'New section',exact:true}).waitFor();
 if(!await owner.locator('.shared-structure-tools').evaluate(el=>el.open))await owner.getByText('Organize manuscript',{exact:true}).click();
 await owner.getByRole('button',{name:'Rename section',exact:true}).click();
 await owner.getByLabel('Section name',{exact:true}).fill('SHARED-CHAPTER');
 await owner.getByRole('button',{name:'Save section name',exact:true}).click();
 await guest.getByRole('navigation',{name:'Manuscript sections'}).getByRole('button',{name:'SHARED-CHAPTER',exact:true}).waitFor();
 await owner.getByRole('button',{name:'New page',exact:true}).click();
 await owner.getByLabel('Manuscript page',{exact:true}).waitFor();
 await owner.getByRole('button',{name:'Move section up',exact:true}).click();
 await guest.waitForFunction(()=>document.querySelector('nav[aria-label="Manuscript sections"] button')?.textContent==='SHARED-CHAPTER');
 await guest.getByRole('navigation',{name:'Manuscript sections'}).getByRole('button',{name:'SHARED-CHAPTER',exact:true}).click();
 await guest.locator('.editor-section-title').filter({hasText:'SHARED-CHAPTER'}).waitFor();
 if(!await owner.locator('.shared-structure-tools').evaluate(el=>el.open))await owner.getByText('Organize manuscript',{exact:true}).click();
 await owner.getByRole('button',{name:'Delete section',exact:true}).click();
 await owner.getByRole('dialog').getByRole('button',{name:'Delete',exact:true}).click();
 await guest.locator('.editor-section-title').filter({hasText:'Chapter one'}).waitFor();
 assert.equal((await (await guest.request.get(origin+'/api/auth/me')).json()).isAuthenticated,true);
 await owner.getByRole('navigation',{name:'Manuscript sections'}).getByRole('button',{name:'Chapter one',exact:true}).click();
 await owner.locator('.ProseMirror table').filter({hasText:'SHARED-CELL'}).waitFor();
 await Promise.all([owner.getByText('Saved to shared manuscript',{exact:true}).waitFor(),guest.getByText('Saved to shared manuscript',{exact:true}).waitFor()]);
 await mkdir('artifacts/realtime-full',{recursive:true});await owner.screenshot({path:'artifacts/realtime-full/editor.png',fullPage:true});
 assert.equal(await owner.locator('.prosa-editor').count(),1);
 assert.equal(await guest.locator('.prosa-editor').count(),1);
 await guest.evaluate(()=>{window.__accountInvalidations=0;window.__accountProbe=new BroadcastChannel('prosa-collaboration-account-v1');window.__accountProbe.onmessage=e=>{if(e.data.identity===null)window.__accountInvalidations++;};});
 await owner.getByRole('button',{name:'Share',exact:true}).click();
 await owner.getByRole('button',{name:'Viewer',exact:true}).click();
 await guest.getByText('Access changed.',{exact:false}).waitFor();
 assert.equal(await guest.evaluate(()=>window.__accountInvalidations),0,'A manuscript role change must not invalidate unrelated account sessions');
 await guest.reload();
 await guest.locator('.ProseMirror[contenteditable=false]').waitFor();
 const before=await guest.locator('.ProseMirror').innerHTML();
 await guest.locator('.ProseMirror').click();await guest.keyboard.type('VIEWER-MUST-NOT-WRITE');
 assert.equal(await guest.locator('.ProseMirror').innerHTML(),before);
 await owner.getByRole('link',{name:'Back to manuscript',exact:true}).click();
 await owner.locator('.ProseMirror[contenteditable=true]').waitFor();
 await owner.locator('.ProseMirror').press('Control+End');await owner.locator('.ProseMirror').pressSequentially(' VIEWER-LIVE');
 await guest.waitForFunction(()=>document.querySelector('.ProseMirror')?.textContent.includes('VIEWER-LIVE'));
 await owner.request.get(origin+'/__probe/pending');
 await owner.locator('.ProseMirror').pressSequentially(' AFTER-PROJECTION-RETRY');
 await guest.waitForFunction(()=>document.querySelector('.ProseMirror')?.textContent.includes('AFTER-PROJECTION-RETRY'),null,{timeout:40000});
 assert.equal(await guest.locator('.prosa-editor').count(),1);
 await guest.screenshot({path:'artifacts/realtime-full/viewer.png',fullPage:true});
 await owner.getByRole('button',{name:'Share',exact:true}).click();
 await owner.getByRole('button',{name:'Revoke access',exact:true}).click();
 await guest.getByText('Access changed.',{exact:false}).waitFor();
 assert.equal((await (await guest.request.get(origin+'/api/auth/me')).json()).isAuthenticated,true);
 assert.equal(await guest.getByText('Logout',{exact:true}).count(),1);
 assert.deepEqual(errors.filter(e=>e.includes('Unhandled')||e.includes('crit:')||e.includes('Error:')),[]);
 await writeFile('artifacts/realtime-full/browser.json',JSON.stringify({passed:true,checks:['full normal Prosa editor','email invitation without code','authenticated inbox acceptance','two-way live edits','reload persistence','page switching and isolated page journals','normal bold and heading controls','collaborative toolbar undo and redo','checked AI preview isolation, Apply and AI Undo in normal editor','invited-editor whole-section AI Apply and scoped Undo','checked replacement remounts both normal editors','whole-page restore through normal History','normal structure controls and live navigation updates','viewer role enforcement','viewer live projection updates and retries','revocation preserves sign-in'],scope:'Actual normal Prosa components and module, Kestrel/SQLite/Node collaboration, isolated synthetic auth and shell data in test proxy only. Live AI provider generation, metadata, font sizing and export are not covered by this probe.'},null,2));
 console.log('PASS: full Prosa editor, email invitation, page isolation, toolbar formatting, undo/redo, viewer updates and revocation');
 }
}catch(error){
 const states=[];for(const context of browser?.contexts()??[])for(const page of context.pages())states.push({url:page.url(),text:(await page.locator('body').innerText()).slice(-5000)});
 throw new Error(error.message+' '+JSON.stringify({states,errors}),{cause:error});
}finally{await browser?.close();await service?.destroy();await new Promise(r=>http.close(r));}

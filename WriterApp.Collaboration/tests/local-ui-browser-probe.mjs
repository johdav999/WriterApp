import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,mkdir,writeFile} from 'node:fs/promises';
import {createRequire} from 'node:module';
import {createCollaborationServer} from '../src/server.mjs';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const backend=process.env.PROBE_BACKEND_URL;let service,browser,viewerPending=0;
const http=createServer(async(req,res)=>{
 try{
  const url=new URL(req.url,'http://localhost');const actor=req.headers['x-test-user']??'owner';
  if(url.pathname==='/__probe/viewer-pending'){viewerPending=12;res.writeHead(204);res.end();return;}
  if(actor==='editor'&&url.pathname.endsWith('/collaboration/read')&&viewerPending>0){viewerPending--;res.writeHead(409,{'Content-Type':'application/json'});res.end(JSON.stringify({code:'collaboration_projection_pending'}));return;}
  const files={'/collaboration/test':['../local-test/index.html','text/html'],'/collaboration/test-client.js':['../local-test/client.mjs','text/javascript'],'/collaboration/test-editor.js':['../../WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js','text/javascript']};
  if(files[url.pathname]){const [file,type]=files[url.pathname];res.setHeader('Content-Type',type);res.end(await readFile(new URL(file,import.meta.url)));return;}
  if(url.pathname==='/api/auth/me'){res.setHeader('Content-Type','application/json');res.end(JSON.stringify({isAuthenticated:true,userId:actor,email:actor+'@fixture.test'}));return;}
  const chunks=[];for await(const chunk of req)chunks.push(chunk);
  const result=await fetch(new URL(req.url,backend),{method:req.method,headers:{'Content-Type':'application/json','X-Test-User':actor},body:req.method==='GET'?undefined:Buffer.concat(chunks)});
  const text=await result.text();let body=text;
  if(result.ok&&url.pathname.includes('/sessions')){const value=JSON.parse(text);value.webSocketUrl=`ws://127.0.0.1:${service.address.port}/`;body=JSON.stringify(value);}
  res.writeHead(result.status,{'Content-Type':'application/json'});res.end(body);
 }catch{res.writeHead(500);res.end();}
});
await new Promise(r=>http.listen(0,'127.0.0.1',r));const origin=`http://127.0.0.1:${http.address().port}`;
try{
 service=createCollaborationServer({backendUrl:backend,serviceKey:process.env.COLLABORATION_SERVICE_KEY,environment:'test',port:0,commitsEnabled:true,interactionsEnabled:true,allowedOrigins:[origin]});await service.listen();
 browser=await chromium.launch({channel:'msedge',headless:true});
 const owner=await browser.newPage({extraHTTPHeaders:{'X-Test-User':'owner'}}),guest=await browser.newPage({extraHTTPHeaders:{'X-Test-User':'editor'}});const errors=[];
 for(const page of[owner,guest])page.on('pageerror',e=>errors.push(e.message));
 await Promise.all([owner.goto(origin+'/collaboration/test'),guest.goto(origin+'/collaboration/test')]);
 await Promise.all([owner.locator('#workspace').waitFor({state:'visible'}),guest.locator('#workspace').waitFor({state:'visible'})]);
 assert.match(await owner.locator('#identity').textContent(),/owner/);assert.match(await guest.locator('#identity').textContent(),/editor/);
 await owner.waitForFunction(()=>!document.body.hasAttribute('aria-busy'));
 await guest.waitForFunction(()=>!document.body.hasAttribute('aria-busy'));
 await guest.locator('#create').click();await guest.locator('.ProseMirror').waitFor();
 const creationRequest=owner.waitForRequest(r=>r.url().endsWith('/api/collaboration/local-test')&&r.method()==='POST');
 await owner.locator('#create').click();const creation=(await creationRequest).postDataJSON();await owner.locator('.ProseMirror').waitFor();
 // A retry must use the migration's original version even after triggers rotate the live version.
 const retry=await owner.request.post(origin+'/api/collaboration/local-test',{data:creation});assert.equal(retry.status(),200,await retry.text());
 const collision=await owner.request.post(origin+'/api/collaboration/local-test',{data:{...creation,pageId:crypto.randomUUID()}});assert.equal(collision.status(),409);
 await owner.locator('#recipient').fill('editor');await owner.locator('#invite').click();
 await owner.waitForFunction(()=>document.getElementById('invite-result').textContent.includes('One-time code:'));
 const invite=await owner.locator('#invite-result').textContent(),id=invite.match(/Invitation ID: ([^\n]+)/)[1],token=invite.match(/One-time code: ([^\n]+)/)[1];
 await guest.locator('#invitation-id').fill(id);await guest.locator('#invitation-code').fill(token);await guest.locator('#accept').click();
 await guest.locator('#shared button').click();await guest.locator('.ProseMirror').waitFor();
 await owner.locator('.ProseMirror').press('End');await owner.locator('.ProseMirror').pressSequentially(' OWNER-LIVE');
 await guest.waitForFunction(()=>document.querySelector('.ProseMirror')?.textContent.includes('OWNER-LIVE'));
 await guest.locator('.ProseMirror').press('End');await guest.locator('.ProseMirror').pressSequentially(' GUEST-LIVE');
 await owner.waitForFunction(()=>document.querySelector('.ProseMirror')?.textContent.includes('GUEST-LIVE'));
 await Promise.all([owner.waitForFunction(()=>document.getElementById('save-status').textContent==='Saved to shared manuscript'),guest.waitForFunction(()=>document.getElementById('save-status').textContent==='Saved to shared manuscript')]);
 await guest.reload();await guest.locator('#shared button').click();await guest.locator('.ProseMirror').waitFor();
 await guest.waitForFunction(()=>document.querySelector('.ProseMirror')?.textContent.includes('OWNER-LIVE')&&document.querySelector('.ProseMirror')?.textContent.includes('GUEST-LIVE'));
 const member=owner.locator('#members li').filter({hasText:'editor'});await member.getByRole('button',{name:'Viewer',exact:true}).click();
 await guest.waitForFunction(()=>document.getElementById('writing').hidden&&!document.getElementById('workspace').hidden);
 await guest.reload();await guest.locator('#shared button').click();await guest.locator('#viewer').waitFor({state:'visible'});
 assert.equal(await guest.locator('.ProseMirror[contenteditable=true]').count(),0);
 await owner.request.post(origin+'/__probe/viewer-pending');
 await owner.locator('.ProseMirror').press('End');await owner.locator('.ProseMirror').pressSequentially(' VIEWER-FOLLOWS');
 await guest.waitForFunction(()=>document.getElementById('save-status').textContent.includes('Updating shared writing'));
 assert.equal(await guest.locator('#writing').isVisible(),true);assert.equal(await guest.locator('#workspace').isVisible(),true);
 await guest.waitForFunction(()=>document.getElementById('viewer').textContent.includes('VIEWER-FOLLOWS'));
 await owner.locator('#members li').filter({hasText:'editor'}).getByRole('button',{name:'Revoke access',exact:true}).click();
 await guest.waitForFunction(()=>document.getElementById('writing').hidden&&!document.getElementById('workspace').hidden&&document.querySelectorAll('#shared button').length===0);
 assert.match(await guest.locator('#identity').textContent(),/editor@fixture.test/);
 assert.equal(await guest.locator('#create').isVisible(),true);
 await guest.locator('#owned button').click();await guest.locator('.ProseMirror[contenteditable=true]').waitFor();
 await guest.locator('.ProseMirror').press('End');await guest.locator('.ProseMirror').pressSequentially(' OWN-WRITING-AFTER-REVOCATION');
 await guest.waitForFunction(()=>document.getElementById('save-status').textContent==='Saved to shared manuscript');
 // Restore a previously revoked member through a fresh Viewer invitation.
 await owner.locator('#role').selectOption('1');await owner.locator('#invite').click();
 await owner.waitForFunction(oldId=>{const text=document.getElementById('invite-result').textContent;return text.includes('One-time code:')&&!text.includes(oldId);},id);
 const reinvite=await owner.locator('#invite-result').textContent(),reid=reinvite.match(/Invitation ID: ([^\n]+)/)[1],retoken=reinvite.match(/One-time code: ([^\n]+)/)[1];
 await guest.locator('#invitation-id').fill(reid);await guest.locator('#invitation-code').fill('incomplete');await guest.locator('#accept').click();
 await guest.waitForFunction(()=>document.getElementById('status').textContent.includes('one-time code is incomplete'));
 await guest.locator('#invitation-code').fill(' '+retoken+' ');await guest.locator('#accept').click();
 await guest.locator('#shared button').click();await guest.locator('#viewer').waitFor({state:'visible'});
 assert.equal(await guest.locator('.ProseMirror[contenteditable=true]').count(),0);
 assert.deepEqual(errors,[]);
 await mkdir('artifacts/realtime-local',{recursive:true});await writeFile('artifacts/realtime-local/browser.json',JSON.stringify({passed:true,checks:['isolated actor identities','create and migrate disposable manuscript','account-bound invitation and acceptance','bidirectional typing','durable receipts','reload persistence','viewer downgrade','viewer follows committed projections','revocation closes only shared writing','account and own manuscript remain usable after revocation'],scope:'Actual local-test frontend and rebuilt editor with real Kestrel/Node/SQLite. Synthetic auth headers only in owned fixture; real Prosa login is tested separately.'},null,2));
 console.log('PASS: local two-account UI creates, invites, edits both ways, reloads, downgrades to live read-only and revokes');
}catch(error){
 const states=[];for(const context of browser?.contexts()??[])for(const page of context.pages())states.push(await page.evaluate(()=>({status:document.getElementById('status')?.textContent,save:document.getElementById('save-status')?.textContent,writingHidden:document.getElementById('writing')?.hidden,editorHidden:document.getElementById('editor')?.hidden,viewer:document.getElementById('viewer')?.textContent})));
 await mkdir('artifacts/realtime-local',{recursive:true});await writeFile('artifacts/realtime-local/browser-failure.json',JSON.stringify({error:error.message,states},null,2));throw new Error(error.message+' '+JSON.stringify(states),{cause:error});
}finally{await browser?.close();await service?.destroy();await new Promise(r=>http.close(r));}

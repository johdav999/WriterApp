// Actual shared proposal module, both shipped editor factories and production IndexedDB.
// Static Razor/interop receivers are fixtures, not authenticated/native/live-provider UAT.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve,dirname} from 'node:path';
import {createRequire} from 'node:module';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const directory=dirname(process.env.WRITERAPP_BRAINSTORMING_CONTEXT_HTML),evidence=resolve(process.env.BRAINSTORMING_EVIDENCE);await mkdir(evidence,{recursive:true});
const files=['WriterApp.UI.Shared/wwwroot/brainstorming-proposal.js','WriterApp.UI.Shared/BrainstormingProposalPreview.razor.css','WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js','WriterApp.Client/wwwroot/js/brainstorming-store.js','WriterApp.Device.Shared/wwwroot/editor/device-editor.js','WriterApp.Device.Shared/wwwroot/editor/device-editor.css'];
const aliases={'/_content/WriterApp.UI.Shared/brainstorming-proposal.js':files[0],'/_content/WriterApp.Device.Shared/editor/device-editor.js':files[4]};
const server=createServer(async(req,res)=>{const url=new URL(req.url,'http://localhost').pathname;const name=aliases[url]??url.slice(1);res.setHeader('Cache-Control','no-store');
 if(['web','device'].includes(name)){res.setHeader('Content-Type','text/html');res.end('<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width"><link rel="stylesheet" href="/'+files[1]+'"><link rel="stylesheet" href="/'+files[5]+'"><style>body{font:16px system-ui;margin:16px}*{box-sizing:border-box}.brainstorm-proposal-editor{max-width:100%}</style>'+await readFile(resolve(directory,'draft-review-'+name+'.html'),'utf8'));return;}
 if(!files.includes(name)){res.writeHead(404);res.end();return;}res.setHeader('Content-Type',name.endsWith('.js')?'text/javascript':'text/css');res.end(await readFile(name));});
await new Promise(done=>server.listen(0,'127.0.0.1',done));const browser=await chromium.launch({channel:'msedge',headless:true}),page=await browser.newPage({viewport:{width:1400,height:950}});const base=`http://127.0.0.1:${server.address().port}`,checks=[],errors=[];page.on('pageerror',e=>errors.push(e.message));
try {
 for(const surface of ['web','device']){
  const cache=JSON.parse(await readFile(resolve(directory,'draft-buffer-'+surface+'.json'),'utf8')),initial=JSON.parse(await readFile(resolve(directory,'draft-base-'+surface+'.json'),'utf8'));
  await page.goto(base+'/'+surface);assert.equal(await page.locator('script,img').count(),0);assert.equal(await page.getByRole('button',{name:'Previous version'}).isVisible(),false);assert(await page.getByRole('button',{name:'Save version'}).isVisible());
  if(surface==='web')await page.addScriptTag({url:base+'/'+files[2]});
  const before=await page.evaluate(async({cache,surface})=>{
   const module=await import('/_content/WriterApp.UI.Shared/brainstorming-proposal.js');window.changes=[];
   const host=document.querySelector('.brainstorm-proposal-editor');if(host.nextElementSibling?.tagName==='PRE')host.nextElementSibling.remove();
   window.proposal=await module.create(host,cache.draftEdit.html,surface==='web'?'Web':'Device',{invokeMethodAsync(...args){window.changes.push(args);return Promise.resolve();}},cache.draftEdit.candidateId,cache.draftEdit.baseFingerprint);
   return window.proposal.snapshot();
  },{cache,surface});assert.equal(before.plainText,cache.draftEdit.plainText);assert(before.html.includes('<strong>Åsa</strong>'));
  const box=page.getByRole('textbox',{name:'Proposed prose editor'});await box.focus();await page.keyboard.press('Control+End');await page.keyboard.type(' She waited.');const edited=await page.evaluate(()=>window.proposal.snapshot());assert(edited.plainText.endsWith(' She waited.'));assert(edited.html.includes('<strong>Åsa</strong>'));await page.evaluate(text=>document.querySelector('.brainstorm-comparison section:last-child pre').textContent=text,edited.plainText);
  const highlights=await page.evaluate(previous=>{window.proposal.setChanges(previous,true);const size=CSS.highlights.get('prosa-working-draft').size;const snapshot=window.proposal.snapshot();window.proposal.setChanges(previous,false);return {size,off:CSS.highlights.get('prosa-working-draft').size,html:snapshot.html};},before.plainText);
  assert.equal(highlights.size,1);assert.equal(highlights.off,0);assert.equal(highlights.html,edited.html);
  await page.getByText(/Version history ·/).click();assert(await page.getByText(/Saved versions · Main draft/).isVisible());await page.getByText(/Version history ·/).click();
  assert((await page.evaluate(()=>window.changes)).some(args=>args[0]==='OnProposalChanged'&&args[3]===cache.draftEdit.candidateId));
  const selected=await page.evaluate(()=>{const root=document.querySelector('[aria-label="Proposed prose editor"]');let leaf=root.querySelector('p strong').firstChild;const range=document.createRange();range.setStart(leaf,0);range.setEnd(leaf,3);const selection=window.getSelection();selection.removeAllRanges();selection.addRange(range);document.dispatchEvent(new Event('selectionchange'));return new Promise(done=>requestAnimationFrame(()=>done(window.proposal.selection())));});assert.equal(selected.selectedText,'Åsa');assert.equal(selected.plainText.slice(selected.selectionStart,selected.selectionEnd),'Åsa');
  await page.getByRole('combobox',{name:'Draft paragraph style'}).selectOption('heading:2');assert(await box.locator('h2').count()>0);
  await page.getByRole('button',{name:'Undo',exact:true}).click();assert.equal(await box.locator('h2').count(),0);
  await page.getByRole('button',{name:'Redo',exact:true}).click();assert(await box.locator('h2').count()>0);
  await page.evaluate(()=>window.proposal.setEditable(false));assert.equal(await box.getAttribute('contenteditable'),'false');assert(await page.getByRole('combobox',{name:'Draft paragraph style'}).isDisabled());assert(await page.getByRole('button',{name:'Bold',exact:true}).isDisabled());await page.evaluate(()=>window.proposal.setEditable(true));
  // Store .NET-validated draft buffer and review metadata through the actual persistence module.
  const stored=await page.evaluate(async ({cache,initial})=>{const store=await import('/WriterApp.Client/wwwroot/js/brainstorming-store.js');const scope=cache.session.scope,op=cache.localReceipts.at(-1).operationId,first=initial.localReceipts.at(-1).operationId;await store.write('a'.repeat(64),scope.documentId,scope.sessionId,JSON.stringify(initial),0,first,JSON.stringify({version:1,operationId:first,expectedRevision:0,proposed:initial,capturedAt:new Date().toISOString()}));await store.write('a'.repeat(64),scope.documentId,scope.sessionId,JSON.stringify(cache),cache.cacheRevision-1,op,JSON.stringify({version:1,operationId:op,expectedRevision:cache.cacheRevision-1,proposed:cache,capturedAt:new Date().toISOString()}));return await store.read('a'.repeat(64),scope.documentId,scope.sessionId);},{cache,initial});assert.deepEqual(JSON.parse(stored),cache);
  // Static Razor fixture: reveal the compare surface; real tab events run in brainstorming-focused.mjs.
  await page.evaluate(()=>document.querySelector('.brainstorm-comparison').parentElement.hidden=false);await page.screenshot({path:resolve(evidence,'draft-'+surface+'-wide.png'),fullPage:true});await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));await box.focus();assert.equal(await page.evaluate(()=>document.activeElement.getAttribute('aria-label')),'Proposed prose editor');await page.screenshot({path:resolve(evidence,'draft-'+surface+'-narrow.png'),fullPage:true});
  await page.evaluate(()=>window.proposal.destroy());await page.reload();
  const restored=await page.evaluate(async cache=>{const store=await import('/WriterApp.Client/wwwroot/js/brainstorming-store.js');return JSON.parse(await store.read('a'.repeat(64),cache.session.scope.documentId,cache.session.scope.sessionId));},cache);assert.deepEqual(restored.draftEdit,cache.draftEdit);assert.deepEqual(restored.draftReview,cache.draftReview);
  if(surface==='web')await page.addScriptTag({url:base+'/'+files[2]});
  const rejected=await page.evaluate(async surface=>{const module=await import('/_content/WriterApp.UI.Shared/brainstorming-proposal.js');try{await module.create(document.querySelector('.brainstorm-proposal-editor'),'<p style="color:red">Unsupported</p>',surface==='web'?'Web':'Device',{invokeMethodAsync(){return Promise.resolve();}});return false;}catch{return true;}},surface);assert(rejected);checks.push(`${surface}: actual rich proposal editing preserves marks/Unicode, verified UTF-16 selection, read-only generation control, responsive comparison, and real IndexedDB draft-buffer reload; unsupported markup refused`);
  await page.setViewportSize({width:1400,height:950});
 }
 assert.deepEqual(errors,[]);const report={scope:'Actual shared proposal JS and both shipped common editors; static actual C# handler Razor and synthetic interop/storage data. No signed-in/native/live-provider claim.',browser:browser.version(),checks,errors};await writeFile(resolve(evidence,'draft-result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
} finally {await browser.close();await new Promise(done=>server.close(done));}

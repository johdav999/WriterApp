// Production Razor handler fixtures plus actual IndexedDB recovery. This does not claim live signed-in/native UI.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve,dirname} from 'node:path';
import {createRequire} from 'node:module';
import {randomUUID} from 'node:crypto';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const directory=dirname(process.env.WRITERAPP_BRAINSTORMING_CONTEXT_HTML),evidence=resolve(process.env.BRAINSTORMING_EVIDENCE);await mkdir(evidence,{recursive:true});
const server=createServer(async(req,res)=>{
 const name=new URL(req.url,'http://localhost').pathname.slice(1);res.setHeader('Cache-Control','no-store');
 if(['web','device'].includes(name)){res.setHeader('Content-Type','text/html');res.end('<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width"><link rel="stylesheet" href="/planning.css"><style>body{font:16px system-ui;margin:16px}*{box-sizing:border-box}</style>'+await readFile(resolve(directory,'planning-review-'+name+'.html'),'utf8')+'<script type="module">window.store=await import("/store.js");</script>');return;}
 if(name==='planning.css'){res.setHeader('Content-Type','text/css');res.end(await readFile('WriterApp.UI.Shared/BrainstormingPlanningPanel.razor.css'));return;}
 if(name==='store.js'){res.setHeader('Content-Type','text/javascript');res.end(await readFile('WriterApp.Client/wwwroot/js/brainstorming-store.js'));return;}
 res.writeHead(404);res.end();
});
await new Promise(done=>server.listen(0,'127.0.0.1',done));const browser=await chromium.launch({channel:'msedge',headless:true}),page=await browser.newPage({viewport:{width:1400,height:950}});const base=`http://127.0.0.1:${server.address().port}`,checks=[],errors=[];page.on('pageerror',e=>errors.push(e.message));
try {
 for(const surface of ['web','device']) {
  const fixture=JSON.parse(await readFile(resolve(directory,'planning-cache-'+surface+'.json'),'utf8'));
  const uncertain=JSON.parse(await readFile(resolve(directory,'planning-unknown-'+surface+'.json'),'utf8'));
  await page.goto(base+'/'+surface);await page.waitForFunction(()=>!!window.store);
  assert(await page.getByRole('button',{name:'Approve selected planning fields',exact:true}).isVisible());
  assert.equal(await page.locator('script:not([type=module])').count(),0);assert.equal(await page.evaluate(()=>window.providerRan),undefined);
  assert(await page.getByText('Exact selected fields',{exact:false}).isVisible());assert.equal(fixture.planningOperations[0].review.fields.length,1);assert.equal(fixture.planningOperations[0].review.fields[0].field,'Logline');
  const partition=(surface==='web'?'a':'b').repeat(64);
  await page.evaluate(async({fixture,partition})=>{
   const scope=fixture.session.scope,op=fixture.localReceipts.at(-1).operationId;
   const incoming={...fixture,cacheRevision:1};
   const outcome=await window.store.write(partition,scope.documentId,scope.sessionId,JSON.stringify(incoming),0,op,JSON.stringify({version:1,operationId:op,expectedRevision:0,proposed:incoming,capturedAt:new Date().toISOString()}));if(outcome.conflict)throw Error('Unexpected initial conflict');
  },{fixture,partition});await page.reload();await page.waitForFunction(()=>!!window.store);
  let recovered=await page.evaluate(async({fixture,partition})=>JSON.parse(await window.store.read(partition,fixture.session.scope.documentId,fixture.session.scope.sessionId)),{fixture,partition});
  assert.deepEqual(recovered.planningOperations,fixture.planningOperations);assert.deepEqual(recovered.session.contexts,fixture.session.contexts);
  checks.push(surface+': durable exact selected-field review, original context and typed decision reload in production IndexedDB');
  const op=randomUUID();recovered={...recovered,cacheRevision:2,localReceipts:[...recovered.localReceipts,{operationId:op,revision:2,payloadHash:'e'.repeat(64)}],planningOperations:uncertain.planningOperations};assert.equal(recovered.planningOperations[0].receipt.state,6);
  await page.evaluate(async({cache,partition,op})=>{const s=cache.session.scope;await window.store.write(partition,s.documentId,s.sessionId,JSON.stringify(cache),1,op,JSON.stringify({version:1,operationId:op,expectedRevision:1,proposed:cache,capturedAt:new Date().toISOString()}));},{cache:recovered,partition,op});
  await page.reload();await page.waitForFunction(()=>!!window.store);assert.deepEqual((await page.evaluate(async({fixture,partition})=>JSON.parse(await window.store.read(partition,fixture.session.scope.documentId,fixture.session.scope.sessionId)),{fixture,partition})).planningOperations,recovered.planningOperations);
  assert.equal(await page.evaluate(async({fixture})=>window.store.read('d'.repeat(64),fixture.session.scope.documentId,fixture.session.scope.sessionId),{fixture}),null);
  checks.push(surface+': uncertain planning transport survives reload, cannot become false success, and foreign partition is empty');
  await page.getByRole('button',{name:'Approve selected planning fields',exact:true}).focus();assert(await page.getByRole('button',{name:'Approve selected planning fields',exact:true}).evaluate(e=>document.activeElement===e));
  await page.screenshot({path:resolve(evidence,'planning-'+surface+'-wide.png'),fullPage:true});await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));
  await page.screenshot({path:resolve(evidence,'planning-'+surface+'-narrow.png'),fullPage:true});await page.setViewportSize({width:1400,height:950});checks.push(surface+': encoded provider-shaped text, before/after subset review, keyboard focus and unclipped 390px layout');
 }
 assert.deepEqual(errors,[]);const report={scope:'Production Razor review fixtures with actual IndexedDB journal/reload; production handler callbacks verified in C#. No signed-in interactive browser, native or provider claim.',browser:browser.version(),checks,errors};await writeFile(resolve(evidence,'planning-result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
}finally{await browser.close();await new Promise(done=>server.close(done));}

// Production shared recovery render + actual IndexedDB. Authenticated host callbacks have separate C# evidence.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve,dirname} from 'node:path';
import {createRequire} from 'node:module';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const directory=dirname(process.env.WRITERAPP_BRAINSTORMING_CONTEXT_HTML),evidence=resolve(process.env.BRAINSTORMING_EVIDENCE);await mkdir(evidence,{recursive:true});
const fixture=JSON.parse(await readFile(resolve(directory,'recovery-cache.json'),'utf8'));
const server=createServer(async(req,res)=>{
 const path=new URL(req.url,'http://localhost').pathname;res.setHeader('Cache-Control','no-store');
 if(path==='/recovery'){res.setHeader('Content-Type','text/html');res.end('<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width"><link rel="stylesheet" href="/recovery.css"><style>body{font:16px system-ui;margin:16px}*{box-sizing:border-box}</style>'+await readFile(resolve(directory,'recovery-review.html'),'utf8')+'<script type="module">window.store=await import("/store.js");</script>');return;}
 if(path==='/recovery.css'){res.setHeader('Content-Type','text/css');res.end(await readFile('WriterApp.UI.Shared/BrainstormingRecoveryPanel.razor.css'));return;}
 if(path==='/store.js'){res.setHeader('Content-Type','text/javascript');res.end(await readFile('WriterApp.Client/wwwroot/js/brainstorming-store.js'));return;}
 res.writeHead(404);res.end();
});
await new Promise(done=>server.listen(0,'127.0.0.1',done));const browser=await chromium.launch({channel:'msedge',headless:true}),page=await browser.newPage({viewport:{width:1400,height:950}});const checks=[],errors=[];page.on('pageerror',e=>errors.push(e.message));
try {
 await page.goto(`http://127.0.0.1:${server.address().port}/recovery`);await page.waitForFunction(()=>!!window.store);
 await page.locator('summary').filter({hasText:'Recovery and application history'}).click();await page.locator('summary').filter({hasText:'Retained device copy'}).click();
 assert(await page.getByText('These are recovery evidence',{exact:false}).isVisible());assert.equal(await page.getByRole('button',{name:/Approve|Keep exactly|Retry/}).count(),0);
 await page.locator('summary').filter({hasText:'Device planning'}).click();assert(await page.getByText('Her brother must remain unaware until Chapter 4.',{exact:false}).isVisible());
 checks.push('Shared recovery review labels device reports separately from authoritative server commits and cannot replay approvals');
 const partition='9'.repeat(64),operation=fixture.localReceipts.at(-1).operationId;
 await page.evaluate(async({fixture,partition,operation})=>{const s=fixture.session.scope;await window.store.write(partition,s.documentId,s.sessionId,JSON.stringify(fixture),0,operation,JSON.stringify({version:1,operationId:operation,expectedRevision:0,proposed:fixture,capturedAt:new Date().toISOString()}));},{fixture,partition,operation});
 await page.reload();await page.waitForFunction(()=>!!window.store);
 const recovered=await page.evaluate(async({fixture,partition})=>JSON.parse(await window.store.read(partition,fixture.session.scope.documentId,fixture.session.scope.sessionId)),{fixture,partition});assert.deepEqual(recovered.recovery,fixture.recovery);assert.equal(recovered.recovery.copies[0].planning[0].receipt.state,6);
 assert.equal(await page.evaluate(async fixture=>window.store.read('8'.repeat(64),fixture.session.scope.documentId,fixture.session.scope.sessionId),fixture),null);checks.push('Actual IndexedDB reload retains original namespaces, exact recovery fields and Unknown without creating a server receipt; foreign partition stays empty');
 await page.locator('summary').first().click();await page.locator('summary').filter({hasText:'Retained device copy'}).click();await page.locator('summary').filter({hasText:'Device planning'}).click();await page.getByRole('button',{name:'Show more recovery history'}).focus();assert(await page.getByRole('button',{name:'Show more recovery history'}).evaluate(e=>document.activeElement===e));
 await page.screenshot({path:resolve(evidence,'recovery-wide.png'),fullPage:true});await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));await page.screenshot({path:resolve(evidence,'recovery-narrow.png'),fullPage:true});checks.push('Keyboard-accessible recovery copy/history remains unclipped at 390px');
 assert.deepEqual(errors,[]);const report={scope:'Production Razor recovery fixture and actual IndexedDB reload; C# verifies actual host callbacks separately. No signed-in/native/provider claim.',browser:browser.version(),checks,errors};await writeFile(resolve(evidence,'recovery-result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
}finally{await browser.close();await new Promise(done=>server.close(done));}

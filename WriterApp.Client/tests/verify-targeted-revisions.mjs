import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const output = resolve(process.env.EDITOR_EVIDENCE_DIR || 'artifacts/ai-parity-p01');
await mkdir(output, { recursive: true });
const root = new URL('../../', import.meta.url);
const allowed = new Set([
 'WriterApp.Client/tests/device-editor.html',
 'WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js',
 'WriterApp.Client/wwwroot/js/tiptap-editor-patch.js',
 'WriterApp.Client/wwwroot/js/web-ai-history-outbox.js',
 'WriterApp.Device.Shared/wwwroot/editor/device-editor.js',
 'WriterApp.Device.Shared/wwwroot/editor/device-editor.css',
 'WriterApp.Device.Shared/wwwroot/app.css',
 'WriterApp.UI.Shared/wwwroot/design-tokens.css',
 'WriterApp.UI.Shared/wwwroot/editor.css'
]);
const server = createServer(async (req, res) => {
 const name = new URL(req.url, 'http://localhost').pathname.slice(1);
 if (!allowed.has(name)) { res.writeHead(404); res.end(); return; }
 try { const data=await readFile(new URL(name,root)); res.setHeader('Content-Type', name.endsWith('.js')?'text/javascript':name.endsWith('.css')?'text/css':'text/html');res.end(data); }
 catch { res.writeHead(500);res.end(); }
});
await new Promise(done=>server.listen(0,'127.0.0.1',done));
let browser;
try {
 browser=await chromium.launch({channel:'msedge',headless:true});
 const page=await browser.newPage({viewport:{width:1280,height:800}}),errors=[];
 page.on('pageerror',error=>errors.push(error.message));
 await page.goto(`http://127.0.0.1:${server.address().port}/WriterApp.Client/tests/device-editor.html`);
 await page.waitForFunction(()=>/tests (passed|failed)/.test(document.title));
 const rows=await page.locator('#results li').allTextContents();
 const report={title:await page.title(),passed:rows.filter(r=>r.startsWith('PASS:')).length,failed:rows.filter(r=>r.startsWith('FAIL:')),errors,rows,
  scope:'Both shipped editor bundles and production client patch in headless Edge, synthetic data. Device source restoration is editor-level evidence; durable device Undo is verified separately by production workflow tests. No native or live provider acceptance.'};
 await writeFile(resolve(output,'browser-results.json'),JSON.stringify(report,null,2));
 console.log(JSON.stringify({passed:report.passed,failed:report.failed,errors}));
 assert.equal(report.failed.length,0);assert.equal(errors.length,0);
 const probe=await page.evaluate(async()=>{
  document.body.innerHTML='<h1>Targeted fix: unchanged formatting retained</h1><p>Shipped editors · synthetic prose · complete sentence rewrite</p><h2>Original</h2><div id="original"></div><h2>Client: Apply</h2><div id="client-fix"></div><h2>Device: preview</h2><div id="device-fix"></div>';
  const device=await import('/WriterApp.Device.Shared/wwwroot/editor/device-editor.js');
  const before='<p>The <strong>clock</strong> rang while another clock answered the clock in the hall.</p>',original='The clock rang while another clock answered the clock in the hall.',proposed='The clock rang while another clock answered the chime in the hall.';
  document.querySelector('#original').innerHTML=before;
  const receiver={invokeMethodAsync(){return Promise.resolve();}};
  const web=window.tiptapEditor.create('client-fix',before,receiver);
  const webResult=window.tiptapEditor.applyQualityIssueFixDetailed(web,{kind:'replace',from:0,to:original.length,docFrom:1,docTo:original.length+1,expectedText:original,anchorText:'clock',text:proposed,issueKey:'parity-fixture'});
  const desktopAfter=device.previewTargetedQualityRevision(before,'Html',0,original.length,original,proposed);
  device.create(document.querySelector('#device-fix'),desktopAfter,'Html',receiver,true);
  return{before,original,proposed,webResult,webAfter:web.getHTML(),desktopAfter};
 });
 assert.equal(probe.webAfter,probe.desktopAfter);assert.equal(probe.webAfter,'<p>The <strong>clock</strong> rang while another clock answered the chime in the hall.</p>');assert(probe.webResult.applied);
 await writeFile(resolve(output,'editor-parity-result.json'),JSON.stringify(probe,null,2));
 await page.screenshot({path:resolve(output,'targeted-fix-1280.png'),fullPage:true});
 await page.setViewportSize({width:480,height:800});await page.screenshot({path:resolve(output,'targeted-fix-480.png'),fullPage:true});
} finally { await browser?.close(); await new Promise(done=>server.close(done)); }

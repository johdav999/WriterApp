// Actual shipped editor transactions and reload, plus production Razor review fixtures.
// Authentication/host callbacks are exercised separately by BrainstormingKeepHostTests.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve,dirname} from 'node:path';
import {createRequire} from 'node:module';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const directory=dirname(process.env.WRITERAPP_BRAINSTORMING_CONTEXT_HTML),evidence=resolve(process.env.BRAINSTORMING_EVIDENCE);await mkdir(evidence,{recursive:true});
const files=new Set(['WriterApp.Client/tests/device-editor.html','WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js','WriterApp.Client/wwwroot/js/tiptap-editor-patch.js','WriterApp.Client/wwwroot/js/web-ai-history-outbox.js','WriterApp.Device.Shared/wwwroot/editor/device-editor.js','WriterApp.Device.Shared/wwwroot/editor/device-editor.css','WriterApp.Device.Shared/wwwroot/app.css','WriterApp.UI.Shared/wwwroot/design-tokens.css','WriterApp.UI.Shared/wwwroot/editor.css','WriterApp.UI.Shared/BrainstormingProposalPreview.razor.css','WriterApp.UI.Shared/BrainstormingPlacementReview.razor.css']);
const server=createServer(async(req,res)=>{const name=new URL(req.url,'http://localhost').pathname.slice(1);res.setHeader('Cache-Control','no-store');
 if(['web','device'].includes(name)){res.setHeader('Content-Type','text/html');res.end('<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width"><link rel="stylesheet" href="/WriterApp.UI.Shared/BrainstormingProposalPreview.razor.css"><link rel="stylesheet" href="/WriterApp.UI.Shared/BrainstormingPlacementReview.razor.css"><style>body{font:16px system-ui;margin:16px}*{box-sizing:border-box}</style>'+await readFile(resolve(directory,'keep-review-'+name+'.html'),'utf8'));return;}
 if(!files.has(name)){res.writeHead(404);res.end();return;}res.setHeader('Content-Type',name.endsWith('.js')?'text/javascript':name.endsWith('.css')?'text/css':'text/html');res.end(await readFile(name));});
await new Promise(done=>server.listen(0,'127.0.0.1',done));const browser=await chromium.launch({channel:'msedge',headless:true}),page=await browser.newPage({viewport:{width:1400,height:950}});const base=`http://127.0.0.1:${server.address().port}`,checks=[],errors=[];page.on('pageerror',e=>errors.push(e.message));
try {
 await page.goto(base+'/WriterApp.Client/tests/device-editor.html');await page.waitForFunction(()=>/tests (passed|failed)/.test(document.title));assert(!(await page.locator('#results li').allTextContents()).some(s=>s.startsWith('FAIL')));
 const reviews=await Promise.all(['empty','insert','replace','new'].map(async mode=>({mode,review:JSON.parse(await readFile(resolve(directory,'keep-'+mode+'.json'),'utf8'))})));
 for(const surface of ['web','device']) {
  const transactions=await page.evaluate(async({surface,reviews})=>{
   const device=await import('/WriterApp.Device.Shared/wwwroot/editor/device-editor.js'),api=surface==='web'?window.tiptapEditor:device;
   const receiver={invokeMethodAsync(){return Promise.resolve();}},checks=[];
   const same=(a,b)=>{if(a!==b)throw Error(`Expected ${JSON.stringify(b)}, got ${JSON.stringify(a)}`);};
   for(const {mode,review:r} of reviews){const before=r.kind===2?'':r.before.pages.find(p=>p.id===r.target.pageId).content;
    try{same(api.previewBrainstormingKeep(before,r.candidate.editorHtml,r.afterHtml,r.kind,r.from,r.to,r.replacedText),r.afterHtml);}catch(e){throw Error(surface+' '+mode+': '+e.message+'; source '+before+'; approved '+r.afterHtml);}
    const host=document.createElement('div');host.id='keep-'+surface+'-'+mode;document.body.append(host);
    const create=html=>surface==='web'?api.create(host.id,html,receiver):api.create(host,html,'Html',receiver,true);
    let editor=create(r.afterHtml),saved=surface==='web'?editor.getHTML():editor.snapshot().html;editor.destroy();editor=create(saved);
    const restored=surface==='web'?editor.getHTML():editor.snapshot().html;same(restored,saved);if(!restored.includes('<strong>Hon tvivlade.</strong>'))throw Error('Approved rich manual mark lost');
    const plain=surface==='web'?api.brainstormingPlainText(restored,'Html'):api.qualityPlainText(restored,'Html');if(!plain.includes(r.candidate.plainText))throw Error('Approved Unicode prose lost');editor.destroy();host.remove();checks.push(surface+': '+mode+' approved exact mapping and actual editor reload');
   }
   const rich='<p><em>Åsa</em> <strong>日本語 🧭</strong> waited.</p><ul><li><p>Untouched list</p></li></ul><table><tbody><tr><td><p>Table</p></td></tr></tbody></table><p><a href="https://example.com">Link</a></p>';
   const candidate='<p><strong>Hon</strong></p>',after=rich.replace('<em>Åsa</em>','<strong>Hon</strong>');same(api.previewBrainstormingKeep(rich,candidate,after,1,0,3,'Åsa'),after);checks.push(surface+': untouched list/table/link/Unicode/marks preserved');
   const reject=(args,label)=>{let refused=false;try{api.previewBrainstormingKeep(...args);}catch{refused=true;}if(!refused)throw Error('Not refused: '+label);checks.push(surface+': rejected '+label);};
   reject([rich,candidate,after,1,0,3,'Other'],'stale passage');reject([rich,candidate,'<p>Forged</p>',1,0,3,'Åsa'],'forged application snapshot');
   reject([rich,candidate,candidate+rich,0,1,1,''],'unsupported insertion offset');
   for(const bad of ['<p style="color:red">x</p>','<video>Lost</video>','<p><img src="/a.png"></p>'])reject([bad,candidate,candidate+bad,0,0,0,''],'unsupported source '+bad);
   return checks;
  },{surface,reviews});checks.push(...transactions);
 }
 for(const surface of ['web','device']){await page.goto(base+'/'+surface);assert(await page.getByRole('button',{name:'Add here'}).isVisible());assert(await page.getByRole('region',{name:'Confirm manuscript application'}).isVisible());assert(await page.getByText('Captured manuscript versus proposal',{exact:true}).isHidden());await page.screenshot({path:resolve(evidence,'keep-'+surface+'-wide.png'),fullPage:true});await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));await page.getByRole('button',{name:'Add here'}).focus();assert(await page.getByRole('button',{name:'Add here'}).evaluate(e=>document.activeElement===e));await page.screenshot({path:resolve(evidence,'keep-'+surface+'-narrow.png'),fullPage:true});await page.setViewportSize({width:1400,height:950});checks.push(surface+': exact approval review, hidden comparison during confirmation and keyboard focus at wide/390px');}
 assert.deepEqual(errors,[]);const report={scope:'Actual shipped web/device editor transaction and reload; production Razor review fixtures; C# host callbacks and owned persistence tested separately. No signed-in interactive browser/native/live-provider claim.',browser:browser.version(),checks,errors};await writeFile(resolve(evidence,'keep-result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
}finally{await browser.close();await new Promise(done=>server.close(done));}

// Run after the Release suite has emitted WRITERAPP_P02..P12_EVIDENCE fixtures.
// Start tests/serve-device-editor.mjs separately; it serves only allowlisted assets.
import {createRequire} from 'node:module';
import {readFile,writeFile,readdir,mkdir} from 'node:fs/promises';
import {resolve,join,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const require=createRequire(import.meta.url);
const {chromium}=require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const repo=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const evidence=resolve(process.argv[2] || join(repo,'artifacts/desktopai-p13'));
const host=process.env.DESKTOPAI_EDITOR_HOST || 'http://127.0.0.1:5179';
const browser=await chromium.launch({headless:true,channel:'msedge'});
const result={host:'Headless Edge; real shipped editors and static actual Razor fixtures',native:false,panels:[],editors:[]};
try {
 await mkdir(join(evidence,'screenshots'),{recursive:true});
 const css=(await Promise.all([
  'WriterApp.UI.Shared/wwwroot/design-tokens.css','WriterApp.Device.Shared/wwwroot/app.css',
  'WriterApp.UI.Shared/obj/Release/net10.0/scopedcss/bundle/WriterApp.UI.Shared.styles.css',
  'WriterApp.Device.Shared/obj/Release/net10.0/scopedcss/bundle/WriterApp.Device.Shared.styles.css',
  'WriterApp.Client/obj/Release/net10.0/scopedcss/bundle/WriterApp.Client.styles.css'
 ].map(path=>readFile(join(repo,path),'utf8')))).join('\n');
 for(const prompt of [2,3,4,5,6,7,8,9,10,11,12,14,15,16,18,19,20,21]) {
   const dir=join(evidence,'p'+String(prompt).padStart(2,'0'));
  const names=await readdir(dir).catch(error=>{if(prompt>=14&&error.code==='ENOENT')return [];throw error});
  for(const name of names.filter(n=>n.endsWith('.html')&&n!=='practice-sample.html').sort()) {
   const html=await readFile(join(dir,name),'utf8');
   const body=html.match(/<body[^>]*>([\s\S]*?)<\/body>/i)?.[1]??html;
   for(const [width,height] of [[1280,720],[1920,1080]]) {
    const page=await browser.newPage({viewport:{width,height}});const errors=[];
    page.on('pageerror',error=>errors.push(error.message));
    await page.setContent(`<!doctype html><html><head><meta charset="utf-8"><style>${css}\nbody{margin:0;padding:16px;font:16px system-ui}*{box-sizing:border-box}</style></head><body>${body}</body></html>`);
    if(prompt===21) {
     await page.locator('details > summary').first().focus();await page.keyboard.press('Enter');
     if(!await page.locator('details').first().evaluate(element=>element.open))errors.push('Keyboard cannot open native account guidance');
    }
    const text=await page.locator('body').innerText();
    if(text.trim().length<20)errors.push('Readable results/status absent');
    const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1);
    if(overflow)errors.push('Horizontal overflow');
    const focused=[];
    // Check actual enabled native controls in the static fixture. Razor event behavior is covered by component tests.
    const controls=await page.locator('button:enabled,a[href],select:enabled,input:enabled,textarea:enabled,summary').count();
    for(let i=0;i<Math.min(controls,12);i++) {
     await page.keyboard.press('Tab');
     focused.push(await page.evaluate(()=>({tag:document.activeElement.tagName,text:document.activeElement.textContent?.trim().slice(0,100),outline:getComputedStyle(document.activeElement).outlineStyle})));
    }
    if(controls&&focused.every(item=>item.tag==='BODY'))errors.push('Keyboard never reaches a control');
    if(prompt===20) {
     const summary=page.locator('details summary').first();await summary.focus();await page.keyboard.press('Enter');
     if(!await page.locator('details').first().evaluate(element=>element.open))errors.push('Keyboard cannot inspect retained intent');
     const detailText=await page.locator('details').first().innerText();
     if(!detailText.includes('Original saved content')||!detailText.includes('<script>alert(1)</script>'))errors.push('Original/proposed evidence is not readable inert text');
     if(await page.locator('details script').count())errors.push('Provider history markup became executable');
     if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1))errors.push('Expanded history evidence overflows');
    }
    await page.evaluate(()=>window.scrollTo(0,0));
    const imageName=`p${prompt}-${name.replace('.html','')}-${width}x${height}`;
    await page.screenshot({path:join(evidence,'screenshots',imageName+'.png')});
    if(await page.evaluate(()=>document.documentElement.scrollHeight>innerHeight))
     await page.screenshot({path:join(evidence,'screenshots',imageName+'-full.png'),fullPage:true});
    result.panels.push({prompt,fixture:name,width,height,overflow,controls,focused,text,errors});await page.close();
   }
  }
 }
 const page=await browser.newPage({viewport:{width:1280,height:720}});const errors=[];
 page.on('pageerror',error=>errors.push(error.message));
 await page.goto(host+'/WriterApp.Client/tests/device-editor.html');
 await page.waitForFunction(()=>/tests (passed|failed)/.test(document.title));
 const tests=await page.locator('#results li').allTextContents();
 result.harness={passed:tests.filter(t=>t.startsWith('PASS')).length,failed:tests.filter(t=>t.startsWith('FAIL')),tests,errors};
 if(result.harness.failed.length||errors.length)throw new Error('Shipped editor harness failed');
 await page.close();
 // Two independent tabs share the real IndexedDB origin and can neither overwrite intents nor downgrade receipts.
 const tabContext=await browser.newContext();const tabs=await Promise.all([tabContext.newPage(),tabContext.newPage()]);
 await Promise.all(tabs.map(tab=>tab.goto(host+'/WriterApp.Client/tests/history-outbox.html')));
 const tabScope='synthetic-multitab-'+Date.now(),tabDocument='00000000-0000-0000-0000-000000000020';
 const ids=['00000000-0000-0000-0000-000000000021','00000000-0000-0000-0000-000000000022'];
 await Promise.all(tabs.map((tab,i)=>tab.evaluate(async ({scope,document,id})=>{
   const outbox=await import('/WriterApp.Client/wwwroot/js/web-ai-history-outbox.js');
   await outbox.prepare(scope,JSON.stringify({version:1,operationId:id,source:{documentId:document},beforeContent:'Authored 日本語',afterContent:'Approved '+id}));
 },{scope:tabScope,document:tabDocument,id:ids[i]})));
 const sharedRows=await tabs[0].evaluate(async ({scope,document})=>JSON.parse(await (await import('/WriterApp.Client/wwwroot/js/web-ai-history-outbox.js')).list(scope,document)),{scope:tabScope,document:tabDocument});
 if(sharedRows.length!==2||ids.some(id=>!sharedRows.some(row=>row.operationId===id)))throw new Error('Real multi-tab intents were overwritten');
 await tabs[0].evaluate(async ({scope,id})=>(await import('/WriterApp.Client/wwwroot/js/web-ai-history-outbox.js')).mark(scope,id,'Confirmed','Synthetic validated receipt','{}'),{scope:tabScope,id:ids[0]});
 await tabs[1].evaluate(async ({scope,id})=>(await import('/WriterApp.Client/wwwroot/js/web-ai-history-outbox.js')).mark(scope,id,'Rejected','Late stale tab response',null),{scope:tabScope,id:ids[0]});
 await tabs[0].reload();
 const reopenedRows=await tabs[0].evaluate(async ({scope,document})=>JSON.parse(await (await import('/WriterApp.Client/wwwroot/js/web-ai-history-outbox.js')).list(scope,document)),{scope:tabScope,document:tabDocument});
 if(reopenedRows.find(row=>row.operationId===ids[0])?.status!=='Confirmed')throw new Error('Reload/late tab downgraded confirmed delivery');
 result.outbox={realIndexedDb:true,tabs:2,concurrentIntents:2,reloadRetained:true,lateTabPreservesConfirmation:true};await tabContext.close();
 const sample=await readFile(join(evidence,'p12/practice-sample.html'),'utf8');
 for(const kind of ['device','client'])for(const [width,height] of [[1280,720],[1920,1080]]) {
  const page=await browser.newPage({viewport:{width,height}});
  await page.goto(host+'/WriterApp.Client/tests/device-editor.html');
  await page.waitForFunction(()=>/tests (passed|failed)/.test(document.title));
  const checks=await page.evaluate(async ({kind,sample})=>{
   const {create,qualityPlainText,previewQualityRevision,captureTranslation}=await import('/WriterApp.Device.Shared/wwwroot/editor/device-editor.js');
   document.body.replaceChildren();
   const heading=document.createElement('h1');heading.textContent=kind==='device'?'Shipped device editor fixture':'Shipped client editor fixture';document.body.append(heading);
   const surface=document.createElement('div');surface.id='p13-editor';surface.className='prosa-editor';document.body.append(surface);
   const receiver={invokeMethodAsync(){return Promise.resolve();}};
   const original=sample+'<h2>Åsa 日本語 🧭</h2><p>Keep <strong>bold</strong> and <em>voice</em>.</p><ul><li><p>Other structure.</p></li></ul>';
   let editor=kind==='device'?create(surface,original,'Html',receiver):window.tiptapEditor.create(surface.id,original,receiver);
   const snapshot=()=>kind==='device'?editor.snapshot().html:window.tiptapEditor.getContent(editor);
   const before=snapshot(),text=qualityPlainText(before,'Html'),quote='letter that she had read three times already',start=text.indexOf(quote);
   if(start<0)throw new Error('Practice text absent');
   if(kind==='device') {
    editor.navigateToQuality(text,{issueKey:'p13',from:start,to:start+quote.length,expectedText:quote,severity:'info'});
    if(editor.captureAi(false,true,true).selectedText!==quote)throw new Error('Selection capture mismatch');
   }
   const reviewed=previewQualityRevision(before,'Html',start,start+quote.length,quote,'letter she had read three times');
   if(snapshot()!==before)throw new Error('Preview mutated writing');
   if(kind==='device')editor.setContent(reviewed);else window.tiptapEditor.setContent(editor,reviewed);
   const after=snapshot();
   if(!after.includes('letter she had read three times')||!after.includes('<strong>bold</strong>')||!after.includes('<em>voice</em>')||!after.includes('日本語'))throw new Error('Approved content/formatting lost');
   if(captureTranslation(after,'Html').runs.map(r=>r.id).join('|')!==captureTranslation(before,'Html').runs.map(r=>r.id).join('|'))throw new Error('Structural run identities changed');
   if(kind==='device')editor.destroy();else window.tiptapEditor.destroy(editor);
   editor=kind==='device'?create(surface,after,'Html',receiver):window.tiptapEditor.create(surface.id,after,receiver);
   if(snapshot()!==after)throw new Error('Reopened content differs');
   return {before,after,reviewInert:true,explicitApply:true,reopened:true,richStructurePreserved:true};
  },{kind,sample});
  await page.screenshot({path:join(evidence,'screenshots',`${kind}-editor-${width}x${height}.png`)});
  result.editors.push({kind,width,height,...checks});await page.close();
 }
 const failures=result.panels.filter(panel=>panel.errors.length);
 await writeFile(join(evidence,'browser-results.json'),JSON.stringify(result,null,2));
 if(failures.length)throw new Error(JSON.stringify(failures.map(({fixture,width,errors})=>({fixture,width,errors}))));
 console.log(`${result.harness.passed} real editor checks; ${result.panels.length} panel renders; ${result.editors.length} equivalent shipped-editor reopen checks passed.`);
} finally {await browser.close();}

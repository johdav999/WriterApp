import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,mkdir,writeFile} from 'node:fs/promises';
import {createRequire} from 'node:module';
import os from 'node:os';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const http=createServer(async(req,res)=>{
 const path=new URL(req.url,'http://localhost').pathname;
 const file=path==='/'?'./collaboration.html':path==='/web.js'?'../wwwroot/js/tiptap-editor.bundle.js':path==='/device.js'?'../../WriterApp.Device.Shared/wwwroot/editor/device-editor.js':null;
 if(!file){res.writeHead(204);res.end();return;}res.setHeader('Content-Type',path==='/'?'text/html':'text/javascript');res.end(await readFile(new URL(file,import.meta.url)));
});await new Promise(r=>http.listen(0,'127.0.0.1',r));let browser;
const results=[];
try{
 browser=await chromium.launch({channel:'msedge',headless:true,args:['--js-flags=--expose-gc','--enable-precise-memory-info']});
 for(const host of['web','device']){
  const page=await browser.newPage();await page.goto(`http://127.0.0.1:${http.address().port}/?host=${host}`);await page.waitForFunction(()=>window.ready);
  const fixtures=[{name:'typical',words:5000,html:'<p>'+Array.from({length:5000},(_,i)=>'word'+i%100).join(' ')+'</p>'},{name:'stress',words:20000,html:'<p>'+Array.from({length:20000},(_,i)=>'word'+i%100).join(' ')+'</p>'},
   {name:'rich',words:null,html:'<table><tbody>'+Array.from({length:100},(_,i)=>`<tr><td><p>Row ${i}</p></td><td><p>Cell</p></td></tr>`).join('')+'</tbody></table>'+Array.from({length:20},(_,i)=>`<img src="/asset-${i}.png" data-asset-url="/asset-${i}.png" data-asset-id="asset-${i}" alt="Reference ${i}">`).join('')+'<p>Typing area</p>'}];
  for(const fixture of fixtures){
   console.log(`Measuring ${host} ${fixture.name}`);
   const measurement=await page.evaluate(async fixture=>{
    const prepare=performance.now(),seed=await api.createIsolatedSeed(fixture.html),preparationMs=performance.now()-prepare;
    const identity={documentId:crypto.randomUUID(),pageId:crypto.randomUUID(),generation:crypto.randomUUID(),environment:'isolated'},scope={...identity,backend:location.origin+'/',ownerId:'owned-benchmark',support:seed.support};
    window.benchmarkStore=new api.IndexedDbJournal(scope,'p10-'+crypto.randomUUID());window.benchmarkAccount=new api.CollaborationAccountFence(null);benchmarkAccount.activate(scope,1);
    const caps={documentId:scope.documentId,mode:2,featureAvailable:true,canRead:true,canWriteCollaboratively:true,pages:[{pageId:scope.pageId,generation:scope.generation,...scope.support}]};
    const started=performance.now();window.benchmarkBinding=await api.createJournaledEditor(document.getElementById('editor'),{store:benchmarkStore,account:benchmarkAccount.capture(scope),committedSeed:seed.state,autoReconnect:false},caps);await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));
    window.paintSamples=[];benchmarkBinding.editor.view.dom.addEventListener('keydown',()=>{const t=performance.now();requestAnimationFrame(()=>requestAnimationFrame(()=>paintSamples.push(performance.now()-t)));});
    benchmarkBinding.editor.commands.focus('end');
    return {preparationMs,warmLocalJournalOpenMs:performance.now()-started,stateBytes:seed.state.length,tableRows:benchmarkBinding.editor.getJSON().content.filter(n=>n.type==='table').flatMap(n=>n.content).length,imageReferences:benchmarkBinding.editor.getJSON().content.filter(n=>n.type==='image').length};
   },fixture);
   await page.locator('.ProseMirror').focus();await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
   for(let i=0;i<20;i++){await page.keyboard.press('x');await page.waitForFunction(n=>paintSamples.length>=n,i+1,{timeout:5000});}
   const result=await page.evaluate(async()=>{await benchmarkBinding.session.flush();const values=[...paintSamples].sort((a,b)=>a-b);const result={samples:values.length,p95:values[Math.ceil(values.length*.95)-1],max:Math.max(...values),pending:benchmarkBinding.session.pending.length,localDurable:benchmarkBinding.session.status.localDurable};await benchmarkBinding.destroy();await benchmarkStore.close();benchmarkAccount.close();return result;});
   assert.equal(result.localDurable,true);assert.equal(result.pending,20);if(fixture.name==='rich'){assert.equal(measurement.tableRows,100);assert.equal(measurement.imageReferences,20);}
   results.push({host,fixture:fixture.name,words:fixture.words,...measurement,keystrokeToTwoAnimationFramesMs:result,provisionalPaintP95AtMost50:result.p95<=50});
  }
  const switches=await page.evaluate(async()=>{
   const seed=await api.createIsolatedSeed('<p>Page switch fixture</p>');const sample=()=>{window.gc?.();return {heapBytes:performance.memory?.usedJSHeapSize??null,editors:document.querySelectorAll('.ProseMirror').length,carets:document.querySelectorAll('.collaboration-remote-caret').length};};
   const baseline=sample();let after20;
   for(let i=0;i<50;i++){
    const scope={backend:location.origin+'/',ownerId:'owned-benchmark',environment:'isolated',documentId:crypto.randomUUID(),pageId:crypto.randomUUID(),generation:crypto.randomUUID(),support:seed.support},store=new api.IndexedDbJournal(scope,'p10-switches'),account=new api.CollaborationAccountFence(null);account.activate(scope,1);
    const caps={documentId:scope.documentId,mode:2,featureAvailable:true,canRead:true,canWriteCollaboratively:true,pages:[{...scope.support,pageId:scope.pageId,generation:scope.generation}]};
    const binding=await api.createJournaledEditor(document.getElementById('editor'),{store,account:account.capture(scope),committedSeed:seed.state,autoReconnect:false,interactionsEnabled:true},caps);
    binding.editor.commands.insertContentAt(1,'Saved ');await binding.session.flush();await binding.destroy();await store.close();account.close();if(i===19)after20=sample();
   }return {baseline,after20,after50:sample()};
  });assert.equal(switches.after50.editors,0);assert.equal(switches.after50.carets,0);results.push({host,fixture:'50-page-switches',memory:switches});await page.close();
 }
 const report={machine:{platform:os.platform(),release:os.release(),logicalCpus:os.cpus().length,cpuModel:os.cpus()[0].model,memoryBytes:os.totalmem()},scope:'Actual rebuilt web/device bundles in desktop Edge with IndexedDB journals. Synthetic capability grants and local binary seed; not physical/mobile or network opening acceptance.',results,productionReady:false};
 await mkdir('artifacts/realtime-p10',{recursive:true});await writeFile('artifacts/realtime-p10/editor-performance.json',JSON.stringify(report,null,2));console.log('PASS: typical/stress/rich editor fixtures, actual keyboard paint samples, durable local envelopes and 50 page-switch cycles in both bundles');
}finally{await browser?.close();await new Promise(r=>http.close(r));}

import {createServer} from 'node:http';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {createRequire} from 'node:module';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root=resolve('artifacts/style-quality/evidence');await mkdir(root,{recursive:true});
const asset=await readFile('WriterApp.Device.Shared/wwwroot/editor/device-editor.js');
const server=createServer((request,response)=>{
  if(request.url==='/editor.js'){response.setHeader('Content-Type','text/javascript');response.end(asset);return;}
  if(request.url!=='/'){response.writeHead(404);response.end();return;}
  response.setHeader('Content-Type','text/html');response.end('<!doctype html><meta charset="utf-8"><div id="host"></div>');
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const browser=await chromium.launch({channel:'msedge',headless:true});
try{
 const page=await browser.newPage();await page.goto(`http://127.0.0.1:${server.address().port}/`);
 const checks=await page.evaluate(async()=>{
  const {create,previewStyleQualityRevision}=await import('/editor.js');
  const host=document.querySelector('#host');const receiver={invokeMethodAsync(){return Promise.resolve();}};
  const checks=[];const check=(value,message)=>{if(!value)throw Error(message);};
  let editor;
  const test=(name,run)=>{try{run();checks.push(name);}finally{editor?.destroy();editor=null;}};
  const open=html=>{editor=create(host,html,'Html',receiver);return editor.captureAi(true);};
  test('Partial approval preserves unchecked wording, marks, headings, lists and tables',()=>{
   const before=open('<h2>Heading</h2><p><strong>Elin</strong> walked very slowly. She was <em>really</em> tired.</p><ul><li><p>Keep list</p></li></ul><table><tbody><tr><td><p>Keep cell</p></td></tr></tbody></table>');
   const edits=[{original:'Elin walked very slowly.',replacement:'Elin walked slowly.'}];
   const preview=previewStyleQualityRevision(before.html,0,before.selectedText,edits);
   const after=editor.applyStyleQuality(before.html,0,before.selectedText,edits);
   check(after.html===preview,'Apply differs from preview');
   check(after.html.includes('<strong>Elin</strong> walked slowly.'),'Unchanged bold text lost');
   check(after.html.includes('She was <em>really</em> tired.'),'Unchecked wording or italics changed');
   check(after.html.includes('<h2>Heading</h2>')&&after.html.includes('Keep list')&&after.html.includes('Keep cell'),'Other blocks changed');
   editor.command('undo');check(editor.snapshot().html===before.html,'Undo did not restore original rich page');
   editor.command('redo');check(editor.snapshot().html===after.html,'Redo did not restore selected edits');
  });
  test('Multiple Unicode edits apply atomically with one undo',()=>{
   const before=open('<p>🧭 Åsa walked slowly. 日本語 was hard.</p>');
   const edits=[{original:'Åsa walked slowly.',replacement:'Åsa walked carefully.'},{original:'日本語 was hard.',replacement:'日本語 was difficult.'}];
   const after=editor.applyStyleQuality(before.html,0,before.selectedText,edits);
   check(after.html.includes('🧭 Åsa walked carefully. 日本語 was difficult.'),'Unicode edits misplaced');
   editor.command('undo');check(editor.snapshot().html===before.html,'Batch required multiple undo steps');
  });
  test('A selection on a later paragraph uses exact plain offsets and preserves outside writing',()=>{
   const before=open('<p>Keep first.</p><p><em>Elin</em> walked slowly.</p><p>Keep last.</p>');
   const source='Elin walked slowly.',from=before.plainText.indexOf(source);
   const after=editor.applyStyleQuality(before.html,from,source,[{original:source,replacement:'Elin walked carefully.'}]);
   check(after.html==='<p>Keep first.</p><p><em>Elin</em> walked carefully.</p><p>Keep last.</p>','Selection range damaged outside writing');
  });
  test('Punctuation insertion remains text and keeps existing formatting',()=>{
   const before=open('<p><strong>Hello Elin.</strong></p>');
   const after=editor.applyStyleQuality(before.html,0,before.selectedText,[{original:'Hello Elin.',replacement:'Hello, Elin.'}]);
   check(after.html==='<p><strong>Hello, Elin.</strong></p>','Insertion lost marks');
  });
  test('Stale, ambiguous, overlapping and malformed batches leave writing unchanged',()=>{
   const before=open('<p>Elin walked slowly. Elin waited.</p>');
   for(const [html,edits] of [
    [before.html,[{original:'Elin',replacement:'Maya'}]],
    [before.html,[{original:'Elin walked slowly.',replacement:'Elin walked carefully.'},{original:'walked slowly',replacement:'walked carefully'}]],
    [before.html,[{original:'Elin waited.',replacement:'Elin rested.'},{original:'missing',replacement:'different'}]],
    ['<p>Stale</p>',[{original:'Elin waited.',replacement:'Elin rested.'}]],
    [before.html,[]]
   ]){
    let rejected=false;try{editor.applyStyleQuality(html,0,before.selectedText,edits);}catch{rejected=true;}
    check(rejected,'Unsafe batch accepted');check(editor.snapshot().html===before.html,'Failed batch partially changed writing');
   }
  });
  test('Mixed formatting and paragraph-spanning replacements fail before mutation',()=>{
   for(const [html,original,replacement] of [
    ['<p>One <strong>two</strong> three.</p>','One two three.','All changed.'],
    ['<p>First.</p><p>Second.</p>','First.\nSecond.','Different.\nOther.']
   ]){
    const before=open(html);let rejected=false;try{editor.applyStyleQuality(before.html,0,before.selectedText,[{original,replacement}]);}catch{rejected=true;}
    check(rejected,'Structural change accepted');check(editor.snapshot().html===before.html,'Rejected structural edit mutated writing');editor.destroy();editor=null;
   }
  });
  return checks;
 });
 assert.equal(checks.length,6);await writeFile(resolve(root,'editor-results.json'),JSON.stringify({surface:'Built production editor in headless Edge; synthetic style edits',checks},null,2));
 console.log(`Passed ${checks.length} style editor scenarios (formatting, scope, atomic rejection, undo and redo)`);
}finally{await browser.close();await new Promise(resolve=>server.close(resolve));}

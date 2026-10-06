import {createServer} from 'node:http';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const evidence='artifacts/targeted-quality-formatting/evidence';await mkdir(evidence,{recursive:true});
const asset=await readFile('WriterApp.Device.Shared/wwwroot/editor/device-editor.js');
const server=createServer((request,response)=>{
 response.setHeader('Content-Type',request.url==='/editor.js'?'text/javascript':'text/html');
 response.end(request.url==='/editor.js'?asset:'<!doctype html><meta charset="utf-8"><div id="host"></div>');
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const browser=await chromium.launch({channel:'msedge',headless:true});
try {
 const page=await browser.newPage();await page.goto(`http://127.0.0.1:${server.address().port}/`);
 const checks=await page.evaluate(async()=>{
  const module=await import('/editor.js');
  const {create,validateQualityRange}=module;
  const validateTargetedQualityRange=module.validateTargetedQualityRange??module.validateQualityRange;
  const previewTargetedQualityRevision=module.previewTargetedQualityRevision??module.previewQualityRevision;
  const checks=[],check=(ok,message)=>{if(!ok)throw Error(message);};
  const rejects=run=>{let rejected=false;try{run();}catch{rejected=true;}check(rejected,'Unsafe revision accepted');};
  const host=document.querySelector('#host'),receiver={invokeMethodAsync(){return Promise.resolve();}};
  const test=(name,html,expected,proposed,wanted)=>{
   const editor=create(host,html,'Html',receiver);
   try {
    const before=editor.snapshot(),source=editor.captureAi(true,false,true),from=source.plainText.lastIndexOf(expected),to=from+expected.length;
    validateTargetedQualityRange(source.html,'Html',from,to,expected);
    const after=previewTargetedQualityRevision(source.html,'Html',from,to,expected,proposed);
    check(after===wanted,`${name}: ${after}`);
    check(JSON.stringify(before)===JSON.stringify(editor.snapshot()),'Preview mutated live editor');
    editor.setContent(after,'Html');check(editor.snapshot().html===after,'Saved preview did not reload exactly');
    checks.push(name);
   }finally{editor.destroy();}
  };
  // This was rejected before generation because unchanged sentence context was bold.
  const sentence='The clock rang while another clock answered the clock in the hall.';
  const marked='<p>The <strong>clock</strong> rang while another clock answered the clock in the hall.</p>';
  rejects(()=>validateQualityRange(marked,'Html',0,sentence.length,sentence));
  test('Repetition fix preserves unchanged bold context',marked,sentence,'The clock rang while another clock answered the chime in the hall.',
   '<p>The <strong>clock</strong> rang while another clock answered the chime in the hall.</p>');
  test('Later identical sentence retains exact quality offsets, links, Unicode and sibling blocks',
   '<h2>🧭 Åsa</h2><p>'+sentence+'</p><p>The clock rang while another clock answered the <em>clock</em> in the hall.</p><p><a href="https://example.org/">Keep link</a></p>',
   sentence,'The clock rang while another clock answered the chime in the hall.',
   '<h2>🧭 Åsa</h2><p>'+sentence+'</p><p>The clock rang while another clock answered the <em>chime</em> in the hall.</p><p><a target="_blank" rel="noopener noreferrer nofollow" href="https://example.org/">Keep link</a></p>');
  test('Marked target preserves its own emphasis and block attributes',
   '<p style="text-align: center;"><strong>Åsa</strong> was <em>really tired</em>.</p>',
   'Åsa was really tired.','Åsa was exhausted.',
   '<p style="text-align: center"><strong>Åsa</strong> was <em>exhausted</em>.</p>');
  test('Duplicate deletion retains first occurrence formatting',
   '<p>Elin <strong>carried</strong> <em>carried</em> her suitcase.</p>','carried carried','carried',
   '<p>Elin <strong>carried</strong> her suitcase.</p>');
  test('Punctuation insertion keeps marked name',
   '<p>Hello <strong>Åsa</strong>.</p>','Hello Åsa.','Hello, Åsa.',
   '<p>Hello, <strong>Åsa</strong>.</p>');
  test('Provider markup stays inert text',
   '<p><em>Åsa</em> waited.</p>','Åsa waited.','Åsa <b>rested</b>.',
   '<p><em>Åsa</em> &lt;b&gt;rested&lt;/b&gt;.</p>');
  test('Uniform paragraph split retains prior behavior',
   '<p><em>Alpha beta. Gamma delta.</em></p><p>Keep.</p>','Alpha beta. Gamma delta.','Alpha beta.\n\nGamma delta.',
   '<p><em>Alpha beta.</em></p><p></p><p><em>Gamma delta.</em></p><p>Keep.</p>');
  for(const args of [
   ['<p>Alpha <strong>beta</strong></p>','Html',0,10,'Alpha beta','New prose'],
   ['<p>Alpha</p><p>beta</p>','Html',0,11,'Alpha\n\nbeta','New prose'],
   ['<p>Alpha<br>beta</p>','Html',0,10,'Alpha\nbeta','New prose'],
   ['<p>Alpha<img src="https://example.org/a.png">beta</p>','Html',0,10,'Alpha\nbeta','New prose'],
   ['<p>Alpha beta</p>','Html',6,10,'nope','new'],
   ['<p>Later edit</p>','Html',6,10,'beta','new'],
   ['<p>Alpha beta</p>','Html',1,5,'lpha','new'],
   ['<p>🧭 Beta</p>','Html',1,2,'\uDDED','new'],
   ['<p>Alpha beta</p>','Html',0,10,'Alpha beta','Alpha beta'],
   ['<p>Alpha <strong>beta.</strong></p>','Html',0,11,'Alpha beta.','Alpha\n\nbeta.']
  ])rejects(()=>previewTargetedQualityRevision(...args));
  checks.push('Actual mixed-format changes, blocks, hard breaks, images, stale and partial targets fail safely');
  return checks;
 });
 assert.equal(checks.length,8);await writeFile(`${evidence}/editor-results.json`,JSON.stringify(checks,null,2));
 console.log(`Passed ${checks.length} targeted formatting scenarios`);
}finally{await browser.close();await new Promise(resolve=>server.close(resolve));}

// Actual production capture/worklet with a synthetic oscillator and permission stubs. No live microphone/provider claim.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve,dirname} from 'node:path';
import {createRequire} from 'node:module';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const directory=dirname(process.env.WRITERAPP_BRAINSTORMING_CONTEXT_HTML),evidence=resolve(process.env.BRAINSTORMING_EVIDENCE);await mkdir(evidence,{recursive:true});
const server=createServer(async(req,res)=>{
 const name=new URL(req.url,'http://localhost').pathname.slice(1);res.setHeader('Cache-Control','no-store');
 if(['web','device'].includes(name)){res.setHeader('Content-Type','text/html');res.end('<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width"><link rel="stylesheet" href="/panel.css"><style>body{font:16px system-ui;margin:16px}*{box-sizing:border-box}button{white-space:normal}</style>'+await readFile(resolve(directory,`dictation-${name}.html`),'utf8'));return;}
 const files={'panel.css':'WriterApp.UI.Shared/BrainstormingConversationPanel.razor.css','microphone.js':'WriterApp.UI.Shared/wwwroot/brainstorming-microphone.js','brainstorming-microphone-worklet.js':'WriterApp.UI.Shared/wwwroot/brainstorming-microphone-worklet.js'};
 if(!files[name]){res.writeHead(404);res.end();return;}res.setHeader('Content-Type',name.endsWith('.css')?'text/css':'text/javascript');res.end(await readFile(files[name]));
});
await new Promise(done=>server.listen(0,'127.0.0.1',done));const browser=await chromium.launch({channel:'msedge',headless:true}),page=await browser.newPage({viewport:{width:1400,height:950}}),checks=[],errors=[];page.on('pageerror',e=>errors.push(e.message));
try {
 for(const surface of ['web','device']){
  await page.goto(`http://127.0.0.1:${server.address().port}/${surface}`);if(await page.locator('.brainstorm-audio').getAttribute('open')===null)await page.locator('.brainstorm-audio > summary').click();assert.equal(await page.getByRole('button',{name:'Send message',exact:true}).count(),1);assert.equal(await page.getByRole('button',{name:/Place corrected message/}).count(),1);
  const details=page.locator('summary').filter({hasText:'Final transcript'});await details.click();assert(await page.locator('pre').filter({hasText:surface==='web'?'She waited':'Åsa väntade'}).first().isVisible());await page.getByRole('textbox').fill('Author correction remains editable.');assert.equal(await page.getByRole('textbox').inputValue(),'Author correction remains editable.');await page.getByRole('combobox',{name:'Spoken language'}).focus();assert(await page.getByRole('combobox',{name:'Spoken language'}).evaluate(e=>document.activeElement===e));await page.screenshot({path:resolve(evidence,`dictation-${surface}-wide.png`),fullPage:true});await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));await page.screenshot({path:resolve(evidence,`dictation-${surface}-narrow.png`),fullPage:true});checks.push(`${surface} actual Razor handler render: language, editable final transcript, explicit prose destination, keyboard and 390px layout`);
 }
 const result=await page.evaluate(async()=>{
  const mic=await import('/microphone.js'),original=navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);let calls=0,events=[],stream,source,sourceContext,node;
  const receiver={invokeMethodAsync:async(method,id,...data)=>events.push({method,id,data:data.map(v=>v instanceof Uint8Array?Array.from(v):v)})};
  const RealNode=AudioWorkletNode;window.AudioWorkletNode=class extends RealNode{constructor(...args){super(...args);node=this;}};
  navigator.mediaDevices.getUserMedia=async()=>{calls++;sourceContext=new AudioContext({sampleRate:16000});source=sourceContext.createOscillator();source.frequency.value=440;const destination=sourceContext.createMediaStreamDestination();source.connect(destination);source.start();await sourceContext.resume();stream=destination.stream;return stream;};
  const supported=mic.availability();const before=calls;await mic.start('pcm',receiver);
  // Wait for actual worklet samples: audio startup can exceed a fixed 400 ms on a busy host.
  let frames=0;const receive=node.port.onmessage;node.port.onmessage=e=>{if(e.data instanceof Int16Array)frames+=e.data.length;receive(e);};
  const deadline=performance.now()+5000;while(frames<3200 && performance.now()<deadline)await new Promise(r=>setTimeout(r,20));
  if(frames<3200)throw new Error(`Synthetic microphone produced only ${frames} frames within five seconds`);
  await mic.stop('pcm');const pcm=events.find(e=>e.method==='Recorded');const stopped=stream.getTracks().every(t=>t.readyState==='ended');await sourceContext.close();source.stop();
  events=[];navigator.mediaDevices.getUserMedia=async()=>{calls++;throw new DOMException('denied','NotAllowedError');};await mic.start('denied',receiver);const denied=events[0];events=[];
  navigator.mediaDevices.getUserMedia=async()=>{throw new DOMException('missing','NotFoundError');};await mic.start('missing',receiver);const missing=events[0];events=[];
  let resolvePermission;navigator.mediaDevices.getUserMedia=()=>new Promise(r=>resolvePermission=r);const late=mic.start('late',receiver);await mic.cancel('late');const cancelled=events[0];const lateContext=new AudioContext();const lateStream=lateContext.createMediaStreamDestination().stream;resolvePermission(lateStream);await late;const lateStopped=lateStream.getTracks().every(t=>t.readyState==='ended');await lateContext.close();events=[];
  navigator.mediaDevices.getUserMedia=async()=>{sourceContext=new AudioContext({sampleRate:16000});stream=sourceContext.createMediaStreamDestination().stream;return stream;};await mic.start('short',receiver);await mic.stop('short');const short=events[0];await sourceContext.close();events=[];
  await mic.start('bounded',receiver);node.port.onmessage({data:new Int16Array(960001).fill(1000)});await new Promise(r=>setTimeout(r,100));const bounded=events.find(e=>e.method==='Recorded');const boundStopped=stream.getTracks().every(t=>t.readyState==='ended');await sourceContext.close();events=[];
  await mic.start('navigate',receiver);window.dispatchEvent(new Event('pagehide'));await new Promise(r=>setTimeout(r,100));const navigation=events[0],navigationStopped=stream.getTracks().every(t=>t.readyState==='ended');await sourceContext.close();navigator.mediaDevices.getUserMedia=original;window.AudioWorkletNode=RealNode;
  return {supported,before,calls,pcm,stopped,denied,missing,cancelled,lateStopped,short,boundedBytes:bounded?.data[0].length,boundStopped,navigation,navigationStopped};
 });
 assert.equal(result.supported.state,0);assert.equal(result.before,0);assert.equal(result.pcm.method,'Recorded');const wav=Buffer.from(result.pcm.data[0]);assert.equal(wav.toString('ascii',0,4),'RIFF');assert.equal(wav.readUInt32LE(24),16000);assert(wav.length>3244 && wav.length<1921024);assert(wav.subarray(44).some(v=>v!==0));assert(result.stopped);checks.push('Production AudioWorklet captures synthetic WebAudio to bounded mono 16 kHz PCM16 WAV and releases tracks before final callback; availability never requests permission');
 assert.equal(result.denied.data[0],'permission');assert.equal(result.missing.data[0],'no-device');assert.equal(result.cancelled.data[0],'cancelled');assert(result.lateStopped);assert.equal(result.short.data[0],'silence');checks.push('Permission denied, no device, short recording and cancellation while permission is pending return actionable states; late permission releases every track');
 assert.equal(result.boundedBytes,1920044);assert(result.boundStopped);assert.equal(result.navigation.data[0],'cancelled');assert(result.navigationStopped);checks.push('Explicit synthetic frame injection verifies 60-second hard bound; pagehide cancels and disposes capture');
 assert.deepEqual(errors,[]);const report={scope:'Production microphone/worklet synthetic audio and static production Razor fixtures. No user microphone, actual recognition, signed-in navigation or native interaction.',browser:browser.version(),checks,errors};await writeFile(resolve(evidence,'dictation-result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
}finally{await browser.close();await new Promise(done=>server.close(done));}

// Actual installed Debug MAUI WebView. No writing, provider, or account mutations.
import {execFileSync} from 'node:child_process';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const adbPath=process.env.PROSA_ADB || 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
const serial=process.env.PROSA_ANDROID_SERIAL || 'emulator-5554';
const evidence=resolve('artifacts/synopsis-android/native');mkdirSync(evidence,{recursive:true});
const adb=(...args)=>execFileSync(adbPath,['-s',serial,...args],{maxBuffer:16*1024*1024}).toString().trim();
function documentHashes() {
    const paths=adb('shell','run-as','com.prosa.writer.android','find','files/development/documents','-maxdepth','1','-type','f').split(/\r?\n/).filter(p=>p.endsWith('.json')).sort();
    return Object.fromEntries(paths.map(p=>[p,adb('shell','run-as','com.prosa.writer.android','sha256sum',p).split(' ')[0]]));
}
const capture=name=>writeFileSync(resolve(evidence,`${name}.png`),execFileSync(adbPath,['-s',serial,'exec-out','screencap','-p'],{maxBuffer:16*1024*1024}));
let pid;
for (let attempt=0;attempt<30;attempt++) {
    try { pid=adb('shell','pidof','com.prosa.writer.android').split(' ')[0]; if(pid) break; } catch { }
    await new Promise(resolve=>setTimeout(resolve,500));
}
assert.ok(pid,'Launch the Debug Prosa Android app first.');
adb('forward','tcp:9334',`localabstract:webview_devtools_remote_${pid}`);
let target;
for (let attempt=0;attempt<40;attempt++) {
    try { target=(await (await fetch('http://127.0.0.1:9334/json')).json()).find(t=>t.type==='page' && t.url.includes('0.0.0.1')); if(target) break; } catch { }
    await new Promise(resolve=>setTimeout(resolve,500));
}
assert.ok(target,'Debug Blazor WebView was not found.');
const socket=new WebSocket(target.webSocketDebuggerUrl),pending=new Map();let sequence=0;
await new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
socket.addEventListener('message',e=>{const m=JSON.parse(e.data),p=pending.get(m.id);if(!p)return;pending.delete(m.id);m.error?p.reject(new Error(JSON.stringify(m.error))):p.resolve(m.result);});
const send=(method,params={})=>new Promise((resolve,reject)=>{const id=++sequence;pending.set(id,{resolve,reject});socket.send(JSON.stringify({id,method,params}));});
async function evaluate(expression){const r=await send('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value;}
async function wait(expression){for(let i=0;i<90;i++){if(await evaluate(expression))return;await new Promise(resolve=>setTimeout(resolve,500));}throw Error('Timed out: '+expression);}
const click=side=>evaluate(`document.querySelector('[data-panel-action="${side}"]').click()`);
const state=()=>evaluate(`({...document.querySelector('[data-responsive-panels]').dataset})`);
const content=async()=>({route:await evaluate('location.pathname'),writingHash:createHash('sha256').update(await evaluate(`document.querySelector('.ProseMirror').innerHTML`)).digest('hex'),documents:documentHashes()});
const baselineFile=resolve(evidence,'baseline.json');
try {
 await wait(`!!document.querySelector('.ProseMirror')`);
 if(process.argv[2]==='baseline') {
  writeFileSync(baselineFile,JSON.stringify(await content(),null,2));
  console.log('Document and manuscript hashes saved before APK update.');
 } else {
  const baseline=JSON.parse(readFileSync(baselineFile,'utf8'));
  assert.deepEqual(await content(),baseline,'Preserve documents and manuscript during update');
  if((await state()).openDrawer!=='right') await click('right');
  await wait(`document.querySelector('[data-responsive-panels]').dataset.openDrawer==='right'`);
  await evaluate(`[...document.querySelectorAll('[role=tab]')].find(e=>e.textContent.trim()==='Story').click()`);
  await wait(`!![...document.querySelectorAll('button')].find(e=>e.textContent.trim()==='Synopsis')`);
  await evaluate(`[...document.querySelectorAll('button')].find(e=>e.textContent.trim()==='Synopsis').click()`);
  await wait(`!!document.querySelector('.desktop-context-ai.is-synopsis-coaching .synopsis-run')`);
  await evaluate(`document.querySelector('.rp-trailing-body').scrollTop=0`);
  const metrics=await evaluate(`(()=>{const p=document.querySelector('.rp-trailing-body'),w=document.querySelector('.desktop-context-ai');return {paneHeight:p.clientHeight,contentHeight:p.scrollHeight,wrapperHeight:w.clientHeight,wrapperContent:w.scrollHeight}})()`);
  assert(metrics.contentHeight>metrics.paneHeight,'Native pane must see form overflow');
  assert(Math.abs(metrics.wrapperHeight-metrics.wrapperContent)<=1,'No clipped wrapper');
  capture('form-top');
  // Native WebView touch input, rather than programmatic scrollIntoView.
  const rect=await evaluate(`(()=>{const r=document.querySelector('.rp-trailing-body').getBoundingClientRect();return {x:r.x+r.width/2,top:r.y+20,bottom:r.bottom-20}})()`);
  assert(rect.bottom>rect.top+40,'Pane must have room for a swipe');
  for(let swipe=0;swipe<6;swipe++) {
   await send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:rect.x,y:rect.bottom}]});
   for(let step=1;step<=10;step++) {
    await send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:rect.x,y:rect.bottom-(rect.bottom-rect.top)*step/10}]});
    await new Promise(resolve=>setTimeout(resolve,20));
   }
   await send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
   await new Promise(resolve=>setTimeout(resolve,250));
  }
  const button=await evaluate(`(()=>{const b=document.querySelector('.synopsis-run'),r=b.getBoundingClientRect(),p=document.querySelector('.rp-trailing-body').getBoundingClientRect();return {label:b.textContent,top:r.top,bottom:r.bottom,paneTop:p.top,paneBottom:p.bottom,viewport:innerHeight,hit:b.contains(document.elementFromPoint(r.x+r.width/2,r.y+r.height/2)),scroll:document.querySelector('.rp-trailing-body').scrollTop}})()`);
  assert(button.scroll>0,'Touch scroll advances the coaching pane');
  assert(button.top>=button.paneTop && button.bottom<=button.paneBottom && button.bottom<=button.viewport,'Run button visible within pane and viewport');
  assert(button.hit,'Run button receives input at its center');
  capture('run-reachable');
  await click('close-right');
  assert.deepEqual(await content(),baseline,'Layout checks preserve all authored data');
  writeFileSync(resolve(evidence,'results.json'),JSON.stringify({scope:'Rebuilt installed Android Debug APK; touch swipe and hit testing; no provider request',metrics,button,preservedDocuments:Object.keys(baseline.documents).length},null,2));
  console.log(JSON.stringify({metrics,button,preservedDocuments:Object.keys(baseline.documents).length},null,2));
 }
} finally {socket.close();adb('forward','--remove','tcp:9334');}

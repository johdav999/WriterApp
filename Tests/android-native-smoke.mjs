// Android emulator acceptance using the actual Debug MAUI WebView and adb lifecycle.
// Local modes do not send provider requests or reset app data. signin-open explicitly starts native browser authentication; never enter/log credentials here.
import {execFileSync} from 'node:child_process';
import {writeFileSync, mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';

const serial = process.env.PROSA_ANDROID_SERIAL ?? 'emulator-5554';
const adbPath = process.env.PROSA_ADB ?? 'adb';
const output = resolve(process.env.PROSA_ANDROID_EVIDENCE ?? 'artifacts/android-uat');
const port = 9333;
mkdirSync(output, {recursive:true});
export function adb(...args) { return execFileSync(adbPath, ['-s',serial,...args], {maxBuffer:16*1024*1024}).toString().trim(); }
const delay = ms => new Promise(resolve => setTimeout(resolve,ms));
export async function connect() {
  let pid;
  for(let i=0;i<20;i++){try{pid=adb('shell','pidof','com.prosa.writer.android').split(' ')[0];if(pid)break;}catch{}await delay(500);}
  if (!pid) throw new Error('Launch the Debug Android app before running acceptance.');
  adb('forward',`tcp:${port}`,`localabstract:webview_devtools_remote_${pid}`);
  let target;
  for (let i=0;i<30;i++) {
    try { target=(await (await fetch(`http://127.0.0.1:${port}/json`)).json()).find(t=>t.type==='page' && t.url.includes('0.0.0.1')); if(target) break; } catch{}
    await delay(500);
  }
  if (!target) throw new Error('The Debug Blazor WebView was not ready.');
  const socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
  let sequence=0; const pending=new Map();
  socket.addEventListener('message',e=>{const message=JSON.parse(e.data);if(!message.id)return;const p=pending.get(message.id);if(!p)return;pending.delete(message.id);message.error?p.reject(new Error(JSON.stringify(message.error))):p.resolve(message.result);});
  const send = (method,params={})=>new Promise((resolve,reject)=>{const id=++sequence;pending.set(id,{resolve,reject});socket.send(JSON.stringify({id,method,params}));});
  const evaluate = async expression=>{const result=await send('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(result.exceptionDetails)throw new Error(JSON.stringify(result.exceptionDetails));return result.result.value;};
  for(let i=0;i<40;i++){if(await evaluate(`!!document.querySelector('.app-body')`))break;await delay(500);}
  for(let i=0;i<40;i++){if(await evaluate(`!(location.pathname.startsWith('/documents/')&&location.pathname.split('/').length===3)||!!document.querySelector('.ProseMirror')`))break;await delay(500);}
  return {send,evaluate,close:()=>socket.close()};
}
export function capture(name) {writeFileSync(resolve(output,name+'.png'),execFileSync(adbPath,['-s',serial,'exec-out','screencap','-p'],{maxBuffer:16*1024*1024}));}
const mode=process.argv[2]??'inspect';
if(mode==='brainstorm-sync'||mode==='brainstorm-sync-baseline') {
  const c=await connect();
  if(mode==='brainstorm-sync') {
    await c.evaluate(`(()=>{const tab=[...document.querySelectorAll('button')].find(b=>b.textContent.trim().endsWith('Conversation'));if(tab)tab.click();})()`);
    await delay(200);
  }
  const state=await c.evaluate(`(()=>{const w=document.querySelector('.brainstorm-workspace');if(!w)throw new Error('Open Brainstorm first');const field=w.querySelector('.brainstorm-preparation-actions');const button=field&&[...field.querySelectorAll('button')].find(b=>b.textContent.startsWith('Enable cloud sync'));const composer=w.querySelector('.brainstorm-composer textarea');const send=[...w.querySelectorAll('button')].find(b=>b.textContent.trim()==='Send message');return{route:location.pathname,syncActionPresent:!!button,syncActionEnabled:button?!button.disabled:false,sendDisabled:send?.disabled,composer:composer?.value??''};})()`);
  state.composerHash=createHash('sha256').update(state.composer).digest('hex');
  state.composerLength=state.composer.length;delete state.composer;
  if(mode==='brainstorm-sync') {
    if(!state.syncActionPresent||!state.syncActionEnabled)throw new Error('Cloud sync recovery action is unavailable in Brainstorm');
    await c.evaluate(`document.querySelector('.brainstorm-preparation-actions').scrollIntoView({block:'center'})`);
    await delay(200);
    const visible=await c.evaluate(`(()=>{const b=[...document.querySelectorAll('.brainstorm-preparation-actions button')].find(b=>b.textContent.startsWith('Enable cloud sync'));const r=b.getBoundingClientRect();return r.width>0&&r.height>0&&r.top>=0&&r.bottom<=innerHeight;})()`);
    if(!visible)throw new Error('Cloud sync recovery button is outside the visible Android conversation');
    const styled=await c.evaluate(`(()=>{const f=document.querySelector('.brainstorm-preparation-actions');return parseFloat(getComputedStyle(f).paddingLeft)>0&&parseFloat(getComputedStyle(f.querySelector('button')).borderRadius)>0;})()`);
    if(!styled)throw new Error('Cloud sync recovery controls are missing their Android conversation styles');
  }
  writeFileSync(resolve(output,mode+'.json'),JSON.stringify(state,null,2));capture(mode);console.log(state);c.close();
}
if(mode==='signin-open') {
  const c=await connect();
  await c.evaluate(`(()=>{document.querySelector('.device-account').open=true;const b=[...document.querySelectorAll('button')].find(e=>e.textContent.trim()==='Sign in with browser');if(!b||b.disabled)throw new Error('Native sign-in is disabled');b.click();})()`);
  await delay(1500);capture('signin-browser');c.close();console.log('Native browser sign-in requested.');
}
if(mode==='signin-state') {
  const c=await connect();
  const state=await c.evaluate(`(()=>{document.querySelector('.device-account').open=true;const b=[...document.querySelectorAll('button')].find(e=>e.textContent.trim()==='Sign in with browser');return{signInButtonPresent:!!b,signInDisabled:b?.disabled,missingConfiguration:document.body.textContent.includes('Native sign-in is not configured'),url:location.pathname};})()`);
  writeFileSync(resolve(output,'signin-state.json'),JSON.stringify(state,null,2));capture('signin-state');console.log(state);c.close();
}
if(mode==='native-inspect') {
  adb('shell','uiautomator','dump','/sdcard/prosa-uat.xml');
  const xml=adb('shell','cat','/sdcard/prosa-uat.xml');
  writeFileSync(resolve(output,'native-dialog.xml'),xml);capture('native-dialog');
  console.log([...xml.matchAll(/<node\b[^>]*text="([^"]*)"[^>]*bounds="([^"]*)"/g)].filter(x=>x[1]).map(x=>({text:x[1],bounds:x[2]})));
}
if(mode==='import') {
  const c=await connect();await c.evaluate(`[...document.querySelectorAll('button')].find(x=>x.innerText==='Import').click()`);await delay(1000);capture('import-picker');c.close();
}
if(mode==='share') {
  const c=await connect();await c.evaluate(`(()=>{const s=document.querySelector('option[value="Html"]').parentElement;s.value='Html';s.dispatchEvent(new Event('change',{bubbles:true}));})()`);await delay(200);
  await c.evaluate(`[...document.querySelectorAll('button')].find(x=>x.innerText==='Share export').click()`);await delay(1000);capture('share-sheet');c.close();
}
if(mode==='keyboard') {
  const c=await connect();
  const point=await c.evaluate(`(()=>{const e=document.querySelector('.ProseMirror');const r=e.getBoundingClientRect();return{x:r.x+20,y:r.y+20};})()`);
  await c.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[point]});await c.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});await delay(600);capture('keyboard');
  const ime=adb('shell','dumpsys','input_method');if(!ime.includes('mInputShown=true'))throw new Error('Keyboard did not open.');
  adb('shell','input','keyevent','4');await delay(500);
  if(await c.evaluate('location.pathname')==='/')throw new Error('First Back should dismiss the keyboard.');
  if(adb('shell','dumpsys','input_method').includes('mInputShown=true'))throw new Error('Keyboard did not dismiss.');
  capture('keyboard-dismissed');console.log('PASS: Back dismissed keyboard and preserved the writing route.');c.close();
}
if(mode==='layout') {
  const c=await connect();capture('layout');
  console.log(await c.evaluate(`JSON.stringify({viewport:{width:innerWidth,height:innerHeight},overflow:document.documentElement.scrollWidth>innerWidth+2,panels:[...document.querySelectorAll('.app-header,.app-header-center,.editor-main,.editor-context-host,.editor-workspace,.editor-content')].map(e=>({class:e.className,display:getComputedStyle(e).display,rect:e.getBoundingClientRect().toJSON()}))})`));c.close();
}
if(mode==='context') {
  const c=await connect();await c.evaluate(`document.querySelector('.editor-context-host').scrollIntoView({block:'end'})`);await delay(400);capture('context-panel');
  const state=await c.evaluate(`(()=>{const e=document.querySelector('.context-drawer');return{display:getComputedStyle(e).display,rect:e.getBoundingClientRect().toJSON(),text:e.innerText.slice(0,300)};})()`);
  if(state.display==='none'||state.rect.height<100||state.rect.y>=915)throw new Error('Context panel is not reachable.');
  console.log('PASS: context panel reachable '+JSON.stringify(state));c.close();
}
if(mode==='open'){
  const c=await connect();
  await c.evaluate(`document.querySelector('a[href^="/documents/"]').click()`);await delay(800);
  console.log(await c.evaluate(`JSON.stringify({url:location.href,text:document.querySelector('.ProseMirror')?.innerText})`));
  c.close();
}
if(mode==='publish'){
  const c=await connect();
  await c.evaluate(`document.querySelector('[aria-label="Document actions"]').click()`);await delay(300);
  await c.evaluate(`document.querySelector('.workspace-transfer').open=true`);await delay(100);
  await c.evaluate(`document.querySelector('a[href$="/publishing"]').click()`);await delay(600);
  console.log(await c.evaluate(`JSON.stringify({text:document.body.innerText,selects:[...document.querySelectorAll('select')].map(x=>({id:x.id,html:x.outerHTML}))})`));
  capture('publishing');c.close();
}
if(mode==='pdf'||mode==='html'){
  const c=await connect();
  await c.evaluate(`(()=>{const select=document.querySelector('option[value="Pdf"]').parentElement;select.value=${JSON.stringify(mode==='pdf'?'Pdf':'Html')};select.dispatchEvent(new Event('change',{bubbles:true}));})()`);
  await delay(200);
  await c.evaluate(`[...document.querySelectorAll('button')].find(x=>x.innerText==='Export to file'||x.innerText==='Print / Save PDF').click()`);
  await delay(1800);
  capture(mode+'-dialog');
  adb('shell','uiautomator','dump','/sdcard/prosa-uat.xml');
  const xml=adb('shell','cat','/sdcard/prosa-uat.xml');writeFileSync(resolve(output,mode+'-dialog.xml'),xml);
  console.log([...xml.matchAll(/<node\b[^>]*text="([^"]*)"[^>]*bounds="([^"]*)"/g)].filter(x=>x[1]).map(x=>({text:x[1],bounds:x[2]})));
  console.log(await c.evaluate('document.body.innerText.slice(-200)'));
  c.close();
}
if(mode==='save-dialog'){
  adb('shell','uiautomator','dump','/sdcard/prosa-uat.xml');const xml=adb('shell','cat','/sdcard/prosa-uat.xml');
  const button=xml.match(/<node\b[^>]*text="SAVE"[^>]*enabled="true"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"/);
  if(!button)throw new Error('An enabled native SAVE button was not found.');
  adb('shell','input','tap',String(Math.round((+button[1]+ +button[3])/2)),String(Math.round((+button[2]+ +button[4])/2)));
  await delay(1200);capture('native-file-saved');const c=await connect();console.log(await c.evaluate('document.body.innerText.slice(-700)'));c.close();
}
if (mode==='inspect') {
  const connection=await connect();
  console.log(await connection.evaluate(`JSON.stringify({url:location.href,title:document.title,text:document.body.innerText.slice(0,7000),buttons:[...document.querySelectorAll('button')].map(x=>({text:x.innerText,label:x.getAttribute('aria-label'),disabled:x.disabled})),inputs:[...document.querySelectorAll('input')].map(x=>({placeholder:x.placeholder,id:x.id})),editors:[...document.querySelectorAll('[contenteditable]')].map(x=>({class:x.className,text:x.innerText}))})`));
  capture('current'); connection.close();
}
if (mode==='create') {
  const connection=await connect();
  await connection.evaluate(`document.querySelector('.new-document-button').click()`);
  await delay(1200);
  console.log(await connection.evaluate(`document.body.innerText`));
  capture('create'); connection.close();
}
if (mode==='write') {
  const c=await connect();
  const text='Android emulator acceptance — Räksmörgås 日本語. Local writing survives background and restart.';
  for(let i=0;i<40;i++){if(await c.evaluate(`!!document.querySelector('.ProseMirror[contenteditable="true"]')`))break;await delay(500);}
  const current=await c.evaluate(`document.querySelector('.ProseMirror[contenteditable="true"]')?.innerText`);
  if(current===undefined || current.trim() && !current.includes('Android emulator acceptance'))throw new Error('Open a blank synthetic test document before the writing/restart check.');
  await c.evaluate(`(()=>{const editor=document.querySelector('.ProseMirror[contenteditable="true"]');editor.focus();getSelection().selectAllChildren(editor);})()`);
  await c.send('Input.insertText',{text});
  await delay(300);
  adb('shell','input','keyevent','3');
  await delay(1200);
  adb('shell','am','force-stop','com.prosa.writer.android');c.close();
  adb('shell','monkey','-p','com.prosa.writer.android','-c','android.intent.category.LAUNCHER','1');
  await delay(1800);
  const reopened=await connect();
  for(let i=0;i<40;i++){if(await reopened.evaluate(`!!document.querySelector('.ProseMirror')`))break;await delay(500);}
  const state=await reopened.evaluate(`JSON.stringify({url:location.href,text:document.querySelector('.ProseMirror')?.innerText,body:document.body.innerText})`);
  writeFileSync(resolve(output,'write-restart.json'),state);
  if(JSON.parse(state).text!==text)throw new Error('Exact writing did not restore after background and restart: '+state);
  capture('writing-restarted');console.log('PASS: Unicode writing saved on background and restored after process restart.');
  adb('shell','input','keyevent','4');await delay(700);
  const back=await reopened.evaluate('location.pathname');
  if(back!=='/')throw new Error('Back did not return to the library: '+back);
  capture('back-library');console.log('PASS: Back safely returned to the local library.');
  reopened.close();
}

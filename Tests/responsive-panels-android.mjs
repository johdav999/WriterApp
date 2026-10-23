// Actual installed Debug MAUI WebView. No writing, provider, or account mutations.
import {execFileSync} from 'node:child_process';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const adbPath=process.env.PROSA_ADB || 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
const serial=process.env.PROSA_ANDROID_SERIAL || 'emulator-5554';
const evidence=resolve('artifacts/responsive-panels/android');mkdirSync(evidence,{recursive:true});
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
        console.log('Baseline writing and document hashes recorded.');
    } else {
        const baseline=JSON.parse(readFileSync(baselineFile,'utf8'));
        assert.deepEqual(await content(),baseline,'APK update must preserve route, writing and documents');
        await wait(`document.querySelector('[data-responsive-panels]')?.dataset.panelsReady==='true'`);
        const checks=[];
        assert.equal((await state()).openDrawer,'');capture('writing');
        await click('right');await wait(`document.querySelector('[data-responsive-panels]').dataset.openDrawer==='right'`);
        await evaluate(`[...document.querySelectorAll('[role=tab]')].find(e=>e.textContent.trim()==='Notes & Tasks').click()`);
        await wait(`[...document.querySelectorAll('[role=tab]')].some(e=>e.textContent.trim()==='Notes & Tasks' && e.getAttribute('aria-selected')==='true')`);capture('tools');
        adb('shell','input','keyevent','KEYCODE_BACK');
        await wait(`document.querySelector('[data-responsive-panels]').dataset.openDrawer===''`);
        assert.equal(await evaluate('location.pathname'),baseline.route);
        checks.push('Native Android Back closes tools and stays in the document');
        await click('left');await wait(`document.querySelector('[data-responsive-panels]').dataset.openDrawer==='left'`);capture('outline');
        assert.equal(await evaluate(`!!document.querySelector('.editor-navigation-host .local-structure-navigator')`),true);
        await click('close-left');await click('focus');
        await wait(`document.querySelector('[data-responsive-panels]').classList.contains('is-focus-mode')`);
        await click('right');await wait(`document.querySelector('[data-responsive-panels]').dataset.openDrawer==='right'`);
        assert.equal(await evaluate(`document.querySelector('[data-responsive-panels]').classList.contains('is-focus-mode')`),false);
        assert.equal(await evaluate(`[...document.querySelectorAll('[role=tab]')].some(e=>e.textContent.trim()==='Notes & Tasks' && e.getAttribute('aria-selected')==='true')`),true);
        await click('close-right');
        assert.deepEqual(await content(),baseline);
        checks.push('Native projects drawer and focus-to-tools flow preserve all document hashes and manuscript content');
        writeFileSync(resolve(evidence,'results.json'),JSON.stringify({checks,documentCount:Object.keys(baseline.documents).length,route:baseline.route},null,2));
        console.log(JSON.stringify({checks},null,2));
    }
} finally {socket.close();adb('forward','--remove','tcp:9334');}

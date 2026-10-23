// Real Android swipes over the manuscript. No writing, provider or account changes.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const mode = process.argv[2] ?? 'verify';
process.argv[2] = 'editor-scroll';
process.env.PROSA_ADB ??= 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
process.env.PROSA_ANDROID_EVIDENCE = 'artifacts/android-editor-scroll';
const { connect, adb, capture } = await import('./android-native-smoke.mjs');
const output = resolve(process.env.PROSA_ANDROID_EVIDENCE);
mkdirSync(output, {recursive:true});
const c = await connect();
const e = c.evaluate;
const pause = () => new Promise(r => setTimeout(r, 700));
const checks = [];
const metrics = () => e(`(() => { const el = document.querySelector('.content'); return {top:el.scrollTop,height:el.clientHeight,scroll:el.scrollHeight,overflow:getComputedStyle(el).overflowY}; })()`);
const wait = async expression => {
    for (let i=0; i<60; i++) { if (await e(expression)) return; await new Promise(r=>setTimeout(r,200)); }
    throw Error('Timed out: ' + expression);
};
const snapshot = async () => {
    const files = adb('shell','run-as','com.prosa.writer.android','find','files/development/documents','-maxdepth','1','-type','f').split(/\r?\n/).filter(p=>p.endsWith('.json')).sort();
    return {
        route: await e('location.pathname'),
        writing: createHash('sha256').update(await e(`document.querySelector('.ProseMirror').innerHTML`)).digest('hex'),
        documents: Object.fromEntries(files.map(p=>[p,adb('shell','run-as','com.prosa.writer.android','sha256sum',p).split(' ')[0]]))
    };
};
async function swipe(up, selector='.ProseMirror') {
    adb('shell','uiautomator','dump','/sdcard/prosa-editor-scroll.xml');
    const xml=adb('shell','cat','/sdcard/prosa-editor-scroll.xml');
    const bounds=xml.match(/class="android.webkit.WebView"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"/);
    assert(bounds, 'Native WebView bounds available');
    const [left,top,right,bottom]=bounds.slice(1).map(Number);
    const p=await e(`(() => { const r=document.querySelector(${JSON.stringify(selector)}).getBoundingClientRect(); const start=Math.max(70,r.top)+16, end=Math.min(innerHeight-30,r.bottom)-16; return {x:r.x+r.width*.65,start,end,width:innerWidth,height:innerHeight}; })()`);
    assert(p.end-p.start>60,'Enough visible writing surface for a swipe');
    const x=Math.round(left+p.x*(right-left)/p.width);
    const ys=[p.end,p.start].map(y=>Math.round(top+y*(bottom-top)/p.height));
    if(!up) ys.reverse();
    adb('shell','input','swipe',String(x),String(ys[0]),String(x),String(ys[1]),'600');
    await pause();
}
async function roundTrip(name) {
    await e(`document.querySelector('.content').scrollTop=0`);
    await pause();
    const before=await metrics();
    assert.equal(before.overflow,'auto', name+': page allows touch scrolling');
    assert(before.scroll>before.height+100,name+': overflowing page');
    await swipe(true);
    const down=await metrics();
    assert(down.top>before.top+50,name+': upward native swipe scrolls down');
    capture(name+'-down');
    await swipe(false);
    const up=await metrics();
    assert(up.top<down.top-50,name+': downward native swipe scrolls up');
    checks.push({name,before,down,up});
}
try {
    await wait(`!!document.querySelector('.ProseMirror')`);
    if(adb('shell','dumpsys','input_method').includes('mInputShown=true')) { adb('shell','input','keyevent','4'); await pause(); }
    if(mode==='baseline') {
        // Clear the exploratory CSS injection to measure the installed old APK.
        await e(`document.getElementById('scroll-candidate')?.remove(); [...document.querySelectorAll('link')].find(l=>l.getAttribute('href')==='android.css').disabled=false;`);
        writeFileSync(resolve(output,'baseline.json'),JSON.stringify(await snapshot(),null,2));
        const before=await metrics(); await swipe(true); const after=await metrics();
        assert.equal(after.top,before.top,'Original native swipe is trapped');
        capture('before');
        writeFileSync(resolve(output,'reproduction.json'),JSON.stringify({before,after},null,2));
        console.log('Reproduced: native swipe does not move overflow:hidden page. Baseline saved.');
    } else {
        const baseline=JSON.parse(readFileSync(resolve(output,'baseline.json'),'utf8'));
        assert.deepEqual(await snapshot(),baseline,'APK update preserves writing and documents');
        if(await e(`document.querySelector('.editor-shell').classList.contains('is-focus-mode')`)) {
            await e(`document.querySelector('[data-panel-action="focus"]').click()`);
            await wait(`!document.querySelector('.editor-shell').classList.contains('is-focus-mode')`);
        }
        await roundTrip('writing');
        // Stress paper taller than the screen without editing author content.
        await e(`document.querySelector('.ProseMirror').style.minHeight='2200px'`);
        await roundTrip('tall-paper');
        let end=await metrics();
        for(let i=0;i<10 && end.top+end.height<end.scroll-2;i++) { await swipe(true); end=await metrics(); }
        assert(end.top+end.height>=end.scroll-2,'Can reach the page end and save status');
        await e(`document.querySelector('.ProseMirror').style.removeProperty('min-height'); document.querySelector('.content').scrollTop=0;`);
        await e(`document.querySelector('[data-panel-action="focus"]').click()`);
        await wait(`document.querySelector('.editor-shell').classList.contains('is-focus-mode')`);
        await roundTrip('focus');
        await e(`document.querySelector('[data-panel-action="focus"]').click(); document.querySelector('.content').scrollTop=0;`);
        await wait(`!document.querySelector('.editor-shell').classList.contains('is-focus-mode')`);
        await e(`document.querySelector('[data-panel-action="right"]').click()`);
        await wait(`document.querySelector('.editor-shell').dataset.openDrawer==='right'`);
        assert.equal(await e(`document.querySelector('.editor-main').inert`),true,'Modal tools still isolate the manuscript');
        await e(`document.querySelector('[data-panel-action="close-right"]').click()`);
        await wait(`document.querySelector('.editor-shell').dataset.openDrawer===''`);
        await e(`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Add comment / task').click()`);
        await wait(`document.querySelector('.editor-shell').dataset.annotationPanel==='true' && document.querySelector('.editor-shell').dataset.rightOpen==='true'`);
        assert.equal((await metrics()).overflow,'hidden','Annotations retain the bounded split view');
        assert.equal(await e(`document.querySelector('.editor-main').inert`),false,'Annotation split keeps manuscript interactive');
        await e(`document.querySelector('[data-panel-action="close-right"]').click(); document.querySelector('.content').scrollTop=0;`);
        await roundTrip('after-annotations');
        await e(`document.querySelector('.content').scrollTop=0`);
        capture('after');
        assert.deepEqual(await snapshot(),baseline,'Swipes and panel navigation preserve all author documents');
        checks.push({name:'end-reachable-panels-and-data',documentsUnchanged:Object.keys(baseline.documents).length});
        writeFileSync(resolve(output,'results.json'),JSON.stringify(checks,null,2));
        console.log('PASS: native swipes work both ways in writing, tall paper, focus and after annotations; tools, split view, page end and documents preserved.');
    }
} finally {
    await e(`document.querySelector('.ProseMirror')?.style.removeProperty('min-height')`);
    c.close();
}

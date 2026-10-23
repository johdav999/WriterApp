// Installed MAUI WebView regression: real native swipes, no writing or AI calls.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const mode = process.argv[2] ?? 'verify';
process.argv[2] = 'tools-scroll'; // Load shared connection helpers without a smoke action.
process.env.PROSA_ADB ??= 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
process.env.PROSA_ANDROID_EVIDENCE = 'artifacts/android-tools-scroll';
const { connect, adb, capture } = await import('./android-native-smoke.mjs');
const output = resolve(process.env.PROSA_ANDROID_EVIDENCE);
mkdirSync(output, { recursive: true });
const c = await connect();
const evaluate = c.evaluate;
const delay = () => new Promise(r => setTimeout(r, 400));
const panel = `document.querySelector('.editor-context-host .rp-scroll')`;
async function snapshot() {
    const paths = adb('shell', 'run-as', 'com.prosa.writer.android', 'find', 'files/development/documents', '-maxdepth', '1', '-type', 'f').split(/\r?\n/).filter(p => p.endsWith('.json')).sort();
    return {
        route: await evaluate('location.pathname'),
        writing: createHash('sha256').update(await evaluate(`document.querySelector('.ProseMirror').innerHTML`)).digest('hex'),
        documents: Object.fromEntries(paths.map(p => [p, adb('shell', 'run-as', 'com.prosa.writer.android', 'sha256sum', p).split(' ')[0]]))
    };
}
const metrics = () => evaluate(`(()=>{const e=${panel};return{top:e.scrollTop,height:e.clientHeight,content:e.scrollHeight,overflow:getComputedStyle(e).overflowY};})()`);
async function swipe(up) {
    // Map CSS coordinates to the current native WebView bounds, including system bars.
    adb('shell', 'uiautomator', 'dump', '/sdcard/prosa-scroll-uat.xml');
    const xml = adb('shell', 'cat', '/sdcard/prosa-scroll-uat.xml');
    const bounds = xml.match(/class="android.webkit.WebView"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"/);
    assert.ok(bounds, 'Native WebView bounds are available');
    const [left, top, right, bottom] = bounds.slice(1).map(Number);
    const p = await evaluate(`(()=>{const r=${panel}.getBoundingClientRect();return{x:r.x+r.width*.85,start:Math.min(r.bottom,innerHeight)-50,end:r.top+80,width:innerWidth,height:innerHeight};})()`);
    const x = Math.round(left + p.x * (right - left) / p.width);
    const ys = [p.start, p.end].map(y => Math.round(top + y * (bottom - top) / p.height));
    if (!up) ys.reverse();
    adb('shell', 'input', 'swipe', String(x), String(ys[0]), String(x), String(ys[1]), '600');
    await new Promise(r => setTimeout(r, 1000)); // Let native fling animation settle.
}
async function selectTool(label) {
    await evaluate(`(()=>{const b=[...document.querySelectorAll('.context-drawer button')].find(b=>b.textContent.trim()===${JSON.stringify(label)});if(!b)throw Error('Missing tool');b.click();})()`);
    await delay();
    await evaluate(`${panel}.scrollTop=0`);
}
async function verifyTool(label) {
    await selectTool(label);
    const before = await metrics();
    assert.ok(before.content > before.height + 100, `${label} has overflowing content to exercise`);
    await swipe(true);
    const down = await metrics();
    assert.ok(down.top > before.top + 100, `${label}: upward native swipe scrolls down`);
    capture(label.replaceAll(/[^a-z0-9]+/gi, '-').toLowerCase() + '-down');
    await swipe(false);
    const up = await metrics();
    assert.ok(up.top < down.top - 100, `${label}: downward native swipe scrolls up`);
    let end = await metrics();
    for (let i = 0; i < 8 && end.content - end.height - end.top > 2; i++) {
        await swipe(true);
        end = await metrics();
    }
    assert.ok(Math.abs(end.content - end.height - end.top) <= 2, `${label}: bottom can be reached`);
    const heading = await evaluate(`(()=>{const r=document.querySelector('.editor-panel-heading').getBoundingClientRect();return{top:r.top,bottom:r.bottom};})()`);
    assert.ok(heading.top >= 0 && heading.bottom <= 100, 'Back to writing remains visible');
    await evaluate(`${panel}.scrollTop=0`);
    return { label, before, down, up, end };
}
try {
    for (let i = 0; i < 75 && !await evaluate(`!!document.querySelector('.ProseMirror')`); i++) await delay();
    assert.ok(await evaluate(`!!document.querySelector('.ProseMirror')`), 'Restored manuscript is ready');
    if (mode === 'baseline') {
        const content = await snapshot();
        const before = await metrics();
        capture('before');
        await swipe(true);
        const after = await metrics();
        assert.equal(after.top, before.top, 'Reproduce the reported blocked native swipe');
        assert.deepEqual(await snapshot(), content);
        writeFileSync(resolve(output, 'baseline.json'), JSON.stringify({ content, before, after }, null, 2));
        console.log(JSON.stringify({ reproduced: true, before, after }, null, 2));
    } else {
        const baseline = JSON.parse(readFileSync(resolve(output, 'baseline.json'), 'utf8'));
        assert.deepEqual(await snapshot(), baseline.content, 'APK update preserves all writing and documents');
        if (await evaluate(`document.querySelector('[data-responsive-panels]').dataset.openDrawer!=='right'`)) {
            await evaluate(`document.querySelector('[data-panel-action="right"]').click()`);
            await delay();
        }
        const checks = [];
        for (const label of ['Style & quality Coach', 'Writing tools', 'Consistency Coach']) checks.push(await verifyTool(label));
        await selectTool('Style & quality Coach');
        adb('shell', 'input', 'keyevent', 'KEYCODE_BACK');
        await delay();
        assert.equal(await evaluate(`document.querySelector('[data-responsive-panels]').dataset.openDrawer`), '');
        await evaluate(`document.querySelector('[data-panel-action="right"]').click()`);
        await delay();
        checks.push(await verifyTool('Style & quality Coach'));
        assert.deepEqual(await snapshot(), baseline.content, 'Swiping, tool switching, and Back preserve all documents');
        capture('fixed');
        writeFileSync(resolve(output, 'results.json'), JSON.stringify({ checks, documentsUnchanged: true, nativeBack: true }, null, 2));
        console.log(JSON.stringify({ checks, documentsUnchanged: true, nativeBack: true }, null, 2));
    }
} finally { c.close(); }

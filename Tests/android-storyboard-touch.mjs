// Real Android touch input. Mutations are limited to a newly created local test
// project; baseline user documents must retain their complete file hashes.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const mode = process.argv[2] ?? 'verify';
process.argv[2] = 'storyboard-touch';
process.env.PROSA_ADB ??= 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
process.env.PROSA_ANDROID_EVIDENCE = 'artifacts/android-storyboard-touch';
const { connect, adb, capture } = await import('./android-native-smoke.mjs');
const output = resolve(process.env.PROSA_ANDROID_EVIDENCE);
mkdirSync(output, { recursive: true });
const c = await connect();
const delay = ms => new Promise(r => setTimeout(r, ms));
async function wait(expression) {
    for (let i = 0; i < 100; i++) { if (await c.evaluate(expression)) return; await delay(250); }
    throw Error('Timed out: ' + expression);
}
const hashes = () => Object.fromEntries(adb('shell', 'run-as', 'com.prosa.writer.android', 'find', 'files/development/documents', '-maxdepth', '1', '-type', 'f')
    .split(/\r?\n/).filter(p => p.endsWith('.json')).sort().map(p => [p, adb('shell', 'run-as', 'com.prosa.writer.android', 'sha256sum', p).split(' ')[0]]));
function unchanged(baseline) {
    const after = hashes();
    for (const [path, hash] of Object.entries(baseline.documents)) assert.equal(after[path], hash, 'Existing document unchanged: ' + path);
}
async function navigate(route) {
    await c.evaluate(`(()=>{const a=document.createElement('a');a.href=${JSON.stringify(route)};document.querySelector('.app-body').append(a);a.click();setTimeout(()=>a.remove(),1000);})()`);
    await wait(`location.pathname===${JSON.stringify(route.split('?')[0])}`);
    await delay(600);
}
const click = label => c.evaluate(`(()=>{const b=[...document.querySelectorAll('button')].find(e=>e.textContent.trim()===${JSON.stringify(label)});if(!b)throw Error('Missing button');b.click();})()`);
const rows = () => c.evaluate(`[...document.querySelectorAll('[data-outline-id]')].map(e=>({id:e.dataset.outlineId,type:e.dataset.outlineType,parent:e.dataset.outlineParent}))`);
const order = () => c.evaluate(`[...document.querySelectorAll('[data-storyboard-chapter]')].map(e=>({chapter:e.dataset.storyboardChapter,scenes:[...e.querySelectorAll('[data-storyboard-scene]')].map(s=>s.dataset.storyboardScene)}))`);
async function point(selector) {
    await c.evaluate(`document.querySelector(${JSON.stringify(selector)}).scrollIntoView({block:'center',inline:'nearest'})`);
    await delay(350);
    return c.evaluate(`(()=>{const r=document.querySelector(${JSON.stringify(selector)}).getBoundingClientRect();return{x:r.x+r.width/2,y:r.y+r.height/2};})()`);
}
async function touchMove(start, end) {
    await c.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [start] });
    for (let i = 1; i <= 12; i++) {
        await c.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: start.x + (end.x - start.x) * i / 12, y: start.y + (end.y - start.y) * i / 12 }] });
        await delay(40);
    }
}
async function drag(scene, target, endType = 'touchEnd', native = false) {
    // Scroll both into view before pressing: scrolling during a captured gesture
    // would test a synthetic relocation instead of an ordinary finger drag.
    await point(target);
    const start = await point(`[data-storyboard-scene="${scene}"] .scene-card-drag-handle`);
    const end = await c.evaluate(`(()=>{const r=document.querySelector(${JSON.stringify(target)}).getBoundingClientRect();return{x:Math.min(innerWidth-45,r.x+r.width/2),y:Math.min(innerHeight-25,r.y+r.height/2)};})()`);
    if (native) {
        adb('shell', 'uiautomator', 'dump', '/sdcard/prosa-storyboard-uat.xml');
        const xml = adb('shell', 'cat', '/sdcard/prosa-storyboard-uat.xml');
        const match = xml.match(/class="android.webkit.WebView"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"/);
        assert.ok(match);
        const [x0, y0, x1, y1] = match.slice(1).map(Number);
        const viewport = await c.evaluate('({width:innerWidth,height:innerHeight})');
        const map = p => [Math.round(x0+p.x*(x1-x0)/viewport.width), Math.round(y0+p.y*(y1-y0)/viewport.height)];
        adb('shell', 'input', 'swipe', ...map(start).map(String), ...map(end).map(String), '900');
    } else {
        await touchMove(start, end);
        assert.equal(await c.evaluate(`document.querySelector('.project-storyboard').classList.contains('is-pointer-dragging')`), true, 'Touch capture transfer retains the gesture');
        await c.send('Input.dispatchTouchEvent', { type: endType, touchPoints: [] });
    }
    await delay(900);
    await wait(`document.querySelector('.project-storyboard').dataset.storyboardBusy==='false'`);
    assert.equal(await c.evaluate(`!!document.querySelector('.is-pointer-dragging')`), false);
    assert.equal(await c.evaluate(`!!document.querySelector('.storyboard-page__error,.project-storyboard-error')`), false);
}
const savedDocument = id => JSON.parse(adb('shell', 'run-as', 'com.prosa.writer.android', 'cat', `files/development/documents/${id.replaceAll('-', '')}.json`)).document;
const pages = d => Object.fromEntries(d.sections.map(s => [s.sectionId, s.pages]));
try {
    if (mode === 'baseline') {
        await wait(`!!document.querySelector('.scene-card-drag-handle')`);
        const baseline = { route: await c.evaluate('location.pathname'), documents: hashes() };
        await c.evaluate(`(()=>{window.__storyboardEvents=[];window.__storyboardProbe=e=>window.__storyboardEvents.push({type:e.type,target:e.target.className,active:document.querySelector('.project-storyboard').classList.contains('is-pointer-dragging')});for(const n of ['gotpointercapture','lostpointercapture'])document.addEventListener(n,window.__storyboardProbe,true);})()`);
        try {
            const start = await point('.scene-card-drag-handle');
            await touchMove(start, { x: start.x + 45, y: start.y });
            baseline.events = await c.evaluate('window.__storyboardEvents');
            baseline.activeAfterMove = await c.evaluate(`!!document.querySelector('.is-pointer-dragging')`);
            await c.send('Input.dispatchTouchEvent', { type: 'touchCancel', touchPoints: [] });
            assert.equal(baseline.activeAfterMove, false, 'Reproduce Android canceling capture transfer');
            unchanged(baseline);
            capture('before');
            writeFileSync(resolve(output, 'baseline.json'), JSON.stringify(baseline, null, 2));
            console.log(JSON.stringify({ reproduced: true, events: baseline.events }, null, 2));
        } finally {
            await c.evaluate(`(()=>{for(const n of ['gotpointercapture','lostpointercapture'])document.removeEventListener(n,window.__storyboardProbe,true);delete window.__storyboardProbe;delete window.__storyboardEvents;})()`);
        }
    } else {
        const baseline = JSON.parse(readFileSync(resolve(output, 'baseline.json'), 'utf8'));
        unchanged(baseline);
        if (mode === 'restart') {
            const result = JSON.parse(readFileSync(resolve(output, 'results.json'), 'utf8'));
            await navigate(result.testRoute);
            await wait(`document.querySelectorAll('[data-storyboard-scene]').length===2`);
            assert.deepEqual(await order(), result.order);
            assert.deepEqual(pages(savedDocument(result.documentId)), result.pages);
            unchanged(baseline);
            result.restartPersistence = true;
            writeFileSync(resolve(output, 'results.json'), JSON.stringify(result, null, 2));
            await navigate(baseline.route);
            console.log('PASS: saved scene order and writing survived Android process restart.');
        } else {
            const installed = await c.evaluate(`fetch('/_content/WriterApp.UI.Shared/storyboard.js').then(r=>r.text())`);
            assert.equal(installed, readFileSync('WriterApp.UI.Shared/wwwroot/storyboard.js', 'utf8'), 'Installed script matches the fixed source');
            await navigate('/projects');
            await click('New project');
            await wait(`document.querySelectorAll('[data-outline-id]').length===2`);
            const projectRoute = await c.evaluate('location.pathname');
            await click('Edit name');
            await wait(`!!document.querySelector('.project-name-input')`);
            await c.evaluate(`(()=>{const e=document.querySelector('.project-name-input');e.value='Android Storyboard touch UAT';e.dispatchEvent(new Event('input',{bubbles:true}));})()`);
            await click('Save'); await delay(400);
            const initial = await rows(), chapter = initial.find(r=>r.type==='chapter'), a = initial.find(r=>r.type==='scene');
            await c.evaluate(`document.querySelector('[data-outline-id="${chapter.id}"] .tree-row').click()`);
            await c.evaluate(`(()=>{const e=document.querySelector('#add-node-type');e.value='scene';e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
            await delay(200); await click('Add scene to parent');
            await wait(`document.querySelectorAll('[data-outline-id]').length===3`);
            const b = (await rows()).find(r=>r.type==='scene'&&r.id!==a.id);
            await c.evaluate(`(()=>{const e=document.querySelector('#add-node-type');e.value='chapter';e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
            await delay(200); await click('Add chapter at root');
            await wait(`document.querySelectorAll('[data-outline-id]').length===4`);
            const other = (await rows()).find(r=>r.type==='chapter'&&r.id!==chapter.id);
            const testRoute = projectRoute + '/storyboard';
            await navigate(testRoute);
            await wait(`document.querySelectorAll('[data-storyboard-scene]').length===2`);
            const documentId = await c.evaluate(`document.querySelector('.storyboard-page__actions a[href^="/documents/"]').getAttribute('href').split('/')[2]`);
            const beforePages = pages(savedDocument(documentId));
            await drag(b.id, `[data-storyboard-before="${a.id}"]`, 'touchCancel');
            assert.deepEqual((await order())[0].scenes, [a.id, b.id], 'Canceled gesture cannot save');
            await drag(b.id, `[data-storyboard-before="${a.id}"]`);
            assert.deepEqual((await order())[0].scenes, [b.id, a.id]);
            await drag(a.id, `[data-storyboard-chapter="${other.id}"]`, 'touchEnd', true);
            assert.deepEqual(await order(), [{ chapter: chapter.id, scenes: [b.id] }, { chapter: other.id, scenes: [a.id] }]);
            await drag(b.id, `[data-storyboard-chapter="${other.id}"] [data-storyboard-before="${a.id}"]`);
            assert.deepEqual((await order())[1].scenes, [b.id, a.id]);
            capture('fixed-test-project');
            const finalOrder = await order();
            await navigate('/projects'); await navigate(testRoute);
            await wait(`document.querySelectorAll('[data-storyboard-scene]').length===2`);
            assert.deepEqual(await order(), finalOrder, 'Saved movement survives route reload');
            assert.deepEqual(pages(savedDocument(documentId)), beforePages, 'All page objects and section IDs are unchanged');
            unchanged(baseline);
            writeFileSync(resolve(output, 'results.json'), JSON.stringify({ checks: ['Touch cancellation preserves order', 'Touch reorder', 'Native adb grip drag into an empty chapter', 'Touch insertion before an occupied chapter scene', 'Saved order survives reopening', 'Section IDs and all page objects unchanged', 'Existing document hashes unchanged', 'Installed script matches source'], testRoute, documentId, order: finalOrder, pages: beforePages }, null, 2));
            await navigate(baseline.route);
            await wait(`!!document.querySelector('.project-storyboard')`);
            capture('original-restored');
            console.log('PASS: Android storyboard touch reorder, cross-chapter moves, cancellation and saved order; original documents unchanged.');
        }
    }
} finally { c.close(); }

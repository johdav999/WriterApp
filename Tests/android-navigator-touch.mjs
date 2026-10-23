// Real touch input in the installed Debug Android WebView. Only a newly created
// synthetic project is rearranged; existing documents are checked by hash.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
process.env.PROSA_ADB ??= 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
const mode = process.argv[2];
process.argv[2] = 'navigator-touch';
const { connect, adb, capture } = await import('./android-native-smoke.mjs');
const output = resolve('artifacts/android-navigator-touch');
mkdirSync(output, { recursive: true });
const delay = ms => new Promise(r => setTimeout(r, ms));
const hashes = () => Object.fromEntries(adb('shell', 'run-as', 'com.prosa.writer.android', 'find', 'files/development/documents', '-maxdepth', '1', '-type', 'f')
    .split(/\r?\n/).filter(p => p.endsWith('.json')).sort().map(p => [p, adb('shell', 'run-as', 'com.prosa.writer.android', 'sha256sum', p).split(' ')[0]]));
const c = await connect();
const wait = async expression => {
    for (let i = 0; i < 80; i++) { if (await c.evaluate(expression)) return; await delay(250); }
    throw Error('Timed out: ' + expression);
};
const navigate = async route => {
    await c.evaluate(`document.querySelector('[data-panel-action="close-right"]')?.click();document.querySelector('[data-panel-action="close-left"]')?.click()`);
    await delay(100);
    await c.evaluate(`(()=>{const route=${JSON.stringify(route)};const existing=[...document.querySelectorAll('a')].find(e=>e.getAttribute('href')===route);if(existing){existing.click();return;}const a=document.createElement('a');a.href=route;document.querySelector('.app-body').append(a);a.click();setTimeout(()=>a.remove(),1500);})()`);
    await delay(600);
};
const rows = () => c.evaluate(`[...document.querySelectorAll('.outline-drag-row')].filter(e=>e.getBoundingClientRect().height>0).map(e=>({id:e.dataset.outlineId,type:e.dataset.outlineType,parent:e.dataset.outlineParent,title:e.querySelector('.tree-select-title').textContent}))`);
const click = text => c.evaluate(`[...document.querySelectorAll('button')].find(e=>e.textContent.trim()===${JSON.stringify(text)}).click()`);
const tapRow = async id => {
    const point = await c.evaluate(`(()=>{const e=[...document.querySelectorAll('[data-outline-id="${id}"] .tree-select')].find(e=>e.getBoundingClientRect().height>0);e.scrollIntoView({block:'center'});const r=e.getBoundingClientRect();return{x:r.x+r.width/2,y:r.y+r.height/2};})()`);
    await c.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [point] });
    await c.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    await delay(300);
};
const selectType = async type => {
    await c.evaluate(`(()=>{const e=document.querySelector('#add-node-type');e.value=${JSON.stringify(type)};e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
    await delay(250);
};
const drag = async (id, target, fraction) => {
    const point = await c.evaluate(`(()=>{const e=[...document.querySelectorAll('[data-outline-id="${id}"] [data-outline-handle]')].find(e=>e.getBoundingClientRect().height>0);e.scrollIntoView({block:'center'});const r=e.getBoundingClientRect();return{x:r.x+r.width/2,y:r.y+r.height/2};})()`);
    await c.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [point] });
    const end = await c.evaluate(`(()=>{const e=[...document.querySelectorAll('[data-outline-id="${target}"]')].find(e=>e.getBoundingClientRect().height>0);const r=e.getBoundingClientRect();return{x:r.x+r.width/2,y:r.y+r.height*${fraction}};})()`);
    for (let i = 1; i <= 8; i++) {
        await c.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: point.x + (end.x - point.x) * i / 8, y: point.y + (end.y - point.y) * i / 8 }] });
        await delay(35);
    }
    assert.equal(await c.evaluate(`[...document.querySelectorAll('[data-outline-id="${target}"]')].some(e=>e.classList.contains('outline-drop-${fraction > .75 ? 'after' : fraction < .25 ? 'before' : 'inside'}'))`), true);
    await c.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    await wait(`!document.querySelector('[data-outline-disabled="true"]')`);
    await delay(600);
    assert.equal(await c.evaluate(`!!document.querySelector('.outline-drag-active')`), false);
};
try {
    if (mode === 'baseline') {
        writeFileSync(resolve(output, 'baseline.json'), JSON.stringify({ route: await c.evaluate('location.pathname'), documents: hashes() }, null, 2));
        console.log('Recorded original route and document hashes.');
    } else {
        const baseline = JSON.parse(readFileSync(resolve(output, 'baseline.json'), 'utf8'));
        const upgraded = hashes();
        for (const [path, hash] of Object.entries(baseline.documents)) assert.equal(upgraded[path], hash, 'APK upgrade preserved existing documents');
        await navigate('/projects');
        await wait(`!![...document.querySelectorAll('button')].find(e=>e.textContent.trim()==='New project')`);
        await click('New project');
        await wait(`location.pathname.startsWith('/projects/')`);
        await delay(400);
        await wait(`document.querySelectorAll('.outline-drag-row').length===2`);
        await click('Edit name');
        await wait(`!!document.querySelector('.project-name-input')`);
        await c.evaluate(`(()=>{const e=document.querySelector('.project-name-input');e.value='Android Navigator touch UAT';e.dispatchEvent(new Event('input',{bubbles:true}));})()`);
        await delay(200);
        await click('Save');
        await delay(400);
        const route = await c.evaluate('location.pathname');
        const initial = await rows();
        const chapter = initial.find(r => r.type === 'chapter'), a = initial.find(r => r.type === 'scene');
        await c.evaluate(`document.querySelector('[data-outline-id="${chapter.id}"] .tree-row').click()`);
        await selectType('scene');
        await click('Add scene to parent');
        await wait(`document.querySelectorAll('.outline-drag-row').length===3`);
        const b = (await rows()).find(r => r.type === 'scene' && r.id !== a.id);
        assert.equal(await c.evaluate(`getComputedStyle(document.querySelector('[data-outline-handle]')).touchAction`), 'none');
        await drag(a.id, b.id, .95);
        assert.deepEqual((await rows()).filter(r => r.type === 'scene').map(r => r.id), [b.id, a.id]);
        await selectType('chapter');
        await click('Add chapter at root');
        await wait(`document.querySelectorAll('.outline-drag-row').length===4`);
        const second = (await rows()).find(r => r.type === 'chapter' && r.id !== chapter.id);
        await drag(a.id, second.id, .5);
        assert.equal((await rows()).find(r => r.id === a.id).parent, second.id);
        // Revisit the route to load structure from the repository, not DOM state.
        await navigate('/projects'); await navigate(route);
        await wait(`document.querySelectorAll('.outline-drag-row').length===4`);
        assert.equal((await rows()).find(r => r.id === a.id).parent, second.id);
        capture('navigator-touch-project');
        await tapRow(a.id);
        await click('Open scene in editor');
        await wait(`!!document.querySelector('.ProseMirror[contenteditable="true"]')`);
        await delay(1000);
        await c.evaluate(`document.querySelector('[data-panel-action="right"]').click()`);
        await c.evaluate(`[...document.querySelectorAll('[role=tab]')].find(e=>e.textContent.trim()==='Navigator').click()`);
        await wait(`[...document.querySelectorAll('[role=tab]')].some(e=>e.textContent.trim()==='Navigator' && e.getAttribute('aria-selected')==='true')`);
        await click('Project navigator');
        await wait(`[...document.querySelectorAll('.is-embedded-navigator [data-outline-handle]')].some(e=>e.getBoundingClientRect().height>0)`);
        await drag(a.id, chapter.id, .5);
        await wait(`[...document.querySelectorAll('[data-outline-id="${a.id}"]')].some(e=>e.getBoundingClientRect().height>0 && e.dataset.outlineParent==='${chapter.id}')`);
        capture('navigator-touch-embedded');
        const after = hashes();
        for (const [path, hash] of Object.entries(baseline.documents)) assert.equal(after[path], hash, 'Existing document unchanged: ' + path);
        writeFileSync(resolve(output, 'results.json'), JSON.stringify({ checks: ['APK update preserved existing documents', 'Real Android touch reordered scenes', 'Touch moved scene into another chapter', 'Saved movement survived route reload', 'Embedded Navigator touch moved scene', 'Existing documents unchanged'], testProjectRoute: route }, null, 2));
        await navigate(baseline.route);
        await wait(`!!document.querySelector('.ProseMirror')`);
        await c.evaluate(`document.querySelector('[data-panel-action="right"]').click()`);
        await c.evaluate(`[...document.querySelectorAll('[role=tab]')].find(e=>e.textContent.trim()==='Navigator').click()`);
        await wait(`[...document.querySelectorAll('[role=tab]')].some(e=>e.textContent.trim()==='Navigator' && e.getAttribute('aria-selected')==='true')`);
        await click('Project navigator');
        console.log('PASS: native touch reorder, reparent, reload and embedded Navigator; existing document hashes preserved.');
    }
} finally { c.close(); }

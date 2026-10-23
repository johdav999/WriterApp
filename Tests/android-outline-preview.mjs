// Installed Android WebView regression: adding structure from reading preview.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
process.env.PROSA_ADB ??= 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
const mode = process.argv[2];
process.argv[2] = 'outline-preview';
const { connect, adb } = await import('./android-native-smoke.mjs');
const dir = 'artifacts/android-outline-preview';
mkdirSync(dir, { recursive: true });
const c = await connect();
const delay = ms => new Promise(r => setTimeout(r, ms));
const hashes = () => Object.fromEntries(adb('shell', 'run-as', 'com.prosa.writer.android', 'find', 'files/development/documents', '-maxdepth', '1', '-type', 'f').split(/\r?\n/).filter(p => p.endsWith('.json')).map(p => [p, adb('shell', 'run-as', 'com.prosa.writer.android', 'sha256sum', p).split(' ')[0]]));
const wait = async expression => { for(let i=0;i<80;i++){if(await c.evaluate(expression))return;await delay(250);}throw Error(expression); };
const click = async text => { await c.evaluate(`[...document.querySelectorAll('button')].find(e=>e.textContent.trim()===${JSON.stringify(text)} && e.getBoundingClientRect().height>0).click()`); await delay(400); };
const navigate = async route => {
    await c.evaluate(`document.querySelector('[data-panel-action="close-left"]')?.click();document.querySelector('[data-panel-action="close-right"]')?.click()`);
    await c.evaluate(`(()=>{const a=document.createElement('a');a.href=${JSON.stringify(route)};document.querySelector('.app-body').append(a);a.click();a.remove();})()`);
    await delay(800);
};
try {
    if(mode==='baseline'){
        const original = { route: await c.evaluate('location.pathname'), hashes: hashes() };
        await navigate('/projects'); await click('New project'); await click('Open manuscript');
        await wait(`!!document.querySelector('.ProseMirror')`);
        original.testRoute = await c.evaluate('location.pathname');
        writeFileSync(`${dir}/baseline.json`, JSON.stringify(original,null,2));
    }
    const baseline = JSON.parse(readFileSync(`${dir}/baseline.json`,'utf8'));
    await navigate(baseline.testRoute);
    await wait(`!!document.querySelector('.ProseMirror')`);
    await c.evaluate(`document.querySelector('.preview-toggle').click()`);
    await wait(`!!document.querySelector('#preview-find')`);
    await c.evaluate(`document.querySelector('[data-panel-action="left"]').click()`);
    await delay(400);
    const before = await c.evaluate(`document.querySelectorAll('.is-embedded-navigator .outline-drag-row').length`);
    await click('Add part at root');
    if(mode==='baseline'){
        assert.equal(await c.evaluate(`document.body.innerText.includes('Save the current writing and planning before changing structure.')`),true);
        console.log('Reproduced: preview incorrectly reports unsaved writing.');
    }else{
        await wait(`document.querySelectorAll('.is-embedded-navigator .outline-drag-row').length===${before+1}`);
        assert.equal(await c.evaluate(`!!document.querySelector('#preview-find')`),false);
        assert.equal(await c.evaluate(`document.body.innerText.includes('Save the current writing and planning before changing structure.')`),false);
        await c.evaluate(`(()=>{const s=document.querySelector('#editor-outline-add-node-type');s.value='scene';s.dispatchEvent(new Event('change',{bubbles:true}));})()`);
        await delay(300);
        await c.evaluate(`[...document.querySelectorAll('.is-embedded-navigator button')].find(e=>e.textContent.trim().startsWith('Add scene')).click()`);
        await wait(`document.querySelectorAll('.is-embedded-navigator .outline-drag-row').length===${before+2}`);
        await navigate('/projects'); await navigate(baseline.testRoute);
        await wait(`document.querySelectorAll('.is-embedded-navigator .outline-drag-row').length===${before+2}`);
        const after=hashes();
        for(const [path,hash] of Object.entries(baseline.hashes))assert.equal(after[path],hash,path);
        writeFileSync(`${dir}/results.json`,JSON.stringify({passed:true,checks:['Add part exits preview','Add scene succeeds','Structure persists after reload','Original documents unchanged']},null,2));
        console.log('PASS: preview add, repeated scene add, persistence and original document hashes.');
    }
    await navigate(baseline.route);
} finally { c.close(); }

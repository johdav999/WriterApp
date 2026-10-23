// Installed Android WebView regression. Native swipes; no document edits or AI calls.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const mode = process.argv[2] ?? 'verify';
process.argv[2] = 'storyboard-scroll';
process.env.PROSA_ADB ??= 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
process.env.PROSA_ANDROID_EVIDENCE = 'artifacts/android-storyboard-scroll';
const { connect, adb, capture } = await import('./android-native-smoke.mjs');
const output = resolve(process.env.PROSA_ANDROID_EVIDENCE);
mkdirSync(output, { recursive: true });
const c = await connect();
const pause = () => new Promise(r => setTimeout(r, 800));
const evaluate = c.evaluate;
const page = `document.querySelector('.content')`;
const selectors = ['.project-storyboard-board', '.storyboard-insights-scroll', '.storyboard-inspector-scroll'];
const hashes = () => Object.fromEntries(adb('shell', 'run-as', 'com.prosa.writer.android', 'find', 'files/development/documents', '-maxdepth', '1', '-type', 'f')
    .split(/\r?\n/).filter(p => p.endsWith('.json')).sort().map(p => [p, adb('shell', 'run-as', 'com.prosa.writer.android', 'sha256sum', p).split(' ')[0]]));
const metrics = selector => evaluate(`(() => {const e=document.querySelector(${JSON.stringify(selector)});return {pageTop:${page}.scrollTop,top:e.scrollTop,height:e.clientHeight,scroll:e.scrollHeight,overscroll:getComputedStyle(e).overscrollBehaviorY};})()`);
async function navigate(route) {
    await evaluate(`(() => {const a=document.createElement('a');a.href=${JSON.stringify(route)};document.querySelector('.app-body').append(a);a.click();setTimeout(()=>a.remove(),1000);})()`);
    for (let i=0; i<40 && await evaluate('location.pathname') !== route; i++) await pause();
    await pause();
}

async function position(selector, bottom = false) {
    // Start each gesture inside a panel at its boundary, where the outer page
    // must take over. Positioning is setup only; every assertion uses native input.
    await evaluate(`(() => {const e=document.querySelector(${JSON.stringify(selector)}),p=${page};p.scrollTop+=e.getBoundingClientRect().top-p.getBoundingClientRect().top-50;e.scrollTop=${bottom ? 'e.scrollHeight' : '0'};})()`);
    await pause();
}
async function swipe(selector, down) {
    adb('shell', 'uiautomator', 'dump', '/sdcard/prosa-storyboard-scroll.xml');
    const xml = adb('shell', 'cat', '/sdcard/prosa-storyboard-scroll.xml');
    const bounds = xml.match(/class="android.webkit.WebView"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"/);
    assert(bounds, 'Native WebView bounds available');
    const [left, top, right, bottom] = bounds.slice(1).map(Number);
    const p = await evaluate(`(() => {const r=document.querySelector(${JSON.stringify(selector)}).getBoundingClientRect(),outer=${page}.getBoundingClientRect();return {x:r.x+r.width*.8,start:Math.max(outer.top,r.top)+25,end:Math.min(innerHeight,r.bottom)-25,width:innerWidth,height:innerHeight};})()`);
    assert(p.end - p.start > 70, 'Enough visible panel for a native swipe: ' + selector);
    const x = Math.round(left + p.x * (right-left) / p.width);
    const ys = [p.start, p.end].map(y => Math.round(top + y * (bottom-top) / p.height));
    if (!down) ys.reverse();
    adb('shell', 'input', 'swipe', String(x), String(ys[0]), String(x), String(ys[1]), '600');
    await pause();
}
async function boundarySwipe(selector, down, before) {
    const attempts = [];
    // A gesture that first reaches the panel edge can remain latched to that
    // panel in WebView. A fresh swipe at the edge must reach the outer page.
    for (let i=0; i<2; i++) {
        await swipe(selector, down);
        attempts.push(await metrics(selector));
        if (Math.abs(attempts.at(-1).pageTop-before.pageTop)>40) break;
    }
    return { after: attempts.at(-1), attempts };
}

try {
    if (mode !== 'baseline') {
        const baseline = JSON.parse(readFileSync(resolve(output, 'baseline.json'), 'utf8'));
        // Android may restore the last writing route after an APK replacement.
        if (!await evaluate(`!!document.querySelector('.storyboard-inspector-empty')`)) {
            await navigate('/projects');
            await navigate(baseline.route);
        }
        for (let i=0; i<40 && !await evaluate(`!!document.querySelector('.storyboard-inspector-empty')`); i++) await pause();
    }
    assert(await evaluate(`!!document.querySelector('.storyboard-inspector-empty')`), 'Open a storyboard without a selected scene');
    if (mode === 'baseline') {
        const baseline = { route: await evaluate('location.pathname'), documents: hashes() };
        await position(selectors[1]);
        baseline.before = await metrics(selectors[1]);
        capture('before');
        Object.assign(baseline, await boundarySwipe(selectors[1], true, baseline.before));
        assert(baseline.before.pageTop > 100, 'Page has scrolled down');
        assert(baseline.before.scroll > baseline.before.height, 'Insights is an overflowing panel');
        assert.equal(baseline.after.pageTop, baseline.before.pageTop, 'Reproduced: downward swipe at the top of Insights cannot scroll page up');
        writeFileSync(resolve(output, 'baseline.json'), JSON.stringify(baseline, null, 2));
        console.log('Reproduced: Storyboard Insights traps the native return swipe at its upper boundary.');
    } else {
        const baseline = JSON.parse(readFileSync(resolve(output, 'baseline.json'), 'utf8'));
        assert.equal(await evaluate('location.pathname'), baseline.route);
        assert.deepEqual(hashes(), baseline.documents, 'APK upgrade preserves all author documents');
        const css = await evaluate(`fetch('/android.css').then(r=>r.text())`);
        assert.equal(css, readFileSync('WriterApp.Android/wwwroot/android.css', 'utf8'), 'Installed CSS matches source');
        const results = [];
        for (const selector of selectors) {
            await position(selector);
            const before = await metrics(selector);
            assert.equal(before.overscroll, 'auto');
            assert(before.pageTop > 100);
            const {after,attempts} = await boundarySwipe(selector, true, before);
            assert(after.pageTop < before.pageTop - 40, selector + ': downward swipe returns toward page heading');
            results.push({ selector, direction: 'up', before, after, attempts });
            await position(selector, true);
            // Leave room below the panel so upward swipes can move the page.
            await evaluate(`${page}.scrollTop=Math.max(0,${page}.scrollTop-150)`);
            await pause();
            const downBefore = await metrics(selector);
            const {after:downAfter,attempts:downAttempts} = await boundarySwipe(selector, false, downBefore);
            assert(downAfter.pageTop > downBefore.pageTop + 40, selector + ': upward swipe continues down the page: ' + JSON.stringify({downBefore,downAfter}));
            results.push({ selector, direction: 'down', before: downBefore, after: downAfter, attempts: downAttempts });
        }
        // Starting in the same empty detail area as the bug, return all the way
        // to the heading through native gestures, then repeat with scene fields.
        await position(selectors[2]);
        capture('detail-reachable');
        for (let i=0; i<12 && await evaluate(`${page}.scrollTop>1`); i++) await swipe('.content', true);
        assert(await evaluate(`${page}.scrollTop<=1`), 'Can return to the very top');
        capture('returned-to-top');
        await evaluate(`document.querySelector('[data-storyboard-scene]').click()`);
        for (let i=0; i<40 && await evaluate(`!!document.querySelector('.storyboard-inspector-empty')`); i++) await pause();
        assert(await evaluate(`!document.querySelector('.storyboard-inspector-empty')`), 'Selected scene inspector is populated');
        await position(selectors[2]);
        const before = await metrics(selectors[2]);
        const {after,attempts} = await boundarySwipe(selectors[2], true, before);
        assert(after.pageTop < before.pageTop - 40, 'Populated inspector also returns to the page');
        results.push({ selector: 'populated inspector', before, after, attempts });
        await navigate('/projects');
        await navigate(baseline.route);
        assert.deepEqual(hashes(), baseline.documents, 'Swipes and selection preserve all author documents');
        await evaluate(`${page}.scrollTop=0`);
        writeFileSync(resolve(output, 'results.json'), JSON.stringify({ checks: results, documentsUnchanged: Object.keys(baseline.documents).length }, null, 2));
        console.log('PASS: native scroll handoff works both ways in all three panels, empty and populated inspector, returns to heading; document hashes unchanged.');
    }
} finally { c.close(); }

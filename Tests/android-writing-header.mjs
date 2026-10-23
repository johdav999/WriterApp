// Inspect the rebuilt Android WebView. Navigation only; no manuscript edits.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
process.env.PROSA_ADB ??= 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
process.argv[2] = 'writing-header';
const { connect, adb, capture } = await import('./android-native-smoke.mjs');
const output = resolve('artifacts/android-writing-header');
mkdirSync(output, { recursive: true });
const c = await connect();
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const wait = async expression => {
    for (let i = 0; i < 80; i++) {
        if (await c.evaluate(expression)) return;
        await delay(200);
    }
    throw new Error('Timed out: ' + expression);
};
const results = [];
try {
    if (adb('shell', 'dumpsys', 'input_method').includes('mInputShown=true')) {
        adb('shell', 'input', 'keyevent', '4');
        await delay(300);
    }
    await wait(`!!document.querySelector('.writing-context-actions')`);
    const route = await c.evaluate('location.pathname + location.search');
    const projectTitle = await c.evaluate(`document.querySelector('.writing-project-link').textContent`);
    const check = async name => {
        const state = await c.evaluate(`(() => {
            const header = document.querySelector('.local-project-context');
            const r = header.getBoundingClientRect();
            return {
                width: innerWidth, height: r.height, left: r.left, right: r.right,
                overflow: header.scrollWidth > header.clientWidth + 1,
                links: [...header.querySelectorAll('a')].map(a => {
                    const b = a.getBoundingClientRect();
                    return {text: a.textContent.trim(), height: b.height, left: b.left, right: b.right, href: a.getAttribute('href')};
                }),
                titleTruncated: (() => { const a = header.querySelector('.writing-project-link span'); return a.scrollWidth > a.clientWidth; })(),
                breadcrumbLabelCenters: [...header.querySelectorAll('.writing-breadcrumb a span')].map(a => { const b = a.getBoundingClientRect(); return b.y + b.height/2; }),
                documentTitle: !!header.querySelector('.writing-document-title'),
                scene: document.querySelector('.editor-main-header h1')?.textContent
            };
        })()`);
        assert(!state.overflow, name + ': header overflows');
        assert(state.left >= -1 && state.right <= state.width + 1, name + ': header outside viewport');
        assert(state.height <= 120, name + ': header consumes too much vertical space');
        assert(Math.max(...state.breadcrumbLabelCenters) - Math.min(...state.breadcrumbLabelCenters) <= 1, name + ': breadcrumb labels misaligned');
        for (const link of state.links) {
            assert(link.height >= 43.5, name + ': undersized touch target');
            assert(link.right <= state.width + 1 && link.left >= -1, name + ': link clipped');
        }
        results.push({name, ...state});
        return state;
    };
    const native = await check('native');
    capture('writing-header-after');
    const shot = await c.send('Page.captureScreenshot', {format:'png'});
    writeFileSync(resolve(output, 'after.png'), Buffer.from(shot.data, 'base64'));
    for (const width of [320, 360, 412, 800, 1280]) {
        await c.send('Emulation.setDeviceMetricsOverride', {width, height:915, deviceScaleFactor:1, mobile:true});
        await delay(150);
        await c.evaluate(`document.querySelector('.writing-project-link span').textContent = ${JSON.stringify('A very long project title — ' + 'Manuscript '.repeat(20))}`);
        const state = await check('long-title-' + width);
        assert(state.titleTruncated, 'Long project title must truncate');
    }
    await c.evaluate(`document.querySelector('.writing-project-link span').textContent = ${JSON.stringify(projectTitle)}`);
    await c.send('Emulation.clearDeviceMetricsOverride');
    for (const selector of ['.writing-context-actions a[href*="/storyboard"]', '.writing-context-actions a[href$="/cover"]', '.writing-project-link', '.writing-projects-link']) {
        const href = await c.evaluate(`document.querySelector(${JSON.stringify(selector)})?.getAttribute('href')`);
        if (!href) continue;
        const point = await c.evaluate(`(() => { const r = document.querySelector(${JSON.stringify(selector)}).getBoundingClientRect(); return {x:r.x+r.width/2,y:r.y+r.height/2}; })()`);
        await c.send('Input.dispatchTouchEvent', {type:'touchStart', touchPoints:[point]});
        await c.send('Input.dispatchTouchEvent', {type:'touchEnd', touchPoints:[]});
        await wait(`location.pathname + location.search === ${JSON.stringify(href)}`);
        results.push({navigation: href, passed:true});
        await c.evaluate('history.back()');
        await wait(`location.pathname + location.search === ${JSON.stringify(route)} && !!document.querySelector('.writing-context-actions')`);
        await delay(200);
    }
    const hashFile = resolve(output, 'document-hashes-before.json');
    const before = JSON.parse(readFileSync(hashFile, 'utf8'));
    const after = Object.fromEntries(Object.keys(before).map(p => [p, adb('shell','run-as','com.prosa.writer.android','sha256sum',p).split(' ')[0]]));
    assert.deepEqual(after, before, 'Existing local documents must stay unchanged');
    results.push({existingDocumentsUnchanged:Object.keys(before).length});
    writeFileSync(resolve(output, 'results.json'), JSON.stringify(results, null, 2));
    console.log(`PASS: ${results.length} header/layout/navigation/data checks; native viewport ${native.width}px`);
} finally {
    await c.send('Emulation.clearDeviceMetricsOverride');
    c.close();
}

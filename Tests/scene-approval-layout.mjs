import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';

const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_P03_EVIDENCE || 'artifacts/ai-parity-p03/ui');
await mkdir(evidence, { recursive: true });
const styles = await readFile('WriterApp.UI.Shared/SceneCoachingReview.razor.css', 'utf8');
const frames = [];
for (const host of ['editor', 'inspector', 'desktop']) {
    for (const state of ['empty', 'partial', 'all', 'busy']) {
        frames.push({ host, state, html: await readFile(resolve(evidence, `${host}-${state}.html`), 'utf8') });
    }
}
const html = `<!doctype html><html><head><meta charset="utf-8"><title>Scene approval review</title><style>
body{font:16px 'Segoe UI',sans-serif;margin:0;padding:16px;background:#f8f7f5;color:#242321}main{max-width:960px;margin:auto}
article{padding:16px;margin:20px 0;border:1px solid #d6d3d1;border-radius:8px;background:white}button{padding:8px 12px}
${styles.replaceAll('::deep ', '')}</style></head><body><main><h1>Scene-card field approval</h1><p>Compiled shared Razor controls · synthetic planning values</p>
${frames.map(f => `<article data-host="${f.host}" data-state="${f.state}"><h2>${f.host} · ${f.state}</h2>${f.html}</article>`).join('')}</main></body></html>`;
await writeFile(resolve(evidence, 'compiled-review.html'), html);
const server = createServer((request, response) => { response.setHeader('Content-Type', 'text/html; charset=utf-8'); response.end(html); });
await new Promise(done => server.listen(0, '127.0.0.1', done));
let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true });
    const page = await browser.newPage();
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const checks = [];
    for (const width of [1280, 480]) {
        await page.setViewportSize({ width, height: 900 }); await page.goto(`http://127.0.0.1:${server.address().port}/`);
        for (const { host, state } of frames) {
            const review = page.locator(`article[data-host="${host}"][data-state="${state}"]`);
            assert.equal(await review.getByRole('checkbox').count(), 2);
            const all = review.getByRole('button', { name: 'Approve all changed fields' });
            const clear = review.getByRole('button', { name: 'Clear selection' });
            assert(await all.isVisible()); assert.equal(await all.isDisabled(), state === 'busy');
            assert.equal(await clear.isDisabled(), ['busy', 'empty'].includes(state));
            assert.equal(await review.locator('input[checked]').count(), state === 'partial' ? 1 : ['all', 'busy'].includes(state) ? 2 : 0);
            assert(await review.getByRole('alert').isVisible());
            if (state !== 'busy') {
                await all.focus(); assert(await all.evaluate(e => e === document.activeElement));
                const checkbox = review.getByRole('checkbox').first(); await checkbox.focus();
                assert(await checkbox.evaluate(e => e === document.activeElement));
            }
            const box = await all.boundingBox(); assert(box.x >= 0 && box.x + box.width <= width);
            checks.push({ width, host, state, controls: true, selection: true, inertOutput: true, noOverflow: true });
        }
        assert.equal(await page.locator('script').count(), 0); assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.screenshot({ path: resolve(evidence, `scene-review-${width}.png`), fullPage: true });
    }
    assert.deepEqual(errors, []);
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify({ checks, errors, scope: 'Compiled shared review HTML and production CSS in headless Edge. Actual Razor checkbox/all/clear dispatch is verified by .NET renderer tests. Host labels represent the shared control used by those routes; full authenticated app and native acceptance remain separate.' }, null, 2));
    console.log(JSON.stringify({ layouts: checks.length, errors }));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

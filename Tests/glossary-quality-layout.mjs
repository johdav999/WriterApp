import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';

const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_PARITY_P04_EVIDENCE || 'artifacts/ai-parity-p04/ui');
await mkdir(evidence, { recursive: true });
const styles = (await Promise.all([
    'WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/ui.css',
    'WriterApp.Device.Shared/wwwroot/app.css', 'WriterApp.UI.Shared/QualityFindings.razor.css'
].map(path => readFile(path, 'utf8')))).join('\n');
const states = ['fresh', 'offline', 'failed', 'empty', 'review', 'unavailable'];
const frames = await Promise.all(states.map(async state => ({ state, html: await readFile(resolve(evidence, `quality-${state}.html`), 'utf8') })));
const html = `<!doctype html><html><head><meta charset="utf-8"><title>Saved glossary quality checks</title><style>${styles.replaceAll('::deep ', '')}
body{font-family:'Segoe UI',sans-serif;margin:0;padding:16px}main{max-width:850px;margin:auto}article{padding:16px;margin:20px 0;border:1px solid #aaa;border-radius:8px}
</style></head><body><main><h1>Saved glossary quality checks</h1><p>Compiled device panel · synthetic glossary</p>${frames.map(f => `<article data-state="${f.state}"><h2>${f.state}</h2>${f.html}</article>`).join('')}</main></body></html>`;
await writeFile(resolve(evidence, 'compiled-quality.html'), html);
const server = createServer((request, response) => { response.setHeader('Content-Type', 'text/html; charset=utf-8'); response.end(html); });
await new Promise(done => server.listen(0, '127.0.0.1', done));
let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true });
    const page = await browser.newPage(); const errors = []; page.on('pageerror', error => errors.push(error.message));
    const checks = [];
    for (const width of [1280, 480]) {
        await page.setViewportSize({ width, height: 900 }); await page.goto(`http://127.0.0.1:${server.address().port}/`);
        for (const state of states) {
            const panel = page.locator(`article[data-state="${state}"]`);
            const button = panel.getByRole('button', { name: 'Check style & quality', exact: true });
            assert(await button.isVisible()); assert.equal(await button.isDisabled(), false);
            await button.focus(); assert(await button.evaluate(e => e === document.activeElement));
            const body = await panel.innerText();
            if (state === 'fresh' || state === 'review') assert(body.includes('Saved glossary loaded for this check.'));
            if (state === 'offline') assert(body.includes('Using cached glossary offline.'));
            if (state === 'failed') assert(body.includes('Glossary refresh failed. Using cached terms'));
            if (state === 'empty') assert(body.includes('Saved glossary verified empty at this check.'));
            if (state === 'unavailable') assert(body.includes('Glossary unavailable: sign in'));
            const terms = panel.locator('[data-quality-issue]').filter({ hasText: / · Glossary/ });
            assert.equal(await terms.count(), ['empty', 'unavailable'].includes(state) ? 0 : 2);
            const apply = panel.getByRole('button', { name: 'Approve & apply quality fix', exact: true });
            assert.equal(await apply.count(), state === 'review' ? 1 : 0);
            const box = await button.boundingBox(); assert(box.x >= 0 && box.x + box.width <= width);
            if (width === 480) await panel.screenshot({ path: resolve(evidence, `quality-${state}-480.png`) });
            checks.push({ width, state, freshness: true, keyboardFocus: true, findings: true, noOverflow: true });
        }
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.screenshot({ path: resolve(evidence, `glossary-quality-${width}.png`), fullPage: true });
    }
    assert.deepEqual(errors, []);
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify({ checks, errors,
        scope: 'Compiled LocalQualityPanel and shared findings HTML with production CSS in headless Edge. Actual Analyze/Review/account events are exercised by .NET renderer tests. This is component UI evidence; native and signed-in full-shell acceptance remain separate.' }, null, 2));
    console.log(JSON.stringify({ layouts: checks.length, errors }));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

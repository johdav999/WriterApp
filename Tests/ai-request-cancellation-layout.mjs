import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';

const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_P02_EVIDENCE || 'artifacts/ai-parity-p02/ui');
await mkdir(evidence, { recursive: true });
const styles = await readFile('WriterApp.UI.Shared/AiRequestProgress.razor.css', 'utf8');
const fixtures = await Promise.all(['writing', 'quality', 'consistency'].map(async name => ({ name, html: await readFile(resolve(evidence, `${name}-pending.html`), 'utf8') })));
const idle = await readFile(resolve(evidence, 'idle.html'), 'utf8');
assert(!idle.includes('Cancel AI request'));
const html = `<!doctype html><html><head><meta charset="utf-8"><title>Compiled cancellation controls</title><style>
body { font: 16px 'Segoe UI', sans-serif; color:#252321; background:#f8f7f5; margin:0; padding:24px; } main { max-width:960px; margin:auto; }
section { padding:20px; margin:18px 0; border:1px solid #d6d3d1; background:white; border-radius:8px; } button { padding:8px 12px; } ${styles}
</style></head><body><main><h1>AI request cancellation</h1><p>Compiled shared controls · synthetic writing · request pending</p>${fixtures.map(f => `<section data-flow="${f.name}"><h2>${f.name}</h2><p>Authored prose remains unchanged while the request is pending.</p>${f.html}</section>`).join('')}<section data-flow="idle"><h2>After cancellation</h2><p role="status">AI request cancelled. Your writing is unchanged. A request already sent may still use your quota.</p>${idle}</section></main></body></html>`;
await writeFile(resolve(evidence, 'compiled-controls.html'), html);
const server = createServer((request, response) => { response.setHeader('Content-Type', 'text/html; charset=utf-8'); response.end(html); });
await new Promise(done => server.listen(0, '127.0.0.1', done));
let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true });
    const page = await browser.newPage();
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const checks = [];
    for (const width of [1280, 480]) {
        await page.setViewportSize({ width, height: 900 });
        await page.goto(`http://127.0.0.1:${server.address().port}/`);
        for (const { name } of fixtures) {
            const section = page.locator(`[data-flow="${name}"]`);
            const button = section.getByRole('button', { name: 'Cancel AI request' });
            assert(await button.isVisible()); assert(await button.isEnabled());
            await button.focus(); assert(await button.evaluate(element => element === document.activeElement));
            const box = await button.boundingBox(); assert(box.height >= 40 && box.x >= 0 && box.x + box.width <= width);
            assert(await section.getByRole('status').isVisible());
            checks.push({ width, flow: name, visible: true, enabled: true, keyboardFocus: true, noOverflow: true });
        }
        assert.equal(await page.locator('[data-flow="idle"]').getByRole('button').count(), 0);
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth));
        await page.screenshot({ path: resolve(evidence, `cancel-controls-${width}.png`), fullPage: true });
    }
    assert.deepEqual(errors, []);
    const result = { checks, errors, scope: 'Actual compiled Razor component HTML and production component CSS in headless Edge. Actual button callback dispatch is verified in the .NET framework renderer. This static fixture does not establish full authenticated browser, live provider or native acceptance.' };
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify({ layouts: checks.length, errors }));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

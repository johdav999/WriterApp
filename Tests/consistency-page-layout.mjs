import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';

const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_PARITY_P05_EVIDENCE || 'artifacts/ai-parity-p05/ui');
await mkdir(evidence, { recursive: true });
const css = await readFile('WriterApp.UI.Shared/ConsistencyPrimaryPassageView.razor.css', 'utf8');
const states = ['located', 'ambiguous', 'missing'];
const frames = await Promise.all(states.map(async state => ({ state, html: await readFile(resolve(evidence, `${state}.html`), 'utf8') })));
const html = `<!doctype html><html><head><meta charset="utf-8"><title>Checked consistency pages</title><style>
body{font:16px 'Segoe UI',sans-serif;padding:16px;margin:0;background:#f8f7f5}main{max-width:850px;margin:auto}article{padding:16px;margin:20px 0;border:1px solid #aaa;background:white;border-radius:8px}${css}
</style></head><body><main><h1>Checked consistency pages</h1><p>Compiled shared passage presentation · synthetic evidence</p>${frames.map(f => `<article data-state="${f.state}"><h2>${f.state}</h2>${f.html}</article>`).join('')}</main></body></html>`;
await writeFile(resolve(evidence, 'compiled-passages.html'), html);
const server = createServer((req, res) => { res.setHeader('Content-Type', 'text/html; charset=utf-8'); res.end(html); });
await new Promise(done => server.listen(0, '127.0.0.1', done));
let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true });
    const page = await browser.newPage(), errors = [], checks = [];
    page.on('pageerror', error => errors.push(error.message));
    for (const width of [1280, 480]) {
        await page.setViewportSize({ width, height: 900 }); await page.goto(`http://127.0.0.1:${server.address().port}`);
        for (const state of states) {
            const article = page.locator(`[data-state="${state}"]`);
            await assert.doesNotReject(() => article.getByText('Passage in your writing', { exact: false }).waitFor({ state: 'visible' }));
            assert.equal(await article.locator('script').count(), 0);
            assert.equal(await article.getByRole('status').count(), state === 'located' ? 0 : 1);
            assert.match(await article.innerText(), state === 'located' ? /Page 2/ : /Run the consistency check again|run the check again/);
            checks.push({ state, width, passed: true });
        }
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.screenshot({ path: resolve(evidence, `passages-${width}.png`), fullPage: true });
    }
    assert.deepEqual(errors, []);
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify({ checks, errors, scope: 'Compiled production passage component and its CSS; synthetic quotes. Handler persistence is tested separately. No authenticated full shell or native acceptance.' }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, errors }));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

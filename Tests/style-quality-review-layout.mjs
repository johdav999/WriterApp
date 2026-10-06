import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_PARITY_P06_UI_EVIDENCE || 'artifacts/ai-parity-p06/ui');
await mkdir(evidence, { recursive: true });
const states = ['idle', 'all', 'partial', 'none', 'empty', 'busy', 'error'];
const css = (await Promise.all(['WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/ui.css',
    'WriterApp.UI.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.UI.Shared.styles.css'].map(p => readFile(p, 'utf8')))).join('\n');
const frames = await Promise.all(states.map(async state => ({ state, html: await readFile(resolve(evidence, state + '.html'), 'utf8') })));
const html = `<!doctype html><html><head><meta charset="utf-8"><title>Explained style review controls</title><style>${css}
body{font:16px 'Segoe UI',sans-serif;padding:16px;margin:0;background:#f8f7f5}main{max-width:850px;margin:auto}article{padding:16px;margin:20px 0;border:1px solid #aaa;background:white;border-radius:8px}
</style></head><body><main><h1>Explained style review</h1><p>Compiled production controls · synthetic states</p>${frames.map(f => `<article data-state="${f.state}"><h2>${f.state}</h2>${f.html}</article>`).join('')}</main></body></html>`;
await writeFile(resolve(evidence, 'compiled-style-review.html'), html);
const server = createServer((req, res) => { res.setHeader('Content-Type', 'text/html; charset=utf-8'); res.end(html); });
await new Promise(done => server.listen(0, '127.0.0.1', done));
let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true });
    const page = await browser.newPage(), errors = [], checks = [];
    page.on('pageerror', e => errors.push(e.message));
    for (const width of [1280, 480]) {
        await page.setViewportSize({ width, height: 900 }); await page.goto(`http://127.0.0.1:${server.address().port}`);
        for (const state of states) {
            const frame = page.locator(`[data-state="${state}"]`);
            assert.equal(await frame.locator('script').count(), 0);
            assert.equal(await frame.getByRole('combobox', { name: 'Style review scope' }).count(), 1);
            assert.equal(await frame.getByRole('button', { name: 'Suggest explained revision' }).isDisabled(), state === 'busy' || state === 'error');
            if (state !== 'idle') {
                const checked = state === 'all' || state === 'busy' ? 2 : state === 'partial' || state === 'error' ? 1 : 0;
                assert.equal(await frame.locator('input[checked]').count(), checked);
                assert.equal(await frame.getByRole('button', { name: 'Apply selected style changes' }).isDisabled(), state === 'busy' || checked === 0);
                assert.equal(await frame.getByRole('button', { name: 'Dismiss style review' }).isDisabled(), state === 'busy');
                if (state !== 'empty') {
                    await frame.locator('summary').focus(); await page.keyboard.press('Enter');
                    assert.equal(await frame.locator('details').getAttribute('open'), '');
                    const preview = await frame.locator('details .ai-preview-text').nth(1).innerText();
                    assert.match(preview, state === 'all' || state === 'busy' ? /beside the chime/ : /beside the clock/);
                    assert.match(preview, checked > 0 ? /She was tired/ : /She was very tired/);
                    assert.match(await frame.innerText(), /Possible correction/); assert.match(await frame.innerText(), /Optional style preference/);
                    assert.match(await frame.innerText(), /Effect on voice or emphasis/);
                }
            }
            for (const box of await frame.locator('button,select').all()) assert((await box.boundingBox()).height >= 40);
            checks.push({ state, width, passed: true });
        }
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.locator('[data-state="partial"]').screenshot({ path: resolve(evidence, `partial-${width}.png`) });
        await page.screenshot({ path: resolve(evidence, `review-${width}.png`), fullPage: true });
    }
    assert.deepEqual(errors, []);
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify({ checks, errors, scope: 'Compiled shared controls and production CSS; keyboard preview, widths and synthetic states. Production callbacks/save tested separately. No authenticated shell/native/provider acceptance.' }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, errors }));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

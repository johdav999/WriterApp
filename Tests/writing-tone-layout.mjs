import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_P13_EVIDENCE || 'artifacts/ai-parity-p13/ui');
await mkdir(evidence, { recursive: true });
const states = ['desktop-executive', 'rewrite-idle', 'change_tone-idle', 'rewrite-busy', 'client-tone-catalog'];
const tones = ['Neutral', 'Formal', 'Casual', 'Friendly', 'Technical', 'Executive'];
const css = (await Promise.all(['WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/ui.css',
    'WriterApp.UI.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.UI.Shared.styles.css',
    'WriterApp.Device.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.Device.Shared.styles.css'].map(p => readFile(p, 'utf8')))).join('\n');
const frames = await Promise.all(states.map(async state => ({ state, html: await readFile(resolve(evidence, state + '.html'), 'utf8') })));
const server = createServer((req, res) => {
    const frame = frames.find(f => f.state === new URL(req.url, 'http://localhost').pathname.slice(1));
    if (!frame) { res.writeHead(404); res.end(); return; }
    res.setHeader('Content-Type', 'text/html; charset=utf-8');
    res.end(`<!doctype html><html><head><meta charset="utf-8"><style>${css}body{font:16px 'Segoe UI',sans-serif;padding:16px;margin:0}main{max-width:850px;margin:auto;min-width:0}button,select,input{max-width:100%;box-sizing:border-box}</style></head><body><main>${frame.html}</main></body></html>`);
});
await new Promise(done => server.listen(0, '127.0.0.1', done)); let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true }); const page = await browser.newPage(), errors = [], checks = [];
    page.on('pageerror', e => errors.push(e.message));
    for (const width of [1280, 480]) for (const state of states) {
        await page.setViewportSize({ width, height: 900 }); await page.goto(`http://127.0.0.1:${server.address().port}/${state}`);
        if (state === 'client-tone-catalog') {
            const buttons = page.getByRole('button');
            assert.deepEqual(await buttons.evaluateAll(es => es.map(e => e.dataset.tone)), tones);
            assert.deepEqual(await buttons.allTextContents(), ['Rewrite (Neutral)', 'Rewrite (Formal)', 'Rewrite (Casual)', 'Change tone (Friendly)', 'Change tone (Technical)', 'Rewrite (Executive)']);
            const executive = page.getByRole('button', { name: 'Rewrite (Executive)', exact: true });
            await executive.focus(); assert(await executive.evaluate(e => e === document.activeElement));
        } else {
            const select = page.getByRole('combobox', { name: 'Tone', exact: true });
            assert.deepEqual(await select.locator('option').evaluateAll(es => es.map(e => e.value)), tones);
            assert.equal(await select.inputValue(), 'Executive');
            if (state === 'rewrite-busy') assert(await select.isDisabled());
            else {
                await select.focus(); assert(await select.evaluate(e => e === document.activeElement));
                await page.keyboard.press('Home'); assert.equal(await select.inputValue(), 'Neutral');
                await page.keyboard.press('End'); assert.equal(await select.inputValue(), 'Executive');
            }
            if (state === 'desktop-executive') assert(await page.getByRole('button', { name: 'Preview rewrite', exact: true }).isEnabled());
        }
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)); checks.push({ width, state, passed: true });
        if (width === 480 && state === 'desktop-executive' || width === 1280 && state === 'client-tone-catalog')
            await page.screenshot({ path: resolve(evidence, `${state}-${width}.png`), fullPage: true });
    }
    assert.deepEqual(errors, []); await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify({ checks, errors,
        scope: 'Compiled desktop panel and shared WritingOptions snapshots with production CSS; client catalog extracted from actual presets, not the authenticated client shell. All six values, legacy labels, Executive selection, keyboard focus/selection, busy disabling and narrow layout. Static snapshots do not execute host callbacks; .NET checks exercise the actual handlers. No live/native acceptance.' }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, errors }));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

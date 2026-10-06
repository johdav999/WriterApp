import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_PARITY_P09_UI_EVIDENCE || 'artifacts/ai-parity-p09/ui');
await mkdir(evidence, { recursive: true });
const states = ['planning', 'aggregate', 'undone', 'legacy', 'copy', 'busy'];
const css = (await Promise.all(['WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/ui.css', 'WriterApp.UI.Shared/wwwroot/icons.css',
    'WriterApp.UI.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.UI.Shared.styles.css'].map(p => readFile(p, 'utf8')))).join('\n');
const frames = await Promise.all(states.map(async state => ({ state, html: await readFile(resolve(evidence, state + '.html'), 'utf8') })));
const html = `<!doctype html><html><head><meta charset="utf-8"><title>Scoped AI recovery</title><style>${css}
body{font:16px 'Segoe UI',sans-serif;padding:16px;margin:0;background:#f8f7f5}main{max-width:850px;margin:auto}article.fixture{margin:20px 0}
</style></head><body><main><h1>AI recovery</h1><p>Compiled production controls · synthetic persisted states</p>${frames.map(f => `<article class="fixture" data-state="${f.state}">${f.html}</article>`).join('')}</main></body></html>`;
await writeFile(resolve(evidence, 'compiled-recovery.html'), html);
const font = await readFile('WriterApp.UI.Shared/wwwroot/material-symbols-rounded.ttf');
const server = createServer((req, res) => {
    if (req.url.endsWith('material-symbols-rounded.ttf')) { res.setHeader('Content-Type', 'font/ttf'); res.end(font); return; }
    res.setHeader('Content-Type', 'text/html; charset=utf-8'); res.end(html);
});
await new Promise(done => server.listen(0, '127.0.0.1', done));
let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true });
    const page = await browser.newPage(), errors = [], checks = [];
    page.on('pageerror', e => errors.push(e.message));
    for (const width of [1280, 480]) {
        await page.setViewportSize({ width, height: 900 }); await page.goto(`http://127.0.0.1:${server.address().port}`); await page.evaluate(() => document.fonts.ready);
        for (const state of states) {
            const frame = page.locator(`[data-state="${state}"]`), control = frame.locator('.history-item-actions button');
            assert.equal(await control.isDisabled(), ['legacy', 'copy', 'busy'].includes(state));
            assert.match(await control.innerText(), state === 'undone' ? /Redo change/ : /Undo change/);
            assert.equal(await frame.locator('script').count(), 0);
            const summary = frame.locator('summary'); await summary.focus(); await page.keyboard.press('Enter');
            assert(await frame.locator('details').evaluate(d => d.open));
            assert.match(await frame.innerText(), /<script>inert<\/script>/);
            if (state === 'legacy') assert.match(await frame.innerText(), /Comparison only/);
            if (state === 'copy') assert.match(await frame.innerText(), /does not delete copies/);
            assert.equal(await frame.getByRole('button', { name: 'Recover a copy' }).count(), 0);
            checks.push({ state, width, passed: true });
        }
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.locator('[data-state="aggregate"]').screenshot({ path: resolve(evidence, `aggregate-${width}.png`) });
    }
    assert.deepEqual(errors, []);
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify({ checks, errors,
        scope: 'Compiled Razor history controls and production CSS; layout, inert comparison, keyboard details and disabled capability states. Actual C# recovery callbacks tested separately; no authenticated/native/live-provider acceptance.' }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, errors }));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

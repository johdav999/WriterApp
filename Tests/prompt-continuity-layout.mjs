import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_P12_EVIDENCE || 'artifacts/ai-parity-p12/ui');
await mkdir(evidence, { recursive: true });
const states = ['cloud-help', 'device-help', 'offline', 'pending-failed', 'conflict', 'confirmed', 'imported'];
const css = (await Promise.all(['WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/ui.css',
    'WriterApp.UI.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.UI.Shared.styles.css',
    'WriterApp.Device.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.Device.Shared.styles.css'].map(p => readFile(p, 'utf8')))).join('\n');
const frames = await Promise.all(states.map(async state => ({ state, html: await readFile(resolve(evidence, 'library-' + state + '.html'), 'utf8') })));
const server = createServer((req, res) => {
    const state = new URL(req.url, 'http://localhost').pathname.slice(1), frame = frames.find(f => f.state === state);
    if (!frame) { res.writeHead(404); res.end(); return; }
    res.setHeader('Content-Type', 'text/html; charset=utf-8');
    res.end(`<!doctype html><html><head><meta charset="utf-8"><style>${css}body{font:16px 'Segoe UI',sans-serif;padding:16px;margin:0}main{max-width:850px;margin:auto;min-width:0}button,select,input,textarea{max-width:100%;box-sizing:border-box}</style></head><body><main>${frame.html}</main></body></html>`);
});
await new Promise(done => server.listen(0, '127.0.0.1', done)); let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true }); const page = await browser.newPage(), errors = [], checks = [];
    page.on('pageerror', e => errors.push(e.message));
    for (const width of [1280, 480]) for (const state of states) {
        await page.setViewportSize({ width, height: 900 }); await page.goto(`http://127.0.0.1:${server.address().port}/${state}`);
        const help = page.getByRole('region', { name: 'Preset storage and transfer' }), summary = help.locator('summary');
        assert.match(await help.innerText(), state === 'cloud-help' ? /Cloud library/ : /Local device library/);
        await summary.focus(); assert(await summary.evaluate(e => e === document.activeElement));
        await page.keyboard.press('Enter'); assert(await help.locator('details').evaluate(e => e.open));
        const text = await help.innerText(); assert.match(text, /Libraries do not synchronize automatically/);
        assert.match(text, /no confirmed receipt/); assert.match(text, /Later local edits are not added/);
        assert.match(text, /same account and backend/); assert.match(text, /does not undo a cloud change/);
        assert.match(text, /Refresh alone does not change existing local copies/);
        const transfers = page.getByRole('button', { name: 'Refresh cloud presets', exact: true });
        if (state !== 'cloud-help' && state !== 'device-help') {
            const cloudDetails = page.locator('details').filter({ has: page.locator('summary').filter({ hasText: /^Cloud copies and transfers$/ }) });
            await cloudDetails.locator(':scope > summary').click();
            assert(await cloudDetails.evaluate(e => e.open));
            if (state === 'offline') { assert(await transfers.isDisabled()); assert.match(await page.locator('main').innerText(), /Sign in to transfer/); }
            else assert(!await transfers.isDisabled());
            if (state === 'pending-failed') { assert.match(await page.locator('[role=alert]').innerText(), /Response lost/); assert.match(await cloudDetails.innerText(), /Pending · no confirmed cloud receipt/); assert.equal(await page.getByRole('button', { name: 'Send / retry retained transfer' }).count(), 1); }
            if (state === 'conflict') { const comparison = page.getByRole('region', { name: 'Resolve preset conflict' }); assert.match(await comparison.innerText(), /Cloud divergence/); assert.match(await comparison.innerText(), /Library divergence/); assert.equal(await comparison.getByRole('button').count(), 3); }
            if (state === 'confirmed' || state === 'imported') { assert.match(await cloudDetails.innerText(), /Confirmed cloud transfer/); assert.equal(await page.getByRole('button', { name: 'Send / retry retained transfer' }).count(), 0); }
            if (state === 'imported') assert.match(await page.getByRole('region', { name: 'Local presets', exact: true }).innerText(), /Imported cloud copy · edits stay local/);
        }
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        assert(await help.evaluate(e => e.scrollWidth <= e.clientWidth)); checks.push({ width, state, passed: true });
        if (width === 480 && state === 'conflict' || width === 1280 && state === 'cloud-help') await page.screenshot({ path: resolve(evidence, `${state}-${width}.png`), fullPage: true });
    }
    assert.deepEqual(errors, []); await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify({ checks, errors,
        scope: 'Compiled shared host-specific help and actual LocalPromptPanel snapshots with production CSS. Keyboard/help, storage origin, failed/pending vs confirmed receipts, conflict comparison and narrow layout. Static snapshots do not execute host callbacks; existing .NET transfer/panel suites cover these separately. No live/native acceptance.' }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, errors }));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

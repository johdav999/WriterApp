import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_P11_UI_EVIDENCE || 'artifacts/ai-parity-p11/ui');
await mkdir(evidence, { recursive: true });
const states = ['disabled-idle', 'enabled-idle', 'disabled-busy', 'enabled-busy'];
const css = (await Promise.all(['WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/ui.css',
    'WriterApp.UI.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.UI.Shared.styles.css'].map(p => readFile(p, 'utf8')))).join('\n');
const frames = await Promise.all(states.map(async state => ({ state, html: await readFile(resolve(evidence, state + '.html'), 'utf8') })));
const html = `<!doctype html><html><head><meta charset="utf-8"><style>${css}body{font:16px 'Segoe UI',sans-serif;padding:16px;margin:0}main{max-width:850px;margin:auto}</style></head><body><main><h1>Targeted quality retry</h1>${frames.map(f => `<article data-state="${f.state}">${f.html}</article>`).join('')}</main></body></html>`;
await writeFile(resolve(evidence, 'compiled-retry.html'), html);
const server = createServer((req, res) => { res.setHeader('Content-Type', 'text/html; charset=utf-8'); res.end(html); });
await new Promise(done => server.listen(0, '127.0.0.1', done));
let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true }); const page = await browser.newPage(), errors = [], checks = [];
    page.on('pageerror', e => errors.push(e.message));
    for (const width of [1280, 480]) {
        await page.setViewportSize({ width, height: 900 }); await page.goto(`http://127.0.0.1:${server.address().port}`);
        for (const state of states) {
            const frame = page.locator(`[data-state="${state}"]`), input = frame.getByRole('checkbox', { name: 'Allow one automatic strict retry for targeted fixes' });
            assert.equal(await input.isChecked(), state.startsWith('enabled')); assert.equal(await input.isDisabled(), state.endsWith('busy'));
            assert.match(await frame.innerText(), /Off by default/); assert.match(await frame.innerText(), /extra request may consume quota/);
            assert(await input.evaluate(e => !!document.getElementById(e.getAttribute('aria-describedby'))));
            if (state.endsWith('idle')) { await input.focus(); assert(await input.evaluate(e => e === document.activeElement)); await page.keyboard.press('Space'); assert.equal(await input.isChecked(), !state.startsWith('enabled')); }
            assert(await frame.evaluate(e => e.scrollWidth <= e.clientWidth)); checks.push({ width, state, passed: true });
        }
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)); await page.screenshot({ path: resolve(evidence, `retry-${width}.png`), fullPage: true });
    }
    assert.deepEqual(errors, []); await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify({ checks, errors,
        scope: 'Actual compiled shared Razor choice and production CSS; keyboard, off-by-default/opt-in, busy guard and quota explanation. Actual callbacks/request counts tested by .NET; full authenticated/native/live-provider gates remain separate.' }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, errors }));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

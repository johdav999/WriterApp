import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_PARITY_P07_UI_EVIDENCE || 'artifacts/ai-parity-p07/ui');
await mkdir(evidence, { recursive: true });
const states = ['client-headlines', 'desktop-headlines', 'client-summary', 'desktop-summary', 'client-busy', 'desktop-busy',
    'desktop-novel.deepen_character', 'desktop-blog.generate_headlines', 'desktop-other.summarize_clearly', 'desktop-novel.continue_scene'];
const css = (await Promise.all(['WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/ui.css',
    'WriterApp.UI.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.UI.Shared.styles.css',
    'WriterApp.Device.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.Device.Shared.styles.css'].map(p => readFile(p, 'utf8')))).join('\n');
const frames = await Promise.all(states.map(async state => ({ state, html: await readFile(resolve(evidence, state + '.html'), 'utf8') })));
const html = `<!doctype html><html><head><meta charset="utf-8"><title>Recommended writing review</title><style>${css}
body{font:16px 'Segoe UI',sans-serif;padding:16px;margin:0;background:#f8f7f5}main{max-width:850px;margin:auto}article{padding:16px;margin:20px 0;border:1px solid #aaa;background:white;border-radius:8px}
</style></head><body><main><h1>Recommended writing</h1><p>Compiled production controls · synthetic states</p>${frames.map(f => `<article data-state="${f.state}"><h2>${f.state}</h2>${f.html}</article>`).join('')}</main></body></html>`;
await writeFile(resolve(evidence, 'compiled-recommendations.html'), html);
const server = createServer((req, res) => { res.setHeader('Content-Type', 'text/html; charset=utf-8'); res.end(html); });
await new Promise(done => server.listen(0, '127.0.0.1', done));
let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true });
    const page = await browser.newPage(), errors = [], checks = [];
    page.on('pageerror', e => errors.push(e.message));
    for (const width of [1280, 480]) {
        await page.setViewportSize({ width, height: 900 }); await page.goto(`http://127.0.0.1:${server.address().port}`);
        // HtmlRenderer emits select value attributes; the Blazor runtime normally applies these as DOM properties.
        await page.locator('select[value]').evaluateAll(selects => selects.forEach(select => select.value = select.getAttribute('value')));
        for (const state of states) {
            const frame = page.locator(`[data-state="${state}"]`), review = frame.locator('.recommended-text-review');
            assert.equal(await frame.locator('script').count(), 0);
            if (await review.count()) {
                const summary = state.includes('summary') || state.includes('summarize'), busy = state.endsWith('busy');
                assert.equal(await review.locator('input[type=radio]').count(), summary ? 1 : 5);
                assert.equal(await review.locator('input:checked').count(), 1);
                assert.equal(await review.getByRole('button', { name: 'Copy selected text' }).isDisabled(), busy);
                assert.equal(await review.getByRole('button', { name: 'Dismiss result' }).isDisabled(), busy);
                assert.equal(await frame.getByRole('button', { name: /Approve.*apply/ }).count(), 0);
                assert.match(await review.innerText(), /manuscript stays unchanged/);
                if (!busy && !summary) {
                    await review.locator('input[type=radio]').first().focus(); await page.keyboard.press('ArrowRight');
                    assert(await review.locator('input[type=radio]').nth(1).isChecked());
                }
                for (const control of await review.locator('button').all()) assert((await control.boundingBox()).height >= 40);
            } else {
                assert.equal(await frame.getByRole('button', { name: 'Approve & apply revision' }).count(), 1);
                assert.match(await frame.innerText(), state.includes('continue') ? /Append after original writing/ : /Deepen Character/);
            }
            if (state.includes('.')) {
                assert.equal(await frame.getByRole('combobox', { name: 'Recommended writing tool' }).count(), 1);
                assert.equal(await frame.getByRole('button', { name: 'Run recommended tool' }).isDisabled(), false);
            }
            checks.push({ state, width, passed: true });
        }
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        for (const state of ['client-headlines', 'desktop-novel.deepen_character', 'desktop-other.summarize_clearly'])
            await page.locator(`[data-state="${state}"]`).screenshot({ path: resolve(evidence, `${state}-${width}.png`) });
    }
    assert.deepEqual(errors, []);
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify({ checks, errors,
        scope: 'Actual rendered Razor controls and compiled production CSS; native HTML radio keyboard selection, disabled states and widths. C# callbacks and saves tested separately; no authenticated shell/native/provider acceptance.' }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, errors }));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

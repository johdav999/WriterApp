import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';

const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_P10_EVIDENCE || 'artifacts/ai-parity-p10/ui');
await mkdir(evidence, { recursive: true });
const states = ['synced', 'pending', 'failed', 'unmapped', 'conflict', 'inactive', 'busy', 'restore-pending'];
const css = (await Promise.all(['WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/ui.css',
    'WriterApp.UI.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.UI.Shared.styles.css'].map(p => readFile(p, 'utf8')))).join('\n');
const frames = await Promise.all(states.map(async state => ({ state, html: await readFile(resolve(evidence, 'review-' + state + '.html'), 'utf8') })));
const html = `<!doctype html><html><head><meta charset="utf-8"><style>${css}
body{font:16px 'Segoe UI',sans-serif;padding:16px;margin:0}main{max-width:850px;margin:auto}
</style></head><body><main><h1>Quality decisions</h1>${frames.map(f => `<article data-state="${f.state}">${f.html}</article>`).join('')}</main></body></html>`;
await writeFile(resolve(evidence, 'compiled-decisions.html'), html);
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
            const frame = page.locator(`[data-state="${state}"]`), summary = frame.locator('summary');
            await summary.focus(); await page.keyboard.press('Enter');
            assert(await frame.locator('details').evaluate(d => d.open));
            assert.equal(await frame.locator('script').count(), 0); assert.match(await frame.innerText(), /<script>inert<\/script>/);
            const restore = frame.getByRole('button', { name: 'Restore finding', exact: true });
            if (state === 'restore-pending') { assert.equal(await restore.count(), 0); assert.match(await frame.innerText(), /Restored locally/); }
            else { assert.equal(await restore.isDisabled(), state === 'busy'); if (state !== 'busy') { await restore.focus(); assert(await restore.evaluate(e => e === document.activeElement)); } }
            if (state === 'inactive') assert.match(await frame.innerText(), /does not hide current findings/);
            if (['failed', 'pending', 'unmapped', 'conflict'].includes(state)) assert.match(await frame.innerText(), /delivery pending/);
            assert(await frame.evaluate(e => e.scrollWidth <= e.clientWidth));
            checks.push({ state, width, passed: true });
        }
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await page.locator('[data-state="pending"]').screenshot({ path: resolve(evidence, `pending-${width}.png`) });
        await page.locator('[data-state="inactive"]').screenshot({ path: resolve(evidence, `inactive-${width}.png`) });
    }
    assert.deepEqual(errors, []);
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify({ checks, errors,
        scope: 'Actual compiled shared review controls and scoped production CSS. Keyboard, status, inert evidence and overflow. Actual device callbacks and authenticated server routes tested separately. Full signed-in/native/live-provider gates remain separate.' }, null, 2));
    console.log(JSON.stringify({ passed: checks.length, errors }));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

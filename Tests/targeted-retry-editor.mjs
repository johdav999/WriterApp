import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_P11_EDITOR_EVIDENCE || 'artifacts/ai-parity-p11/editor'); await mkdir(evidence, { recursive: true });
const allowed = new Set(['WriterApp.Client/tests/device-editor.html', 'WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js',
    'WriterApp.Client/wwwroot/js/tiptap-editor-patch.js', 'WriterApp.Client/wwwroot/js/web-ai-history-outbox.js',
    'WriterApp.Device.Shared/wwwroot/editor/device-editor.js', 'WriterApp.Device.Shared/wwwroot/editor/device-editor.css',
    'WriterApp.Device.Shared/wwwroot/app.css', 'WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/editor.css']);
const server = createServer(async (req, res) => { const path = new URL(req.url, 'http://localhost').pathname.slice(1); if (!allowed.has(path)) { res.writeHead(404); res.end(); return; }
    const data = await readFile(path); res.setHeader('Content-Type', path.endsWith('.js') ? 'text/javascript' : path.endsWith('.css') ? 'text/css' : 'text/html'); res.end(data); });
await new Promise(done => server.listen(0, '127.0.0.1', done)); let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true }); const page = await browser.newPage(), errors = []; page.on('pageerror', e => errors.push(e.message));
    await page.goto(`http://127.0.0.1:${server.address().port}/WriterApp.Client/tests/device-editor.html`); await page.waitForFunction(() => /tests (passed|failed)/.test(document.title));
    const baseline = await page.locator('#results li').allTextContents();
    const checks = await page.evaluate(async () => {
        const api = window.tiptapEditor, device = await import('/WriterApp.Device.Shared/wwwroot/editor/device-editor.js');
        const receiver = { invokeMethodAsync() { return Promise.resolve(); } }, checks = [];
        for (const [name, html, expected, valid] of [
            ['plain', '<p>The door was opened by Anna.</p>', 'The door was opened by Anna.', true],
            ['rich marks', '<p>The <strong>door</strong> was opened by Anna.</p>', 'The door was opened by Anna.', true],
            ['anchor drift', '<p>The gate was opened by Anna.</p>', 'The door was opened by Anna.', false],
            ['embedded image', '<p>The door was opened</p><img src="/keep.png"><p>by Anna.</p>', 'The door was opened\n\nby Anna.', false],
            ['cross block', '<p>The door was opened</p><p>by Anna.</p>', 'The door was opened\n\nby Anna.', false],
            ['hard break', '<p>The door was<br>opened by Anna.</p>', 'The door was\nopened by Anna.', false]
        ]) {
            const element = document.createElement('div'); element.id = 'retry-' + checks.length; document.body.append(element);
            const editor = api.create(element.id, html, receiver), before = editor.getHTML();
            let last = 1; editor.state.doc.descendants((node, pos) => { if (node.isText) last = pos + node.nodeSize; });
            const actual = api.validateTargetedQualityRange(editor, 1, last, expected);
            if (actual !== valid || editor.getHTML() !== before) throw new Error(name + ': wrong preflight or mutation');
            checks.push({ host: 'client', name, passed: true }); editor.destroy(); element.remove();
            const deviceHost = document.createElement('div'); document.body.append(deviceHost);
            const local = device.create(deviceHost, html, 'Html', receiver); const localBefore = local.snapshot().html; let accepted = true;
            try { device.validateTargetedQualityRange(html, 'Html', 0, expected.length, expected); } catch { accepted = false; }
            if (accepted !== valid || local.snapshot().html !== localBefore) throw new Error(name + ': device parity preflight or mutation');
            checks.push({ host: 'device', name, passed: true }); local.destroy(); deviceHost.remove();
        }
        return checks;
    });
    const report = { baselinePassed: baseline.filter(x => x.startsWith('PASS:')).length, baselineFailures: baseline.filter(x => x.startsWith('FAIL:')), checks, errors,
        scope: 'Both shipped editor bundles and current client patch. Read-only preflight before generation/retry; no transaction dispatch, authored mutation or live provider.' };
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify(report, null, 2)); console.log(JSON.stringify(report));
    assert.equal(report.baselineFailures.length, 0); assert.deepEqual(errors, []); assert(checks.every(c => c.passed));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_PARITY_P07_EDITOR_EVIDENCE || 'artifacts/ai-parity-p07/editor');
await mkdir(evidence, { recursive: true });
const allowed = new Set(['WriterApp.Client/tests/device-editor.html', 'WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js',
    'WriterApp.Client/wwwroot/js/tiptap-editor-patch.js', 'WriterApp.Client/wwwroot/js/web-ai-history-outbox.js',
    'WriterApp.Client/wwwroot/js/tiptap-commands.js', 'WriterApp.Device.Shared/wwwroot/editor/device-editor.js',
    'WriterApp.Device.Shared/wwwroot/editor/device-editor.css', 'WriterApp.Device.Shared/wwwroot/app.css',
    'WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/editor.css']);
const server = createServer(async (req, res) => {
    const path = new URL(req.url, 'http://localhost').pathname.slice(1);
    if (!allowed.has(path)) { res.writeHead(404); res.end(); return; }
    const data = await readFile(path); res.setHeader('Content-Type', path.endsWith('.js') ? 'text/javascript' : path.endsWith('.css') ? 'text/css' : 'text/html'); res.end(data);
});
await new Promise(done => server.listen(0, '127.0.0.1', done));
let browser;
try {
    browser = await chromium.launch({ channel: 'msedge', headless: true });
    const page = await browser.newPage(), errors = []; page.on('pageerror', e => errors.push(e.message));
    await page.goto(`http://127.0.0.1:${server.address().port}/WriterApp.Client/tests/device-editor.html`);
    await page.waitForFunction(() => /tests (passed|failed)/.test(document.title));
    const baseline = await page.locator('#results li').allTextContents();
    const checks = await page.evaluate(async () => {
        const api = window.tiptapEditor, device = await import('/WriterApp.Device.Shared/wwwroot/editor/device-editor.js');
        const commands = await import('/WriterApp.Client/wwwroot/js/tiptap-commands.js'), checks = [];
        const receiver = { invokeMethodAsync() { return Promise.resolve(); } };
        const same = (a, b) => { if (a !== b) throw new Error(`Expected ${JSON.stringify(b)}, got ${JSON.stringify(a)}`); };
        function check(name, fn) { try { fn(); checks.push({ name, passed: true }); } catch (e) { checks.push({ name, passed: false, error: e.message }); } }
        function editor(html) { const element = document.createElement('div'); element.id = `recommended-${checks.length}-${Math.random()}`; document.body.append(element); return api.create(element.id, html, receiver); }
        for (const html of ['<p>The <strong>clock</strong> stood beside the clock.</p><p>Later prose.</p>',
            '<h2>Title</h2><p><em>Åsa</em> read the <a href="https://example.com">letter</a>.</p><p></p>',
            '<blockquote><p>日本語 🧭 letter.</p></blockquote><p></p>', '<p></p>']) {
            check(`Both hosts preserve mapped page structure ${checks.length}`, () => {
                const source = api.captureTranslation(html, 'Html'), native = device.captureTranslation(html, 'Html');
                same(JSON.stringify(source), JSON.stringify(native));
                const changed = source.runs.map(r => ({ ...r, text: r.text.replace('stood beside the clock', 'waited beside the chime').replace('Later prose', 'Later writing').replace('letter', 'message') }));
                const result = api.previewTranslation(html, 'Html', changed);
                same(result, device.previewTranslation(html, 'Html', changed));
                same((result.match(/<strong>/g) || []).length, (html.match(/<strong>/g) || []).length);
                same((result.match(/<p><\/p>/g) || []).length, (html.match(/<p><\/p>/g) || []).length);
            });
        }
        check('Opening-only revision preserves later paragraphs and rich marks', () => {
            const html = '<p>The <strong>clock</strong> stood beside the clock.</p><p>Later prose.</p>', source = api.captureTranslation(html, 'Html');
            const changed = source.runs.map(r => ({ ...r, text: r.id.startsWith('0.') ? r.text.replace('stood beside the clock', 'waited beside the chime') : r.text }));
            same(api.previewTranslation(html, 'Html', changed), '<p>The <strong>clock</strong> waited beside the chime.</p><p>Later prose.</p>');
        });
        for (const html of ['<p>Original <strong>clock</strong>.</p>', '<p></p>']) check(`Actual web append command and Undo ${checks.length}`, () => {
            const e = editor(html), before = e.getHTML(), text = 'A new paragraph with <img src=x> 日本語 🧭.';
            commands.appendParagraph(e, text);
            same(e.getHTML(), device.previewContinuation(before, 'Html', text));
            same(e.view.dom.querySelectorAll('img').length, 0); same(e.state.doc.textContent.includes('<img'), true);
            e.commands.undo(); same(e.getHTML(), before); e.destroy();
        });
        check('Malformed mapped result refuses without live editor mutation', () => {
            const html = '<p>Original <strong>clock</strong>.</p>', e = editor(html), before = e.getHTML(); let refused = false;
            try { api.previewTranslation(html, 'Html', [{ id: 'wrong', text: 'Replacement' }]); } catch { refused = true; }
            same(refused, true); same(e.getHTML(), before); e.destroy();
        });
        return checks;
    });
    const report = { baselinePassed: baseline.filter(x => x.startsWith('PASS:')).length, baselineFailures: baseline.filter(x => x.startsWith('FAIL:')), checks, errors,
        scope: 'Both shipped editor bundles, real section-map preview and real web append command with Undo. Synthetic prose; host checked persistence/cancellation tested separately, no authenticated full shell/native/provider acceptance.' };
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify(report, null, 2));
    console.log(JSON.stringify(report)); assert.equal(report.baselineFailures.length, 0); assert.deepEqual(errors, []); assert(checks.every(c => c.passed));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

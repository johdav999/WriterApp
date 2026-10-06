import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.WRITERAPP_PARITY_P06_EVIDENCE || 'artifacts/ai-parity-p06/editor');
await mkdir(evidence, { recursive: true });
const allowed = new Set(['WriterApp.Client/tests/device-editor.html', 'WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js',
    'WriterApp.Client/wwwroot/js/tiptap-editor-patch.js', 'WriterApp.Client/wwwroot/js/web-ai-history-outbox.js',
    'WriterApp.Device.Shared/wwwroot/editor/device-editor.js', 'WriterApp.Device.Shared/wwwroot/editor/device-editor.css',
    'WriterApp.Device.Shared/wwwroot/app.css', 'WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/editor.css']);
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
        const api = window.tiptapEditor, receiver = { invokeMethodAsync() { return Promise.resolve(); } }, checks = [];
        const device = await import('/WriterApp.Device.Shared/wwwroot/editor/device-editor.js');
        function check(name, fn) { try { fn(); checks.push({ name, passed: true }); } catch (e) { checks.push({ name, passed: false, error: e.message }); } }
        const same = (a, b) => { if (a !== b) throw new Error(`Expected ${JSON.stringify(b)}, got ${JSON.stringify(a)}`); };
        function editor(html) { const element = document.createElement('div'); element.id = `style-${checks.length}-${Math.random()}`; document.body.append(element); return api.create(element.id, html, receiver); }
        function reject(html, edits, mutate) { const e = editor(html), source = api.captureStyleQuality(e, true); mutate?.(e, source); const before = e.getHTML(); let refused = false; try { api.applyStyleQuality(e, source, edits); } catch { refused = true; } same(refused, true); same(e.getHTML(), before); e.destroy(); }
        const html = '<p>The <strong>clock</strong> stood beside the clock.</p><p>She was very tired.</p>';
        const edits = [{ original: 'The clock stood beside the clock.', replacement: 'The clock stood beside the chime.' }, { original: 'She was very tired.', replacement: 'She was tired.' }];
        for (const subset of [[0], [1], [0, 1], [1, 0]]) check(`Atomic partial approval ${subset}`, () => {
            const e = editor(html), source = api.captureStyleQuality(e, true), before = e.getHTML();
            same(api.previewStyleQuality(e, source, edits), true); same(e.getHTML(), before);
            const after = api.applyStyleQuality(e, source, subset.map(i => edits[i]));
            same(after.includes('<strong>clock</strong>'), true); same(after.includes('chime'), subset.includes(0)); same(after.includes('very'), !subset.includes(1));
            e.commands.undo(); same(e.getHTML(), before); e.destroy();
        });
        check('Desktop reference all edits preserve the same rich source', () => {
            same(device.previewStyleQualityRevision(html, 0, 'The clock stood beside the clock.\nShe was very tired.', edits), '<p>The <strong>clock</strong> stood beside the chime.</p><p>She was tired.</p>');
        });
        check('Current selection has a page-local offset and changes only selected text', () => {
            const e = editor(html); e.commands.setTextSelection({ from: 36, to: 55 });
            const source = api.captureStyleQuality(e, false); same(source.from, 35); same(source.text, 'She was very tired.');
            api.applyStyleQuality(e, source, [edits[1]]); same(e.getHTML(), '<p>The <strong>clock</strong> stood beside the clock.</p><p>She was tired.</p>');
            e.commands.setTextSelection(2); let refused = false; try { api.captureStyleQuality(e, false); } catch { refused = true; } same(refused, true); e.destroy();
        });
        check('Mixed changed span rejects all edits before the first mutation', () => reject('<p>She was very tired.</p><p><strong>Bold</strong> plain.</p>', [edits[1], { original: 'Bold plain.', replacement: 'Different words.' }]));
        check('Duplicate quotes never pick the first occurrence', () => reject('<p>Echo. Echo.</p>', [{ original: 'Echo.', replacement: 'Sound.' }]));
        check('Overlapping edits reject the whole transaction', () => reject(html, [edits[0], { original: 'stood beside', replacement: 'waited near' }]));
        check('Edited HTML invalidates the captured review', () => reject(html, edits, e => e.commands.insertContent('New ')));
        check('Forged plain source cannot retarget a revision', () => reject(html, edits, (_, s) => s.plain = 'Other writing'));
        check('Zero approved edits never create a transaction', () => reject(html, []));
        check('Embedded image in changed wording rejects atomically', () => reject('<p>She was very tired.</p><p>Before <img src="data:image/png;base64,AA=="> after.</p>', [edits[1], { original: 'Before  after.', replacement: 'Elsewhere.' }]));
        check('Unchanged italic/link marks remain intact', () => {
            const e = editor('<p><em>Åsa</em> read the <a href="https://example.com">letter</a> very slowly.</p>'), source = api.captureStyleQuality(e, true), before = e.getHTML();
            api.applyStyleQuality(e, source, [{ original: source.text, replacement: 'Åsa read the letter slowly.' }]);
            same(e.getHTML().includes('<em>Åsa</em>'), true); same(e.getHTML().includes('href="https://example.com"'), true); e.commands.undo(); same(e.getHTML(), before); e.destroy();
        });
        check('Combining-character anchor is refused without mutation', () => reject('<p>Cafe\u0301 clock.</p>', [{ original: 'Cafe', replacement: 'Tea' }]));
        check('Mid-word anchors are refused without mutation', () => reject('<p>Clockwork remains.</p>', [{ original: 'Clock', replacement: 'Gear' }]));
        check('Paragraph-changing output is refused without mutation', () => reject(html, [{ original: edits[1].original, replacement: 'She rested.\nShe slept.' }]));
        check('Provider HTML remains text and cannot insert an element', () => {
            const e = editor('<p>A word.</p>'), source = api.captureStyleQuality(e, true); api.applyStyleQuality(e, source, [{ original: source.text, replacement: 'A <img src=x onerror=alert(1)> word.' }]);
            same(e.view.dom.querySelectorAll('img').length, 0); same(e.state.doc.textContent.includes('<img'), true); e.destroy();
        });
        check('A failed final edit leaves no partial result to Undo', () => reject(html, [edits[0], { original: 'Missing passage', replacement: 'Other prose' }]));
        check('Too many edits are refused', () => reject(html, Array(25).fill(edits[0])));
        check('Empty paragraphs keep their exact structure', () => {
            const e = editor('<p>First page.</p><p></p><p>She was very tired.</p><p></p>'), source = api.captureStyleQuality(e, true);
            same(source.plain, 'First page.\n\nShe was very tired.');
            api.applyStyleQuality(e, source, [edits[1]]); same(e.getHTML(), '<p>First page.</p><p></p><p>She was tired.</p><p></p>'); e.destroy();
        });
        check('Selection after empty paragraphs uses the saved page offset', () => {
            const e = editor('<p>First page.</p><p></p><p></p><p>She was very tired.</p>');
            e.commands.setTextSelection({ from: 18, to: 37 }); const source = api.captureStyleQuality(e, false);
            same(source.from, 13); same(source.text, 'She was very tired.');
            api.applyStyleQuality(e, source, [edits[1]]); same(e.getHTML(), '<p>First page.</p><p></p><p></p><p>She was tired.</p>'); e.destroy();
        });
        check('Inline breaks outside a change stay intact with saved offsets', () => {
            const e = editor('<p>First line.<br>She was very tired.</p>'), source = api.captureStyleQuality(e, true);
            same(source.plain, 'First line.\nShe was very tired.');
            api.applyStyleQuality(e, source, [edits[1]]); same(e.getHTML(), '<p>First line.<br>She was tired.</p>'); e.destroy();
        });
        return checks;
    });
    const report = { baselinePassed: baseline.filter(x => x.startsWith('PASS:')).length, baselineFailures: baseline.filter(x => x.startsWith('FAIL:')), checks, errors,
        scope: 'Both shipped editor bundles, new client atomic style transaction and desktop preview reference. Synthetic prose; no authenticated full shell, native or live provider acceptance.' };
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify(report, null, 2));
    console.log(JSON.stringify(report)); assert.equal(report.baselineFailures.length, 0); assert.deepEqual(errors, []); assert(checks.every(c => c.passed));
} finally { await browser?.close(); await new Promise(done => server.close(done)); }

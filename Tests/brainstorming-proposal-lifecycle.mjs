// Exercise both shipped factories without provider requests or user cache writes.
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve('artifacts/brainstorming-follow-up');
await mkdir(evidence, { recursive: true });
const files = {
    '/proposal.js': 'WriterApp.UI.Shared/wwwroot/brainstorming-proposal.js',
    '/_content/WriterApp.Device.Shared/editor/device-editor.js': 'WriterApp.Device.Shared/wwwroot/editor/device-editor.js',
    '/web.js': 'WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js'
};
const server = createServer(async (request, response) => {
    if (request.url === '/') {
        response.setHeader('Content-Type', 'text/html');
        response.end('<!doctype html><meta charset="utf-8"><div id="proposal"></div>');
    } else if (files[request.url]) {
        response.setHeader('Content-Type', 'text/javascript');
        response.end(await readFile(files[request.url]));
    } else { response.writeHead(404); response.end(); }
});
await new Promise(done => server.listen(0, '127.0.0.1', done));
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const page = await browser.newPage();
const results = [];
try {
    for (const surface of ['Web', 'Device']) {
        await page.goto(`http://127.0.0.1:${server.address().port}/`);
        if (surface === 'Web') await page.addScriptTag({ url: '/web.js' });
        const result = await page.evaluate(async surface => {
            const module = await import('/proposal.js');
            const host = document.querySelector('#proposal');
            const receiver = { invokeMethodAsync: () => Promise.resolve() };
            const old = await module.create(host, '<p>Anna hears the patrol approaching.</p>', surface, receiver, 'first', 'a'.repeat(64));
            const latest = await module.create(host, '<p>The sergeant stops beside Anna and asks the team to leave.</p>', surface, receiver, 'second', 'b'.repeat(64));
            // An older asynchronous creation completes/cleans up after the follow-up mounts.
            old.destroy();
            const text = host.querySelector('[role=textbox]')?.textContent ?? '';
            const editors = host.querySelectorAll('[role=textbox]').length;
            const toolbars = host.querySelectorAll('.proposal-formatting').length;
            latest.setEditable(true);
            latest.destroy();
            return { surface, text, editors, toolbars, remainingNodes: host.childNodes.length };
        }, surface);
        results.push(result);
        assert.match(result.text, /The sergeant stops beside Anna/, `${surface}: old cleanup removed new prose`);
        assert.equal(result.editors, 1);
        assert.equal(result.toolbars, 1);
        assert.equal(result.remainingNodes, 0);
    }
} finally {
    await writeFile(resolve(evidence, 'editor-lifecycle-result.json'), JSON.stringify(results, null, 2));
    await browser.close();
    await new Promise(done => server.close(done));
}
console.log(JSON.stringify(results, null, 2));

// Run npm run build and tests/serve-device-editor.mjs first.
import { createRequire } from 'node:module';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';

const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const output = resolve(process.argv[2] || '../artifacts/consistency-highlight');
const browser = await chromium.launch({ headless: true, channel: 'msedge' });
try {
    await mkdir(output, { recursive: true });
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(`${process.env.EDITOR_TEST_HOST || 'http://127.0.0.1:5179'}/WriterApp.Client/tests/device-editor.html`);
    await page.waitForFunction(() => /tests (passed|failed)/.test(document.title));
    const checks = await page.locator('#results li').allTextContents();
    const failures = checks.filter(text => text.startsWith('FAIL'));
    await page.getByRole('button', { name: 'Jump to consistency example', exact: true }).click();
    // Returning keyboard focus to the coach must not hide the passage.
    await page.getByRole('button', { name: 'Jump to consistency example', exact: true }).focus();
    const highlight = page.locator('#editor .wa-consistency-passage');
    assert.equal(await highlight.textContent(), 'narrow layouts');
    assert.equal(await highlight.evaluate(element => getComputedStyle(element).backgroundColor), 'rgb(255, 226, 122)');
    await page.locator('.prosa-editor').screenshot({ path: resolve(output, 'passage-highlight.png') });
    await writeFile(resolve(output, 'browser-results.json'), JSON.stringify({
        surface: 'Headless Edge using shipped editor assets; native desktop not exercised',
        checks, failures, errors, coachFocusHighlight: true
    }, null, 2));
    assert.deepEqual(errors, [], 'Browser errors');
    assert.deepEqual(failures, [], 'Editor regressions');
    console.log(`${checks.length} editor checks passed; passage color survives coach focus.`);
} finally {
    await browser.close();
}

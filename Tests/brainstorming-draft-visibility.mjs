// Rebuilt application with disposable LocalDev data. Never sends a provider request.
import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const base = process.env.BRAINSTORMING_WEB_URL ?? 'http://127.0.0.1:5399';
assert.equal(new URL(base).hostname, '127.0.0.1');
const seed = JSON.parse(await readFile(process.env.BRAINSTORMING_WEB_SEED, 'utf8'));
const evidence = resolve(process.env.BRAINSTORMING_EVIDENCE);
await mkdir(evidence, { recursive: true });
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } });
const errors = [], checks = [];
let sends = 0;
page.on('pageerror', error => errors.push(error.message));
await page.route('**/conversation/stream', async route => { sends++; await route.abort(); });
const button = name => page.getByRole('button', { name, exact: true });
const rail = page.getByRole('navigation', { name: 'Brainstorm panes' });
const box = page.getByRole('textbox', { name: 'Proposed prose editor', exact: true });
const chat = page.locator('.brainstorm-chat');
const visiblePane = pane => page.waitForFunction(pane => document.querySelector('.editor-workspace')?.dataset.brainstormPane === pane, pane);
try {
    await page.goto(`${base}/app/documents/${seed.documentId}/sections/${seed.sectionId}`);
    await button('Brainstorm').click({ timeout: 45000 });
    await page.getByText('Context ready', { exact: true }).waitFor();
    await rail.getByRole('button', { name: 'Manuscript', exact: true }).click();
    await visiblePane('manuscript');
    await button('History').click();
    await button('Find synced conversations').click();
    await page.getByRole('combobox', { name: /^Saved conversation/ }).selectOption(seed.restoredSessionId);
    await button('Open saved conversation').click();
    await box.waitFor();
    await visiblePane('draft');
    assert.match(await box.innerText(), /She kept her distance/);
    assert.equal(await page.locator('.editor-main').isVisible(), false);
    checks.push('A selected saved prose candidate opens its draft from the manuscript pane.');
    for (const width of [1600, 900, 390]) {
        await page.setViewportSize({ width, height: 1000 });
        await rail.getByRole('button', { name: 'Manuscript', exact: true }).click();
        await visiblePane('manuscript');
        if (width < 1100) await rail.getByRole('button', { name: 'Conversation', exact: true }).click();
        const preview = chat.locator('.brainstorm-generated-prose details').first();
        await preview.locator('summary').click();
        assert.match(await preview.locator('pre').innerText(), /She kept her distance/);
        assert(await preview.locator('pre').isVisible());
        await page.screenshot({ path: resolve(evidence, `conversation-preview-${width}.png`) });
        await preview.locator('summary').click();
        await chat.getByRole('button', { name: 'View working draft', exact: true }).first().click();
        await visiblePane('draft');
        assert(await box.isVisible());
        assert.match(await box.innerText(), /She kept her distance/);
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
        await page.screenshot({ path: resolve(evidence, `draft-visible-${width}.png`) });
        checks.push(`${width}px: generated prose is readable in the conversation; its action opens the retained draft.`);
    }
    const source = await page.request.get(`${base}/api/brainstorming/sources/${seed.documentId}`);
    assert(source.ok());
    const manuscript = await source.json();
    assert.equal(manuscript.pages.length, seed.sourcePages.length);
    for (const original of seed.sourcePages) assert.equal(manuscript.pages.find(p => p.id === original.Id)?.content, original.Content);
    assert.equal(sends, 0); assert.deepEqual(errors, []);
    await writeFile(resolve(evidence, 'result.json'), JSON.stringify({ checks, sends, errors, browser: browser.version(), scope: 'Rebuilt web app, disposable SQLite data, synthetic saved prose. No live model calls or manuscript changes.' }, null, 2));
    console.log(JSON.stringify({ checks, sends, errors }, null, 2));
} catch (error) {
    await page.screenshot({ path: resolve(evidence, 'failure.png'), fullPage: true });
    await writeFile(resolve(evidence, 'failure.txt'), await page.locator('body').innerText());
    throw error;
} finally { await browser.close(); }

// Rebuilt production editor with disposable LocalDev data; no AI dispatch.
import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.env.BRAINSTORMING_EVIDENCE ?? 'artifacts/brainstorming-redesign/browser');
const base = process.env.BRAINSTORMING_WEB_URL ?? 'http://127.0.0.1:5407';
assert.equal(new URL(base).hostname, '127.0.0.1');
const seed = JSON.parse(await readFile(process.env.BRAINSTORMING_WEB_SEED ?? 'artifacts/brainstorming-redesign/interactive-seed.json', 'utf8'));
await mkdir(evidence, { recursive: true });
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } });
const errors = [], failedStyles = [], checks = [];
let sends = 0;
page.on('pageerror', e => errors.push(e.message));
page.on('response', r => { if (r.url().includes('.css') && r.status() >= 400) failedStyles.push(r.url()); });
page.on('request', r => { if (r.url().includes('/conversation/stream')) sends++; });
try {
    for (let attempt = 0; attempt < 30; attempt++) {
        try { if ((await page.request.get(`${base}/api/auth/me`)).ok()) break; } catch { /* Host startup. */ }
        if (attempt === 29) throw new Error('Disposable acceptance host did not become ready.');
        await new Promise(resolve => setTimeout(resolve, 500));
    }
    await page.goto(`${base}/app/documents/${seed.documentId}/sections/${seed.sectionId}`);
    await page.getByRole('button', { name: 'Brainstorm', exact: true }).click({ timeout: 45000 });
    const panel = page.getByRole('complementary', { name: 'Brainstorm workspace', exact: true });
    await panel.getByRole('button', { name: 'New conversation', exact: true }).click();
    const composer = panel.getByRole('textbox', { name: 'Your message', exact: true });
    await composer.waitFor();
    await page.screenshot({ path: resolve(evidence, 'chat-wide.png') });
    await panel.screenshot({ path: resolve(evidence, 'chat-panel.png') });
    assert.equal(await panel.getByRole('button', { name: 'Start microphone', exact: true }).isVisible(), false);
    assert.equal(await panel.getByRole('button', { name: 'Open saved conversation', exact: true }).isVisible(), false);
    await panel.getByRole('button', { name: /Explore a character/ }).click();
    assert.match(await composer.inputValue(), /character's motives/);
    assert.equal(sends, 0);
    const draft = 'My retained idea\nÅsa and her brother';
    await composer.fill(draft);
    await panel.getByRole('button', { name: 'Context', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.brainstorm-tabs button[aria-pressed="true"]')?.textContent === 'Context');
    await panel.getByLabel('Destination', { exact: true }).selectOption('new');
    await panel.getByLabel('Saved chapter number', { exact: true }).fill('2');
    await page.screenshot({ path: resolve(evidence, 'context-wide.png') });
    await panel.screenshot({ path: resolve(evidence, 'context-panel.png') });
    await panel.getByRole('button', { name: 'History', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.brainstorm-tabs button[aria-pressed="true"]')?.textContent === 'History');
    await page.screenshot({ path: resolve(evidence, 'history-wide.png') });
    await panel.screenshot({ path: resolve(evidence, 'history-panel.png') });
    await panel.getByRole('button', { name: 'Context', exact: true }).click();
    assert.equal(await panel.getByLabel('Destination', { exact: true }).inputValue(), 'new');
    assert.equal(await panel.getByLabel('Saved chapter number', { exact: true }).inputValue(), '2');
    await panel.getByRole('button', { name: 'Prepare / refresh context', exact: true }).click();
    await panel.getByText('Context budget reviewed.', { exact: false }).waitFor();
    await panel.getByRole('button', { name: 'Chat', exact: true }).click();
    assert.equal(await composer.inputValue(), draft);
    assert.equal(await panel.getByRole('button', { name: 'Send message', exact: true }).isEnabled(), true);
    checks.push('Prompt starter fills without sending; Chat/Context/History preserve composer and destination; explicit context preparation enables Send.');
    const layout = await panel.evaluate(el => {
        const box = el.getBoundingClientRect(), input = el.querySelector('.brainstorm-composer textarea').getBoundingClientRect();
        return { panel: { top: box.top, bottom: box.bottom, width: box.width }, composer: { top: input.top, bottom: input.bottom, width: input.width }, viewport: innerHeight, overflow: el.scrollWidth > el.clientWidth + 1 };
    });
    assert.equal(layout.overflow, false, JSON.stringify(layout));
    assert(layout.composer.bottom <= layout.viewport && layout.composer.width > layout.panel.width * .7, JSON.stringify(layout));
    await panel.locator('.brainstorm-audio > summary').click();
    assert(await panel.getByRole('button', { name: 'Start microphone', exact: true }).isVisible());
    await panel.locator('.brainstorm-audio > summary').click();
    checks.push('Full-width composer stays in viewport; voice controls are available on demand.');
    await page.setViewportSize({ width: 900, height: 800 });
    await page.getByRole('button', { name: 'Conversation', exact: true }).click();
    await composer.waitFor({ state: 'visible' });
    await page.screenshot({ path: resolve(evidence, 'chat-narrow.png') });
    assert(await composer.evaluate(el => el.getBoundingClientRect().bottom <= innerHeight));
    assert(await panel.evaluate(el => el.scrollWidth <= el.clientWidth + 1));
    assert.equal(await composer.inputValue(), draft);
    // Inspect a phone-sized conversation pane too; the surrounding editor owns mobile navigation.
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({ path: resolve(evidence, 'chat-phone.png') });
    assert(await composer.evaluate(el => el.getBoundingClientRect().bottom <= innerHeight));
    assert(await panel.getByRole('button', { name: 'Send message', exact: true }).evaluate(el => {
        const box = el.getBoundingClientRect(), parent = el.closest('.brainstorm-composer').getBoundingClientRect();
        return box.bottom <= innerHeight && box.bottom <= parent.bottom;
    }));
    assert(await panel.evaluate(el => el.scrollWidth <= el.clientWidth + 1));
    await page.setViewportSize({ width: 1600, height: 1000 });
    await panel.getByRole('button', { name: 'History', exact: true }).click();
    await panel.getByRole('button', { name: 'Open saved conversation', exact: true }).click();
    assert.equal(await composer.inputValue(), draft);
    assert(await panel.getByRole('button', { name: 'Send message', exact: true }).isDisabled());
    checks.push('Narrow/phone conversation has no horizontal overflow; reopening retains draft and revokes context readiness.');
    assert.equal(sends, 0);
    assert.deepEqual(errors, []);
    assert.deepEqual(failedStyles, []);
    await writeFile(resolve(evidence, 'result.json'), JSON.stringify({ checks, layout, sends, errors, failedStyles, browser: browser.version(), scope: 'Actual rebuilt web editor with disposable LocalDev database; no live provider/audio/native acceptance.' }, null, 2));
    console.log(JSON.stringify({ checks, layout, sends }, null, 2));
} catch (error) {
    await page.screenshot({ path: resolve(evidence, 'failure.png'), fullPage: true });
    await writeFile(resolve(evidence, 'failure.txt'), await page.locator('body').innerText());
    throw error;
} finally { await browser.close(); }

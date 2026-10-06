import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';

const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const evidence = resolve(process.env.WRITERAPP_P08_EVIDENCE || 'artifacts/uat/synopsis-coaching/final');
await mkdir(evidence, { recursive: true });
const css = (await Promise.all([
    'WriterApp.UI.Shared/wwwroot/design-tokens.css',
    'WriterApp.UI.Shared/wwwroot/editor.css',
    'WriterApp.UI.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.UI.Shared.styles.css',
    'WriterApp.Device.Shared/obj/Debug/net10.0/scopedcss/bundle/WriterApp.Device.Shared.styles.css'
].map(path => readFile(path, 'utf8')))).join('\n');
const frames = Object.fromEntries(await Promise.all(['suggest', 'evaluate', 'questions', 'suggest-long', 'evaluate-long', 'questions-long'].map(async mode => [mode, await readFile(resolve(evidence, `${mode}-frame.html`), 'utf8')])));
const browser = await chromium.launch({ channel: 'msedge', headless: true, ignoreDefaultArgs: ['--hide-scrollbars'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const checks = [];
const errors = []; page.on('pageerror', error => errors.push(error.message));
const document = (frame, styles = css, width = 640, height = 720) => `<!doctype html><html lang="en"><meta charset="utf-8"><title>Synopsis coaching</title><style>${styles}
    *{box-sizing:border-box}body{margin:0;font:16px var(--font-sans);background:var(--color-bg)}
    .fixture.prosa-editor{display:block;width:min(${width}px,100vw);height:${height}px;min-height:0;margin:auto;padding:0;--story-panel-height:140px;border:1px solid var(--color-border);border-radius:16px;overflow:hidden}
    </style><main class="fixture prosa-editor">${frame}</main></html>`;
try {
    // Compare the original compiled panel and stylesheet with the corrected one.
    try {
        const before = await readFile(resolve(evidence, '../baseline/suggest-review.html'), 'utf8');
        const beforeCss = (await Promise.all(['device.css', 'shared.css'].map(name => readFile(resolve(evidence, '../baseline', name), 'utf8')))).join('\n');
        const baselineGlobalCss = (await Promise.all(['WriterApp.UI.Shared/wwwroot/design-tokens.css', 'WriterApp.UI.Shared/wwwroot/editor.css'].map(path => readFile(path, 'utf8')))).join('\n');
        await page.setContent(document(frames.suggest, baselineGlobalCss + beforeCss));
        await page.locator('.desktop-context-ai').evaluate((element, markup) => element.innerHTML = markup, before);
        await page.screenshot({ path: resolve(evidence, 'before.png') });
    } catch (error) { if (error.code !== 'ENOENT') throw error; }

    for (const mode of ['suggest', 'evaluate', 'questions', 'suggest-long', 'evaluate-long', 'questions-long']) {
        for (const [width, height] of [[640,720], [360,720], [280,420]]) {
            await page.setContent(document(frames[mode], css, width, height));
            const lower = page.locator('.rp-trailing-body');
            assert.equal(await lower.evaluate(e => getComputedStyle(e).overflowY), 'scroll');
            const metrics = await lower.evaluate(e => ({ height: e.clientHeight, content: e.scrollHeight, width: e.clientWidth, scrollWidth: e.scrollWidth }));
            assert(metrics.height > 100 && metrics.content > metrics.height, `${mode}: contained vertical overflow`);
            assert(metrics.scrollWidth <= metrics.width, `${mode}: no horizontal overflow`);
            assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));

            const controls = page.locator('.synopsis-workspace select, .synopsis-workspace textarea');
            for (let i = 0; i < await controls.count(); i++) {
                const control = controls.nth(i);
                const box = await control.boundingBox();
                assert(box.width >= 180 && box.height >= 42, `${mode}: usable control size`);
                await control.focus();
                assert(await control.evaluate(e => e === document.activeElement));
                assert.notEqual(await control.evaluate(e => getComputedStyle(e).outlineStyle), 'none');
            }
            assert.equal(await page.getByRole('heading', { name: 'Review AI result', exact: true }).count(), 0);
            const comparison = page.locator('.synopsis-feedback .ai-preview-columns');
            if (mode.startsWith('suggest')) {
                const columns = await comparison.evaluate(e => getComputedStyle(e).gridTemplateColumns.split(' ').length);
                assert.equal(columns, width >= 640 ? 2 : 1, 'Comparison responds to pane width');
                for (const pane of await page.locator('.synopsis-feedback .ai-preview-text').all())
                    assert.equal(await pane.evaluate(e => getComputedStyle(e).maxHeight), 'none', 'Read comparison through pane scrolling');
            }
            await lower.evaluate(e => e.scrollTop = 0);
            await lower.focus(); await page.keyboard.press('PageDown');
            await page.waitForFunction(() => document.querySelector('.rp-trailing-body').scrollTop > 0);
            await lower.evaluate(e => e.scrollTop = 0);
            const rect = await lower.boundingBox();
            await page.mouse.move(rect.x + rect.width / 2, rect.y + rect.height - 10);
            await page.mouse.wheel(0, 300);
            await page.waitForFunction(() => document.querySelector('.rp-trailing-body').scrollTop > 0);
            await lower.evaluate(e => e.scrollTop = e.scrollHeight);
            const dismiss = page.getByRole('button', { name: 'Dismiss', exact: true });
            await dismiss.focus();
            const button = await dismiss.boundingBox(); const viewport = await lower.boundingBox();
            assert(button.y >= viewport.y && button.y + button.height <= viewport.y + viewport.height, `${mode}: Dismiss reachable`);
            if (mode.startsWith('suggest')) {
                const apply = page.getByRole('button', { name: 'Apply field suggestion', exact: true });
                await apply.focus(); assert(await apply.isEnabled());
                const box = await apply.boundingBox(); assert(box.y >= viewport.y && box.y + box.height <= viewport.y + viewport.height);
            } else assert.equal(await page.getByRole('button', { name: 'Apply field suggestion' }).count(), 0);
            await page.getByText('Synopsis analyzed', { exact: true }).click();
            await lower.evaluate(e => e.scrollTop = e.scrollHeight);
            assert(await page.getByText('Author coaching notes', { exact: true }).count() <= 1);
            checks.push({ mode, width, height, mouseScroll: true, keyboardScroll: true, actionsReachable: true, noHorizontalOverflow: true });
            if (mode === 'suggest') {
                // Capture a fresh, unfocused form after exercising scroll/focus behavior.
                await page.setContent(document(frames[mode], css, width, height));
                assert.equal(await lower.evaluate(e => e.scrollTop), 0);
                await page.screenshot({ path: resolve(evidence, `after-${width}.png`) });
                await lower.evaluate(e => e.scrollTo({ top:e.scrollHeight, behavior:'instant' }));
                await page.screenshot({ path: resolve(evidence, `review-${width}.png`) });
            }
        }
    }
    await page.setViewportSize({ width:390, height:844 });
    await page.setContent(document(frames['suggest-long'], css, 390, 724));
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    assert(await page.locator('.rp-trailing-body').evaluate(e => { e.scrollTop = e.scrollHeight; return e.scrollTop > 0; }));
    checks.push({ mobileViewport:390, verticalScrolling:true, noHorizontalOverflow:true });
    assert.deepEqual(errors, []);
    await writeFile(resolve(evidence, 'browser-results.json'), JSON.stringify({ scope:'Compiled production Razor components, scoped styles and editor CSS in headless Edge. Synthetic data and mock AI responses; native running app and live provider not verified.', checks, errors }, null, 2));
    console.log(`Passed ${checks.length} synopsis coaching layout scenarios`);
} finally { await browser.close(); }

// Regression for the device host wrapper, which older component-only fixtures omitted.
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const evidence = resolve('artifacts/synopsis-android/browser');
await mkdir(evidence, { recursive: true });
const css = (await Promise.all([
    'WriterApp.UI.Shared/wwwroot/design-tokens.css',
    'WriterApp.UI.Shared/wwwroot/editor.css',
    'WriterApp.UI.Shared/RightPanelShell.razor.css',
    'WriterApp.UI.Shared/StoryPanelResizeHandle.razor.css',
    'WriterApp.Device.Shared/Pages/DocumentWorkspace.razor.css',
    'WriterApp.Device.Shared/Components/LocalAiPanel.razor.css'
].map(p => readFile(p, 'utf8')))).join('\n').replaceAll('::deep ', '');
const host = await readFile('WriterApp.Device.Shared/Pages/DocumentWorkspace.razor', 'utf8');
assert.match(host, /ContextAiMode == "synopsis" \? "is-synopsis-coaching"/);
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const page = await browser.newPage();
const checks = [];
try {
    for (const [width, height] of [[390,844], [412,867], [844,390], [900,900], [1440,900]]) {
        await page.setViewportSize({ width, height });
        await page.setContent(`<!doctype html><html><meta charset="utf-8"><style>${css}
            *{box-sizing:border-box}body{margin:0;font:16px var(--font-sans)}
            .fixture{display:flex;flex-direction:column;height:100dvh;width:min(100vw,440px)}
            .fixture>.context-drawer{display:flex;flex:1;min-height:0;padding:0 0 12px}
            .rp-shell{height:100% !important;min-height:0}
            </style><main class="fixture">
            <header class="editor-panel-heading"><strong>Writing tools</strong><button>Back to writing</button></header>
            <aside class="context-drawer"><div class="rp-shell has-pinned-body"><div class="rp-scroll">
            <div class="rp-sticky-header"><div class="rp-tabs">Writing · Story · Navigator<br>Notes & Tasks · History · Advanced</div><div class="rp-header">Story — Synopsis</div></div>
            <div class="rp-panel" style="--story-panel-height:120px"><div class="rp-content">${'<p>Saved synopsis field</p>'.repeat(20)}</div>
            <div class="story-panel-resize-handle"></div><div class="rp-trailing-body" tabindex="0">
            <div class="desktop-context-ai is-synopsis-coaching"><div class="ai-workspace synopsis-workspace ai-workspace-fixed">
            <div class="ai-request"><h3>Synopsis coaching</h3><p>Review field suggestions before Apply.</p>
            <fieldset><legend>Analyze saved synopsis</legend><label>Synopsis coaching mode<select><option>Improve a field</option></select></label>
            <label>Synopsis target<select><option>Ending intent</option></select></label>
            <label>Coaching notes<textarea rows="6">Help clarify the ending.</textarea></label>
            <p>These notes guide the request and remain separate from your authored synopsis notes.</p>
            <button class="primary synopsis-run">Improve synopsis field</button></fieldset></div>
            <div class="ai-response">${'<p>Long coaching result</p>'.repeat(30)}<button>Apply field suggestion</button><button>Dismiss</button></div>
            </div></div></div></div></div></div></aside></main></html>`);
        const pane = page.locator('.rp-trailing-body');
        const run = page.getByRole('button', { name: 'Improve synopsis field', exact: true });
        // Prove the original host rules reproduce the clipping before testing the fix.
        await page.locator('.desktop-context-ai').evaluate(e => e.classList.remove('is-synopsis-coaching'));
        assert.equal(await pane.evaluate(e => e.scrollHeight > e.clientHeight), false, 'Old wrapper hides overflow from the scroll owner');
        await page.locator('.desktop-context-ai').evaluate(e => e.classList.add('is-synopsis-coaching'));
        assert(await pane.evaluate(e => e.scrollHeight > e.clientHeight), 'Entire form participates in pane scrolling');
        for (const button of [run, page.getByRole('button', {name:'Dismiss', exact:true})]) {
            await button.scrollIntoViewIfNeeded();
            const r = await button.boundingBox(), p = await pane.boundingBox();
            assert(r.y >= p.y - 1 && r.y + r.height <= p.y + p.height + 1);
            assert(r.y >= 0 && r.y + r.height <= height);
            await button.click({ trial: true });
        }
        await pane.evaluate(e => e.scrollTop = 0);
        await pane.focus(); await page.keyboard.press('End');
        await page.waitForFunction(() => document.querySelector('.rp-trailing-body').scrollTop > 0);
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        await run.scrollIntoViewIfNeeded();
        await page.screenshot({ path: resolve(evidence, `${width}x${height}.png`) });
        checks.push({width,height,originalClippingReproduced:true,runReachable:true,reviewActionsReachable:true});
    }
    await writeFile(resolve(evidence,'results.json'), JSON.stringify({scope:'Production CSS with device-host layout fixture; no provider requests',checks},null,2));
    console.log(`Passed ${checks.length} synopsis reachability scenarios`);
} finally { await browser.close(); }

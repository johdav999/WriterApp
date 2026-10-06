import {createRequire} from 'node:module';
import {readFile, writeFile} from 'node:fs/promises';
import {resolve, join} from 'node:path';
import assert from 'node:assert/strict';

const {chromium} = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = resolve(process.argv[2] || 'artifacts/writing-tools/evidence');
const css = (await Promise.all([
  'WriterApp.UI.Shared/wwwroot/design-tokens.css',
  'WriterApp.Device.Shared/wwwroot/app.css',
  'WriterApp.UI.Shared/obj/Debug/net10.0/scopedcss/projectbundle/WriterApp.UI.Shared.bundle.scp.css',
  'WriterApp.Device.Shared/obj/Debug/net10.0/scopedcss/projectbundle/WriterApp.Device.Shared.bundle.scp.css'
].map(path => readFile(path, 'utf8')))).join('\n');
const browser = await chromium.launch({channel:'msedge', headless:true});
const page = await browser.newPage();
const checks = [];
try {
  for (const name of ['action-rewrite', 'action-expand', 'action-tighten', 'action-change_tone', 'action-show_dont_tell', 'saved-rewrite', 'saved-change_tone', 'saved-tighten']) {
    const markup = await readFile(join(root, `${name}.html`), 'utf8');
    const document = `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Writing tools</title><style>${css}\n*{box-sizing:border-box}body{margin:0;padding:20px}.preview{max-width:640px;margin:auto;padding:16px;background:var(--color-surface-2)}</style><main class="prosa-editor preview"><section class="desktop-writing-tools">${markup}</section></main></html>`;
    await writeFile(join(root, `${name}-preview.html`), document);
    for (const width of [320, 420, 680]) {
      await page.setViewportSize({width, height:1000}); await page.setContent(document);
      assert.equal(await page.locator('.writing-preview').count(), 1);
      assert.equal(await page.locator('.writing-operations [aria-pressed="true"]').count(), 1);
      assert.equal(await page.locator('[aria-label="Rewrite preset"]').count(), 0);
      assert.equal(await page.locator('.writing-action-selected').evaluate(e => getComputedStyle(e).backgroundColor), 'rgb(232, 238, 252)');
      await page.locator('.writing-action-selected').focus();
      await page.getByText('Save as preset', {exact:true}).click();
      assert.equal(await page.locator('input[aria-label="Preset name"]').isVisible(), true);
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false, `${name}: page overflow at ${width}`);
      assert.equal(await page.locator('.writing-panel').evaluate(e => e.scrollWidth > e.clientWidth + 1), false, `${name}: panel overflow at ${width}`);
      await page.screenshot({path:join(root, `${name}-${width}.png`), fullPage:true});
      checks.push(`${name}: one preview, clear active action, save disclosure and no clipping at ${width}px`);
    }
  }
  await writeFile(join(root, 'layout-results.json'), JSON.stringify({surface:'Real Razor markup and production CSS in headless Edge; synthetic provider, not native desktop acceptance', checks}, null, 2));
  console.log(`Passed ${checks.length} responsive Writing tools checks`);
} finally { await browser.close(); }

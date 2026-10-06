import {createRequire} from 'node:module';
import {readFile, writeFile} from 'node:fs/promises';
import {resolve, join} from 'node:path';
import assert from 'node:assert/strict';
const {chromium} = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = resolve(process.argv[2] || 'artifacts/style-quality/evidence');
const css = (await Promise.all([
  'WriterApp.UI.Shared/wwwroot/design-tokens.css',
  'WriterApp.Device.Shared/wwwroot/app.css',
  'WriterApp.UI.Shared/obj/Debug/net10.0/scopedcss/projectbundle/WriterApp.UI.Shared.bundle.scp.css'
].map(path => readFile(path, 'utf8')))).join('\n');
const markup = await readFile(join(root, 'review.html'), 'utf8');
const document = `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Style &amp; quality Coach</title><style>${css}\n*{box-sizing:border-box}body{margin:0;padding:16px;font:15px/1.5 system-ui;background:#f3f3f5;color:#192c45}main{max-width:640px;margin:auto;padding:18px;background:white;border-radius:12px}h2,h3{margin:0 0 12px}h3{margin-top:22px}mark{padding:1px 0}</style><main>${markup}</main></html>`;
await writeFile(join(root, 'preview.html'), document);
const browser = await chromium.launch({channel:'msedge',headless:true});
const page = await browser.newPage();
const checks=[];
try {
  for (const width of [320,420,680]) {
    await page.setViewportSize({width,height:1000}); await page.setContent(document);
    assert.equal(await page.getByLabel('Style goal',{exact:true}).count(),1);
    assert.equal(await page.getByLabel('Style goal',{exact:true}).inputValue(),'concise');
    assert.equal(await page.locator('.style-edit-choice input:checked').count(),1);
    assert.equal(await page.locator('.style-edit-list li').count(),2);
    assert.equal(await page.locator('script').count(),0);
    assert.equal(await page.locator('mark.ai-change-before').count(),2);
    assert.equal(await page.locator('mark.ai-change-after').count(),2);
    assert.equal(await page.locator('mark.ai-change-before').first().evaluate(e=>getComputedStyle(e).backgroundColor),'rgb(255, 227, 223)');
    assert.match(await page.locator('mark.ai-change-before').first().evaluate(e=>getComputedStyle(e).textDecorationLine),/line-through/);
    await page.getByText('Full preview with selected changes',{exact:true}).click();
    assert.match(await page.locator('details .ai-preview-text').last().innerText(),/Elin walked slowly\. She was really tired\./);
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false,`Page overflow at ${width}`);
    for(const element of await page.locator('.style-quality-options,.style-revision-review,.ai-preview-pane').all())
      assert.equal(await element.evaluate(e=>e.scrollWidth>e.clientWidth+1),false,`Coach overflow at ${width}`);
    await page.screenshot({path:join(root,`review-${width}.png`),fullPage:true});
    checks.push(`${width}px: criteria, selected goal, explained edits, highlighted changes, partial preview and no horizontal clipping`);
  }
  await writeFile(join(root,'layout-results.json'),JSON.stringify({surface:'Actual Razor markup and production CSS in headless Edge; synthetic AI findings, not live provider or native acceptance',checks},null,2));
  console.log(`Passed ${checks.length} responsive coach layout scenarios`);
} finally {await browser.close();}

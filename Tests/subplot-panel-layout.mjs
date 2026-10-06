import {createRequire} from 'node:module';
import {readFile,writeFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import assert from 'node:assert/strict';

const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root=resolve(process.argv[2] || 'artifacts/uat/subplot-continuity');
const css=(await Promise.all([
 'WriterApp.UI.Shared/wwwroot/design-tokens.css',
 'WriterApp.UI.Shared/obj/Debug/net10.0/scopedcss/projectbundle/WriterApp.UI.Shared.bundle.scp.css',
 'WriterApp.Device.Shared/obj/Debug/net10.0/scopedcss/projectbundle/WriterApp.Device.Shared.bundle.scp.css'
].map(path=>readFile(path,'utf8')))).join('\n');
const browser=await chromium.launch({channel:'msedge',headless:true});
const page=await browser.newPage();
const checks=[];
try {
 for(const name of ['findings-panel','empty-panel','invalid-panel']) {
  const markup=await readFile(join(root,`${name}.html`),'utf8');
  const document=`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Storyboard continuity review</title><style>${css}\n*{box-sizing:border-box}body{margin:0;padding:20px}.preview{max-width:640px;margin:auto}</style><main class="preview">${markup}</main></html>`;
  await writeFile(join(root,`${name}-preview.html`),document);
  for(const width of [320,420,729]) {
   await page.setViewportSize({width,height:1000});await page.setContent(document);
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false,`${name} page overflow at ${width}`);
   assert.equal(await page.locator('.subplot-review').evaluate(e=>e.scrollWidth>e.clientWidth+1),false,`${name} report overflow at ${width}`);
   assert.equal(await page.locator('textarea').evaluate(e=>e.getBoundingClientRect().width<=e.parentElement.getBoundingClientRect().width),true);
   if(name==='findings-panel') {
    await page.getByText('Storyboard included in this check',{exact:true}).click();
    assert.equal(await page.locator('.subplot-review__source').getAttribute('open'),'');
   }
   await page.screenshot({path:join(root,`${name}-${width}.png`),fullPage:true});
   checks.push(`${name}: no horizontal clipping, usable textarea, source disclosure at ${width}px`);
  }
 }
 await writeFile(join(root,'layout-results.json'),JSON.stringify({surface:'Real Razor HTML and compiled scoped CSS in headless Edge; synthetic provider',checks},null,2));
 console.log(`Passed ${checks.length} responsive panel checks`);
} finally { await browser.close(); }

// Full Development Blazor shell acceptance with isolated data and the existing mock-text provider.
import {createRequire} from 'node:module';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const evidence=resolve(process.argv[2] || 'artifacts/desktopai-p22');
const fixture=JSON.parse(await readFile(join(evidence,'acceptance-fixture.json'),'utf8'));
const host=process.env.DESKTOPAI_WEB_HOST || 'http://127.0.0.1:5390';
assert.equal(new URL(host).hostname,'127.0.0.1');
const browser=await chromium.launch({channel:'msedge',headless:true});
const context=await browser.newContext({viewport:{width:1280,height:720},acceptDownloads:true});
// Both server and WASM export flags must opt into the local EPUB acceptance run.
await context.route(/\/app\/appsettings(?:\.Development)?\.json(?:\?.*)?$/, async route => {
 const response=await route.fetch(); const settings=await response.json();
 settings.Exports={...settings.Exports,EpubEnabled:true};
 await route.fulfill({response,json:settings});
});
const page=await context.newPage();const errors=[],checks=[],downloads=[];
page.on('pageerror',e=>errors.push(e.stack || e.message));page.on('console',m=>{if(m.type()==='error'||m.type()==='warning')console.log('BROWSER '+m.text());});
const route=`${host}/app/documents/${fixture.document}/sections/${fixture.sections[0]}`;
const editor=page.locator('.ProseMirror[contenteditable=true]').first();
const record=(name)=>{checks.push(name);console.log('PASS '+name);};
async function snapshot(){const result=[];for(const section of fixture.sections){const r=await context.request.get(`${host}/api/sections/${section}/pages`);assert.equal(r.status(),200);result.push(...await r.json());}return result;}
async function open(){await page.goto(route);await editor.waitFor({timeout:60000});const skip=page.getByRole('button',{name:'Skip',exact:true});if(await skip.isVisible())await skip.click();await page.locator('#manuscript-page').waitFor();await page.waitForLoadState('networkidle');}
async function save(){await page.getByRole('button',{name:'Document actions',exact:true}).click();await page.getByRole('button',{name:'Save now',exact:true}).click();await page.keyboard.press('Escape');}
await mkdir(join(evidence,'web-screenshots'),{recursive:true});await mkdir(join(evidence,'exports'),{recursive:true});
try {
 await open();
 const original=await snapshot();assert.deepEqual(original.map(p=>p.id),fixture.pages);assert.ok(original[1].content.startsWith(fixture.html[1]));
 assert.ok(!(await editor.innerText()).includes('Second page'));record('Multi-page shell opens first page without merging later content');
 for(const [width,height] of [[1280,720],[1920,1080]]) {
  await page.setViewportSize({width,height});
  await page.locator('#manuscript-page').selectOption(fixture.pages[1]);await editor.locator('blockquote').waitFor();
  assert.ok((await editor.innerText()).includes('Second page'));await page.locator('#manuscript-page').focus();await page.keyboard.press('ArrowUp');await page.keyboard.press('Enter');
  await editor.locator('strong').first().waitFor();assert.equal(await page.locator('#manuscript-page').inputValue(),fixture.pages[0]);record(`Keyboard page navigation ${width}x${height}`);
  await page.getByRole('button',{name:'Document actions',exact:true}).focus();await page.keyboard.press('Enter');await page.getByRole('button',{name:'Save now',exact:true}).waitFor();await page.keyboard.press('Escape');
  assert.equal(await page.getByRole('button',{name:'Document actions',exact:true}).evaluate(e=>e===document.activeElement),true);record(`Document menu Escape returns focus ${width}x${height}`);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.screenshot({path:join(evidence,'web-screenshots',`web-shell-${width}x${height}.png`),fullPage:true});
 }
 // Native browser selection feeds the actual production handler; the provider is explicitly synthetic.
 await editor.locator('strong').first().dblclick();await page.getByRole('button',{name:'Rewrite (Neutral)',exact:true}).click();
 await page.getByRole('button',{name:'Discard',exact:true}).waitFor({timeout:30000});
 await page.screenshot({path:join(evidence,'web-screenshots','web-rewrite-review.png'),fullPage:true});
 const review=await snapshot();await page.getByRole('button',{name:'Discard',exact:true}).click();assert.deepEqual(await snapshot(),review);record('Mock rewrite review and Discard leave saved writing unchanged');
 await editor.locator('strong').first().dblclick();await page.getByRole('button',{name:'Rewrite (Neutral)',exact:true}).click();await page.getByRole('button',{name:'Apply',exact:true}).waitFor();
 const reportPattern='**/api/ai/actions/history/web/operations/*/report';await page.route(reportPattern,r=>r.abort('internetdisconnected'));
 const appliedSave=page.waitForResponse(r=>r.url().endsWith(`/api/pages/${fixture.pages[0]}`)&&r.request().method()==='PUT'&&r.status()===200);
 await page.getByRole('button',{name:'Apply',exact:true}).click();await appliedSave;
 const applied=await snapshot();assert.notEqual(applied[0].content,review[0].content);assert.equal(applied[1].content,review[1].content);assert.equal(applied[2].content,review[2].content);record('Checked mock Apply saves one page and preserves all other pages');
 await page.unroute(reportPattern);await open();await page.getByRole('button',{name:'Retry delivery',exact:true}).click();
 await page.locator('[aria-label="AI history delivery"] summary').filter({hasText:'Confirmed'}).first().waitFor({timeout:30000});assert.equal((await snapshot())[0].content,applied[0].content);record('Actual IndexedDB history receipt retries after report interruption and reload without reapplying');
 const undoneSave=page.waitForResponse(r=>r.url().endsWith(`/api/pages/${fixture.pages[0]}`)&&r.request().method()==='PUT'&&r.status()===200);await page.getByRole('button',{name:'Undo AI change',exact:false}).click();await undoneSave;
 assert.equal((await snapshot())[0].content,review[0].content);record('Scoped cloud Undo restores only approved page');
 const redoneSave=page.waitForResponse(r=>r.url().endsWith(`/api/pages/${fixture.pages[0]}`)&&r.request().method()==='PUT'&&r.status()===200);await page.getByRole('button',{name:'Redo AI change',exact:false}).click();await redoneSave;assert.equal((await snapshot())[0].content,applied[0].content);record('Scoped cloud Redo restores only approved page');
 await page.locator('#manuscript-page').selectOption(fixture.pages[1]);await editor.locator('blockquote').waitFor();await editor.click();await page.keyboard.press('Control+End');await page.keyboard.type(' Offline draft retained');
 await context.setOffline(true);await page.locator('#manuscript-page').selectOption(fixture.pages[0]);await page.getByRole('alert').filter({hasText:'draft is retained'}).waitFor();assert.ok((await editor.innerText()).includes('Offline draft retained'));record('Offline page switch retains active unsaved rich draft');
 await context.setOffline(false);await save();await page.locator('#manuscript-page').selectOption(fixture.pages[0]);await editor.locator('strong').first().waitFor();const persisted=await snapshot();assert.ok(persisted[1].content.includes('Offline draft retained'));assert.equal(persisted[2].content,original[2].content);record('Reconnect saves offline draft to original second-page identity');
 await open();assert.deepEqual((await snapshot()).map(p=>p.id),fixture.pages);record('Reload retains all structural page identities');
 await page.getByRole('button',{name:'Document actions',exact:true}).click();await page.getByRole('button',{name:'Export...',exact:true}).click();await page.locator('#export-format').selectOption('docx');
 const include=page.locator('.export-panel-checkbox').filter({hasText:'Include cover'}).locator('input');
 for(const format of ['docx','epub']) {
  await page.locator('#export-format').selectOption(format);
  await page.waitForLoadState('networkidle');
  await include.check();assert.equal(await include.isChecked(),true);
  await page.waitForLoadState('networkidle');
  const exportRequest=page.waitForRequest(r=>r.url().endsWith(`/api/documents/${fixture.document}/export`)&&r.method()==='POST');
  const downloadEvent=page.waitForEvent('download');await page.getByRole('button',{name:'Export',exact:true}).click();const requested=await exportRequest;assert.equal(requested.postDataJSON().includeCover,true);const download=await downloadEvent;const file=join(evidence,'exports',`published-cover.${format}`);await download.saveAs(file);
  assert.equal(await download.failure(),null);const bytes=await readFile(file);assert.ok(bytes.length>500);downloads.push({format,file,bytes:bytes.length,sha256:createHash('sha256').update(bytes).digest('hex')});record(`Physical browser ${format} download saved to disk`);
  if(format==='docx') {await page.getByRole('button',{name:'Document actions',exact:true}).click();await page.getByRole('button',{name:'Export...',exact:true}).click();}
 }
 const final=await snapshot();assert.deepEqual(final.map(p=>p.id),fixture.pages);assert.equal(final[2].content,original[2].content);assert.deepEqual(errors,[]);
 const projectResponse=await context.request.get(`${host}/api/projects`);assert.equal(projectResponse.status(),200);
 const project=(await projectResponse.json()).find(p=>p.id===fixture.project);assert.ok(project);assert.equal(project.coverImageUrl,fixture.cover);record('Publishing retains project cover source metadata');
 await writeFile(join(evidence,'web-release-results.json'),JSON.stringify({host,authentication:'Existing Development LocalDev',provider:'mock-text (synthetic)',demo:false,checks,errors,downloads,pages:fixture.pages,persisted:final},null,2));
 console.log(JSON.stringify({passed:checks.length,errors,downloads}));
} catch(error) {await page.screenshot({path:join(evidence,'web-screenshots','failure.png'),fullPage:true});await writeFile(join(evidence,'web-shell-inspection.txt'),await page.locator('body').innerText());await writeFile(join(evidence,'web-release-failure.json'),JSON.stringify({checks,errors,message:error.message},null,2));throw error;}
finally {await browser.close();}


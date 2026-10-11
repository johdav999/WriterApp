// Rebuilt production web UI with an owned disposable database; no AI/provider calls.
import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const base = process.env.BRAINSTORMING_WEB_URL ?? 'http://127.0.0.1:5399';
assert.equal(new URL(base).hostname, '127.0.0.1');
const evidence = resolve(process.env.BRAINSTORMING_EVIDENCE ?? 'artifacts/brainstorming-placement/browser');
const seed = JSON.parse(await readFile(process.env.BRAINSTORMING_WEB_SEED ?? 'artifacts/brainstorming-placement/interactive-seed.json', 'utf8'));
await mkdir(evidence, { recursive: true });
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const page = await browser.newPage({ viewport: { width: 1540, height: 1000 } });
// The server hosts the web client. Serve the actual device bundle for its separate mapping checks.
await page.route('**/_content/WriterApp.Device.Shared/editor/device-editor.js',async route=>route.fulfill({contentType:'text/javascript',body:await readFile('WriterApp.Device.Shared/wwwroot/editor/device-editor.js','utf8')}));
const errors = [], failedStyles = [], checks = [];
let sends = 0;
page.on('pageerror', e => errors.push(e.message));
page.on('response', r => { if (r.url().includes('.css') && r.status() >= 400) failedStyles.push(r.url()); });
page.on('request', r => { if (r.url().includes('/conversation/stream')) sends++; });
const button = name => page.getByRole('button', { name, exact: true });
const source = async () => { const response = await page.request.get(`${base}/api/brainstorming/sources/${seed.documentId}`); assert(response.ok()); return response.json(); };
const unchanged = async () => { const s = await source(); assert.equal(s.pages.length, seed.sourcePages.length); for (const p of seed.sourcePages) assert.equal(s.pages.find(x => x.id === p.Id)?.content, p.Content); };
const fits = async locator => locator.evaluate(e => { const b = e.getBoundingClientRect(); return b.width > 0 && b.top >= 0 && b.right <= innerWidth + 1 && b.bottom <= innerHeight + 1; });
try {
    for(let attempt=0;attempt<40;attempt++) {
        try { if((await page.request.get(`${base}/api/auth/me`)).ok())break; } catch { /* Owned host starting. */ }
        if(attempt===39)throw new Error('Owned acceptance host did not become ready.');
        await new Promise(done=>setTimeout(done,500));
    }
    await page.goto(`${base}/app/documents/${seed.documentId}/sections/${seed.sectionId}`);
    await button('Brainstorm').click({ timeout: 45000 });
    await page.getByText('Context ready', {exact:true}).waitFor();
    await button('History').click();await button('Find synced conversations').click();
    await page.getByRole('combobox', {name:/^Saved conversation/}).selectOption(seed.restoredSessionId);
    await button('Open saved conversation').click();
    await page.getByRole('textbox', {name:'Proposed prose editor',exact:true}).waitFor();
    await button('Review & keep').click();
    const placement = page.getByRole('region', {name:'Manuscript placement',exact:true});
    await placement.waitFor();
    assert(await page.getByRole('radio', {name:/Continue this section/}).isChecked());
    assert((await placement.innerText()).includes('Preserve this later page.'));
    assert((await placement.innerText()).includes('after the final paragraph'));
    assert(await fits(button('Add here')));
    await button('Add here').focus();assert(await button('Add here').evaluate(e => e === document.activeElement));
    await unchanged();
    await page.screenshot({path:resolve(evidence,'continue-wide.png'),fullPage:true});
    checks.push('Default appends after the target section’s last page; review shows surrounding prose; approval is keyboard reachable and visible.');

    await page.getByRole('radio',{name:/Create a new section/}).check();
    const title = page.getByRole('textbox',{name:'New section title',exact:true});
    await title.fill('');await page.getByRole('alert').filter({hasText:'Give your new section a title'}).waitFor();
    assert(await button('Add here').isDisabled());
    await title.fill('The next encounter');
    await page.waitForFunction(() => ![...document.querySelectorAll('button')].find(b => b.textContent.trim() === 'Add here')?.disabled);
    assert((await placement.innerText()).includes('The next encounter · new section after'));
    await page.screenshot({path:resolve(evidence,'new-section-wide.png'),fullPage:true});
    checks.push('New section has a title and named after-section destination; an empty title blocks approval and preserves the draft.');

    await page.getByRole('radio',{name:/Choose a place in the manuscript/}).check();
    await button('Insert here').first().click();
    const preview = page.getByRole('region',{name:'Manuscript insertion preview',exact:true});
    assert((await preview.innerText()).includes('Later arrival'));
    assert((await preview.innerText()).includes('Preserve this later page.'));
    assert(await button('Insert here').first().getAttribute('aria-pressed') === 'true');
    await page.screenshot({path:resolve(evidence,'paragraph-wide.png'),fullPage:true});
    await unchanged();
    await page.setViewportSize({width:390,height:844});
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    assert(await fits(button('Add here')));
    await page.locator('.proposal-approval').evaluate(e=>e.scrollTop=0);
    await page.screenshot({path:resolve(evidence,'paragraph-narrow.png'),fullPage:true});
    checks.push('Author can select a boundary between real passages; source remains unchanged; 390px layout has no horizontal overflow and keeps approval visible.');
    await page.setViewportSize({width:1540,height:1000});

    const mapped = await page.evaluate(async () => {
        const device = await import('/_content/WriterApp.Device.Shared/editor/device-editor.js');
        const source='<h2>Before</h2><p><em>Åsa 日本語 🧭</em></p><ul><li><p>Keep list</p></li></ul>';
        const draft='<p><strong>Approved prose.</strong></p>';
        const middle='<h2>Before</h2>'+draft+'<p><em>Åsa 日本語 🧭</em></p><ul><li><p>Keep list</p></li></ul>';
        const checks=[];
        for(const [surface,api] of [['web',window.tiptapEditor],['device',device]]) {
            for(const [index,after] of [[1,middle],[3,source+draft]]) {
                if(api.previewBrainstormingKeep(source,draft,after,0,0,0,'',index)!==after)throw Error(surface+' mapping failed');
                checks.push(surface+': approved paragraph boundary '+index+' preserves marks, list and Unicode');
            }
            let rejected=false;try{api.previewBrainstormingKeep(source,draft,middle,0,0,0,'',999);}catch{rejected=true;}
            if(!rejected)throw Error(surface+' accepted missing boundary');
        }
        return checks;
    });checks.push(...mapped);

    await page.getByRole('radio',{name:/Create a new section/}).check();await title.fill('The next encounter');
    await button('Add here').click();await page.getByText('Added to your manuscript',{exact:true}).waitFor();
    const added = await source();assert.equal(added.pages.length,seed.sourcePages.length+1);
    for(const p of seed.sourcePages)assert.equal(added.pages.find(x=>x.id===p.Id)?.content,p.Content);
    const newPage=added.pages.find(p=>!seed.sourcePages.some(x=>x.Id===p.id));assert(newPage.content.includes('<strong>She kept her distance.</strong>'));
    await page.screenshot({path:resolve(evidence,'added-wide.png'),fullPage:true});
    await page.locator('.proposal-kept').getByRole('button',{name:'Undo',exact:true}).click();await page.locator('.proposal-status').filter({hasText:'Undone'}).waitFor();await unchanged();
    await button('Details').click();await button('Redo kept version').click();
    await page.getByText('Added to your manuscript',{exact:true}).waitFor();
    await button('View in manuscript').click();
    await page.waitForFunction(() => !document.querySelector('.brainstorm-proposal-preview'));
    await page.waitForFunction(() => [...document.querySelectorAll('.ProseMirror')].some(e => e.getBoundingClientRect().width > 0 && e.textContent.includes('She kept her distance.')));
    assert((await page.locator('.ProseMirror').filter({visible:true}).first().innerText()).includes('She kept her distance.'));
    await page.screenshot({path:resolve(evidence,'view-manuscript.png'),fullPage:true});
    checks.push('Actual approval creates a filled section, preserves all original pages, supports guarded undo/redo, and View in manuscript opens the approved writing.');
    assert.equal(sends,0);assert.deepEqual(errors,[]);assert.deepEqual(failedStyles,[]);
    await writeFile(resolve(evidence,'result.json'),JSON.stringify({scope:'Production web flow on disposable LocalDev database; actual web and device rich-editor mappings; device persistence covered separately. No live provider or Windows interactive UI claim.',browser:browser.version(),checks,errors,failedStyles,sends},null,2));
    console.log(JSON.stringify({checks,errors,failedStyles,sends},null,2));
} finally { await browser.close(); }

import { createRequire } from 'node:module';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';

const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const output = resolve(process.argv[2] || 'artifacts/uat/story-panel-resize');
await mkdir(output, { recursive: true });
const css = (await Promise.all([
    'WriterApp.UI.Shared/wwwroot/design-tokens.css',
    'WriterApp.UI.Shared/RightPanelShell.razor.css',
    'WriterApp.UI.Shared/StoryPanelResizeHandle.razor.css',
    'WriterApp.UI.Shared/Projects/ScenePlanningFields.razor.css'
].map(path => readFile(path, 'utf8')))).join('\n');
const source = await readFile('WriterApp.UI.Shared/wwwroot/story-panel-resize.js', 'utf8');
const summary = Array.from({ length: 60 }, (_, i) => `Summary line ${i + 1}`).join('\n');
const html = `<!doctype html><html lang="en"><meta charset="utf-8"><style>${css}
    *{box-sizing:border-box}body{margin:0;font:15px system-ui;background:#faf9f7}
    .fixture{height:720px;width:min(640px,100vw);margin:auto}.rp-tabs{height:150px}
    .rp-content>section{align-self:start}h2{margin-top:0}.fact{padding:24px;border:1px solid #ddd;margin-bottom:16px}
    </style><div class="fixture"><div class="rp-shell has-pinned-body"><div class="rp-scroll">
    <div class="rp-sticky-header"><div class="rp-tabs">Writing · Story · Navigator<br><br>Scene card Coach · Storyboard · Synopsis</div><div class="rp-header">STORY</div></div>
    <div class="rp-panel"><div class="rp-content" tabindex="0"><section class="scene-planning-fields"><label>Title<input value="Chapter 1"></label>
    <label>Summary<textarea rows="3">${summary}</textarea></label>
    ${['Notes','Narrative intent','Emotional beat','Key events','Open questions'].map(x=>`<label>${x}<textarea rows="3"></textarea></label>`).join('')}</section></div>
    <div class="story-panel-resize-handle" role="separator" tabindex="0" aria-orientation="horizontal" aria-label="Resize Story and coaching panels"></div>
    <div class="rp-trailing-body" tabindex="0"><h2>Scene card Coach</h2><p>Review every proposal before Apply.</p>
    ${Array.from({length:12},(_,i)=>`<div class="fact">Saved story facts ${i+1}<p>Characters, places and events found in your manuscript.</p></div>`).join('')}</div>
    </div></div></div></div></html>`;
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const checks = [];
try {
    await page.route('http://story-panel.test/**', route => route.fulfill({contentType:'text/html', body:html}));
    await page.goto('http://story-panel.test/');
    await page.evaluate(async code => { window.resizeModule = await import(`data:text/javascript;base64,${btoa(code)}`); window.resizeModule.attach(document.querySelector('[role=separator]')); }, source);
    const metrics = () => page.evaluate(() => {
        const upper=document.querySelector('.rp-content'),lower=document.querySelector('.rp-trailing-body'),handle=document.querySelector('[role=separator]');
        return { upper:upper.clientHeight,lower:lower.clientHeight,upperOverflow:upper.scrollHeight>upper.clientHeight,lowerOverflow:lower.scrollHeight>lower.clientHeight,
            now:Number(handle.getAttribute('aria-valuenow')),max:Number(handle.getAttribute('aria-valuemax')) };
    });
    assert.equal((await metrics()).upper,320);
    assert.ok((await metrics()).lower >= 160);
    checks.push('Story starts at 320px and reserves room for coaching');
    const handle = page.getByRole('separator');
    await handle.focus(); await page.keyboard.press('ArrowUp');
    assert.equal((await metrics()).upper,304);
    const rect = await handle.boundingBox();
    await page.mouse.move(rect.x+rect.width/2,rect.y+rect.height/2);
    await page.mouse.down(); await page.mouse.move(rect.x+rect.width/2,rect.y+rect.height/2-70); await page.mouse.up();
    assert.equal((await metrics()).upper,234);
    checks.push('Mouse drag and keyboard change the panel split');
    assert.equal(await page.evaluate(()=>localStorage.getItem('prosa.story-panel-height')),'234');
    await page.reload();
    await page.evaluate(async code=>{window.resizeModule=await import(`data:text/javascript;base64,${btoa(code)}`);window.resizeModule.attach(document.querySelector('[role=separator]'));},source);
    assert.equal((await metrics()).upper,234);
    checks.push('Panel height survives reload');
    await handle.focus(); await page.keyboard.press('End');
    const end = await metrics(); assert.equal(end.upper,end.max); assert.ok(end.lower>=159);
    await page.keyboard.press('Home'); assert.equal((await metrics()).upper,120);
    await handle.dblclick(); assert.equal((await metrics()).upper,320);
    checks.push('Limits preserve both panels; double-click restores the larger initial height');
    const cancelRect = await handle.boundingBox();
    await page.mouse.move(cancelRect.x+20,cancelRect.y+6); await page.mouse.down();
    await page.mouse.move(cancelRect.x+20,cancelRect.y-40); await page.keyboard.press('Escape'); await page.mouse.up();
    assert.equal((await metrics()).upper,320);
    checks.push('Escape cancels a drag');
    assert.ok((await metrics()).upperOverflow && (await metrics()).lowerOverflow);
    for (const selector of ['.rp-content','.rp-trailing-body','textarea']) {
        assert.ok(await page.locator(selector).first().evaluate(e=>{e.scrollTop=e.scrollHeight;return e.scrollTop>0;}), `${selector} scrolls`);
    }
    checks.push('Story, coaching, and the Summary text each scroll vertically');
    await page.locator('.rp-content').evaluate(e=>e.scrollTop=0);
    await page.locator('.rp-trailing-body').evaluate(e=>e.scrollTop=0);
    await page.screenshot({path:resolve(output,'story-panel.png')});
    await page.locator('.fixture').evaluate(e=>e.style.height='420px');
    await page.waitForFunction(()=>Number(document.querySelector('[role=separator]').getAttribute('aria-valuenow'))<320);
    assert.ok((await metrics()).upper>0 && (await metrics()).lower>0);
    checks.push('Short windows clamp the saved height and keep both panels visible');
    await page.setViewportSize({width:729,height:900});
    await handle.focus(); await page.keyboard.press('ArrowUp');
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
    assert.ok((await metrics()).upper>0 && (await metrics()).lowerOverflow);
    checks.push('Narrow layouts retain resizing and scrolling without horizontal overflow');
    await page.evaluate(()=>{Object.defineProperty(window,'localStorage',{get(){throw Error('blocked');}});window.resizeModule.detach(document.querySelector('[role=separator]'));window.resizeModule.attach(document.querySelector('[role=separator]'));});
    await handle.focus(); await page.keyboard.press('ArrowUp');
    assert.equal((await metrics()).upper,304);
    checks.push('Resize remains usable when storage is unavailable');
    await writeFile(resolve(output,'results.json'),JSON.stringify({surface:'Headless Edge fixture using production panel CSS and resize module; not native desktop acceptance',checks},null,2));
    console.log(`Passed ${checks.length} Story panel layout checks`);
} finally { await browser.close(); }

// Production panel controller and styles; host focus/context callbacks are simulated.
// Native/WebView acceptance is recorded separately from these browser checks.
import assert from 'node:assert/strict';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const evidence = resolve(process.env.PROSA_PANELS_EVIDENCE || 'artifacts/responsive-panels/browser');
await mkdir(evidence, { recursive: true });
const css = (await Promise.all(['wwwroot/design-tokens.css','wwwroot/shell.css','wwwroot/editor.css','EditorWorkspace.razor.css','PanelResizeHandle.razor.css']
    .map(path => readFile(`WriterApp.UI.Shared/${path}`, 'utf8')))).join('\n').replaceAll('::deep ', '');
const source = await readFile('WriterApp.UI.Shared/wwwroot/editor-panels.js', 'utf8');
const resizeSource = await readFile('WriterApp.UI.Shared/wwwroot/panel-resize.js', 'utf8');
const html = `<!doctype html><html lang="en"><meta charset="utf-8"><style>${css}
body{margin:0;font:15px system-ui}*{box-sizing:border-box}button{font:inherit}textarea{width:100%}.editor-main{display:flex;flex-direction:column}.editor-canvas{overflow:auto;flex:1}
</style><div class="app-shell"><header class="app-header"><div class="app-header-left"><a class="app-brand" href="/">Prosa</a></div><div class="app-header-content"><div class="app-header-center">Document</div><div class="app-header-right"><details class="editor-account-menu" open><summary>Account &amp; settings</summary><div><a href="#account">Account</a><button>Settings</button></div></details></div></div></header>
<main class="app-main"><div class="app-body"><nav class="app-nav"><a href="#projects">Projects</a></nav><article class="content"><div class="prosa-editor"><div class="editor-shell" data-responsive-panels data-editor-location="page-1"><div class="editor-workspace">
<aside class="editor-navigation-host" aria-label="Projects and outline" tabindex="-1"><header class="editor-panel-heading"><strong>Projects &amp; outline</strong><button data-panel-action="close-left">Close</button></header><div class="editor-navigation-scroll"><nav class="editor-project-links"><a href="#projects">Projects</a></nav><button id="section-2">Section 2</button><label>New section title<input id="outline-draft"></label></div><div class="panel-resize-handle is-editor-left" role="separator" tabindex="0"></div></aside>
<section class="editor-main"><div class="editor-main-header"><div class="editor-section-info"><div class="editor-section-label">Untitled</div><div class="editor-section-title">Section 1</div></div><div class="editor-main-header-actions"><button class="panel-toggle">Preview</button><button class="panel-toggle">Brainstorm</button><button class="panel-toggle" data-panel-action="left">Projects &amp; outline</button><button class="panel-toggle" data-panel-action="right">Writing tools</button><button class="panel-toggle" data-panel-action="focus">Focus mode</button></div></div><div class="editor-canvas"><div id="manuscript" contenteditable="true">The quiet cafe.<br>${'<p>Writing stays in place.</p>'.repeat(80)}</div></div></section>
<div class="editor-context-host" aria-label="Writing tools" tabindex="-1"><header class="editor-panel-heading"><strong>Writing tools</strong><button data-panel-action="close-right">Back to writing</button></header><aside class="context-drawer"><div class="rp-shell"><div class="rp-scroll"><div class="rp-content"><button id="notes-tab">Notes</button><label>Notes<textarea id="note-draft">Unfinished note</textarea></label>${'<p>Tool content</p>'.repeat(40)}<button id="last-tool">Last tool</button></div></div></div></aside><div class="panel-resize-handle is-right" role="separator" tabindex="0"></div></div>
</div><button class="editor-panel-backdrop" data-panel-action="dismiss" aria-label="Close panel and return to writing" tabindex="-1"></button></div></div></article></div></main></div></html>`;
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
const errors = [], checks = [];
page.on('pageerror', error => errors.push(error.message));
const trigger = side => page.locator(`[data-panel-action="${side}"]`);
const state = () => page.locator('[data-responsive-panels]').evaluate(root => ({...root.dataset}));
async function scrollTools(label) {
    const pane = page.locator('.rp-scroll');
    await pane.evaluate(e => e.scrollTop = 0);
    const box = await pane.boundingBox();
    await page.mouse.move(box.x + box.width / 2, box.y + Math.min(box.height / 2, 200));
    await page.mouse.wheel(0, 350);
    await page.waitForFunction(() => document.querySelector('.rp-scroll').scrollTop > 100);
    const down = await pane.evaluate(e => e.scrollTop);
    await page.mouse.wheel(0, -350);
    await page.waitForFunction(top => document.querySelector('.rp-scroll').scrollTop < top - 100, down);
    assert.equal(await trigger('close-right').isVisible(), true);
    checks.push(`${label}: nested tools scroll down and up using the mouse wheel`);
}
async function viewport(width, height) {
    await page.setViewportSize({width, height});
    await page.waitForFunction(compact => document.querySelector('[data-responsive-panels]').dataset.panelsCompact === String(compact), width < 1200);
}
async function mount() {
    await page.goto('http://panels.test/');
    await page.evaluate(async ({source, resizeSource}) => {
        const root = document.querySelector('[data-responsive-panels]'), workspace = root.querySelector('.editor-workspace');
        window.host = { focus: false, collapsed: false, calls: 0 };
        function render() {
            root.classList.toggle('is-focus-mode', host.focus);
            workspace.classList.toggle('is-context-collapsed', host.focus || host.collapsed);
            root.querySelector('.context-drawer').classList.toggle('is-collapsed', host.focus || host.collapsed);
            root.querySelector('[data-panel-action="focus"]').textContent = host.focus ? 'Exit focus' : 'Focus mode';
        }
        root.querySelector('[data-panel-action="focus"]').addEventListener('click', () => { host.focus = !host.focus; render(); });
        root.querySelector('[data-panel-action="right"]').addEventListener('click', () => { host.calls++; host.collapsed = !host.collapsed; render(); });
        document.querySelector('#section-2').onclick = () => { root.dataset.editorLocation = 'page-2'; document.querySelector('.editor-section-title').textContent = 'Section 2'; };
        window.panels = await import(`data:text/javascript;base64,${btoa(source)}`);
        window.resizer = await import(`data:text/javascript;base64,${btoa(resizeSource)}`);
        panels.attach(root);
        resizer.attach(root.querySelector('.is-editor-left'), 'editor-left');
        resizer.attach(root.querySelector('.is-right'), 'right');
    }, {source, resizeSource});
}
try {
    await page.route('http://panels.test/**', route => route.fulfill({contentType:'text/html',body:html}));
    await mount();
    assert.equal((await state()).openDrawer, '');
    assert.equal(await page.locator('.editor-context-host').isVisible(), false);
    assert.equal(await page.locator('.app-nav').isVisible(), false);
    assert.equal(await page.locator('.editor-account-menu').evaluate(e => e.open), false);
    await page.locator('.editor-canvas').evaluate(e => e.scrollTop = 250);
    await page.evaluate(() => { const range=document.createRange(),text=document.querySelector('#manuscript').firstChild; range.setStart(text,4);range.setEnd(text,9);getSelection().removeAllRanges();getSelection().addRange(range); });
    await trigger('right').click();
    assert.equal((await state()).openDrawer, 'right');
    assert.equal(await page.getByRole('dialog', {name:'Writing tools'}).isVisible(), true);
    assert.equal(await page.locator('.editor-context-host').evaluate(e => Math.round(e.getBoundingClientRect().width)), 390);
    assert.equal(await page.locator('.editor-main').evaluate(e => e.inert), true);
    assert.equal(await page.evaluate(() => host.calls), 0);
    await page.locator('#note-draft').fill('A note in progress');
    await scrollTools('Phone');
    await page.screenshot({path:resolve(evidence,'phone-tools.png')});
    await page.locator('#last-tool').focus(); await page.keyboard.press('Tab');
    assert.equal(await trigger('close-right').evaluate(e => e === document.activeElement), true);
    await page.keyboard.press('Shift+Tab');
    assert.equal(await page.locator('#last-tool').evaluate(e => e === document.activeElement), true);
    await page.keyboard.press('Escape');
    assert.equal((await state()).openDrawer, '');
    assert.equal(await trigger('right').evaluate(e => e === document.activeElement), true);
    assert.equal(await page.locator('.editor-canvas').evaluate(e => e.scrollTop), 250);
    assert.equal(await page.evaluate(()=>getSelection().toString()), 'quiet');
    await trigger('right').click(); assert.equal(await page.locator('#note-draft').inputValue(), 'A note in progress');
    assert.equal(await page.evaluate(() => panels.closeActive()), true);
    assert.equal(await page.evaluate(() => panels.closeActive()), false);
    checks.push('Phone tools open full screen, trap focus, preserve notes and scroll, and close through Escape/native Back');

    await trigger('left').click();
    await page.locator('#outline-draft').fill('New chapter draft');
    await page.screenshot({path:resolve(evidence,'phone-outline.png')});
    await page.locator('#section-2').click();
    assert.equal((await state()).openDrawer, '');
    await trigger('left').click(); assert.equal(await page.locator('#outline-draft').inputValue(), 'New chapter draft');
    await page.evaluate(() => panels.closeActive());
    checks.push('Selecting a section returns to writing; unfinished outline input survives reopening');

    await trigger('focus').click();
    await trigger('right').click();
    await page.waitForFunction(() => document.querySelector('[data-responsive-panels]').dataset.openDrawer === 'right');
    assert.equal(await page.evaluate(() => host.focus), false);
    await trigger('close-right').click();
    await viewport(900,900);
    await trigger('right').click();
    assert.equal(await page.locator('.editor-context-host').evaluate(e => Math.round(e.getBoundingClientRect().width)), 440);
    await scrollTools('Tablet');
    await page.locator('.editor-panel-backdrop').click({position:{x:20,y:100}});
    checks.push('Opening tools exits focus; tablet drawer closes using its backdrop');

    await viewport(1600,1000);
    assert.equal((await state()).leftOpen, 'true'); assert.equal((await state()).rightOpen, 'true');
    await scrollTools('Desktop');
    await page.evaluate(()=>document.querySelector('.editor-workspace').classList.add('is-preview-mode','is-panels-collapsed'));
    assert.equal((await state()).leftOpen,'false');assert.equal((await state()).rightOpen,'false');
    await page.evaluate(()=>document.querySelector('.editor-workspace').classList.remove('is-preview-mode','is-panels-collapsed'));
    assert.equal((await state()).leftOpen,'true');assert.equal((await state()).rightOpen,'true');
    await trigger('right').click(); assert.equal((await state()).rightOpen, 'false');
    await trigger('left').click(); assert.equal((await state()).leftOpen, 'false');
    await trigger('focus').click(); await trigger('focus').click();
    assert.equal((await state()).leftOpen, 'false'); assert.equal((await state()).rightOpen, 'false');
    await trigger('left').click(); await trigger('right').click();
    await page.locator('.is-editor-left').focus(); await page.keyboard.press('ArrowRight');
    assert.equal(await page.evaluate(() => localStorage.getItem('prosa.panel-width.editor-left')), '256');
    await page.screenshot({path:resolve(evidence,'desktop-panels.png')});
    assert.ok(await page.locator('.editor-main').evaluate(e => e.clientWidth) >= 420);
    await trigger('left').click(); await trigger('right').click();
    await mount(); assert.equal((await state()).leftOpen, 'false'); assert.equal((await state()).rightOpen, 'false');
    await viewport(390,844);
    await trigger('right').click(); await page.evaluate(() => panels.closeActive());
    await page.evaluate(() => { host.collapsed=false; document.querySelector('.editor-workspace').classList.remove('is-context-collapsed'); });
    await page.waitForFunction(()=>document.querySelector('[data-responsive-panels]').dataset.openDrawer==='right');
    await page.evaluate(() => panels.closeActive());
    await viewport(1600,1000);
    assert.equal((await state()).rightOpen, 'false');
    checks.push('Desktop widths/visibility persist, focus restores preferences, and mobile activity does not overwrite them');

    await viewport(390,844);
    await trigger('right').click();
    await page.evaluate(() => { const root = document.querySelector('[data-responsive-panels]'); panels.detach(root); });
    assert.equal(await page.locator('.editor-main').evaluate(e => e.inert), false);
    await page.evaluate(() => panels.attach(document.querySelector('[data-responsive-panels]')));
    assert.equal((await state()).openDrawer, '');
    checks.push('Disposal restores background interaction and reattachment starts with drawers closed');
    await page.evaluate(() => {
        const root=document.querySelector('[data-responsive-panels]');panels.detach(root);
        Object.defineProperty(window,'localStorage',{configurable:true,value:{getItem(){throw Error('Blocked');},setItem(){throw Error('Blocked');}}});panels.attach(root);
    });
    await trigger('right').click();assert.equal((await state()).openDrawer,'right');await trigger('close-right').click();
    checks.push('Panels remain usable when preference storage is blocked');
    await page.evaluate(() => { document.querySelector('[data-responsive-panels]').dataset.annotationPanel='true'; });
    await trigger('right').click();
    assert.equal(await page.locator('.editor-main').evaluate(e=>e.inert),false);
    assert.equal(await page.locator('.editor-context-host').getAttribute('aria-modal'),null);
    const manuscript=await page.locator('.editor-main').boundingBox(), annotation=await page.locator('.editor-context-host').boundingBox();
    assert.ok(manuscript.y+manuscript.height<=annotation.y+1);
    await scrollTools('Compact annotations');
    await page.locator('.editor-main [contenteditable]').focus();
    assert.equal(await page.locator('.editor-main [contenteditable]').evaluate(e=>e===document.activeElement),true);
    await trigger('close-right').click();
    assert.equal(await page.locator('.editor-main').evaluate(e=>e.inert),false);
    checks.push('Annotation panel reserves space beneath interactive text without modal focus trapping');
    assert.deepEqual(errors, []);
    await writeFile(resolve(evidence,'results.json'), JSON.stringify({checks,errors},null,2));
    console.log(JSON.stringify({passed:checks.length,checks},null,2));
} finally { await browser.close(); }

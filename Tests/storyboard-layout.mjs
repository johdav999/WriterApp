import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import { resolve, join } from 'node:path';

// Browser geometry regression for the production storyboard CSS and host DOM
// structure. This fixture does not exercise authentication or native WebView input.
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const output = resolve(process.env.WRITERAPP_STORYBOARD_EVIDENCE || 'artifacts/uat/storyboard-layout');
const baseline = process.argv.includes('--baseline');
await mkdir(output, { recursive: true });
const readCss = async path => (await readFile(path, 'utf8')).replace(/::deep\s*/g, '');
const shared = (await Promise.all([
    'WriterApp.UI.Shared/wwwroot/bootstrap.min.css',
    'WriterApp.UI.Shared/wwwroot/design-tokens.css',
    'WriterApp.UI.Shared/wwwroot/shell.css',
    'WriterApp.UI.Shared/Projects/ProjectDocuments.razor.css',
    'WriterApp.UI.Shared/Projects/StoryboardBoard.razor.css',
    'WriterApp.UI.Shared/Projects/StoryboardInsights.razor.css',
    'WriterApp.UI.Shared/Projects/SceneCard.razor.css',
    'WriterApp.Device.Shared/Components/LocalPlanningPanel.razor.css',
].map(readCss))).join('\n');

function markup(host, { error, collapsed, bulk }) {
    const cards = Array.from({ length: 8 }, (_, i) => `<article class="scene-card"><h4>Scene ${i + 1}</h4><p>A scene summary with enough text to wrap within its chapter.</p><p>Draft · POV: protagonist</p></article>`).join('');
    const chapters = Array.from({ length: 6 }, (_, i) => `<section class="storyboard-column"><header class="storyboard-column-header"><h4>Chapter ${i + 1}</h4><span class="storyboard-column-meta">8 scenes</span><button>Open chapter</button></header><div class="storyboard-column-body"><div class="storyboard-scene-list">${cards}</div></div></section>`).join('');
    const inspector = `<aside class="storyboard-inspector"><div class="storyboard-inspector-header"><div><h2>Scene Detail</h2><p>Edit the selected storyboard card without leaving the board.</p></div></div><div class="storyboard-inspector-scroll"><div class="local-planning-panel"><p>Chapter 1</p>${['Title', 'Summary', 'Notes', 'Narrative intent', 'Key events', 'Open questions'].map(label => `<label>${label}<textarea rows="3">Scene planning content</textarea></label>`).join('')}</div></div></aside>`;
    return `<div class="app-shell"><header class="app-header"><div class="app-header-left">Prosa</div><div class="app-header-content">Projects &amp; documents</div></header><main class="app-main"><div class="app-body"><nav class="app-nav"><a class="app-nav-link">Projects &amp; documents</a><a class="app-nav-link active">Storyboard</a></nav><article class="content">
    <section class="storyboard-page${error ? ' has-error' : ''}">
      <div class="project-documents"><label>Document <select><option>Untitled project ★ · manuscript</option></select></label></div>
      <header class="storyboard-page__header"><div><h1>Storyboard</h1><p class="storyboard-page__subtitle">Untitled project · Manage chapter and scene flow.</p></div><div class="storyboard-page__actions"><button class="secondary">Refresh storyboard</button><button class="secondary">Open structure</button><button class="secondary">Open manuscript</button></div></header>
      ${error ? '<div class="storyboard-page__error" role="alert">Unable to refresh the storyboard. Your current scene plan is still available.</div>' : ''}
      <div class="storyboard-page__layout${collapsed ? ' storyboard-page__layout--insights-collapsed' : ''}">
        <div class="storyboard-page__panel storyboard-page__panel--board"><h2 class="storyboard-page__panel-title">Storyboard</h2><div class="storyboard-page__panel-body"><section class="project-storyboard"><div class="project-storyboard-main"><div class="project-storyboard-toolbar"><div class="project-storyboard-board-meta"><span>6 chapters</span><span>Scroll horizontally to move across the manuscript.</span><span class="project-storyboard-selection-count">${bulk ? '2' : '1'} selected</span><div class="project-storyboard-filters"><button class="project-storyboard-filters-trigger">Filters</button></div></div>${bulk ? '<div class="project-storyboard-bulk-bar"><strong>2 scenes selected</strong><label>Status <select><option>Draft</option></select></label><button>Apply to selected</button></div>' : ''}</div><div class="project-storyboard-board">${chapters}</div></div></section></div></div>
        ${collapsed ? '' : '<div class="storyboard-page__panel storyboard-page__panel--insights"><div class="storyboard-page__panel-body"><aside class="storyboard-insights"><header class="storyboard-insights-header"><div class="storyboard-insights-header-main"><div class="storyboard-insights-header-copy"><h2>Storyboard Insights</h2><p>Board-level structure signals drawn from the current chapter and scene layout.</p></div><button class="storyboard-insights-toggle">← Collapse</button></div><p>Scene-level AI tools are available in the scene detail panel.</p></header><div class="storyboard-insights-scroll">' + Array.from({ length: 8 }, () => '<section class="storyboard-insights-section"><h3>Subplot continuity</h3><p>Follow each story thread through your chapter and scene plan.</p><button>Review</button></section>').join('') + '</div></aside></div></div>'}
        <div class="storyboard-page__panel storyboard-page__panel--detail">${host === 'web' ? `<div class="storyboard-page__panel-body">${inspector}</div>` : inspector}</div>
        ${collapsed ? '<button class="storyboard-page__insights-rail">Insights</button>' : ''}
      </div>
    </section></article></div></main></div>`;
}

const browser = await chromium.launch({ channel: 'msedge', headless: true });
const results = [];
try {
    const page = await browser.newPage();
    for (const host of ['desktop', 'web']) {
        const path = `WriterApp.${host === 'desktop' ? 'Device.Shared' : 'Client'}/Pages/Storyboard.razor.css`;
        const hostCss = baseline ? execFileSync('git', ['show', `HEAD:${path}`], { encoding: 'utf8' }) : await readCss(path);
        const inspectorCss = host === 'web' ? await readCss('WriterApp.Client/Components/Projects/StoryboardSceneInspector.razor.css') : '';
        for (const [width, height] of [[1696, 568], [1440, 900], [1280, 720], [1201, 720], [1024, 768], [729, 900], [390, 844]]) {
            for (const state of [{ error: false, collapsed: false, bulk: false }, { error: true, collapsed: false, bulk: true }, { error: false, collapsed: true, bulk: false }]) {
                await page.setViewportSize({ width, height });
                const document = `<!doctype html><html><meta charset="utf-8"><style>${shared}\n${hostCss}\n${inspectorCss}\nhtml,body{margin:0;height:100%}*{box-sizing:border-box}</style>${markup(host, state)}</html>`;
                await page.setContent(document);
                const bounds = await page.evaluate(() => {
                    const rect = selector => { const r = document.querySelector(selector).getBoundingClientRect(); return { top: r.top, bottom: r.bottom, left: r.left, right: r.right, height: r.height }; };
                    const scroll = selector => { const e = document.querySelector(selector); return { height: e.clientHeight, scrollHeight: e.scrollHeight, width: e.clientWidth, scrollWidth: e.scrollWidth }; };
                    return {
                        selector: rect('.project-documents'), header: rect('.storyboard-page__header'),
                        title: rect('h1'), subtitle: rect('.storyboard-page__subtitle'),
                        layout: rect('.storyboard-page__layout'), boardTitle: rect('.storyboard-page__panel-title'),
                        toolbar: rect('.project-storyboard-toolbar'), canvas: rect('.project-storyboard-board'),
                        panels: [...document.querySelectorAll('.storyboard-page__panel')].map(e => { const r = e.getBoundingClientRect(); return { top: r.top, bottom: r.bottom, left: r.left, right: r.right }; }),
                        boardScroll: scroll('.project-storyboard-board'), inspectorScroll: scroll('.storyboard-inspector-scroll'),
                        insightsScroll: document.querySelector('.storyboard-insights-scroll') ? scroll('.storyboard-insights-scroll') : null,
                        overflow: document.documentElement.scrollWidth > innerWidth + 1,
                        fieldsFit: [...document.querySelectorAll('textarea')].every(e => e.getBoundingClientRect().right <= e.closest('.storyboard-page__panel').getBoundingClientRect().right + 1),
                        selectorFits: document.querySelector('.project-documents select').getBoundingClientRect().right <= document.querySelector('.storyboard-page').getBoundingClientRect().right + 1,
                    };
                });
                const name = `${host}-${width}x${height}-${state.error ? 'error-bulk' : state.collapsed ? 'collapsed' : 'selected'}`;
                if (baseline) await page.screenshot({ path: join(output, `${name}-baseline.png`), fullPage: true });
                assert.ok(bounds.header.top >= bounds.selector.bottom - 1, `${name}: selector overlaps header`);
                assert.ok(bounds.header.bottom >= bounds.subtitle.bottom - 1, `${name}: header squeezes its text`);
                assert.ok(bounds.layout.top >= bounds.header.bottom - 1, `${name}: panels overlap header`);
                assert.ok(bounds.boardTitle.top >= bounds.subtitle.bottom - 1, `${name}: headings overlap`);
                assert.ok(bounds.canvas.top >= bounds.toolbar.bottom - 1, `${name}: board overlaps toolbar`);
                assert.ok(bounds.boardScroll.height > 100, `${name}: board canvas is squeezed`);
                assert.ok(bounds.inspectorScroll.height > 100, `${name}: inspector is squeezed`);
                assert.ok(bounds.inspectorScroll.scrollHeight > bounds.inspectorScroll.height, `${name}: inspector must scroll`);
                assert.ok(bounds.boardScroll.scrollWidth > bounds.boardScroll.width, `${name}: chapters must scroll horizontally`);
                assert.ok(!bounds.overflow && bounds.fieldsFit, `${name}: content overflows horizontally`);
                assert.ok(bounds.selectorFits, `${name}: document selector is clipped`);
                for (let i = 0; i < bounds.panels.length; i++) for (let j = i + 1; j < bounds.panels.length; j++) {
                    const a = bounds.panels[i], b = bounds.panels[j];
                    assert.ok(a.right <= b.left + 1 || b.right <= a.left + 1 || a.bottom <= b.top + 1 || b.bottom <= a.top + 1, `${name}: panels overlap`);
                }
                if (width === 1696 || width === 390) {
                    await page.screenshot({ path: join(output, `${name}.png`), fullPage: true });
                    await writeFile(join(output, `${name}.html`), document);
                }
                results.push({ name, bounds });
            }
        }
    }
    await writeFile(join(output, 'results.json'), JSON.stringify({ surface: 'Headless Edge, production source CSS, fixture matching storyboard host structure', results }, null, 2));
    console.log(`Passed ${results.length} storyboard layout cases across both hosts.`);
} finally { await browser.close(); }

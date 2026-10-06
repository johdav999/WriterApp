// Real Development client shell with isolated migrated fixtures and existing mock-text provider.
import { createRequire } from 'node:module';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import assert from 'node:assert/strict';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence = resolve(process.argv[2] || 'artifacts/ai-parity-p14');
const fixture = JSON.parse(await readFile(join(evidence, 'acceptance-fixture.json'), 'utf8'));
const host = process.env.DESKTOPAI_WEB_HOST || 'http://127.0.0.1:5396';
assert.equal(new URL(host).hostname, '127.0.0.1'); assert.equal(fixture.demo, false);
const browser = await chromium.launch({ channel: 'msedge', headless: true });
const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
const page = await context.newPage(), errors = [], checks = [], requests = [];
page.on('pageerror', e => errors.push(e.stack || e.message));
page.on('request', r => { if (r.method() === 'POST' && /\/api\/ai\/actions\/[^/]+\/execute$/.test(r.url())) requests.push({ action: r.url().split('/').at(-2), body: r.postDataJSON() }); });
const route = `${host}/app/documents/${fixture.document}/sections/${fixture.sections[0]}`;
const editor = page.locator('.ProseMirror[contenteditable=true]').first();
const record = name => { checks.push(name); console.log('PASS ' + name); };
async function snapshot() { const pages = []; for (const section of fixture.sections) { const r = await context.request.get(`${host}/api/sections/${section}/pages`); assert.equal(r.status(), 200); pages.push(...await r.json()); } return pages; }
async function open() { await page.goto(route); await editor.waitFor({ timeout: 60000 }); const skip = page.getByRole('button', { name: 'Skip', exact: true }); if (await skip.isVisible()) await skip.click(); await page.locator('#manuscript-page').waitFor(); await page.waitForLoadState('networkidle'); }
async function selectOpening() { await editor.locator('strong').first().dblclick(); }
const executive = () => page.getByRole('button', { name: 'Rewrite (Executive)', exact: true }).first();
await mkdir(join(evidence, 'web-screenshots'), { recursive: true });
try {
    await open(); const loaded = await snapshot(); assert.deepEqual(loaded.map(p => p.id), fixture.pages);
    assert(await editor.locator('strong').first().isVisible()); record('Real client loads the isolated rich multi-page graph');
    for (const width of [1280, 1920]) {
        await page.setViewportSize({ width, height: width === 1280 ? 720 : 1080 });
        await page.locator('#manuscript-page').selectOption(fixture.pages[1]); await editor.locator('blockquote').waitFor();
        assert((await editor.innerText()).includes('Second')); await page.locator('#manuscript-page').selectOption(fixture.pages[0]); await editor.locator('strong').first().waitFor();
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
        await page.screenshot({ path: join(evidence, 'web-screenshots', `shell-${width}.png`), fullPage: true }); record(`Page navigation and real shell layout ${width}`);
    }
    // Visiting a list-ended page currently persists the StarterKit trailing empty paragraph.
    // Record this separately; every AI assertion below uses the exact saved pre-request snapshot.
    const original = await snapshot();
    assert.equal(original[0].content, loaded[0].content); assert.equal(original[2].content, loaded[2].content);
    assert(original[1].content === loaded[1].content || original[1].content === loaded[1].content + '<p></p>');
    await writeFile(join(evidence, 'navigation-normalization.json'), JSON.stringify({ before: loaded[1], after: original[1],
        finding: 'P14-N02: visiting a list-ended page can persist one empty trailing paragraph before any AI Apply; original authored blocks/text/marks remain. Separate from the AI contract.' }, null, 2));
    await selectOpening();
    for (const label of ['Rewrite (Neutral)', 'Rewrite (Formal)', 'Rewrite (Casual)', 'Change tone (Friendly)', 'Change tone (Technical)', 'Rewrite (Executive)'])
        assert(await page.getByRole('button', { name: label, exact: true }).first().isVisible());
    record('Reachable client menu advertises all six legacy tone labels');
    await executive().click(); await page.getByRole('button', { name: 'Discard', exact: true }).waitFor({ timeout: 30000 });
    assert.equal(requests.length, 1); const params = requests[0].body.parameters;
    assert.equal(params.tone, 'Executive'); assert.equal(params.instruction, 'Rewrite (Executive)'); assert.equal(params.length, 'Same'); assert.equal(params.preserve_terms, true);
    assert.deepEqual(await snapshot(), original); await page.getByRole('button', { name: 'Discard', exact: true }).click(); assert.deepEqual(await snapshot(), original);
    record('Executive checked request, review and Discard preserve saved writing');
    let entered; const gate = new Promise(done => { entered = done; });
    const executePattern = '**/api/ai/actions/rewrite.selection/execute';
    await page.route(executePattern, async r => { entered(); /* Keep the dispatch pending until AbortController cancels it. */ });
    await selectOpening(); await executive().click(); await gate;
    await page.getByRole('button', { name: 'Cancel AI request', exact: true }).click();
    await page.getByRole('button', { name: 'Cancel AI request', exact: true }).waitFor({ state: 'hidden' });
    await page.unroute(executePattern); assert.deepEqual(await snapshot(), original); record('Visible in-flight Cancel releases the client without saved mutation');
    await selectOpening(); await executive().click(); await page.getByRole('button', { name: 'Apply', exact: true }).waitFor({ timeout: 30000 });
    const reports = '**/api/ai/actions/history/web/operations/*/report'; await page.route(reports, r => r.abort('internetdisconnected'));
    const saved = page.waitForResponse(r => r.url().endsWith(`/api/pages/${fixture.pages[0]}`) && r.request().method() === 'PUT' && r.status() === 200);
    await page.getByRole('button', { name: 'Apply', exact: true }).click(); await saved;
    const applied = await snapshot(); assert.notEqual(applied[0].content, original[0].content); assert.equal(applied[1].content, original[1].content); assert.equal(applied[2].content, original[2].content);
    assert(applied[0].content.includes('<strong>')); record('Fresh request after cancellation explicitly Applies one checked rich page');
    await page.unroute(reports); await open(); await page.getByRole('button', { name: 'Retry delivery', exact: true }).click();
    await page.locator('[aria-label="AI history delivery"] summary').filter({ hasText: 'Confirmed' }).first().waitFor({ timeout: 30000 });
    assert.equal((await snapshot())[0].content, applied[0].content); record('Reload/retry acknowledges retained history without reapplying writing');
    let persisted = page.waitForResponse(r => r.url().endsWith(`/api/pages/${fixture.pages[0]}`) && r.request().method() === 'PUT' && r.status() === 200);
    await page.getByRole('button', { name: 'Undo AI change', exact: false }).click(); await persisted;
    assert.equal((await snapshot())[0].content, original[0].content); record('Real client Undo restores exact approved-page source');
    persisted = page.waitForResponse(r => r.url().endsWith(`/api/pages/${fixture.pages[0]}`) && r.request().method() === 'PUT' && r.status() === 200);
    await page.getByRole('button', { name: 'Redo AI change', exact: false }).click(); await persisted;
    assert.equal((await snapshot())[0].content, applied[0].content); await open(); assert.equal((await snapshot())[0].content, applied[0].content);
    record('Real client Redo and reopen preserve approved content and untouched pages');
    await page.screenshot({ path: join(evidence, 'web-screenshots', 'confirmed-history.png'), fullPage: true });
    assert.deepEqual(errors, []);
    await writeFile(join(evidence, 'integrated-browser.json'), JSON.stringify({ passed: true, checks, requests, errors, host,
        scope: 'Actual compiled WASM client, existing Development identity/Professional fixture, isolated migrated SQLite and existing mock-text. No external authentication/provider, native launch or deployed/package acceptance. Pending cancellation is held before provider dispatch; transport/server/provider races are tested separately.' }, null, 2));
} catch (error) {
    await page.screenshot({ path: join(evidence, 'web-screenshots', 'failure.png'), fullPage: true }).catch(() => {});
    await writeFile(join(evidence, 'integrated-browser-failure.json'), JSON.stringify({ checks, requests, errors, error: String(error), text: await page.locator('body').innerText().catch(() => '') }, null, 2));
    throw error;
} finally { await browser.close(); }

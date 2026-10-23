// Actual Blazor client and shared components; API responses are isolated synthetic fixtures.
// Run with the current client available at PROBE_CLIENT_URL (default localhost:5398).
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { mkdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const client = process.env.PROBE_CLIENT_URL || 'http://localhost:5398';
const evidence = 'artifacts/sharing-polish';
await mkdir(evidence, { recursive: true });
const id = '1396210e-fbac-4714-bb20-37560d6b40bf';
const guestId = '2396210e-fbac-4714-bb20-37560d6b40bf';
const inviteId = '3396210e-fbac-4714-bb20-37560d6b40bf';
const document = (documentId, title, role) => ({ documentId, title, role, ownerId: 'owner', membershipRevision: 1 });
const invitation = (id, documentId, documentTitle) => ({ id, documentId, documentTitle, role: 1, recipientEmail: 'reader@fixture.test', expiresAtUnixMilliseconds: Date.now() + 86400000, revision: 1, state: 'pending' });
let inbox, owned, shared, outgoing, failure = false, available = true, delay = 0;
const requests = [], errors = [], checks = [];
function seed() {
  inbox = [invitation(inviteId, guestId, 'The Atlas of Quiet Places')];
  owned = [document(id, 'Where the Light Returns', 3), document('4396210e-fbac-4714-bb20-37560d6b40bf', 'Notes from the Northern Shore', 3)];
  shared = [document('5396210e-fbac-4714-bb20-37560d6b40bf', 'A Season for Small Beginnings', 2)];
  outgoing = []; failure = false; available = true; delay = 0;
}
seed();
const server = createServer(async (req, res) => {
  try {
    const path = new URL(req.url, 'http://localhost').pathname;
    const json = value => { res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify(value)); };
    if (!path.startsWith('/api/')) {
      const result = await fetch(new URL(req.url, client));
      res.writeHead(result.status, { 'Content-Type': result.headers.get('content-type') || 'application/octet-stream' });
      res.end(Buffer.from(await result.arrayBuffer())); return;
    }
    if (path === '/api/auth/me') return json({ isAuthenticated: true, userId: 'owner', name: 'Alex', roles: [], planKey: 'professional', isPaidAccessActive: true });
    if (path === '/api/ai/status') return json({ aiEnabled: false, uiEnabled: false });
    if (path === '/api/onboarding/state') return json({ hasCompletedOnboarding: true });
    if (path.endsWith('/collaboration/setup')) return json({ title: owned[0]?.title, collaborationMode: 2, expectedVersion: 'fixture' });
    if (path === '/api/collaboration/availability') { if (delay) await new Promise(r => setTimeout(r, delay)); if (failure) { res.statusCode = 503; return json({}); } return json({ enabled: available }); }
    if (req.method === 'POST') {
      const chunks = []; for await (const chunk of req) chunks.push(chunk);
      requests.push({ path, body: JSON.parse(Buffer.concat(chunks).toString() || '{}') });
      if (path.endsWith('/decline')) inbox = [];
      if (path.endsWith('/accept')) { inbox = []; shared.push(document(guestId, 'The Atlas of Quiet Places', 1)); }
      if (path.endsWith('/invitations/by-email')) { const invite = invitation(inviteId, id, owned[0].title); outgoing.push(invite); return json({ invitation: invite, token: null, replayed: false }); }
      return json({});
    }
    if (path === '/api/collaboration/owned') return json({ complete: true, documents: owned });
    if (path === '/api/collaboration/shared') return json({ complete: true, documents: shared });
    if (path === '/api/collaboration/invitations') return json(inbox);
    if (path.endsWith('/members')) return json([{ userId: 'owner', displayName: 'Alex Morgan', role: 3, revision: 0, revoked: false }, { userId: 'editor', displayName: 'Sam Rivera', role: 2, revision: 1, revoked: false }]);
    if (path.endsWith('/invitations')) return json(outgoing);
    if (path.endsWith('/supporting')) return json([]);
    res.statusCode = 404; json({});
  } catch (e) { res.statusCode = 500; res.end(e.message); }
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const origin = `http://127.0.0.1:${server.address().port}`;
const browser = await chromium.launch({ channel: 'msedge', headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1400 } });
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if(m.type()==='error') console.error(m.text()); });
  await page.goto(origin + '/app/collaboration');
  await page.getByRole('heading', { name: 'The Atlas of Quiet Places', exact: true }).waitFor().catch(async e => { console.log((await page.locator('body').innerText()).slice(0,3500)); await page.screenshot({path: `${evidence}/failure.png`}); throw e; });
  await page.screenshot({ path: `${evidence}/desktop.png`, fullPage: true });
  assert.equal(await page.locator('.sharing-manuscript').count(), 3); checks.push('Actual Blazor page renders invitations, owned and shared manuscripts.');
  await page.getByRole('searchbox', { name: 'Find a shared manuscript' }).fill('northern');
  assert.equal(await page.locator('.sharing-manuscript').count(), 1);
  await page.getByRole('status').filter({ hasText: 'No shared manuscripts match' }).waitFor();
  await page.getByRole('searchbox', { name: 'Find a shared manuscript' }).fill(''); checks.push('Search filters both libraries and empty results recover on clear.');
  await page.getByRole('button', { name: 'Decline', exact: true }).click();
  await page.getByText('You’re all caught up', { exact: true }).waitFor();
  assert(requests.some(r => r.path.endsWith('/decline'))); checks.push('Decline refreshes the inbox.');
  seed(); await page.getByRole('button', { name: 'Refresh access', exact: true }).click();
  await page.getByRole('button', { name: 'Accept', exact: true }).waitFor();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: `${evidence}/mobile.png`, fullPage: true });
  assert(await page.locator('.collaboration-sharing').evaluate(e => e.scrollWidth <= e.clientWidth && e.clientWidth >= 350));
  await page.locator('.sharing-privacy').scrollIntoViewIfNeeded();
  assert(await page.locator('.sharing-privacy').evaluate(e => { const r=e.getBoundingClientRect(); return r.bottom <= innerHeight && r.top >= 0; }));
  await page.getByRole('button', { name: 'Accept', exact: true }).scrollIntoViewIfNeeded();
  checks.push('390px layout uses full width with no overflow; privacy footer is scroll-reachable.');
  await page.getByRole('button', { name: 'Accept', exact: true }).click();
  await page.waitForURL(`**/app/documents/${guestId}`);
  assert(requests.some(r => r.path.endsWith('/accept'))); checks.push('Accept submits the invitation and navigates to its manuscript.');
  await page.goto(origin + `/app/documents/${id}/sharing`);
  await page.getByLabel('Email address', { exact: true }).waitFor();
  await page.getByLabel('Email address', { exact: true }).fill('reader@fixture.test');
  await page.getByLabel('Email address', { exact: true }).press('Enter');
  await page.getByRole('status').filter({ hasText: 'Invitation ready.' }).waitFor();
  await page.getByRole('button', { name: 'Cancel invitation', exact: true }).waitFor();
  checks.push('Submitting an invitation with Enter immediately adds its pending row.');
  assert(await page.locator('.collaboration-sharing').evaluate(e => e.scrollWidth <= e.clientWidth));
  await page.screenshot({ path: `${evidence}/owner-mobile.png`, fullPage: true });
  await page.setViewportSize({ width: 1440, height: 1400 });
  await page.screenshot({ path: `${evidence}/owner-desktop.png`, fullPage: true });
  owned = []; shared = []; inbox = [];
  await page.goto(origin + '/app/collaboration');
  await page.getByText('Make room for another perspective', { exact: true }).waitFor();
  await page.screenshot({ path: `${evidence}/empty.png`, fullPage: true }); checks.push('Empty workspace offers Projects and invitation guidance.');
  failure = true;
  await page.getByRole('button', { name: 'Refresh access', exact: true }).click();
  await page.getByRole('alert').waitFor();
  failure = false;
  await page.getByRole('button', { name: 'Refresh access', exact: true }).click();
  await page.getByRole('alert').waitFor({ state: 'hidden' }); checks.push('Failed refresh shows an alert and retry clears it.');
  available = false;
  await page.getByRole('button', { name: 'Refresh access', exact: true }).click();
  await page.getByRole('heading', { name: 'Your writing stays private', exact: true }).waitFor(); checks.push('Unavailable sharing has a distinct private-writing state.');
  seed(); delay = 1800;
  await page.reload();
  await page.getByRole('status').filter({ hasText: 'Loading your sharing workspace' }).waitFor();
  assert.equal(await page.getByRole('heading', { name: 'Your writing stays private', exact: true }).count(), 0);
  await page.getByRole('heading', { name: 'The Atlas of Quiet Places', exact: true }).waitFor(); checks.push('Initial loading never claims sharing is unavailable.');
  delay = 0;
  owned[0].title = 'A very long manuscript title that must wrap clearly without hiding its controls: ' + 'UnbrokenTitle'.repeat(12);
  await page.getByRole('button', { name: 'Refresh access', exact: true }).click();
  await page.getByRole('heading', { name: owned[0].title, exact: true }).waitFor();
  for (const width of [320, 768]) {
    await page.setViewportSize({ width, height: 1000 });
    assert(await page.locator('.collaboration-sharing').evaluate(e => e.scrollWidth <= e.clientWidth));
    await page.locator('.sharing-privacy').scrollIntoViewIfNeeded();
    assert(await page.locator('.sharing-privacy').evaluate(e => { const r=e.getBoundingClientRect(); return r.bottom <= innerHeight && r.top >= 0; }));
  }
  checks.push('Long unbroken titles wrap at 320px and 768px; the footer remains reachable.');
  assert.deepEqual(errors, []);
  await writeFile(`${evidence}/results.json`, JSON.stringify({ checks, errors, boundary: 'Actual Blazor UI with synthetic API responses; no production or native-device acceptance.' }, null, 2));
  console.log(`${checks.length} sharing workspace checks passed.`);
} finally { await browser.close(); await new Promise(r => server.close(r)); }

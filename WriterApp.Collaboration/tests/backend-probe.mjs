// Invoked by the .NET test fixture against its owned Kestrel host and real SQLite database.
import assert from 'node:assert/strict';
import { HocuspocusProvider, HocuspocusProviderWebsocket } from '@hocuspocus/provider';
import * as Y from 'yjs';
import { createCollaborationServer } from '../src/server.mjs';

const base = process.env.PROBE_BACKEND_URL;
const path = process.env.PROBE_SESSION_PATH;
const request = JSON.parse(process.env.PROBE_SESSION_REQUEST);
async function api(route, body, owner = 'owner', key) {
  const response = await fetch(new URL(route, base), { method: 'POST', headers: {
    'Content-Type': 'application/json', 'X-Test-User': owner, ...(key ? { Authorization: `Bearer ${key}` } : {}),
  }, body: JSON.stringify(body) });
  return { status: response.status, body: response.status === 204 ? null : await response.json().catch(() => null) };
}
const server = createCollaborationServer({ backendUrl: base, serviceKey: process.env.COLLABORATION_SERVICE_KEY,
  environment: 'test', allowedOrigins: ['http://localhost:9999'], port: 0, revalidateMilliseconds: 100 });
const providers = [];
async function waitFor(predicate, description) {
  const end = Date.now() + 6000;
  while (Date.now() < end) { if (predicate()) return; await new Promise(resolve => setTimeout(resolve, 20)); }
  throw new Error(`Timed out: ${description}`);
}
function client(ticket, name = ticket.roomName, seed = false) {
  const document = new Y.Doc(); if (seed) document.getText('writing').insert(0, 'forbidden');
  const transport = new HocuspocusProviderWebsocket({ url: `ws://127.0.0.1:${server.address.port}`, WebSocketPolyfill: WebSocket,
    autoConnect: false, maxAttempts: 1, initialDelay: 0, delay: 10000 });
  const status = { synced: false, denied: false, closed: false };
  const provider = new HocuspocusProvider({ websocketProvider: transport, name, document, token: ticket.ticket, awareness: null,
    onSynced: ({ state }) => { if (state) status.synced = true; },
    onAuthenticationFailed: () => { status.denied = true; transport.disconnect(); },
    onClose: () => { status.closed = true; transport.disconnect(); },
  });
  providers.push({ provider, transport, document });
  provider.attach();
  transport.connect().catch(() => {});
  return { provider, transport, document, status };
}
let assertions = 0;
function check(value, expected) { assert.equal(value, expected); assertions++; }
try {
  await server.listen();
  check((await api(path, request, 'anonymous')).status, 401);
  check((await api(path, request, 'other')).status, 403);
  check((await api(path, { ...request, protocolVersion: 99 })).status, 403);
  check((await api(path, { ...request, schemaFingerprint: 'f'.repeat(64) })).status, 403);
  check((await api(path, { ...request, generation: crypto.randomUUID() })).status, 403);
  const issued = await api(path, request); check(issued.status, 200); const ticket = issued.body;
  check(ticket.readOnly, true);
  check((await api('/api/internal/collaboration/authorize', { ticket: ticket.ticket, roomName: ticket.roomName, connectionId: crypto.randomUUID(), acquire: true })).status, 401);
  const forged = client({ ...ticket, ticket: `${ticket.ticket.slice(0, -4)}XXXX` });
  await waitFor(() => forged.status.denied, 'forged ticket rejection'); assertions++;
  const wrongRoom = client(ticket, ticket.roomName.replace(request.generation, crypto.randomUUID()));
  await waitFor(() => wrongRoom.status.denied, 'cross-room replay rejection'); assertions++;
  const wrongEnvironment = client(ticket, ticket.roomName.replace('test/', 'prod/'));
  await waitFor(() => wrongEnvironment.status.denied, 'cross-environment replay rejection'); assertions++;
  for (const index of [1, 2]) {
    const parts = ticket.roomName.split('/'); parts[index] = crypto.randomUUID();
    const crossScope = client(ticket, parts.join('/'));
    await waitFor(() => crossScope.status.denied, 'cross-document/page replay rejection'); assertions++;
  }
  const good = client(ticket); await waitFor(() => good.status.synced, 'read-only provider handshake'); assertions++;
  check(server.hocuspocus.documents.get(ticket.roomName).getText('writing').toString(), '');
  const replay = client(ticket); await waitFor(() => replay.status.denied, 'active-ticket replay rejection'); assertions++;
  const invalidRefresh = await api(`${path}/${ticket.sessionId}/refresh`, { version: ticket.version }, 'other'); check(invalidRefresh.status, 403);
  const refresh = await api(`${path}/${ticket.sessionId}/refresh`, { version: ticket.version }); check(refresh.status, 200);
  check((await api(`${path}/${ticket.sessionId}/refresh`, { version: ticket.version })).status, 403);
  await waitFor(() => good.status.closed, 'refresh closes idle prior socket'); assertions++;
  const expiredVersion = client(ticket); await waitFor(() => expiredVersion.status.denied, 'old-ticket reconnect rejection'); assertions++;
  const refreshed = client(refresh.body); await waitFor(() => refreshed.status.synced, 'refreshed ticket reconnect'); assertions++;
  refreshed.document.getText('writing').insert(0, 'forbidden update');
  await waitFor(() => refreshed.status.closed, 'raw provider text update closes connection'); assertions++;
  const next = await api(`${path}/${ticket.sessionId}/refresh`, { version: refresh.body.version }); check(next.status, 200);
  const seeded = client(next.body, next.body.roomName, true);
  await waitFor(() => seeded.status.closed, 'preloaded SyncStep2 write rejection'); assertions++;
  const finalTicket = (await api(`${path}/${ticket.sessionId}/refresh`, { version: next.body.version })).body;
  const final = client(finalTicket); await waitFor(() => final.status.synced, 'final readonly session'); assertions++;
  check(final.document.getText('writing').toString(), '');
  check((await api(`${path}/${ticket.sessionId}/revoke`, {}, 'other')).status, 403);
  check((await api(`${path}/${ticket.sessionId}/revoke`, {})).status, 204);
  await waitFor(() => final.status.closed, 'revocation closes idle socket'); assertions++;
  const revoked = client(finalTicket); await waitFor(() => revoked.status.denied, 'revoked-ticket reconnect rejection'); assertions++;
  check((await api(`${path}/${ticket.sessionId}/refresh`, { version: finalTicket.version })).status, 403);
  console.log(`PASS: ${assertions} real .NET / Hocuspocus authorization and wire checks`);
} finally {
  for (const { provider, transport, document } of providers) { provider.destroy(); transport.destroy(); document.destroy(); }
  await server.destroy();
}

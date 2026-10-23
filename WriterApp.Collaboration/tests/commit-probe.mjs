import assert from 'node:assert/strict';
import { fork } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { once } from 'node:events';
import { HocuspocusProvider, HocuspocusProviderWebsocket } from '@hocuspocus/provider';
import * as Y from 'yjs';
import { sha256 } from '../src/commits.mjs';

const base = process.env.PROBE_BACKEND_URL, path = process.env.PROBE_SESSION_PATH, request = JSON.parse(process.env.PROBE_SESSION_REQUEST);
const owned = [], clients = []; let host; let assertions = 0;
function equal(value, expected) { assert.deepEqual(value, expected); assertions++; }
async function waitFor(predicate, description, milliseconds = 8000) {
  const until = Date.now() + milliseconds;
  while (Date.now() < until) { if (predicate()) return; await new Promise(resolve => setTimeout(resolve, 20)); }
  throw new Error(`Timed out: ${description}`);
}
async function api(route, body, internal = false) {
  const response = await fetch(new URL(route, base), { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Test-User': 'owner',
    ...(internal ? { Authorization: `Bearer ${process.env.COLLABORATION_SERVICE_KEY}` } : {}) }, body: JSON.stringify(body) });
  const value = await response.json(); assert.equal(response.status, 200, JSON.stringify(value));
  return route === path ? { ...value, installationId: body.installationId } : value;
}
async function start() {
  const process = fork(fileURLToPath(new URL('./commit-host.mjs', import.meta.url)), { silent: true });
  const current = { process, connections: new Map(), armed: null, port: null, errors: '' }; owned.push(current);
  process.stdout.resume(); process.stderr.on('data', bytes => { current.errors += bytes.toString(); });
  process.on('message', message => {
    if (message.type === 'listening') current.port = message.port;
    if (message.type === 'connection') current.connections.set(message.ticketHash, message.connectionId);
    if (message.type === 'armed') current.armed = message.point;
  });
  await waitFor(() => current.port, 'owned service startup'); return current;
}
async function arm(point) { host.armed = null; host.process.send({ type: 'arm', point }); await waitFor(() => host.armed === point, 'fault armed'); }
async function connect(ticket) {
  const document = new Y.Doc(); const status = { synced: false, closed: false }; const messages = [];
  const transport = new HocuspocusProviderWebsocket({ url: `ws://127.0.0.1:${host.port}`, WebSocketPolyfill: WebSocket, autoConnect: false, maxAttempts: 1, delay: 10000 });
  const provider = new HocuspocusProvider({ websocketProvider: transport, name: ticket.roomName, token: ticket.ticket, document, awareness: null,
    onSynced: ({ state }) => { if (state) status.synced = true; }, onClose: () => { status.closed = true; transport.disconnect(); },
    onStateless: ({ payload }) => { messages.push(JSON.parse(payload)); } });
  const client = { document, transport, provider, ticket, status, messages, host }; clients.push(client);
  provider.attach(); transport.connect().catch(() => {}); await waitFor(() => status.synced, 'provider read handshake');
  await waitFor(() => client.host.connections.has(sha256(Buffer.from(ticket.ticket))), 'connection binding');
  return client;
}
const text = client => client.document.getXmlFragment('default').toString();
function envelope(client, suffix, root = 'default') {
  const draft = new Y.Doc(); Y.applyUpdate(draft, Y.encodeStateAsUpdate(client.document));
  let bytes;
  draft.on('update', update => { bytes = update; });
  if (root === 'default') { const node = draft.getXmlFragment('default').get(0).get(0); node.insert(node.length, suffix); }
  else draft.getText(root).insert(0, suffix);
  const ids = client.ticket.roomName.split('/');
  const value = { type: 'commitUpdate', protocolVersion: 1, documentId: ids[1], pageId: ids[2], generation: ids[3], installationId: client.ticket.installationId,
    updateId: crypto.randomUUID(), update: Buffer.from(bytes).toString('base64'), sha256: sha256(bytes) };
  draft.destroy(); return value;
}
async function send(client, value, expected = 'durableReceipt') {
  const from = client.messages.length; client.provider.sendStateless(JSON.stringify(value));
  await waitFor(() => client.messages.slice(from).some(message => message.type === expected && (message.receipt?.updateId ?? message.updateId) === value.updateId), expected);
  return client.messages.slice(from).find(message => message.type === expected && (message.receipt?.updateId ?? message.updateId) === value.updateId);
}
async function bootstrap(client) {
  return api('/api/internal/collaboration/bootstrap', { ticket: client.ticket.ticket, roomName: client.ticket.roomName,
    connectionId: client.host.connections.get(sha256(Buffer.from(client.ticket.ticket))) }, true);
}
async function refresh(ticket) { return { ...await api(`${path}/${ticket.sessionId}/refresh`, { version: ticket.version }), installationId: ticket.installationId }; }
try {
  host = await start();
  const ticketA = await api(path, request), ticketB = await api(path, { ...request, installationId: crypto.randomUUID() });
  equal(ticketA.readOnly, false);
  let a = await connect(ticketA), b = await connect(ticketB);
  const first = envelope(a, ' A'); const firstReceipt = (await send(a, first)).receipt;
  equal(firstReceipt.commitSequence, 1);
  await waitFor(() => text(b).includes(' A'), 'committed peer broadcast'); assertions++;
  equal((await bootstrap(a)).committedSequence, 1);
  equal((await send(a, first)).receipt, firstReceipt);
  equal((await send(b, first, 'commitRejected')).code, 'collaboration_update_invalid');
  const invalid = envelope(a, ' invalid'); invalid.sha256 = 'f'.repeat(64);
  equal((await send(a, invalid, 'commitRejected')).code, 'collaboration_update_invalid');
  equal((await send(a, envelope(a, ' unknown', 'private-root'), 'commitRejected')).code, 'collaboration_update_unsupported');
  const reused = { ...envelope(a, ' reused'), updateId: first.updateId };
  equal((await send(a, reused, 'commitRejected')).code, 'collaboration_update_identity_reused');
  equal((await bootstrap(a)).committedSequence, 1);
  equal(text(a).includes('invalid'), false); equal(text(b).includes('unknown'), false);
  const second = envelope(b, ' B'); equal((await send(b, second)).receipt.commitSequence, 2);
  const compacted = await bootstrap(b); equal(compacted.snapshotSequence, 2); equal(compacted.updates.length, 1);
  // Receipt identity survives compaction and a retry never appends the old edit again.
  equal((await send(a, first)).receipt, firstReceipt);
  const third = envelope(a, ' C'), fourth = envelope(b, ' D');
  const concurrent = await Promise.all([send(a, third), send(b, fourth)]);
  equal(concurrent.map(item => item.receipt.commitSequence).sort(), [3, 4]);
  await waitFor(() => text(a).includes(' C') && text(a).includes(' D') && text(a) === text(b), 'serialized concurrent delivery'); assertions++;
  const readonly = await connect(await api(path, { ...request, requestCommit: false }));
  readonly.provider.sendStateless(JSON.stringify(envelope(readonly, ' forbidden-readonly')));
  await waitFor(() => readonly.status.closed, 'read-only ticket custom channel rejection'); assertions++;
  const raw = await connect(await api(path, request));
  raw.document.getXmlFragment('default').get(0).get(0).insert(0, 'raw-bypass');
  await waitFor(() => raw.status.closed, 'stock update rejection while custom commits enabled'); assertions++;
  equal((await bootstrap(a)).committedSequence, 4);
  // Die in a separate Node process after the database commit, before canonical apply or receipt.
  const lostAck = envelope(a, ' recovered-after-crash'); await arm('afterPersist');
  const priorMessages = a.messages.length; a.provider.sendStateless(JSON.stringify(lostAck));
  await waitFor(() => host.process.exitCode !== null, 'process death after persist'); equal(host.process.exitCode, 91);
  equal(a.messages.slice(priorMessages).some(message => message.type === 'durableReceipt'), false);
  equal(text(b).includes('recovered-after-crash'), false);
  const persisted = await bootstrap(a); equal(persisted.committedSequence, 5);
  console.log('Verified persisted-before-apply process crash; waiting for the expired writer lease before takeover.');
  await new Promise(resolve => setTimeout(resolve, 31000));
  host = await start();
  a = await connect(await refresh(a.ticket)); b = await connect(await refresh(b.ticket));
  equal(text(a).includes('recovered-after-crash'), true); equal(text(a), text(b));
  equal((await send(a, lostAck)).receipt.commitSequence, 5);
  equal((await bootstrap(a)).committedSequence, 5);
  const sixth = envelope(a, ' after-restart'); equal((await send(a, sixth)).receipt.commitSequence, 6);
  equal((await bootstrap(a)).snapshotSequence, 6);
  // A failure before commit never reaches peers or the ledger; retry immutable bytes after room reload.
  const before = envelope(a, ' before-persist-retry'); await arm('beforePersist');
  a.provider.sendStateless(JSON.stringify(before)); await waitFor(() => a.status.closed, 'before-persist failure closes room');
  equal((await bootstrap(a)).committedSequence, 6);
  a = await connect(await refresh(a.ticket)); equal(text(a).includes('before-persist-retry'), false);
  equal((await send(a, before)).receipt.commitSequence, 7);
  // Apply/broadcast can succeed while receipt delivery fails; retry still returns the original commit.
  const after = envelope(a, ' after-apply-retry'); await arm('afterApply');
  a.provider.sendStateless(JSON.stringify(after)); await waitFor(() => a.status.closed, 'after-apply failure closes room');
  equal((await bootstrap(a)).committedSequence, 8);
  a = await connect(await refresh(a.ticket)); equal(text(a).includes('after-apply-retry'), true);
  equal((await send(a, after)).receipt.commitSequence, 8);
  const readyResponse = await fetch(`http://127.0.0.1:${host.port}/health/ready`);
  equal(readyResponse.status, 200); equal((await readyResponse.json()).customCommits, true);
  console.log(`PASS: ${assertions} durable transport, process-crash, receipt, compaction and wire checks`);
} finally {
  for (const client of clients) { client.provider.destroy(); client.transport.destroy(); client.document.destroy(); }
  for (const child of owned) if (child.process.exitCode === null) {
    child.process.send({ type: 'stop' });
    await Promise.race([once(child.process, 'exit'), new Promise(resolve => setTimeout(resolve, 2000))]);
    if (child.process.exitCode === null) { child.process.kill(); await once(child.process, 'exit'); }
  }
}

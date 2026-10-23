import test from 'node:test';
import assert from 'node:assert/strict';
import * as encoding from 'lib0/encoding';
import { createServer, request } from 'node:http';
import { once } from 'node:events';
import { HocuspocusProvider, HocuspocusProviderWebsocket } from '@hocuspocus/provider';
import * as Y from 'yjs';
import { requireReadOnlyFrame, createCollaborationServer } from '../src/server.mjs';

const room = 'test/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222/33333333-3333-3333-3333-333333333333';
function frame(type, subtype, payload, target = room) {
  const e = encoding.createEncoder(); encoding.writeVarString(e, target); encoding.writeVarUint(e, type);
  if (subtype !== undefined) encoding.writeVarUint(e, subtype);
  if (payload !== undefined) encoding.writeVarUint8Array(e, payload);
  return encoding.toUint8Array(e);
}
test('only read requests and empty sync acknowledgements pass the wire gate', () => {
  for (const type of [0, 4]) {
    assert.doesNotThrow(() => requireReadOnlyFrame(frame(type, 0, new Uint8Array([0])), room));
    assert.doesNotThrow(() => requireReadOnlyFrame(frame(type, 1, new Uint8Array([0, 0])), room));
    assert.throws(() => requireReadOnlyFrame(frame(type, 2, new Uint8Array([0, 0])), room));
    assert.throws(() => requireReadOnlyFrame(frame(type, 1, new Uint8Array([0, 1])), room));
  }
  for (const type of [1, 2, 5, 6, 8, 9, 10, 99]) assert.throws(() => requireReadOnlyFrame(frame(type), room));
  for (const type of [3, 7]) assert.doesNotThrow(() => requireReadOnlyFrame(frame(type), room));
});
test('malformed, oversized, appended, and cross-room frames fail closed', () => {
  for (const bytes of [new Uint8Array(), new Uint8Array([255]), new Uint8Array(1048577), frame(0, 0, new Uint8Array([255])),
    frame(0, 0, new Uint8Array([0, 1])), frame(3, undefined, undefined, 'other'), new Uint8Array([...frame(3), 0])]) assert.throws(() => requireReadOnlyFrame(bytes, room));
});
test('service refuses insecure remote backends and missing credentials', () => {
  for (const config of [{ backendUrl: 'http://remote.example/' }, { serviceKey: 'short' }, { environment: '../prod' }, { backendUrl: 'https://example.com/?token=secret' }]) {
    assert.throws(() => createCollaborationServer({ backendUrl: 'http://localhost/', serviceKey: 'k'.repeat(32), environment: 'test', ...config }));
  }
});

test('browser origin and URL credential checks run before WebSocket upgrade', async () => {
  const server = createCollaborationServer({ backendUrl: 'http://127.0.0.1/', serviceKey: 'k'.repeat(32),
    environment: 'test', allowedOrigins: ['http://localhost:9999'], port: 0 });
  try {
    await server.listen();
    async function upgrade(path, origin) {
      return new Promise((resolve, reject) => {
        const req = request({ hostname: '127.0.0.1', port: server.address.port, path, headers: { Connection: 'Upgrade', Upgrade: 'websocket',
          'Sec-WebSocket-Version': '13', 'Sec-WebSocket-Key': Buffer.from('1234567890123456').toString('base64'), Origin: origin } });
        req.on('response', response => { response.resume(); resolve(response.statusCode); });
        req.on('upgrade', (response, socket) => { socket.destroy(); resolve(response.statusCode); });
        req.on('error', reject); req.end();
      });
    }
    assert.equal(await upgrade('/', 'https://attacker.example'), 403);
    assert.equal(await upgrade('/?token=do-not-log', 'http://localhost:9999'), 403);
    assert.equal(await upgrade('/', 'http://localhost:9999'), 101);
  } finally { await server.destroy(); }
});

test('idle sockets close when the authorization backend fails', { timeout: 10000 }, async () => {
  let healthy = true;
  const backend = createServer(async (req, res) => {
    for await (const ignored of req) { /* drain the small owned request */ }
    res.setHeader('Content-Type', 'application/json');
    if (!healthy) { res.writeHead(503); res.end('{}'); return; }
    res.end(JSON.stringify(req.url.endsWith('bootstrap') ? { updates: ['AAA='], committedSequence: 0 } : { readOnly: true }));
  });
  backend.listen(0, '127.0.0.1'); await once(backend, 'listening');
  const server = createCollaborationServer({ backendUrl: `http://127.0.0.1:${backend.address().port}/`, serviceKey: 'k'.repeat(32), environment: 'test', port: 0, revalidateMilliseconds: 100 });
  let inbound = 0;
  server.hocuspocus.configuration.extensions.unshift({ async beforeHandleMessage() { inbound++; } });
  const document = new Y.Doc(); let provider, transport;
  try {
    await server.listen();
    transport = new HocuspocusProviderWebsocket({ url: `ws://127.0.0.1:${server.address.port}`, WebSocketPolyfill: WebSocket, autoConnect: false, maxAttempts: 1 });
    provider = new HocuspocusProvider({ websocketProvider: transport, name: room, token: 'synthetic-ticket', document, awareness: null });
    const synced = new Promise(resolve => provider.on('synced', ({ state }) => { if (state) resolve(); }));
    provider.attach(); transport.connect().catch(() => {});
    await synced;
    // Let the final handshake acknowledgements drain; closure must then need no inbound message.
    await new Promise(resolve => setTimeout(resolve, 200));
    const beforeFailure = inbound;
    const closed = new Promise(resolve => provider.on('close', () => { transport.disconnect(); resolve(); }));
    healthy = false;
    await closed;
    assert.equal(inbound, beforeFailure);
  } finally {
    provider?.destroy(); transport?.destroy(); document.destroy(); await server.destroy();
    backend.closeAllConnections(); await new Promise(resolve => backend.close(resolve));
  }
});

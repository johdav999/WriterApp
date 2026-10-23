import { Server } from '@hocuspocus/server';
import * as Y from 'yjs';
import * as decoding from 'lib0/decoding';
import { CommitRooms, CommitError } from './commits.mjs';
import { PresenceRooms } from './presence.mjs';
import { getSupport, validateCommittedDoc } from './generated/schema.mjs';

const denied = () => Object.assign(new Error('Collaboration session denied'), { code: 4403, reason: 'session_denied' });

// Defense in depth: Hocuspocus readOnly alone accepts several mutating message types.
export function requireReadOnlyFrame(bytes, room) {
  try {
    if (bytes.byteLength > 1024 * 1024) throw denied();
    const decoder = decoding.createDecoder(bytes);
    if (decoding.readVarString(decoder).split('\0')[0] !== room) throw denied();
    const type = decoding.readVarUint(decoder);
    if (type === 0 || type === 4) {
      const subtype = decoding.readVarUint(decoder);
      const payload = decoding.readVarUint8Array(decoder);
      if (subtype !== 0 && !(subtype === 1 && payload.length === 2 && payload[0] === 0 && payload[1] === 0)) throw denied();
      if (subtype === 0) {
        // Parse the state vector before it reaches Yjs; malformed vectors fail closed.
        const vector = decoding.createDecoder(payload);
        const count = decoding.readVarUint(vector);
        if (count > 65536) throw denied();
        for (let index = 0; index < count; index++) { decoding.readVarUint(vector); decoding.readVarUint(vector); }
        if (decoding.hasContent(vector)) throw denied();
      }
    } else if (type !== 3 && type !== 7) throw denied();
    if (decoding.hasContent(decoder)) throw denied();
  } catch { throw denied(); }
}

export function createCollaborationServer({ backendUrl, serviceKey, environment, allowedOrigins = [], port = 1234,
  address = '127.0.0.1', revalidateMilliseconds = 1000, commitsEnabled = false, interactionsEnabled = false, checkpointEvery = 64, fault }) {
  const backend = new URL(backendUrl);
  if (!(backend.protocol === 'https:' || (backend.protocol === 'http:' && ['127.0.0.1', 'localhost', '[::1]'].includes(backend.hostname)))
      || backend.username || backend.password || backend.search || backend.hash || backend.pathname !== '/'
      || typeof serviceKey !== 'string' || serviceKey.length < 32 || !/^[A-Za-z0-9_-]{1,64}$/.test(environment)
      || typeof commitsEnabled !== 'boolean' || typeof interactionsEnabled !== 'boolean' || !Number.isInteger(revalidateMilliseconds) || revalidateMilliseconds < 100 || revalidateMilliseconds > 1000) {
    throw new Error('Invalid collaboration configuration');
  }
  const origins = new Set(allowedOrigins);
  for (const origin of origins) if (new URL(origin).origin !== origin) throw new Error('Invalid allowed origin');
  const active = new Map();
  const validationDue = new WeakMap();
  const heartbeats = new WeakMap();
  const scope = (context, acquire = false) => ({ ticket: context.ticket, roomName: context.room, connectionId: context.connectionId, acquire });
  async function call(path, body) {
    try {
      const response = await fetch(new URL(`api/internal/collaboration/${path}`, backend), {
        method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${serviceKey}` },
        body: JSON.stringify(body),
        signal: AbortSignal.timeout(3000), redirect: 'error',
      });
      // Bootstrap is bounded by .NET to 16 MiB binary; prevent an unbounded upstream response too.
      const reader = response.body.getReader(); let size = 0; const chunks = [];
      for (;;) { const { done, value } = await reader.read(); if (done) break;
        size += value.length; if (size > 24 * 1024 * 1024) { await reader.cancel(); throw denied(); } chunks.push(value); }
      const result = JSON.parse(Buffer.concat(chunks).toString('utf8'));
      if (!response.ok) throw new CommitError(/^collaboration_[a-z_]+$/.test(result.code) ? result.code : 'collaboration_storage_unavailable', response.status >= 500);
      if (path === 'authorize' && result.readOnly !== true) throw denied();
      return result;
    } catch (error) { if (error instanceof CommitError) throw error; throw new CommitError('collaboration_storage_unavailable', true); }
  }
  const rooms = commitsEnabled ? new CommitRooms({ call, checkpointEvery, fault }) : null;
  const presence = new PresenceRooms(session => call('authorize', scope(session)));
  let readyAt = 0;
  const server = new Server({
    // crossws sends transport pings every 30 seconds; leave time for idle read-only peers to pong.
    port, address, quiet: true, stopOnSignals: false, timeout: 60000,
    websocketOptions: { maxPayload: 1024 * 1024 },
    maxPendingDocuments: 1, maxUnauthenticatedQueueSize: 16384, maxUnauthenticatedQueueMessages: 4,
    async onUpgrade({ request, socket }) {
      // Never accept credentials in a URL. Native clients may omit Origin, but still need a scoped ticket.
      if (request.url !== '/' || (request.headers.origin && !origins.has(request.headers.origin))) {
        socket.end('HTTP/1.1 403 Forbidden\r\nConnection: close\r\n\r\n'); throw null;
      }
    },
    async onAuthenticate({ token, documentName, socketId, connectionConfig }) {
      const parts = documentName.split('/');
      const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
      if (parts.length !== 4 || parts[0] !== environment || !parts.slice(1).every(x => guid.test(x))
          || typeof token !== 'string' || token.length > 8192) throw denied();
      const session = { ticket: token, room: documentName, connectionId: socketId };
      session.authority = await call('authorize', scope(session, true));
      connectionConfig.readOnly = true;
      return { session };
    },
    async onLoadDocument({ context, document }) {
      const bootstrap = await call('bootstrap', scope(context.session));
      if (!Array.isArray(bootstrap.updates) || !bootstrap.updates.length || bootstrap.updates.length > 4097
          || !Number.isSafeInteger(bootstrap.committedSequence) || bootstrap.committedSequence < 0) throw denied();
      const candidate = new Y.Doc();
      try {
        let size = 0;
        for (const encoded of bootstrap.updates) {
          if (typeof encoded !== 'string' || !/^[A-Za-z0-9+/]+={0,2}$/.test(encoded)) throw denied();
          const bytes = Buffer.from(encoded, 'base64'); size += bytes.length;
          if (size > 16 * 1024 * 1024) throw denied();
          Y.applyUpdate(candidate, bytes);
        }
        if (candidate.store.pendingStructs || candidate.store.pendingDs) throw denied();
        if (interactionsEnabled) {
          const support = await getSupport();
          if (Object.keys(support).some(key => context.session.authority[key] !== support[key])) throw denied();
          validateCommittedDoc(candidate);
        }
        Y.applyUpdate(document, Y.encodeStateAsUpdate(candidate));
        if (rooms) await rooms.load(document, bootstrap, context.session);
      } catch { throw denied(); } finally { candidate.destroy(); }
    },
    async connected({ context, connection }) {
      active.set(connection, context.session);
      validationDue.set(connection, Date.now() + revalidateMilliseconds);
      // Recheck after asynchronous loading before allowing the session to remain connected.
      try { await call('authorize', scope(context.session)); } catch { connection.close(denied()); }
    },
    async beforeHandleMessage({ context, update, documentName, connection, document }) {
      if (update.byteLength <= 4096) {
        const decoder = decoding.createDecoder(update);
        if (decoding.readVarString(decoder).split('\0')[0] !== documentName) throw denied();
        if (decoding.readVarUint(decoder) === 5) {
          const value = JSON.parse(decoding.readVarString(decoder));
          if (decoding.hasContent(decoder)) throw denied();
          if (value?.type === 'heartbeat') {
            if (Object.keys(value).length !== 1) throw denied();
            const now = Date.now(); if (now - (heartbeats.get(connection) ?? 0) < 1000) return;
            await call('authorize', scope(context.session)); heartbeats.set(connection, now);
            connection.sendStateless(JSON.stringify({type:'heartbeat'})); return;
          }
          if (interactionsEnabled && value?.type === 'presence') { await presence.update(connection, context.session, document, value); return; }
        }
      }
      if (rooms && context.session.authority.commitAllowed && update.byteLength <= 1024 * 1024) {
        const decoder = decoding.createDecoder(update);
        if (decoding.readVarString(decoder).split('\0')[0] !== documentName) throw denied();
        if (decoding.readVarUint(decoder) === 5) {
          const value = JSON.parse(decoding.readVarString(decoder));
          if (decoding.hasContent(decoder)) throw denied();
          try {
            const receipt = await rooms.commit(context.session, value);
            connection.sendStateless(JSON.stringify({ type: 'durableReceipt', receipt }));
          } catch (error) {
            connection.sendStateless(JSON.stringify({ type: 'commitRejected', updateId: typeof value?.updateId === 'string' && value.updateId.length === 36 ? value.updateId : null,
              code: error instanceof CommitError ? error.code : 'collaboration_reload_required', preserveLocalDraft: true }));
            if (!(error instanceof CommitError) || error.uncertain) connection.close({ code: 4410, reason: 'reload_committed_state' });
          }
          return; // The library's default stateless handler performs no mutation; this is the only commit handler.
        }
      }
      requireReadOnlyFrame(update, documentName);
      await call('authorize', scope(context.session));
    },
    async onTokenSync() { throw denied(); }, // Refresh through .NET, then reconnect with the new ticket.
    async onDisconnect({ context }) {
      for (const [connection, session] of active) if (session === context.session) { active.delete(connection); await presence.remove(connection); }
    },
    async afterUnloadDocument({ documentName }) { rooms?.remove(documentName); },
    async onRequest({ request, response }) {
      const ready = Date.now() - readyAt < 5000 && ![...(rooms?.rooms.values() ?? [])].some(room => room.failed);
      if (request.url === '/health/live' || request.url === '/health/ready') {
        response.writeHead(request.url === '/health/live' || ready ? 200 : 503, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
        response.end(JSON.stringify({ live: true, ready, customCommits: commitsEnabled }));
      } else { response.writeHead(404); response.end(); }
      throw null;
    },
  });
  // Spread idle authorization over short ticks instead of issuing every room's
  // database queries at once. Start checks at most one tick EARLY, never later
  // than the existing revalidation interval. Commits still authorize atomically.
  const pollMilliseconds = Math.min(100, revalidateMilliseconds);
  let polling = false, maintenanceDue = 0;
  const timer = setInterval(async () => {
    if (polling) return; polling = true;
    try {
      await Promise.all([...active].map(async ([connection, session]) => {
        if ((validationDue.get(connection) ?? 0) > Date.now() + pollMilliseconds) return;
        validationDue.set(connection, Date.now() + revalidateMilliseconds);
        try { const authority = await call('authorize', scope(session)); if (authority.interactionsEnabled !== true) await presence.disable(connection); }
        catch { active.delete(connection); await presence.remove(connection); connection.close(denied()); }
      }));
      if (Date.now() >= maintenanceDue) {
        maintenanceDue = Date.now() + revalidateMilliseconds;
        await rooms?.renew([...active.values()]);
        await presence.expire();
        try { const health = await call('health', {}); readyAt = health.ready && (!commitsEnabled || health.commitsEnabled) ? Date.now() : 0; }
        catch { readyAt = 0; }
      }
    } finally { polling = false; }
  }, pollMilliseconds);
  timer.unref();
  const destroy = server.destroy.bind(server);
  server.destroy = async () => { clearInterval(timer); active.clear(); presence.clear(); await destroy(); };
  return server;
}


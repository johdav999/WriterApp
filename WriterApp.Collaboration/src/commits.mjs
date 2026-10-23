import { randomUUID, createHash } from 'node:crypto';
import * as Y from 'yjs';
import { getSupport, validateCommittedDoc } from './generated/schema.mjs';

export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export class CommitError extends Error {
  constructor(code, uncertain = false) { super(code); this.code = code; this.uncertain = uncertain; }
}

export function decodeEnvelope(value, session) {
  const fields = ['type', 'protocolVersion', 'documentId', 'pageId', 'generation', 'installationId', 'updateId', 'update', 'sha256'];
  if (!value || typeof value !== 'object' || Array.isArray(value) || Object.keys(value).length !== fields.length
      || Object.keys(value).some(key => !fields.includes(key)) || value.type !== 'commitUpdate' || value.protocolVersion !== 1
      || ![value.documentId, value.pageId, value.generation, value.installationId, value.updateId].every(id => typeof id === 'string' && uuid.test(id))
      || `${session.room.split('/')[0]}/${value.documentId}/${value.pageId}/${value.generation}` !== session.room
      || value.installationId !== session.authority.installationId || typeof value.update !== 'string' || value.update.length > 699052
      || !/^[A-Za-z0-9+/]+={0,2}$/.test(value.update) || typeof value.sha256 !== 'string' || !/^[0-9a-f]{64}$/.test(value.sha256))
    throw new CommitError('collaboration_update_invalid');
  const bytes = Buffer.from(value.update, 'base64');
  if (!bytes.length || bytes.length > 512 * 1024 || bytes.toString('base64') !== value.update || sha256(bytes) !== value.sha256)
    throw new CommitError('collaboration_update_invalid');
  return { ...value, bytes };
}

export class CommitRooms {
  constructor({ call, checkpointEvery = 64, fault = async () => {} }) {
    if (!Number.isInteger(checkpointEvery) || checkpointEvery < 1 || checkpointEvery > 1024) throw new Error('Invalid checkpoint interval');
    this.call = call; this.checkpointEvery = checkpointEvery; this.fault = fault;
    this.workerId = randomUUID(); this.rooms = new Map();
  }
  async load(document, bootstrap, session) {
    const support = await getSupport();
    if (session.authority.schemaFingerprint !== support.schemaFingerprint || session.authority.protocolVersion !== support.protocolVersion
        || session.authority.contentContractVersion !== support.contentContractVersion) throw new CommitError('collaboration_upgrade_required');
    validateCommittedDoc(document);
    const room = { document, sequence: bootstrap.committedSequence, snapshot: bootstrap.snapshotSequence ?? 0,
      lease: null, tail: Promise.resolve(), pending: 0, failed: false };
    // A writable room is ready only after acquiring its fenced writer authority.
    // Keep the first edit off the cold lease-acquisition path; read-only rooms
    // never acquire a writer lease. Commit still checks policy and epoch in SQL.
    try { if (session.authority.commitAllowed) await this.lease(room, session); }
    catch (error) { room.failed = true; throw error; }
    this.rooms.set(document.name, room);
  }
  remove(name) { const room = this.rooms.get(name); if (room) room.failed = true; this.rooms.delete(name); }
  quarantine(room) {
    room.failed = true;
    for (const connection of room.document.getConnections()) connection.close({ code: 4410, reason: 'reload_committed_state' });
  }
  enqueue(room, work) {
    if (!room || room.failed) return Promise.reject(new CommitError('collaboration_room_unavailable', true));
    if (room.pending >= 32) return Promise.reject(new CommitError('collaboration_busy'));
    room.pending++;
    const task = room.tail.then(async () => {
      if (room.failed) throw new CommitError('collaboration_room_unavailable', true);
      return work();
    });
    room.tail = task.catch(() => {}).finally(() => { room.pending--; });
    return task;
  }
  async lease(room, session) {
    if (!room.lease || room.lease.expiresAtUnixMilliseconds - Date.now() < 10000) {
      const lease = await this.call('lease', { session: this.scope(session), workerId: this.workerId, epoch: room.lease?.epoch ?? null });
      if (!Number.isSafeInteger(lease.epoch) || lease.epoch < 1 || lease.committedSequence !== room.sequence
          || !Number.isSafeInteger(lease.expiresAtUnixMilliseconds) || lease.expiresAtUnixMilliseconds <= Date.now())
        throw new CommitError('collaboration_lease_lost', true);
      room.lease = lease;
    }
  }
  scope(session) { return { ticket: session.ticket, roomName: session.room, connectionId: session.connectionId, acquire: false }; }
  commit(session, value) {
    if (!session.authority.commitAllowed) return Promise.reject(new CommitError('collaboration_commits_disabled'));
    const envelope = decodeEnvelope(value, session);
    const room = this.rooms.get(session.room);
    return this.enqueue(room, async () => {
      let candidate;
      try {
        // Lease/commit authorize against current policy inside the durable transaction,
        // after queueing. An additional HTTP policy read here cannot grant the commit.
        candidate = new Y.Doc();
        Y.applyUpdate(candidate, Y.encodeStateAsUpdate(room.document));
        try { Y.applyUpdate(candidate, envelope.bytes); validateCommittedDoc(candidate); }
        catch { throw new CommitError('collaboration_update_unsupported'); }
        if (Y.encodeStateAsUpdate(candidate).length > 16 * 1024 * 1024) throw new CommitError('collaboration_page_too_large');
        await this.lease(room, session);
        await this.fault('beforePersist', { room, envelope });
        const result = await this.call('commit', { session: this.scope(session), workerId: this.workerId, epoch: room.lease.epoch,
          expectedSequence: room.sequence, updateId: envelope.updateId, update: envelope.update, sha256: envelope.sha256 });
        // No canonical mutation or broadcast has happened above this line.
        await this.fault('afterPersist', { room, envelope, result });
        const receipt = result.receipt;
        if (!receipt || receipt.updateId !== envelope.updateId || receipt.installationId !== envelope.installationId
            || receipt.pageId !== envelope.pageId || receipt.generation !== envelope.generation || receipt.sha256 !== envelope.sha256
            || !Number.isSafeInteger(receipt.commitSequence) || receipt.commitSequence < 1 || receipt.commitSequence > result.committedSequence
            || !Number.isSafeInteger(receipt.committedAtUnixMilliseconds)
            || ![room.sequence, room.sequence + 1].includes(result.committedSequence)) throw new CommitError('collaboration_receipt_invalid', true);
        Y.applyUpdate(room.document, envelope.bytes, 'durable-commit');
        room.sequence = result.committedSequence;
        await this.fault('afterApply', { room, envelope, result });
        if (room.sequence - room.snapshot >= this.checkpointEvery) await this.checkpoint(room, session);
        return receipt;
      } catch (error) {
        if (!(error instanceof CommitError) || error.uncertain || !['collaboration_update_unsupported', 'collaboration_update_invalid', 'collaboration_page_too_large', 'collaboration_update_identity_reused'].includes(error.code)) {
          this.quarantine(room);
          throw new CommitError('collaboration_reload_required', true);
        }
        throw error;
      } finally { candidate?.destroy(); }
    });
  }
  async checkpoint(room, session) {
    const state = Y.encodeStateAsUpdate(room.document);
    const result = await this.call('checkpoint', { session: this.scope(session), workerId: this.workerId, epoch: room.lease.epoch,
      expectedSequence: room.sequence, state: Buffer.from(state).toString('base64'), sha256: sha256(state) });
    if (result.snapshotSequence !== room.sequence) throw new CommitError('collaboration_checkpoint_invalid', true);
    room.snapshot = result.snapshotSequence;
  }
  async renew(sessions) {
    await Promise.all([...this.rooms.values()].filter(room => room.lease && !room.failed).map(room => {
      const session = sessions.find(s => s.room === room.document.name && s.authority.commitAllowed);
      if (!session) return;
      return this.enqueue(room, async () => { try { await this.lease(room, session); } catch { this.quarantine(room); } }).catch(() => {});
    }));
  }
}

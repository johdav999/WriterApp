import { resolveAnchor } from './anchors.mjs';

// Never put presence in the Y.Doc or journal. Identity and connection ID are server supplied.
export class PresenceRooms {
  constructor(authorize, now = Date.now) { this.entries = new Map(); this.authorize = authorize; this.now = now; }
  async update(connection, session, document, value) {
    if (!value || Object.keys(value).sort().join(',') !== 'cursor,type' || value.type !== 'presence') throw Error('collaboration_presence_invalid');
    const previous = this.entries.get(connection), now = this.now();
    // Network jitter can bunch legitimate cursor updates together. Drop excess ephemeral work;
    // do not interrupt the independent durable manuscript session for a harmless burst.
    if (previous && now - previous.received < 100) return;
    if (value.cursor !== null && resolveAnchor(document, value.cursor, true).detached) throw Error('collaboration_presence_invalid');
    const authority = await this.authorize(session);
    if (authority.interactionsEnabled !== true) { await this.disable(connection); return; }
    if (typeof authority.actorId !== 'string' || authority.actorId.length > 128) throw Error('collaboration_presence_denied');
    if (!previous && [...this.entries.values()].filter(e => e.session.room === session.room).length >= 64) throw Error('collaboration_presence_limit');
    this.entries.set(connection, { session, actorId: authority.actorId, displayName: this.displayName(authority), cursor: value.cursor, received: now });
    await this.publish(session.room);
  }
  async publish(room) {
    const entries = [...this.entries].filter(([,e]) => e.session.room === room);
    await Promise.all(entries.map(async ([connection,entry]) => {
      try { const grant = await this.authorize(entry.session); if (grant.interactionsEnabled !== true) { this.entries.delete(connection); connection.sendStateless(JSON.stringify({type:'presenceSnapshot',members:[]})); } else { entry.displayName = this.displayName(grant); } }
      catch { this.entries.delete(connection); connection.close({ code: 4403, reason: 'session_denied' }); }
    }));
    const members = [...this.entries].filter(([,e]) => e.session.room === room);
    const payload = JSON.stringify({ type: 'presenceSnapshot', members: members.map(([,e]) => ({ connectionId: e.session.connectionId, actorId: e.actorId, displayName: e.displayName, cursor: e.cursor })) });
    for (const [connection] of members) connection.sendStateless(payload);
  }
  displayName(authority) { return typeof authority.displayName === 'string' && authority.displayName.trim() && authority.displayName.trim() !== authority.actorId ? authority.displayName.trim().slice(0, 256) : 'Collaborator'; }
  async remove(connection) { const entry = this.entries.get(connection); this.entries.delete(connection); if (entry) await this.publish(entry.session.room); }
  async disable(connection) { if (!this.entries.has(connection)) return; connection.sendStateless(JSON.stringify({type:'presenceSnapshot',members:[]})); await this.remove(connection); }
  async expire() {
    const rooms = new Set();
    for (const [connection,entry] of this.entries) if (this.now() - entry.received > 15000) { this.entries.delete(connection); connection.sendStateless(JSON.stringify({type:'presenceSnapshot',members:[]})); rooms.add(entry.session.room); }
    for (const room of rooms) await this.publish(room);
  }
  clear() { this.entries.clear(); }
}

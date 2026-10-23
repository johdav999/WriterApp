import test from 'node:test';
import assert from 'node:assert/strict';
import * as Y from 'yjs';
import { randomUUID } from 'node:crypto';
import { CommitRooms, CommitError, sha256 } from '../src/commits.mjs';
import { createSeed, getSupport } from '../src/generated/schema.mjs';

async function harness({ fault, gate, denyAfterFirstCommit = false, readOnly = false } = {}) {
  const ids = [randomUUID(), randomUUID(), randomUUID()];
  const session = { ticket: 'synthetic', room: `test/${ids.join('/')}`, connectionId: randomUUID(),
    authority: { ...await getSupport(), commitAllowed: !readOnly, installationId: randomUUID() } };
  const document = new Y.Doc(); Y.applyUpdate(document, createSeed({ type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'Seed' }] }] }));
  document.name = session.room; let closed = 0; document.getConnections = () => [{ close() { closed++; } }];
  const storage = { sequence: 0, receipts: [], snapshots: [], leases: [] };
  const manager = new CommitRooms({ fault, checkpointEvery: 2, async call(path, body) {
    if (path === 'authorize') return session.authority;
    if (path === 'lease') { storage.leases.push(body); return { epoch: 1, expiresAtUnixMilliseconds: Date.now() + 30000, committedSequence: storage.sequence }; }
    if (path === 'checkpoint') { storage.snapshots.push(body); return { snapshotSequence: storage.sequence }; }
    if (path === 'commit') {
      if (denyAfterFirstCommit && storage.sequence > 0) throw new CommitError('collaboration_session_denied');
      if (gate) await gate;
      const receipt = { pageId: ids[1], generation: ids[2], installationId: session.authority.installationId,
        updateId: body.updateId, sha256: body.sha256, commitSequence: ++storage.sequence, committedAtUnixMilliseconds: Date.now() };
      storage.receipts.push(receipt); return { receipt, committedSequence: storage.sequence };
    }
    throw new Error('Unexpected test path');
  } });
  await manager.load(document, { committedSequence: 0, snapshotSequence: 0 }, session);
  function envelope(suffix, change) {
    const draft = new Y.Doc(); Y.applyUpdate(draft, Y.encodeStateAsUpdate(document)); let bytes;
    draft.on('update', update => { bytes = update; });
    if (change) change(draft); else { const node = draft.getXmlFragment('default').get(0).get(0); node.insert(node.length, suffix); }
    const value = { type: 'commitUpdate', protocolVersion: 1, documentId: ids[0], pageId: ids[1], generation: ids[2],
      installationId: session.authority.installationId, updateId: randomUUID(), update: Buffer.from(bytes).toString('base64'), sha256: sha256(bytes) };
    draft.destroy(); return value;
  }
  return { session, document, manager, storage, envelope, get closed() { return closed; }, destroy() { document.destroy(); } };
}

test('canonical state and broadcast remain unchanged until persistence completes', async () => {
  let release; const gate = new Promise(resolve => { release = resolve; }); const h = await harness({ gate });
  try {
    let broadcasts = 0; h.document.on('update', () => broadcasts++);
    const update = h.envelope(' persisted'); const task = h.manager.commit(h.session, update);
    await new Promise(resolve => setTimeout(resolve, 20));
    assert.equal(h.storage.sequence, 0); assert.equal(broadcasts, 0); assert.equal(h.document.getXmlFragment('default').toString().includes('persisted'), false);
    release(); const receipt = await task;
    assert.equal(receipt.commitSequence, 1); assert.equal(h.storage.sequence, 1); assert.equal(broadcasts, 1);
  } finally { release(); h.destroy(); }
});

test('writable opening acquires a fenced lease without committing; read-only opening acquires none', async () => {
  const writer = await harness(), reader = await harness({ readOnly: true });
  try {
    assert.equal(writer.storage.leases.length, 1); assert.equal(reader.storage.leases.length, 0);
    assert.equal(writer.storage.sequence, 0); assert.equal(writer.storage.receipts.length, 0);
    assert.equal(reader.storage.sequence, 0); assert.equal(reader.storage.receipts.length, 0);
    await assert.rejects(reader.manager.commit(reader.session, reader.envelope(' denied')), /collaboration_commits_disabled/);
    await writer.manager.commit(writer.session, writer.envelope(' accepted'));
    assert.equal(writer.storage.leases.length, 1); assert.equal(writer.storage.receipts.length, 1);
  } finally { writer.destroy(); reader.destroy(); }
});

for (const point of ['beforePersist', 'afterPersist', 'afterApply']) test(`failure at ${point} quarantines the room with the correct durable prefix`, async () => {
  const h = await harness({ async fault(actual) { if (actual === point) throw new Error('intentional owned test fault'); } });
  try {
    await assert.rejects(h.manager.commit(h.session, h.envelope(' accepted')), /collaboration_reload_required/);
    assert.equal(h.storage.sequence, point === 'beforePersist' ? 0 : 1);
    assert.equal(h.document.getXmlFragment('default').toString().includes('accepted'), point === 'afterApply');
    assert.equal(h.closed, 1);
    await assert.rejects(h.manager.commit(h.session, h.envelope(' later')), /collaboration_room_unavailable/);
  } finally { h.destroy(); }
});

test('unknown schema, malformed updates, unresolved dependencies and unsafe links never persist', async () => {
  const h = await harness();
  try {
    const invalidRoot = h.envelope('', doc => doc.getText('unknown').insert(0, 'unsafe'));
    await assert.rejects(h.manager.commit(h.session, invalidRoot), /collaboration_update_unsupported/);
    const broken = h.envelope('ignored'); broken.update = '////'; broken.sha256 = sha256(Buffer.from(broken.update, 'base64'));
    await assert.rejects(h.manager.commit(h.session, broken), /collaboration_update_unsupported/);
    const draft = new Y.Doc(); Y.applyUpdate(draft, Y.encodeStateAsUpdate(h.document)); const node = draft.getXmlFragment('default').get(0).get(0);
    node.insert(node.length, 'missing predecessor'); let dependent;
    draft.on('update', bytes => { dependent = bytes; }); node.insert(node.length, 'dependent');
    const value = { ...h.envelope('ignored'), update: Buffer.from(dependent).toString('base64'), sha256: sha256(dependent) }; draft.destroy();
    await assert.rejects(h.manager.commit(h.session, value), /collaboration_update_unsupported/);
    const unsafe = h.envelope('', doc => { const text = doc.getXmlFragment('default').get(0).get(0); text.format(0, 1, { link: { href: 'javascript:alert(1)' } }); });
    await assert.rejects(h.manager.commit(h.session, unsafe), /collaboration_update_unsupported/);
    assert.equal(h.storage.sequence, 0); assert.equal(h.closed, 0);
  } finally { h.destroy(); }
});

test('room queue orders concurrent validated commits and snapshots after the committed prefix', async () => {
  const h = await harness();
  try {
    const results = await Promise.all([h.manager.commit(h.session, h.envelope(' A')), h.manager.commit(h.session, h.envelope(' B'))]);
    assert.deepEqual(results.map(r => r.commitSequence), [1, 2]);
    assert.equal(h.storage.snapshots.length, 1); assert.equal(h.storage.snapshots[0].expectedSequence, 2);
    const value = h.document.getXmlFragment('default').toString(); assert.ok(value.includes(' A')); assert.ok(value.includes(' B'));
  } finally { h.destroy(); }
});

test('authority withdrawn at the durable boundary rejects queued work without applying or broadcasting it', async () => {
  let release; const gate = new Promise(resolve => { release = resolve; });
  const h = await harness({ gate, denyAfterFirstCommit: true });
  try {
    let broadcasts = 0; h.document.on('update', () => broadcasts++);
    const first = h.manager.commit(h.session, h.envelope(' accepted'));
    const second = h.manager.commit(h.session, h.envelope(' denied'));
    const rejected = assert.rejects(second, /collaboration_reload_required/);
    release(); await first; await rejected;
    assert.equal(h.storage.sequence, 1); assert.equal(h.storage.receipts.length, 1);
    assert.equal(broadcasts, 1); assert.equal(h.closed, 1);
    assert.ok(h.document.getXmlFragment('default').toString().includes(' accepted'));
    assert.ok(!h.document.getXmlFragment('default').toString().includes(' denied'));
  } finally { release(); h.destroy(); }
});

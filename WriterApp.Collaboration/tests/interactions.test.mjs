import test from 'node:test';
import assert from 'node:assert/strict';
import * as Y from 'yjs';
import { resolveAnchor } from '../src/anchors.mjs';
import { PresenceRooms } from '../src/presence.mjs';

const encode = p => Buffer.from(Y.encodeRelativePosition(p)).toString('base64');
function fixture() {
  const doc = new Y.Doc(), paragraph = new Y.XmlElement('paragraph'), text = new Y.XmlText();
  doc.getXmlFragment('default').insert(0, [paragraph]); paragraph.insert(0, [text]); text.insert(0, 'Hello world');
  const anchor = { start: encode(Y.createRelativePositionFromTypeIndex(text, 1)), end: encode(Y.createRelativePositionFromTypeIndex(text, 5)) };
  return { doc, text, anchor };
}
test('relative passage anchors survive insertion, detach on deletion and reject foreign roots or malformed bytes', () => {
  const { doc, text, anchor } = fixture();
  try {
    assert.equal(resolveAnchor(doc, anchor).detached, false); text.insert(0, 'Remote '); assert.equal(resolveAnchor(doc, anchor).detached, false);
    text.delete(8,4); assert.equal(resolveAnchor(doc, anchor).detached, true);
    assert.throws(() => resolveAnchor(doc, { ...anchor, start: anchor.start + 'garbage' }));
    const foreign = doc.getText('private'); foreign.insert(0,'secret');
    assert.throws(() => resolveAnchor(doc, { start: encode(Y.createRelativePositionFromTypeIndex(foreign,0)), end: encode(Y.createRelativePositionFromTypeIndex(foreign,3)) }));
    assert.throws(() => resolveAnchor(doc, { ...anchor, actorId: 'owner' }));
  } finally { doc.destroy(); }
});
test('presence identity comes only from authority, is ephemeral, room scoped, bounded and never mutates text', async () => {
  const { doc, anchor } = fixture(); let now = 1000; const revoked = new Set();
  const rooms = new PresenceRooms(async s => { if (revoked.has(s.connectionId)) throw Error(); return { interactionsEnabled: true, actorId: s.connectionId === 'a' ? 'alice' : 'bob' }; }, () => now);
  const connection = () => ({ messages: [], sendStateless(value) { this.messages.push(JSON.parse(value)); }, close() { this.closed = true; } });
  const a = connection(), b = connection(), c = connection(); const session = (id,room='one') => ({ connectionId:id,room });
  const before = Y.encodeStateAsUpdate(doc);
  await rooms.update(a,session('a'),doc,{type:'presence',cursor:anchor});
  await rooms.update(b,session('b'),doc,{type:'presence',cursor:null});
  assert.equal(a.messages.at(-1).members.length,2);assert.equal(a.messages.at(-1).members[0].actorId,'alice');
  await rooms.update(c,session('c','other'),doc,{type:'presence',cursor:null});assert.equal(c.messages.at(-1).members.length,1);
  await assert.rejects(rooms.update(a,session('a'),doc,{type:'presence',cursor:null,actorId:'owner'}));
  await rooms.update(a,session('a'),doc,{type:'presence',cursor:null});assert.deepEqual(rooms.entries.get(a).cursor,anchor);
  now+=101;revoked.add('b');await rooms.update(a,session('a'),doc,{type:'presence',cursor:null});assert.equal(b.closed,true);assert.equal(a.messages.at(-1).members.length,1);
  assert.deepEqual(Y.encodeStateAsUpdate(doc),before);
  now+=16000;await rooms.expire();assert.equal(rooms.entries.size,0);rooms.clear();doc.destroy();
});
test('presence names come from current authority, use a safe fallback and cannot be spoofed', async () => {
  const {doc}=fixture(); let displayName='  Alex Writer  ';
  const rooms=new PresenceRooms(async()=>({actorId:'internal-id',displayName,interactionsEnabled:true}));
  const connection={messages:[],sendStateless(value){this.messages.push(JSON.parse(value));},close(){}};
  const session={connectionId:'a',room:'room'};
  try {
    await rooms.update(connection,session,doc,{type:'presence',cursor:null});
    assert.equal(connection.messages.at(-1).members[0].displayName,'Alex Writer');
    await assert.rejects(rooms.update(connection,session,doc,{type:'presence',cursor:null,displayName:'Imposter'}));
    for (const [value,expected] of [['New Name','New Name'],[undefined,'Collaborator'],['   ','Collaborator'],['internal-id','Collaborator'],['x'.repeat(300),'x'.repeat(256)]]) {
      displayName=value; await rooms.publish('room');
      assert.equal(connection.messages.at(-1).members[0].displayName,expected);
    }
  } finally { rooms.clear(); doc.destroy(); }
});
test('presence refuses more than 64 participants and invalid authorities', async () => {
  const {doc}=fixture(); const rooms=new PresenceRooms(async()=>({actorId:'member',interactionsEnabled:true}));
  const connection=()=>({sendStateless(){},close(){}});
  for(let i=0;i<64;i++) await rooms.update(connection(),{connectionId:String(i),room:'room'},doc,{type:'presence',cursor:null});
  await assert.rejects(rooms.update(connection(),{connectionId:'overflow',room:'room'},doc,{type:'presence',cursor:null}));
  const disabled=new PresenceRooms(async()=>({actorId:'member',interactionsEnabled:false}));
  await disabled.update(connection(),{connectionId:'a',room:'room'},doc,{type:'presence',cursor:null});assert.equal(disabled.entries.size,0);doc.destroy();
});
test('disabling interactions clears presence without closing an authorized text session',async()=>{
 const {doc}=fixture();let enabled=true;const rooms=new PresenceRooms(async()=>({actorId:'member',interactionsEnabled:enabled}));
 const connection={messages:[],closed:false,sendStateless(value){this.messages.push(JSON.parse(value));},close(){this.closed=true;}};
 await rooms.update(connection,{connectionId:'a',room:'room'},doc,{type:'presence',cursor:null});enabled=false;await rooms.publish('room');
 assert.equal(rooms.entries.size,0);assert.equal(connection.closed,false);assert.deepEqual(connection.messages.at(-1).members,[]);doc.destroy();
});

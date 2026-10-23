import assert from 'node:assert/strict';
import { HocuspocusProvider, HocuspocusProviderWebsocket } from '@hocuspocus/provider';
import * as Y from 'yjs';
import { createCollaborationServer } from '../src/server.mjs';
import { sha256 } from '../src/commits.mjs';

const base = process.env.PROBE_BACKEND_URL, path = process.env.PROBE_SESSION_PATH, request = JSON.parse(process.env.PROBE_SESSION_REQUEST);
const id = path.split('/')[3], clients = []; let assertions = 0;
const server = createCollaborationServer({ backendUrl: base, serviceKey: process.env.COLLABORATION_SERVICE_KEY,
  environment: 'test', port: 0, commitsEnabled: true, revalidateMilliseconds: 100 });
const equal = (a,b) => { assert.deepEqual(a,b); assertions++; };
async function api(actor, route, body) {
  const response = await fetch(new URL(route, base), { method: 'POST', headers: { 'X-Test-User': actor, 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  return { status: response.status, value: await response.json().catch(() => null) };
}
async function wait(check, label) {
  const until = Date.now() + 7000; while (Date.now() < until) { if (check()) return; await new Promise(r=>setTimeout(r,20)); } throw Error(label);
}
async function client(actor, writable, membershipRevision = actor === 'owner' ? 0 : 1) {
  const installationId = crypto.randomUUID();
  const issued = await api(actor, path, { ...request, installationId, requestCommit: writable, membershipRevision }); equal(issued.status,200);
  const ticket = issued.value, document = new Y.Doc(), status = { synced: false, closed: false }, messages = [];
  const transport = new HocuspocusProviderWebsocket({ url:`ws://127.0.0.1:${server.address.port}`, WebSocketPolyfill:WebSocket, autoConnect:false, maxAttempts:1 });
  const provider = new HocuspocusProvider({ websocketProvider:transport, name:ticket.roomName, document, token:ticket.ticket, awareness:null,
    onSynced:({state})=>{status.synced=state;}, onClose:()=>{status.closed=true;transport.disconnect();},
    onStateless:({payload})=>messages.push(JSON.parse(payload)) });
  provider.attach(); void transport.connect().catch(()=>{}); const result = {actor,installationId,issued,ticket,document,status,messages,provider,transport}; clients.push(result);
  await wait(()=>status.synced, actor+' sync'); return result;
}
function envelope(c, text) {
  const doc = new Y.Doc(); Y.applyUpdate(doc,Y.encodeStateAsUpdate(c.document)); const vector = Y.encodeStateVector(doc);
  const node=doc.getXmlFragment('default').get(0).get(0); node.insert(node.length,text); const bytes=Y.encodeStateAsUpdate(doc,vector);doc.destroy();
  const parts=c.ticket.roomName.split('/');return {type:'commitUpdate',protocolVersion:1,documentId:parts[1],pageId:parts[2],generation:parts[3],installationId:c.installationId,
    updateId:crypto.randomUUID(),update:Buffer.from(bytes).toString('base64'),sha256:sha256(bytes)};
}
try {
  await server.listen();
  const owner=await client('owner',true), editor=await client('editor',true), viewer=await client('viewer',false);
  // Installation identity is obtained from the issued session request, never trusted from another actor.
  equal(viewer.ticket.readOnly,true);equal((await api('viewer',path,{...request,requestCommit:true})).status,403);
  equal((await api('stranger',path,request)).status,403);
  const installation = await fetch(new URL('/api/internal/collaboration/authorize',base),{method:'POST',headers:{Authorization:`Bearer ${process.env.COLLABORATION_SERVICE_KEY}`,'Content-Type':'application/json'},body:JSON.stringify({ticket:editor.ticket.ticket,roomName:editor.ticket.roomName,connectionId:crypto.randomUUID()})});
  equal(installation.status,403); // a different connection cannot steal an acquired ticket
  // The envelope must use the client's captured request identity.
  const update=envelope(editor,' Shared edit');update.installationId=editor.installationId;
  editor.provider.sendStateless(JSON.stringify(update));await wait(()=>editor.messages.some(m=>m.type==='durableReceipt'),'receipt');assertions++;
  await wait(()=>viewer.document.getXmlFragment('default').toString().includes('Shared edit'),'viewer committed broadcast');assertions++;
  viewer.provider.sendStateless(JSON.stringify(envelope(viewer,' forbidden viewer edit')));
  await wait(()=>viewer.status.closed,'viewer custom writes rejected');assertions++;
  equal(owner.document.getXmlFragment('default').toString().includes('forbidden'),false);
  const offline=envelope(editor,' PRIVATE offline draft');offline.installationId=editor.installationId;const unchanged=JSON.stringify(offline);
  equal((await api('owner',`/api/collaboration/manuscripts/${id}/members/editor/change`,{expectedRevision:1,role:1})).status,200);
  await wait(()=>editor.status.closed,'downgraded socket closes');assertions++;
  equal((await api('editor',path,{...request,requestCommit:true})).status,403);
  const downgraded=await client('editor',false,2);equal(downgraded.ticket.readOnly,true);
  equal((await api('owner',`/api/collaboration/manuscripts/${id}/members/editor/change`,{expectedRevision:2,role:1,revoke:true})).status,200);
  await wait(()=>downgraded.status.closed,'revoked viewer closes');assertions++;
  equal((await api('editor',path,{...request,requestCommit:false})).status,403);
  equal(JSON.stringify(offline),unchanged);equal(owner.document.getXmlFragment('default').toString().includes('PRIVATE'),false);
  equal((await api('viewer',`/api/collaboration/manuscripts/${id}/members/owner/change`,{expectedRevision:0,role:2,revoke:true})).status,403);
  console.log(`PASS: ${assertions} multi-account socket, viewer, downgrade, revocation and private-draft checks`);
} finally { for(const c of clients){c.provider.destroy();c.transport.destroy();c.document.destroy();}await server.destroy(); }

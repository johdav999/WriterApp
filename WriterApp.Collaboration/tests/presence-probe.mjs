import assert from 'node:assert/strict';
import * as Y from 'yjs';
import { HocuspocusProvider,HocuspocusProviderWebsocket } from '@hocuspocus/provider';
import { createCollaborationServer } from '../src/server.mjs';
const base=process.env.PROBE_BACKEND_URL,path=process.env.PROBE_SESSION_PATH,request=JSON.parse(process.env.PROBE_SESSION_REQUEST),id=path.split('/')[3],clients=[];let count=0;
const server=createCollaborationServer({backendUrl:base,serviceKey:process.env.COLLABORATION_SERVICE_KEY,environment:'test',port:0,interactionsEnabled:true,revalidateMilliseconds:100});
const equal=(a,b)=>{assert.deepEqual(a,b);count++;};
const wait=async(check,label)=>{const until=Date.now()+7000;while(Date.now()<until){if(check())return;await new Promise(r=>setTimeout(r,20));}throw Error(label);};
async function post(actor,url,body){return fetch(new URL(url,base),{method:'POST',headers:{'X-Test-User':actor,'Content-Type':'application/json'},body:JSON.stringify(body)});}
async function client(actor){
 const issued=await post(actor,path,{...request,installationId:crypto.randomUUID(),membershipRevision:actor==='owner'?0:1});equal(issued.status,200);const ticket=await issued.json(),doc=new Y.Doc();
 const value={actor,synced:false,closed:false,members:[],doc};const transport=new HocuspocusProviderWebsocket({url:`ws://127.0.0.1:${server.address.port}`,WebSocketPolyfill:WebSocket,autoConnect:false,maxAttempts:1});
 const provider=new HocuspocusProvider({websocketProvider:transport,name:ticket.roomName,token:ticket.ticket,document:doc,awareness:null,
 onSynced:({state})=>value.synced=state,onClose:()=>{value.closed=true;transport.disconnect();},onStateless:({payload})=>{const m=JSON.parse(payload);if(m.type==='presenceSnapshot')value.members=m.members;}});
 Object.assign(value,{provider,transport});clients.push(value);provider.attach();void transport.connect().catch(()=>{});await wait(()=>value.synced,'sync');return value;
}
const send=(client,cursor=null)=>client.provider.sendStateless(JSON.stringify({type:'presence',cursor}));
try{
 await server.listen();const owner=await client('owner'),editor=await client('editor'),viewer=await client('viewer');const before=Buffer.from(Y.encodeStateAsUpdate(owner.doc)).toString('base64');
 send(owner);send(editor);send(viewer);await wait(()=>owner.members.length===3,'roster');equal(owner.members.map(m=>m.actorId).sort(),['editor','owner','viewer']);
 equal(Object.fromEntries(owner.members.map(m=>[m.actorId,m.displayName])),{owner:'Alex Writer',editor:'Sam Editor',viewer:'Collaborator'});
 const text=editor.doc.getXmlFragment('default').get(0).get(0),position=Buffer.from(Y.encodeRelativePosition(Y.createRelativePositionFromTypeIndex(text,1))).toString('base64');
 await new Promise(r=>setTimeout(r,120));send(editor,{start:position,end:position});await wait(()=>owner.members.some(m=>m.actorId==='editor'&&m.cursor),'caret');count++;
 equal(Buffer.from(Y.encodeStateAsUpdate(owner.doc)).toString('base64'),before);
 editor.provider.sendStateless(JSON.stringify({type:'presence',cursor:null,displayName:'Spoofed Name'}));await wait(()=>editor.closed,'spoof denied');await wait(()=>owner.members.length===2,'spoof removed');count++;
 equal((await post('owner',`/api/collaboration/manuscripts/${id}/members/viewer/change`,{expectedRevision:1,role:1,revoke:true})).status,200);
 await wait(()=>viewer.closed,'revoked viewer closes');await wait(()=>owner.members.length===1,'revoked presence removed');equal(owner.members[0].actorId,'owner');
 const foreign=await client('editor');const other=new Y.Doc();other.getText('default').insert(0,'foreign');const bad=Buffer.from(Y.encodeRelativePosition(Y.createRelativePositionFromTypeIndex(other.getText('default'),1))).toString('base64');send(foreign,{start:bad,end:bad});await wait(()=>foreign.closed,'foreign position denied');count++;other.destroy();
 equal(Buffer.from(Y.encodeStateAsUpdate(owner.doc)).toString('base64'),before);console.log(`PASS: ${count} authenticated presence, caret, spoofing, revocation and text-isolation checks`);
}finally{for(const c of clients){c.provider.destroy();c.transport.destroy();c.doc.destroy();}await server.destroy();}

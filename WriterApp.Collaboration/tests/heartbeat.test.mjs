import test from 'node:test';
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {once} from 'node:events';
import {HocuspocusProvider,HocuspocusProviderWebsocket} from '@hocuspocus/provider';
import * as Y from 'yjs';
import {createCollaborationServer} from '../src/server.mjs';
test('authenticated heartbeat keeps a quiet read session alive and still closes on authority loss',{timeout:10000},async()=>{
 let healthy=true,acks=0,closed=false;
 const backend=createServer(async(req,res)=>{for await(const ignored of req){}res.setHeader('Content-Type','application/json');if(!healthy){res.writeHead(403);res.end('{}');return;}res.end(JSON.stringify(req.url.endsWith('bootstrap')?{updates:['AAA='],committedSequence:0}:{readOnly:true}));});backend.listen(0,'127.0.0.1');await once(backend,'listening');
 const server=createCollaborationServer({backendUrl:`http://127.0.0.1:${backend.address().port}/`,serviceKey:'k'.repeat(32),environment:'test',port:0,revalidateMilliseconds:100});const document=new Y.Doc();let provider,transport,timer;
 try{
  await server.listen();transport=new HocuspocusProviderWebsocket({url:`ws://127.0.0.1:${server.address.port}`,WebSocketPolyfill:WebSocket,autoConnect:false,maxAttempts:1,messageReconnectTimeout:1500});
  provider=new HocuspocusProvider({websocketProvider:transport,name:'test/11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222/33333333-3333-3333-3333-333333333333',token:'test-ticket',document,awareness:null,onClose:()=>{closed=true;transport.disconnect();},onStateless:({payload})=>{if(JSON.parse(payload).type==='heartbeat')acks++;}});
  const synced=new Promise(r=>provider.on('synced',({state})=>{if(state)r();}));provider.attach();void transport.connect().catch(()=>{});await synced;
  timer=setInterval(()=>provider.sendStateless(JSON.stringify({type:'heartbeat'})),200);await new Promise(r=>setTimeout(r,2600));assert.equal(closed,false);assert.ok(acks>=2);
  healthy=false;const deadline=Date.now()+2000;while(!closed&&Date.now()<deadline)await new Promise(r=>setTimeout(r,20));assert.equal(closed,true);
 }finally{clearInterval(timer);provider?.destroy();transport?.destroy();document.destroy();await server.destroy();backend.closeAllConnections();await new Promise(r=>backend.close(r));}
});

import assert from 'node:assert/strict';
import {createServer,connect} from 'node:net';
import {mkdir,writeFile} from 'node:fs/promises';
import os from 'node:os';
import {monitorEventLoopDelay,performance as nodePerformance} from 'node:perf_hooks';
import * as Y from 'yjs';
import * as decoding from 'lib0/decoding';
import {HocuspocusProvider,HocuspocusProviderWebsocket} from '@hocuspocus/provider';
import {createCollaborationServer} from '../src/server.mjs';
import {sha256} from '../src/commits.mjs';
const backend=process.env.PROBE_BACKEND_URL,request=JSON.parse(process.env.PROBE_SESSION_REQUEST),workload=JSON.parse(process.env.PROBE_WORKLOAD),clients=new Set(),sockets=new Set();let stockWrites=0,peakRss=process.memoryUsage().rss;
let measuring=false;const backendTimes=new Map(),submitted=new Map(),beforePersist=new Map(),prePersistTimes=[],persistenceTimes=[];
const originalFetch=globalThis.fetch;
globalThis.fetch=async(input,init)=>{const path=new URL(input).pathname,started=performance.now();try{return await originalFetch(input,init);}finally{if(measuring&&path.startsWith('/api/internal/collaboration/')){const values=backendTimes.get(path)??[];values.push(performance.now()-started);backendTimes.set(path,values);}}};
const loopDelay=monitorEventLoopDelay({resolution:10});loopDelay.enable();let utilization;
const server=createCollaborationServer({backendUrl:backend,serviceKey:process.env.COLLABORATION_SERVICE_KEY,environment:'test',port:0,commitsEnabled:true,checkpointEvery:64,
 fault:async(point,{envelope})=>{if(!measuring||!envelope)return;if(point==='beforePersist'){beforePersist.set(envelope.updateId,performance.now());prePersistTimes.push(performance.now()-submitted.get(envelope.updateId));}if(point==='afterPersist')persistenceTimes.push(performance.now()-beforePersist.get(envelope.updateId));}});
server.hocuspocus.configuration.extensions.unshift({async beforeHandleMessage({update}){const d=decoding.createDecoder(update);decoding.readVarString(d);const t=decoding.readVarUint(d);if((t===0||t===4)&&decoding.readVarUint(d)===2)stockWrites++;}});
await server.listen();
// An owned loopback TCP proxy adds 40 ms in each direction, including WebSocket traffic.
const proxy=createServer(socket=>{const upstream=connect(server.address.port,'127.0.0.1');sockets.add(socket);sockets.add(upstream);const timers=new Set();
 const forward=(target,data)=>{const timer=setTimeout(()=>{timers.delete(timer);if(!target.destroyed)target.write(data);},40);timers.add(timer);};
 socket.on('data',data=>forward(upstream,data));upstream.on('data',data=>forward(socket,data));
 const close=()=>{for(const timer of timers)clearTimeout(timer);socket.destroy();upstream.destroy();sockets.delete(socket);sockets.delete(upstream);};socket.on('error',close);upstream.on('error',close);socket.on('close',close);upstream.on('close',close);
});await new Promise(r=>proxy.listen(0,'127.0.0.1',r));
const wait=async(check,label,timeout=15000)=>{const until=Date.now()+timeout;while(Date.now()<until){if(check())return;await new Promise(r=>setTimeout(r,5));}throw Error(label);};
const sample=()=>{global.gc?.();const m=process.memoryUsage();peakRss=Math.max(peakRss,m.rss);return {rss:m.rss,heapUsed:m.heapUsed,loadedRooms:server.hocuspocus.documents.size};};
const baseline=sample(),openTimes=[],receiptTimes=[],visibilityTimes=[];
async function client(room,actor='owner',installationId=crypto.randomUUID()){
 const response=await fetch(new URL(`/api/documents/${room.documentId}/pages/${room.pageId}/collaboration/sessions`,backend),{method:'POST',headers:{'Content-Type':'application/json','X-Test-User':actor},body:JSON.stringify({...request,generation:room.generation,installationId,membershipRevision:actor==='owner'?0:1})});assert.equal(response.status,200);const ticket=await response.json(),started=performance.now(),doc=new Y.Doc(),messages=new Map(),value={doc,actor,installationId,room,messages,closed:false,synced:false};
 const transport=new HocuspocusProviderWebsocket({url:`ws://127.0.0.1:${proxy.address().port}`,WebSocketPolyfill:WebSocket,autoConnect:false,maxAttempts:1});
 const provider=new HocuspocusProvider({websocketProvider:transport,name:ticket.roomName,token:ticket.ticket,document:doc,awareness:null,onSynced:({state})=>value.synced=state,onClose:()=>{value.closed=true;transport.disconnect();},onStateless:({payload})=>{const m=JSON.parse(payload);if(m.type==='durableReceipt')messages.set(m.receipt.updateId,{receipt:m.receipt,at:performance.now()});}});
 let heartbeat;Object.assign(value,{provider,transport,stop(){clearInterval(heartbeat);provider.destroy();transport.destroy();doc.destroy();clients.delete(value);}});clients.add(value);provider.attach();void transport.connect().catch(()=>{});await wait(()=>value.synced,'room open');heartbeat=setInterval(()=>{if(!value.closed)provider.sendStateless(JSON.stringify({type:'heartbeat'}));},10000);openTimes.push(performance.now()-started);return value;
}
function envelope(client,text){const doc=new Y.Doc();Y.applyUpdate(doc,Y.encodeStateAsUpdate(client.doc));const vector=Y.encodeStateVector(doc),node=doc.getXmlFragment('default').get(0).get(0);node.insert(node.length,text);const bytes=Y.encodeStateAsUpdate(doc,vector);doc.destroy();return {type:'commitUpdate',protocolVersion:1,...client.room,installationId:client.installationId,updateId:crypto.randomUUID(),update:Buffer.from(bytes).toString('base64'),sha256:sha256(bytes)};}
const percentile=(values,q)=>[...values].sort((a,b)=>a-b)[Math.min(values.length-1,Math.ceil(values.length*q)-1)];
const summary=values=>({samples:values.length,p50:percentile(values,.5),p95:percentile(values,.95),max:Math.max(...values)});
let replay;
try{
 const editors=[];for(let i=0;i<20;i++)editors.push(await client(workload.rooms[0],'editor-'+i));
 for(let start=1;start<workload.rooms.length;start+=5)await Promise.all(workload.rooms.slice(start,start+5).map(room=>client(room)));const loaded=sample();assert.equal(loaded.loadedRooms,100);
 measuring=true;loopDelay.reset();const utilizationStart=nodePerformance.eventLoopUtilization();
 for(let round=0;round<5;round++)await Promise.all(editors.map(async(c,index)=>{const token=` [r${round}e${index}]`,value=envelope(c,token),started=performance.now();submitted.set(value.updateId,started);if(round===4&&index===0)replay={value,installationId:c.installationId};c.provider.sendStateless(JSON.stringify(value));
  await wait(()=>c.messages.has(value.updateId),'durable receipt');receiptTimes.push(c.messages.get(value.updateId).at-started);const observer=editors[(index+1)%20];await wait(()=>observer.doc.getXmlFragment('default').toString().includes(token),'remote visibility');visibilityTimes.push(performance.now()-started);
 }));
 measuring=false;utilization=nodePerformance.eventLoopUtilization(utilizationStart);loopDelay.disable();
 const original=editors[0].messages.get(replay.value.updateId).receipt;
 // Simulated proxy connection loss preserves the original installation/update IDs on retry.
 for(const socket of [...sockets])socket.destroy();await wait(()=>editors.every(c=>c.closed),'proxy disconnect');
 const reconnected=await client(workload.rooms[0],'editor-0',replay.installationId);reconnected.provider.sendStateless(JSON.stringify(replay.value));await wait(()=>reconnected.messages.has(replay.value.updateId),'receipt replay');assert.deepEqual(reconnected.messages.get(replay.value.updateId).receipt,original);
 for(const c of clients)c.stop();await wait(()=>server.hocuspocus.documents.size===0,'rooms unload');const afterLoad=sample();
 let after20;
 for(let i=0;i<50;i++){const c=await client(workload.rooms[i%100]);c.stop();await wait(()=>server.hocuspocus.documents.size===0,'page switch unload');if(i===19)after20=sample();}
 const after50=sample();assert.equal(after50.loadedRooms,0);assert.equal(stockWrites,0);
 const result={provider:workload.provider,machine:{platform:os.platform(),release:os.release(),logicalCpus:os.cpus().length,cpuModel:os.cpus()[0].model,memoryBytes:os.totalmem()},workload:{rooms:100,activeEditorsPerHotRoom:20,wordsPerPage:5000,concurrentConnections:119,commits:100,proxyRoundTripMilliseconds:80,pageSwitchCycles:50,roomOpeningRampBatchSize:5,hotRoomOpeningRampBatchSize:1},
  warmOpenAfterAuthorizationMs:summary(openTimes),durableReceiptMs:summary(receiptTimes),remoteVisibilityMs:summary(visibilityTimes),diagnostics:{backend:Object.fromEntries([...backendTimes].map(([path,values])=>[path,summary(values)])),submissionToBeforePersistMs:summary(prePersistTimes),persistenceCallMs:summary(persistenceTimes),eventLoopUtilization:utilization,eventLoopDelayP95Ms:loopDelay.percentile(95)/1e6},memory:{baseline,loaded,afterLoad,after20,after50,peakRss},stockWrites,duplicateReplayPreserved:true,
  provisionalBudgets:{warmOpenP95AtMost2000:percentile(openTimes,.95)<=2000,receiptP95AtMost1000:percentile(receiptTimes,.95)<=1000,visibilityP95AtMost300:percentile(visibilityTimes,.95)<=300,retainedRoomsAfterSwitches:after50.loadedRooms},productionReady:false};
 await mkdir('artifacts/realtime-p10',{recursive:true});await writeFile(`artifacts/realtime-p10/load-${workload.provider}.json`,JSON.stringify(result,null,2));console.log('PASS: 100 rooms, 20 concurrent editors, 100 durable commits, exact receipt replay after proxy loss, 50 unload cycles and zero ordinary provider writes');console.log(JSON.stringify(result.provisionalBudgets));
}catch(error){await mkdir('artifacts/realtime-p10',{recursive:true});await writeFile(`artifacts/realtime-p10/load-${workload.provider}-failure.json`,JSON.stringify({error:error.message,loadedRooms:server.hocuspocus.documents.size,opened:openTimes.length,receipts:receiptTimes.length,stockWrites,productionReady:false},null,2));throw error;}
finally{globalThis.fetch=originalFetch;loopDelay.disable();for(const c of clients)c.stop();for(const socket of sockets)socket.destroy();await new Promise(r=>proxy.close(r));await server.destroy();}

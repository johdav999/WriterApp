const recordings = new Map();
export function availability() {
    const supported = globalThis.isSecureContext && navigator.mediaDevices?.getUserMedia && globalThis.AudioContext && globalThis.AudioWorkletNode;
    return {state:supported?0:3,message:supported?'Microphone capture supported. Permission is requested only on Start.':'Microphone capture requires HTTPS and AudioWorklet support. Continue typing.'};
}
function wav(samples) {
    const bytes=new Uint8Array(44+samples.length*2),v=new DataView(bytes.buffer);const text=(at,s)=>{for(let i=0;i<s.length;i++)bytes[at+i]=s.charCodeAt(i);};
    text(0,'RIFF');v.setUint32(4,bytes.length-8,true);text(8,'WAVE');text(12,'fmt ');v.setUint32(16,16,true);v.setUint16(20,1,true);v.setUint16(22,1,true);v.setUint32(24,16000,true);v.setUint32(28,32000,true);v.setUint16(32,2,true);v.setUint16(34,16,true);text(36,'data');v.setUint32(40,samples.length*2,true);
    for(let i=0;i<samples.length;i++)v.setInt16(44+i*2,samples[i],true);return bytes;
}
export async function start(id,receiver) {
    if(recordings.size)throw new Error('Another recording is active.');if(availability().state!==0)throw new Error(availability().message);
    const r={id,receiver,cancelled:false,finished:false,frames:0,chunks:[],stream:null,context:null,node:null,source:null,timer:null};recordings.set(id,r);
    r.hidden=()=>{if(document.hidden)cancel(id);};r.unload=()=>cancel(id);document.addEventListener('visibilitychange',r.hidden);window.addEventListener('pagehide',r.unload);
    r.timer=setTimeout(()=>finish(r,'permission','Microphone permission timed out. Choose Start again when ready, or continue typing.'),60000);
    try {
        const stream=await navigator.mediaDevices.getUserMedia({audio:{channelCount:1,echoCancellation:true,noiseSuppression:true},video:false});r.stream=stream;
        if(r.cancelled || r.finished){stream.getTracks().forEach(t=>t.stop());return;}clearTimeout(r.timer);
        for(const track of stream.getAudioTracks())track.addEventListener('ended',()=>{if(!r.finished)finish(r,'device','Microphone disconnected. Composer retained.');});
        r.context=new AudioContext({sampleRate:16000});if(r.context.sampleRate!==16000)throw new Error('16 kHz recording unsupported. Continue typing.');
        await r.context.audioWorklet.addModule(new URL('brainstorming-microphone-worklet.js',import.meta.url));if(r.cancelled || r.finished){await release(r);return;}
        r.node=new AudioWorkletNode(r.context,'prosa-dictation');r.source=r.context.createMediaStreamSource(stream);r.source.connect(r.node);r.node.connect(r.context.destination);
        r.node.port.onmessage=e=>{if(r.finished || r.cancelled)return;const chunk=e.data;if(!(chunk instanceof Int16Array))return;const remaining=960000-r.frames;if(remaining<=0){stop(id);return;}const take=chunk.subarray(0,remaining);r.chunks.push(take);r.frames+=take.length;if(r.frames>=960000)stop(id);};
        await r.context.resume();if(r.cancelled || r.finished){await release(r);return;}r.timer=setTimeout(()=>stop(id),60000);
    }catch(e){const code=e.name==='NotAllowedError'?'permission':e.name==='NotFoundError'?'no-device':e.name==='NotReadableError'?'device':'unsupported';await finish(r,code,code==='permission'?'Microphone permission denied. Allow it in browser settings or continue typing.':code==='no-device'?'No microphone found. Connect a device or continue typing.':'Microphone unavailable or interrupted. Check device and secure browser support.');}
}
async function release(r) {
    clearTimeout(r.timer);document.removeEventListener('visibilitychange',r.hidden);window.removeEventListener('pagehide',r.unload);
    try{r.source?.disconnect();}catch{}try{r.node?.disconnect();}catch{}r.stream?.getTracks().forEach(t=>t.stop());if(r.context && r.context.state!=='closed')try{await r.context.close();}catch{}recordings.delete(r.id);
}
async function finish(r,code='',message='') {
    if(r.finished)return;r.finished=true;let audio=null;
    if(!code && !r.cancelled){if(r.frames<1600){code='silence';message='Recording too short. Speak and record again.';}else{const samples=new Int16Array(r.frames);let at=0;for(const c of r.chunks){samples.set(c,at);at+=c.length;}audio=wav(samples);samples.fill(0);}}
    await release(r);r.chunks.forEach(c=>c.fill(0));r.chunks=[];
    try{if(audio){await r.receiver.invokeMethodAsync('Recorded',r.id,audio);}else await r.receiver.invokeMethodAsync('RecordingFailed',r.id,code||'cancelled',message||'Recording cancelled. Typed text retained.');}catch{}finally{audio?.fill(0);}
}
export async function stop(id){const r=recordings.get(id);if(r)await finish(r);}
export async function cancel(id){const r=recordings.get(id);if(!r)return;r.cancelled=true;await finish(r,'cancelled','Recording cancelled. Typed text retained.');}

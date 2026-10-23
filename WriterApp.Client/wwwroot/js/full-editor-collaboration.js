const mounted = new WeakMap();
const api = () => window.tiptapEditor.collaboration;
async function request(url,body) {
    const response=await fetch(url,{credentials:'same-origin',cache:'no-store',method:body===undefined?'GET':'POST',headers:body===undefined?{}:{'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});
    if(!response.ok){let code;try{code=(await response.json()).code;}catch{}throw Object.assign(new Error(code??'Shared writing is unavailable'),{status:response.status,code});}
    return response.json();
}
export async function create(elementId,documentId,pageId,callbacks) {
    const host=document.getElementById(elementId),support=await api().getSupport();
    if(!host?.isConnected)throw Error('The editor was closed.');
    const identity=await request('/api/auth/me');if(!identity.isAuthenticated)throw Error('Sign in to open this manuscript.');
    const getCapabilities=()=>request(`/api/documents/${documentId}/collaboration/capabilities?`+new URLSearchParams({pageId,protocolVersion:support.protocolVersion,schemaFingerprint:support.schemaFingerprint}));
    const caps=await getCapabilities();
    if(caps.blockedReason)throw Error(caps.blockedReason.message);
    let editor,binding,store,account,timer,stopped=false,checking=false;
    const state={localDurable:true,remoteDurable:true,pending:0,queued:0,connected:true,error:null,recoveryReason:null,accountChanged:false};
    // A retired binding can emit while its asynchronous close is draining.
    // Those callbacks belong to its old mount and must not remount its successor.
    const notify=(change,final=false)=>{if(stopped&&!final)return;Object.assign(state,change);void callbacks.invokeMethodAsync('OnCollaborationStatus',{...state}).catch(()=>{});};
    const invalidate=()=>{if(stopped)return;stopped=true;clearInterval(timer);binding?.invalidate();editor?.setEditable(false);notify({recoveryReason:'Access changed. Return to Shared and reopen the manuscript. Your private local draft is retained.'},true);};
    const factory=options=>window.tiptapEditor.create(elementId,'',callbacks,options);
    const readSnapshot=async()=>{
        for(let attempt=0;;attempt++){
            try{return await request(`/api/documents/${documentId}/collaboration/read`,{});}
            catch(error){if(error.code!=='collaboration_projection_pending'||attempt>=19)throw error;await new Promise(r=>setTimeout(r,250));}
        }
    };
    const initialSnapshot=await readSnapshot();let structureRevision=initialSnapshot.structureRevision;
    if(caps.canWriteCollaboratively){
        const page=caps.pages.find(p=>p.pageId===pageId),scope={backend:location.origin+'/',ownerId:identity.userId,environment:caps.environment,documentId,pageId,generation:page.generation,support,membershipRevision:caps.membershipRevision};
        store=new(api().IndexedDbJournal)(scope);account=new(api().CollaborationAccountFence)();account.activate(scope,1);
        try{binding=await api().createJournaledEditor(host,{store,account:account.capture(scope),getTicket:api().webTicketSource(scope),onStatus:notify,interactionsEnabled:caps.interactionsAvailable},caps,factory);editor=binding.editor;}
        catch(error){await store.close();account.close();throw error;}
    }else{
        const snapshot=await readSnapshot();
        editor=api().createReadOnlyEditor(host,snapshot.pages.find(p=>p.pageId===pageId).html,factory);
        notify({readOnly:true});
    }
    async function check(){
        if(stopped||checking)return;checking=true;
        try{
            const me=await request('/api/auth/me');if(stopped)return;
            if(!me.isAuthenticated||me.userId!==identity.userId){invalidate();return;}
            const next=await getCapabilities();if(stopped)return;
            // A deleted section is a structure change, not an account/access failure.
            // Keep its journal, stop its writer, and let the normal navigator choose
            // a remaining section from the newly authorized workspace.
            if(next.membershipRevision===caps.membershipRevision && !next.pages.find(p=>p.pageId===pageId)?.generation){
                stopped=true;clearInterval(timer);binding?.invalidate();editor?.setEditable(false);
                await callbacks.invokeMethodAsync('OnSharedStructureChanged');return;
            }
            if(next.blockedReason||next.membershipRevision!==caps.membershipRevision||next.canWriteCollaboratively!==caps.canWriteCollaboratively){invalidate();return;}
            if(next.pages.find(p=>p.pageId===pageId)?.generation!==caps.pages.find(p=>p.pageId===pageId)?.generation){
                if(next.pages.some(p=>p.pageId===pageId)&&state.localDurable&&state.pending===0&&state.queued===0){stopped=true;clearInterval(timer);notify({reloadRequired:true},true);}
                else invalidate();
                return;
            }
            const snapshot=await request(`/api/documents/${documentId}/collaboration/read`,{});
            if(snapshot.structureRevision!==structureRevision){structureRevision=snapshot.structureRevision;await callbacks.invokeMethodAsync('OnSharedStructureChanged');}
            if(!binding&&!stopped)api().updateReadOnlyEditor(editor,snapshot.pages.find(p=>p.pageId===pageId).json);
        }catch(error){if([401,403,404].includes(error.status))invalidate();}
        finally{checking=false;}
    }
    timer=setInterval(check,2000);
    const beforeUnload=e=>{if(!state.localDurable){e.preventDefault();e.returnValue='';}};window.addEventListener('beforeunload',beforeUnload);
    mounted.set(editor,{binding,store,account,state,invalidate,async close(){stopped=true;clearInterval(timer);window.removeEventListener('beforeunload',beforeUnload);if(binding)await binding.destroy();await store?.close();account?.close();mounted.delete(editor);}});
    return editor;
}
export async function flush(editor){
    const current=mounted.get(editor);if(!current)return;
    if(current.state.recoveryReason||current.state.accountChanged)throw Error(current.state.recoveryReason??'Access changed. Reopen the manuscript from Shared. Your private local draft is retained.');
    if(current.state.error)throw Error(`Shared writing could not be saved: ${String(current.state.error)}. Keep this page open; your private local draft is retained.`);
    await current.binding?.session.flush();
    for(let i=0;i<100&&!current.state.remoteDurable;i++){
        if(current.state.error||current.state.recoveryReason)throw Error('Shared writing could not be saved. Your private local draft is retained.');
        await new Promise(r=>setTimeout(r,100));
    }
    if(!current.state.remoteDurable)throw Error('Waiting for shared writing to save. Try again when connected.');
}
export function invalidate(editor){mounted.get(editor)?.invalidate();}
export async function close(editor){await mounted.get(editor)?.close();}

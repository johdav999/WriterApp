import '/collaboration/test-editor.js';
const api=window.tiptapEditor.collaboration,$=id=>document.getElementById(id);
let actor,account,binding,store,documentId,pageId,selectedCapabilities,pendingCreate,pendingInvite,busy=false,polling=false,lastManagement=0;
let pollTask=Promise.resolve();
const status=text=>$('status').textContent=text;
async function request(path,body,method){const response=await fetch(path,{method:method??(body===undefined?'GET':'POST'),credentials:'same-origin',cache:'no-store',headers:body===undefined?{}:{'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});if(!response.ok){let code;try{code=(await response.json()).code;}catch{}throw Object.assign(Error(response.status===401?'Sign in with Prosa to continue.':code??`Request failed (${response.status}). Your local writing is retained.`),{status:response.status,code});}return response.status===204?null:response.json();}
async function run(work){if(busy)return;busy=true;document.body.setAttribute('aria-busy','true');for(const b of document.querySelectorAll('button'))b.disabled=true;try{await pollTask;await work();}catch(error){status(error.message);}finally{busy=false;document.body.removeAttribute('aria-busy');for(const b of document.querySelectorAll('button'))b.disabled=false;}}
function button(label,action){const b=document.createElement('button');b.textContent=label;b.disabled=busy;b.onclick=()=>run(action);return b;}
function item(target,text,...actions){const li=document.createElement('li');li.append(document.createTextNode(text+' '),...actions);$(target).append(li);}
async function close(){if(binding){await binding.destroy();binding=null;}if(store){await store.close();store=null;}account?.close();account=null;}
function invalidate(message){binding?.invalidate();$('workspace').hidden=true;$('invite-result').textContent='';status(message);}
async function releaseManuscript(message){
 binding?.invalidate();documentId=null;pageId=null;selectedCapabilities=null;pendingInvite=null;
 $('writing').hidden=true;$('manage').hidden=true;$('invite-result').textContent='';
 // Keep the account and its library available; the private journal stays in its original scope.
 try{await refresh();}catch{}status(message);
}
function saved(s){$('save-status').textContent=s.recoveryReason||s.accountChanged?'Access changed · Private local draft retained. Reopen after checking access.':s.error?'Writing is not saved locally. Keep this page open.':!s.localDurable?'Saving on this computer…':s.remoteDurable?'Saved to shared manuscript':`Saved on this computer · ${s.pending+s.queued} update(s) waiting to sync`;}
async function read(id){let error;for(let i=0;i<8;i++){try{return await request(`/api/documents/${id}/collaboration/read`,{});}catch(e){error=e;if(!e.message.includes('projection_pending'))throw e;await new Promise(r=>setTimeout(r,250));}}throw error;}
async function capabilities(id,page){const support=await api.getSupport();return request(`/api/documents/${id}/collaboration/capabilities?`+new URLSearchParams({pageId:page,protocolVersion:support.protocolVersion,schemaFingerprint:support.schemaFingerprint}));}
async function open(id,requestedPage){await close();documentId=id;const snapshot=await read(id);pageId=requestedPage??snapshot.pages[0]?.pageId;if(!pageId)throw Error('This manuscript has no available page.');
 $('writing').hidden=false;$('manuscript-title').textContent='Shared writing';$('pages').replaceChildren();for(const page of snapshot.pages){const option=document.createElement('option');option.value=page.pageId;option.textContent=page.title;option.selected=page.pageId===pageId;$('pages').append(option);}
 const caps=selectedCapabilities=await capabilities(id,pageId);$('manage').hidden=caps.effectiveAccess!=='owner';$('toolbar').hidden=!caps.canWriteCollaboratively;$('editor').hidden=!caps.canWriteCollaboratively;$('viewer').hidden=caps.canWriteCollaboratively;
 if(caps.canWriteCollaboratively){const support=await api.getSupport(),page=caps.pages.find(p=>p.pageId===pageId),scope={backend:location.origin+'/',ownerId:actor.userId,environment:caps.environment,documentId:id,pageId,generation:page.generation,support,membershipRevision:caps.membershipRevision};
  store=new api.IndexedDbJournal(scope);account=new api.CollaborationAccountFence();account.activate(scope,1);
  binding=await api.createJournaledEditor($('editor'),{store,account:account.capture(scope),getTicket:api.webTicketSource(scope),onStatus:saved,interactionsEnabled:caps.interactionsAvailable},caps);
  status('Ready. Invite the other account, then type in both browser profiles.');
 }else{$('viewer').innerHTML=snapshot.pages.find(p=>p.pageId===pageId).html;$('save-status').textContent='Viewer · Read only';status('This account has read-only access.');}
 if(caps.effectiveAccess==='owner')await management();
}
async function management(){if(!documentId||selectedCapabilities?.effectiveAccess!=='owner')return;
 const [members,outgoing]=await Promise.all([request(`/api/collaboration/manuscripts/${documentId}/members`),request(`/api/collaboration/manuscripts/${documentId}/invitations`)]);
 $('members').replaceChildren();for(const member of members){const actions=member.role===3?[]:[button('Viewer',()=>change(member,1,false)),button('Editor',()=>change(member,2,false)),button('Revoke access',()=>change(member,member.role,true))];item('members',`${member.userId} · ${member.revoked?'Revoked':({1:'Viewer',2:'Editor',3:'Owner'})[member.role]}`,...actions);}
 $('outgoing').replaceChildren();for(const invite of outgoing)item('outgoing',`${invite.recipientUserId??invite.recipientEmail} · ${invite.id}`,button('Cancel',async()=>{await request(`/api/collaboration/manuscripts/${documentId}/invitations/${invite.id}/cancel`,invite.revision);await management();}));
 lastManagement=Date.now();
}
async function change(member,role,revoke){await request(`/api/collaboration/manuscripts/${documentId}/members/${encodeURIComponent(member.userId)}/change`,{expectedRevision:member.revision,role,revoke});await management();status('Access updated.');}
async function refresh(){const me=await request('/api/auth/me');if(!me.isAuthenticated)throw Error('Sign in with Prosa to continue.');if(actor&&actor.userId!==me.userId){invalidate('Account changed. Local writing is retained in its original account. Reload to continue.');return;}
 actor=me;$('identity').replaceChildren(document.createTextNode(`Signed in: ${me.email??me.displayName??'Prosa account'} · Account ID: `));const code=document.createElement('code');code.textContent=me.userId;$('identity').append(code);$('workspace').hidden=false;$('signout').hidden=false;
 const [owned,shared,inbox]=await Promise.all([request('/api/collaboration/local-test'),request('/api/collaboration/shared'),request('/api/collaboration/invitations')]);
 $('owned').replaceChildren();for(const doc of owned.documents)item('owned',doc.title,button('Open',()=>open(doc.documentId)));
 $('shared').replaceChildren();for(const doc of shared.documents)item('shared',doc.title,button('Open',()=>open(doc.documentId)));
 $('inbox').replaceChildren();for(const invite of inbox)item('inbox',invite.id,button('Decline',async()=>{await request(`/api/collaboration/invitations/${invite.id}/decline`,{});await refresh();}));
 if(documentId&&selectedCapabilities?.effectiveAccess==='owner')await management();
}
$('create').onclick=()=>run(async()=>{pendingCreate??={documentId:crypto.randomUUID(),projectId:crypto.randomUUID(),sectionId:crypto.randomUUID(),pageId:crypto.randomUUID()};const result=await request('/api/collaboration/local-test',pendingCreate);pendingCreate=null;await refresh();await open(result.documentId);});
$('refresh').onclick=()=>run(refresh);$('pages').onchange=()=>run(()=>open(documentId,$('pages').value));
$('invite').onclick=()=>run(async()=>{const recipient=$('recipient').value.trim();if(!recipient)throw Error('Enter the other account’s account ID.');pendingInvite??={documentId,request:{operationId:crypto.randomUUID(),role:Number($('role').value),recipientUserId:recipient,recipientEmail:null}};
 if(pendingInvite.documentId!==documentId)throw Error('Reopen the previous manuscript to finish its pending invitation first.');
 $('invite-result').textContent='Creating invitation…';
 const result=await request(`/api/collaboration/manuscripts/${documentId}/invitations`,pendingInvite.request);pendingInvite=null;$('invite-result').textContent=result.token?`Invitation ID: ${result.invitation.id}\nOne-time code: ${result.token}\nGive both to the other account. No email is sent.`:'Invitation already exists. Cancel it and create another if the one-time code was lost.';
 if(result.token){$('invite-result').append(document.createElement('br'),button('Copy invitation ID',async()=>{await navigator.clipboard.writeText(result.invitation.id);status('Invitation ID copied. Paste it in the other account’s window.');}),button('Copy one-time code',async()=>{await navigator.clipboard.writeText(result.token);status('One-time code copied. Paste it in the other account’s window.');}));}
 await management();});
$('accept').onclick=()=>run(async()=>{
 const invitationId=$('invitation-id').value.trim(),token=$('invitation-code').value.trim();
 if(!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(invitationId))throw Error('Paste the complete invitation ID from the owner’s window.');
 if(!/^[A-Za-z0-9_-]{43}$/.test(token))throw Error('The one-time code is incomplete or contains extra text. Use Copy one-time code in the owner’s window and paste it here.');
 try{await request('/api/collaboration/invitations/accept',{invitationId,token});}catch(error){if(error.message==='collaboration_access_unavailable')throw Error('Invitation could not be accepted. Check that the ID and code come from the same invitation and that it is addressed to the account shown above. Ask the owner for a new invitation if needed. You are still signed in.');throw error;}
 $('invitation-code').value='';await refresh();status('Invitation accepted. Open the manuscript under Shared with you.');});
$('signout').onclick=()=>run(async()=>{await close();await request('/local-collaboration/sign-out',{});location.reload();});
for(const element of document.querySelectorAll('[data-command]'))element.onclick=()=>binding?.editor.chain().focus()[element.dataset.command]().run();
window.addEventListener('beforeunload',event=>{if(binding&&!binding.session.status.localDurable){event.preventDefault();event.returnValue='';}});
setInterval(()=>{if(busy||polling||!actor)return;pollTask=checkAccess();},2000);
async function checkAccess(){polling=true;try{
 let me;try{me=await request('/api/auth/me');}catch{status('Sign-in could not be checked. Your private local draft is retained.');return;}
 if(!me.isAuthenticated||me.userId!==actor.userId){invalidate('Sign-in changed. Your original private local draft is retained.');return;}
 if(documentId){try{
  const next=await capabilities(documentId,pageId);
  if(selectedCapabilities.membershipRevision!==next.membershipRevision||selectedCapabilities.canWriteCollaboratively!==next.canWriteCollaboratively){await releaseManuscript('Your access to this manuscript changed. You are still signed in. Reopen it from your library to check the new role. Your private local draft is retained.');return;}
  if(!binding){const snapshot=await read(documentId);$('viewer').innerHTML=snapshot.pages.find(p=>p.pageId===pageId).html;$('save-status').textContent='Viewer · Read only';status('This account has read-only access.');}
  if(next.effectiveAccess==='owner'&&Date.now()-lastManagement>5000)await management();
 }catch(error){
  if(error.status===403||error.status===404){await releaseManuscript('This manuscript is no longer available. You are still signed in and can use your other manuscripts. Your private local draft is retained.');}
  else if(error.code==='collaboration_projection_pending'){$('save-status').textContent='Updating shared writing… Showing the last available version.';status('The owner’s updates are being prepared for viewing. This page will update automatically.');}
  else{status('Shared writing could not be updated. Retrying automatically. Your sign-in and private local draft are retained.');}
 }}
 }finally{polling=false;}}
const signInError=new URLSearchParams(location.search).get('signin');
const signInMessage=signInError==='account-mismatch'?'The provider returned a different account from the email you requested. No account switch was made. Retry with your Prosa email and Prosa password.':signInError==='email-unavailable'?'The provider did not return an email address to verify your selection. No account switch was made.':signInError==='email-required'?'Enter the Prosa account email you want to sign in with.':signInError?'Sign-in did not complete. Try Sign in with Prosa again.':null;
await run(async()=>{try{await refresh();}catch(error){if(!signInMessage)throw error;}status(signInMessage??'Signed in. Create a test manuscript or accept an invitation.');});

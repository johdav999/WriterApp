// Normal Prosa workspace adapter. All writes use the durable collaboration journal.
export async function mount(host, documentId) {
    const api = window.tiptapEditor.collaboration;
    const el = name => host.querySelector(`[data-live="${name}"]`);
    const status = message => { el('status').textContent = message; };
    let binding, store, account, actor, caps, pageId, stopped = false, busy = false, timer;
    let work = Promise.resolve();
    async function request(path, body) {
        const response = await fetch(path, { method: body === undefined ? 'GET' : 'POST', credentials: 'same-origin', cache: 'no-store',
            headers: body === undefined ? {} : { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
        if (!response.ok) { let code; try { code = (await response.json()).code; } catch { }
            throw Object.assign(new Error(code || 'Shared writing is temporarily unavailable.'), { status: response.status, code }); }
        return response.json();
    }
    function invalidate(message = 'Your sign-in changed. Reopen the manuscript from Shared. Your private local draft is retained.') {
        stopped = true; clearInterval(timer); binding?.invalidate(); el('writing').hidden = true; el('share').hidden = true; status(message);
    }
    async function closeEditor() {
        if (binding) { await binding.destroy(); binding = null; }
        if (store) { await store.close(); store = null; }
        account?.close(); account = null;
    }
    function saved(s) {
        el('saved').textContent = s.recoveryReason || s.accountChanged ? 'Access changed · Private local draft retained'
            : s.error ? 'Writing is not saved locally. Keep this page open.'
                : !s.localDurable ? 'Saving on this computer…' : s.remoteDurable ? 'Saved to shared manuscript'
                    : 'Saved on this computer · Waiting to sync';
    }
    const support = await api.getSupport();
    const capabilities = page => request(`/api/documents/${documentId}/collaboration/capabilities?` + new URLSearchParams({ pageId: page, protocolVersion: support.protocolVersion, schemaFingerprint: support.schemaFingerprint }));
    async function read() {
        for (let i = 0; ; i++) {
            try { return await request(`/api/documents/${documentId}/collaboration/read`, {}); }
            catch (error) { if (error.code !== 'collaboration_projection_pending' || i >= 7 || stopped) throw error; await new Promise(r => setTimeout(r, 250)); }
        }
    }
    async function open(requestedPage) {
        await closeEditor();
        const snapshot = await read();
        if (stopped) return;
        pageId = requestedPage ?? snapshot.pages[0]?.pageId;
        if (!pageId) throw new Error('This manuscript has no available page.');
        caps = await capabilities(pageId);
        if (stopped) return;
        el('title').textContent = snapshot.title || 'Shared writing';
        el('pages').replaceChildren(...snapshot.pages.map(page => {
            const option = document.createElement('option'); option.value = page.pageId; option.textContent = page.title; option.selected = page.pageId === pageId; return option;
        }));
        el('share').hidden = caps.effectiveAccess !== 'owner';
        el('toolbar').hidden = !caps.canWriteCollaboratively;
        el('editor').hidden = !caps.canWriteCollaboratively;
        el('viewer').hidden = caps.canWriteCollaboratively;
        if (caps.canWriteCollaboratively) {
            const page = caps.pages.find(p => p.pageId === pageId);
            const scope = { backend: location.origin + '/', ownerId: actor.userId, environment: caps.environment, documentId, pageId,
                generation: page.generation, support, membershipRevision: caps.membershipRevision };
            store = new api.IndexedDbJournal(scope); account = new api.CollaborationAccountFence(); account.activate(scope, 1);
            binding = await api.createJournaledEditor(el('editor'), { store, account: account.capture(scope), getTicket: api.webTicketSource(scope), onStatus: saved, interactionsEnabled: caps.interactionsAvailable }, caps);
            if (stopped) { binding.invalidate(); await closeEditor(); return; }
            status('Live editing · Changes are shared automatically.');
        } else {
            el('viewer').innerHTML = snapshot.pages.find(p => p.pageId === pageId)?.html ?? '';
            el('saved').textContent = 'Read only · Live updates';
            status(caps.effectiveAccess === 'viewer' ? 'You can view this manuscript. The owner’s changes will appear here.' : 'Read only · Live editing is unavailable for this build.');
        }
        el('writing').hidden = false;
    }
    async function check() {
        const me = await request('/api/auth/me');
        if (stopped) return;
        if (!me.isAuthenticated || me.userId !== actor.userId) { invalidate(); return; }
        if (!pageId) { await open(); return; }
        const next = await capabilities(pageId);
        if (stopped) return;
        if (next.membershipRevision !== caps.membershipRevision || next.canWriteCollaboratively !== caps.canWriteCollaboratively || next.pages.find(p=>p.pageId===pageId)?.generation !== caps.pages.find(p=>p.pageId===pageId)?.generation) {
            invalidate('Your access to this manuscript changed. You are still signed in. Return to Shared and reopen it. Your private local draft is retained.'); return;
        }
        if (!binding) {
            const snapshot = await read(); if (stopped) return;
            el('viewer').innerHTML = snapshot.pages.find(p => p.pageId === pageId)?.html ?? '';
            el('saved').textContent = 'Read only · Live updates'; status('You can view this manuscript. The owner’s changes will appear here.');
        }
    }
    async function run(action) {
        if (busy || stopped) return; busy = true;
        try { await action(); }
        catch (error) {
            if (stopped) return;
            if ([401, 403, 404].includes(error.status)) invalidate('This manuscript is no longer available to this account. You can still use your other manuscripts. Your private local draft is retained.');
            else if (error.code === 'collaboration_projection_pending') status('Updating shared writing… Showing the last available version.');
            else status('Shared writing could not be updated. Retrying automatically. Your private local draft is retained.');
        } finally { busy = false; }
    }
    el('pages').onchange = () => { work = work.then(() => run(() => open(el('pages').value))); };
    for (const button of host.querySelectorAll('[data-command]')) button.onclick = () => binding?.editor.chain().focus()[button.dataset.command]().run();
    const unload = event => { if (binding && !binding.session.status.localDurable) { event.preventDefault(); event.returnValue = ''; } };
    window.addEventListener('beforeunload', unload);
    work = run(async () => { actor = await request('/api/auth/me'); if (!actor.isAuthenticated) { invalidate(); return; } await open(); });
    // Return immediately so the Razor owner can dispose even while network initialization is pending.
    timer = setInterval(() => { if (!busy && !stopped) work = run(check); }, 2000);
    return { invalidate, async dispose() { stopped = true; clearInterval(timer); window.removeEventListener('beforeunload', unload); await work; await closeEditor(); } };
}

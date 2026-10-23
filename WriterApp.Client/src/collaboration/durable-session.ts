import * as Y from 'yjs';
import { getSchema } from '@tiptap/core';
import { yDocToProsemirrorJSON } from '@tiptap/y-tiptap';
import { HocuspocusProvider, HocuspocusProviderWebsocket } from '@hocuspocus/provider';
import { richExtensions } from '../editor-schema';
import { validateJson } from '../editor-content';
import { validateDocumentRoots } from './document-roots';
import { getSupport } from './session';
import { decode, digest, encode, normalizeScope, requireReceipt, same, scopeIdentity,
    type DurableReceipt, type JournalPage, type JournalScope, type JournalStore, type PendingEnvelope } from './journal';

export interface AccountLease { valid(): boolean; subscribe(stopped: () => void): () => void; invalidate?(): void }
/** Hosts activate this from authenticated account state, never from a manuscript or author-supplied user ID. */
export class CollaborationAccountFence {
    private epoch = 0;
    private identity: string | null = null;
    private generation: number | string = 0;
    private listeners = new Set<() => void>();
    private readonly channel?: BroadcastChannel;
    constructor(channelName: string | null = 'prosa-collaboration-account-v1') {
        if (channelName && typeof BroadcastChannel !== 'undefined') {
            this.channel = new BroadcastChannel(channelName);
            this.channel.onmessage = event => {
                if (event.data?.identity === this.identity && event.data?.generation === this.generation) return;
                // A different tab can invalidate a lease, but cannot authenticate this tab as another owner.
                this.identity = null; this.epoch++; for (const listener of [...this.listeners]) listener();
            };
        }
    }
    activate(scope: JournalScope | null, authenticationGeneration: number | string = 0) {
        const identity = scope ? [normalizeScope(scope).backend, scope.ownerId, scope.environment].join('\n') : null;
        if (identity === this.identity && authenticationGeneration === this.generation) return;
        this.identity = identity; this.generation = authenticationGeneration; this.epoch++;
        this.channel?.postMessage({ identity, generation: authenticationGeneration });
        for (const listener of [...this.listeners]) listener();
    }
    capture(scope: JournalScope): AccountLease {
        const identity = [normalizeScope(scope).backend, scope.ownerId, scope.environment].join('\n'), epoch = this.epoch;
        if (identity !== this.identity) throw new Error('collaboration_account_changed');
        return { valid: () => epoch === this.epoch && identity === this.identity,
            invalidate: () => { if (epoch === this.epoch) this.activate(null, this.generation); },
            subscribe: stopped => { const check = () => { if (epoch !== this.epoch || identity !== this.identity) stopped(); }; this.listeners.add(check); return () => this.listeners.delete(check); } };
    }
    close() { this.channel?.close(); this.identity = null; this.epoch++; for (const listener of [...this.listeners]) listener(); this.listeners.clear(); }
}
export interface SessionTicket { sessionId: string; version: number; ticket: string; roomName: string; webSocketUrl: string; expiresAtUnixMilliseconds: number; readOnly: boolean }
export interface DurableStatus { localDurable: boolean; remoteDurable: boolean; pending: number; queued: number; connected: boolean; error: string | null; recoveryReason: string | null; accountChanged: boolean }
interface Work { run(): Promise<void>; resolve(): void; reject(error: unknown): void }
const schema = getSchema(richExtensions(true));
const remote = Symbol('journal-committed-remote');
export function validateJournalDoc(doc: Y.Doc) {
    validateDocumentRoots(doc);
    const value = yDocToProsemirrorJSON(doc, 'default'); validateJson(value); schema.nodeFromJSON(value).check();
}
export interface DurableSessionOptions {
    store: JournalStore; account: AccountLease; committedSeed?: Uint8Array;
    getTicket?: (installationId: string, previous?: SessionTicket) => Promise<SessionTicket>;
    onStatus?: (status: DurableStatus) => void;
    autoReconnect?: boolean;
    interactionsEnabled?: boolean;
}
export interface RelativeAnchor { start: string; end: string }
export interface PresenceMember { connectionId: string; actorId: string; displayName?: string; cursor: RelativeAnchor | null }
function requireTicket(scope: JournalScope, ticket: SessionTicket, write: boolean) {
    const room = `${scope.environment}/${scope.documentId}/${scope.pageId}/${scope.generation}`, url = new URL(ticket.webSocketUrl);
    if (ticket.roomName !== room || write && ticket.readOnly || !ticket.ticket || ticket.ticket.length > 8192 || ticket.expiresAtUnixMilliseconds <= Date.now()
        || !(url.protocol === 'wss:' || url.protocol === 'ws:' && ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)) || url.username || url.password || url.search || url.hash)
        throw new Error('collaboration_access_unavailable');
    return room;
}
async function committedSeed(options: DurableSessionOptions): Promise<Uint8Array> {
    if (!options.getTicket) throw new Error('collaboration_offline_checkpoint_missing');
    // This ephemeral read ticket never owns an envelope. The actual installation ID is durable before any commit.
    const ticket = await options.getTicket(crypto.randomUUID());
    await options.store.assertCurrent(); if (!options.account.valid()) throw new Error('collaboration_account_changed');
    const room = requireTicket(options.store.scope, ticket, false);
    return new Promise((resolve, reject) => {
        const doc = new Y.Doc(); let provider: HocuspocusProvider | undefined, transport: HocuspocusProviderWebsocket | undefined, done = false;
        let stop = () => {};
        const timer = setTimeout(() => finish(new Error('collaboration_transport_unavailable')), 15000);
        const finish = (error?: Error, state?: Uint8Array) => {
            if (done) return; done = true; clearTimeout(timer); stop(); provider?.destroy(); transport?.destroy(); doc.destroy();
            if (error) reject(error); else resolve(state!);
        };
        stop = options.account.subscribe(() => finish(new Error('collaboration_account_changed')));
        transport = new HocuspocusProviderWebsocket({ url: ticket.webSocketUrl, autoConnect: false, maxAttempts: 1 });
        provider = new HocuspocusProvider({ websocketProvider: transport, name: room, token: ticket.ticket, document: doc, awareness: null,
            onSynced: ({ state }) => { if (!state) return; try { validateJournalDoc(doc); finish(undefined, Y.encodeStateAsUpdate(doc)); } catch { finish(new Error('collaboration_state_unsupported')); } },
            onAuthenticationFailed: () => finish(new Error('collaboration_access_unavailable')),
            onClose: () => finish(new Error('collaboration_transport_unavailable')),
        });
        provider.attach(); void transport.connect().catch(() => finish(new Error('collaboration_transport_unavailable')));
    });
}

export class JournaledSession {
    readonly doc: Y.Doc;
    readonly scope: JournalScope;
    private page: JournalPage;
    private jobs: Work[] = [];
    private running: Promise<void> | null = null;
    private storageError: string | null = null;
    private changedAccount = false;
    private disposed = false;
    private readonly stopAccount: () => void;
    private readonly stopWatch?: () => void;
    private provider?: HocuspocusProvider;
    private transport?: HocuspocusProviderWebsocket;
    private readDoc?: Y.Doc;
    private synced = false;
    private wireGeneration = 0;
    private sending: string | null = null;
    private lastTicket?: SessionTicket;
    private retryTimer?: ReturnType<typeof setTimeout>;
    private refreshTimer?: ReturnType<typeof setTimeout>;
    private heartbeatTimer?: ReturnType<typeof setInterval>;
    private manuallyOffline = true;
    private resumeRecovery = false;
    private retries = 0;
    private readonly statusListeners = new Set<(status: DurableStatus) => void>();
    private readonly presenceListeners = new Set<(members: PresenceMember[]) => void>();
    private presenceMembers: PresenceMember[] = [];
    watchPresence(listener: (members: PresenceMember[]) => void) { this.presenceListeners.add(listener); listener(this.presenceMembers); return () => { this.presenceListeners.delete(listener); }; }
    private clearPresence() { this.presenceMembers = []; for (const listener of this.presenceListeners) listener([]); }
    publishPresence(cursor: RelativeAnchor | null) {
        if (this.options.interactionsEnabled && this.current() && this.synced && !this.page.recoveryReason)
            this.provider?.sendStateless(JSON.stringify({ type: 'presence', cursor: this.status.remoteDurable ? cursor : null }));
    }

    private constructor(private readonly options: DurableSessionOptions, page: JournalPage, doc: Y.Doc) {
        this.doc = doc; this.page = page; this.scope = Object.freeze(normalizeScope(options.store.scope));
        this.stopAccount = options.account.subscribe(() => this.invalidate());
        this.stopWatch = options.store.watch?.(() => { if (this.current()) void this.enqueue(() => this.reload()).catch(() => {}); });
        doc.on('update', this.localUpdate);
        this.emit();
    }
    static async open(options: DurableSessionOptions) {
        if (!options.account.valid()) throw new Error('collaboration_account_changed');
        await options.store.assertCurrent();
        const support = await getSupport();
        if (!same(options.store.scope.support, support)) throw new Error('collaboration_upgrade_required');
        let page = await options.store.load();
        if (!page) {
            const bytes = options.committedSeed ?? await committedSeed(options);
            const seed = new Y.Doc(); try { Y.applyUpdate(seed, bytes); validateJournalDoc(seed); } finally { seed.destroy(); }
            await options.store.assertCurrent(); if (!options.account.valid()) throw new Error('collaboration_account_changed');
            page = await options.store.initialize(bytes);
        }
        const doc = new Y.Doc();
        try {
            Y.applyUpdate(doc, decode(page.state), remote); validateJournalDoc(doc);
            await options.store.assertCurrent(); if (!options.account.valid()) throw new Error('collaboration_account_changed');
            return new JournaledSession(options, page, doc);
        } catch (error) { doc.destroy(); throw error; }
    }
    private current() { return !this.disposed && !this.changedAccount && this.options.account.valid(); }
    private async requireCurrent() { if (!this.current()) throw new Error('collaboration_account_changed'); await this.options.store.assertCurrent(); if (!this.current()) throw new Error('collaboration_account_changed'); }
    get status(): DurableStatus {
        const local = this.jobs.length === 0 && !this.storageError;
        return Object.freeze({ localDurable: local, remoteDurable: this.current() && !this.page.recoveryReason && local && this.page.pending.length === 0 && (this.page.receipts.length > 0 || this.synced),
            pending: this.page.pending.length, queued: this.jobs.length, connected: this.synced, error: this.storageError,
            recoveryReason: this.page.recoveryReason, accountChanged: this.changedAccount });
    }
    get installationId() { return this.page.installationId; }
    get pending(): readonly PendingEnvelope[] { return structuredClone(this.page.pending); }
    watchStatus(listener: (status: DurableStatus) => void) { this.statusListeners.add(listener); listener(this.status); return () => this.statusListeners.delete(listener); }
    private emit() { const status = this.status; this.options.onStatus?.(status); for (const listener of this.statusListeners) listener(status); }
    private localUpdate = (bytes: Uint8Array, _origin: unknown, _doc: Y.Doc, transaction: Y.Transaction) => {
        if (!transaction.local || _origin === remote || !this.current()) return;
        // Capture this exact transaction's checkpoint NOW, never a later in-memory state with unjournaled edits.
        const update = new Uint8Array(bytes), state = Y.encodeStateAsUpdate(this.doc), updateId = crypto.randomUUID();
        void this.enqueue(async () => {
            const envelope: PendingEnvelope = { type: 'commitUpdate', protocolVersion: 1, documentId: this.scope.documentId, pageId: this.scope.pageId,
                generation: this.scope.generation, installationId: this.page.installationId, updateId, update: encode(update), sha256: await digest(update) };
            await this.persist(state, envelope);
        }).catch(() => {});
    };
    private enqueue(run: () => Promise<void>) {
        const result = new Promise<void>((resolve, reject) => this.jobs.push({ run, resolve, reject })); this.emit(); this.pump(); return result;
    }
    private pump() {
        if (this.running || this.storageError) return;
        this.running = (async () => {
            while (this.jobs.length && !this.storageError) {
                const job = this.jobs[0];
                try { await job.run(); this.jobs.shift(); job.resolve(); }
                catch (error) {
                    this.storageError = String(error).includes('account_changed') ? 'collaboration_account_changed' : 'collaboration_local_save_failed';
                    if (this.storageError.includes('account_changed')) this.invalidate(); else this.disconnectWire();
                    job.reject(error); break; // Retain immutable job and bytes for retry/recovery; never skip to a later snapshot.
                }
                this.emit();
            }
        })().finally(() => { this.running = null; this.emit(); if (this.current()) void this.deliver(); });
    }
    private async persist(captured: Uint8Array, envelope?: PendingEnvelope) {
        for (let attempt = 0; attempt < 16; attempt++) {
            const latest = await this.options.store.load(); if (!latest) throw new Error('collaboration_checkpoint_missing');
            const candidate = new Y.Doc();
            try {
                Y.applyUpdate(candidate, decode(latest.state)); Y.applyUpdate(candidate, captured); validateJournalDoc(candidate);
                try { this.page = await this.options.store.save(latest.revision, Y.encodeStateAsUpdate(candidate), envelope); }
                catch (error) { if (String(error).includes('revision_changed')) continue; throw error; }
                if (this.current()) Y.applyUpdate(this.doc, decode(this.page.state), remote);
                return;
            } finally { candidate.destroy(); }
        }
        throw new Error('collaboration_journal_busy');
    }
    private async reload() {
        await this.requireCurrent(); const page = await this.options.store.load(); if (!page) throw new Error('collaboration_checkpoint_missing');
        const candidate = new Y.Doc(); try { Y.applyUpdate(candidate, decode(page.state)); validateJournalDoc(candidate); } finally { candidate.destroy(); }
        await this.requireCurrent(); this.page = page; Y.applyUpdate(this.doc, decode(page.state), remote);
    }
    async flush() { this.pump(); while (this.running) await this.running; if (this.storageError || this.jobs.length) throw new Error(this.storageError ?? 'collaboration_local_save_failed'); }
    async retryLocalPersistence() { await this.requireCurrent(); this.storageError = null; await this.flush(); }
    exportRecovery() { return { version: 1, scope: structuredClone(this.scope), state: encode(Y.encodeStateAsUpdate(this.doc)), pending: this.pending,
        queuedTransactions: this.jobs.length, localDurable: this.status.localDurable }; }
    invalidate(accountChanged = true) { if (this.changedAccount) return; this.changedAccount = true; this.manuallyOffline = true; this.disconnectWire(); if(accountChanged)this.options.account.invalidate?.(); this.emit(); }
    disconnect() { this.manuallyOffline = true; this.disconnectWire(); this.emit(); }
    private disconnectWire() {
        this.clearPresence();
        this.wireGeneration++; clearTimeout(this.retryTimer); clearTimeout(this.refreshTimer); clearInterval(this.heartbeatTimer); this.synced = false; this.sending = null;
        const provider = this.provider, transport = this.transport, readDoc = this.readDoc;
        this.provider = undefined; this.transport = undefined; this.readDoc = undefined;
        provider?.destroy(); transport?.destroy(); readDoc?.destroy();
    }
    private schedule() {
        if (this.manuallyOffline || !this.current() || this.storageError || this.page.recoveryReason || this.options.autoReconnect === false) return;
        clearTimeout(this.retryTimer); this.retryTimer = setTimeout(() => { void this.connect().catch(() => {}); }, Math.min(30000, 500 * 2 ** Math.min(this.retries++, 6)));
    }
    async connect(recover = false) {
        await this.requireCurrent(); await this.flush();
        if (!this.options.getTicket) throw new Error('collaboration_transport_not_configured');
        // A transient authentication failure (for example during a server restart)
        // must not permanently strand an entirely committed checkpoint. Revalidate
        // its exact account/membership/generation with a fresh ticket. Keep private
        // unsent drafts and schema/generation failures behind explicit recovery.
        await this.reload();
        recover ||= this.page.recoveryReason === 'access_unavailable' && this.page.pending.length === 0;
        if (this.page.recoveryReason && !recover) throw new Error('collaboration_private_recovery_required');
        this.disconnectWire(); this.manuallyOffline = false; this.resumeRecovery = recover;
        if (recover) this.lastTicket = undefined;
        const generation = this.wireGeneration;
        try {
            const ticket = await this.options.getTicket(this.page.installationId, this.lastTicket); await this.requireCurrent();
            if (generation !== this.wireGeneration) return;
            const room = requireTicket(this.scope, ticket, true);
            this.lastTicket = ticket;
            // This doc starts EMPTY and is NEVER populated from the local journal or editor.
            const readDoc = new Y.Doc(); this.readDoc = readDoc;
            const transport = new HocuspocusProviderWebsocket({ url: ticket.webSocketUrl, autoConnect: false, maxAttempts: 1 }); this.transport = transport;
            const valid = () => generation === this.wireGeneration && this.current();
            const saveRemote = () => {
                if (!valid() || !this.synced) return;
                const bytes = Y.encodeStateAsUpdate(readDoc);
                void this.enqueue(async () => { if (!valid()) return; await this.requireCurrent(); await this.persist(bytes); }).catch(() => {});
            };
            readDoc.on('update', saveRemote);
            const provider = new HocuspocusProvider({ websocketProvider: transport, name: room, token: ticket.ticket, document: readDoc, awareness: null,
                onSynced: ({ state }) => {
                    if (!state || !valid()) return; this.synced = true; this.retries = 0; saveRemote();
                    clearInterval(this.heartbeatTimer); this.heartbeatTimer = setInterval(() => { if (valid() && this.synced) provider.sendStateless(JSON.stringify({type:'heartbeat'})); }, 10000);
                    if (this.resumeRecovery) void this.enqueue(async () => { await this.requireCurrent(); this.page = await this.options.store.recovery(null); this.resumeRecovery = false; }).catch(() => {});
                    this.emit(); void this.deliver();
                },
                onStateless: ({ payload }) => { if (valid()) void this.receiveReceipt(payload, generation); },
                onAuthenticationFailed: () => { if (valid()) void this.block('access_unavailable'); },
                onClose: ({ event }) => {
                    if (!valid()) return;
                    if (event.code === 4403 || event.code === 4401) { void this.block('access_unavailable'); return; }
                    this.disconnectWire(); this.emit(); this.schedule();
                },
            });
            this.provider = provider; provider.attach(); void transport.connect().catch(() => { if (valid()) { this.disconnectWire(); this.emit(); this.schedule(); } });
            this.refreshTimer = setTimeout(() => { if (valid()) void this.connect().catch(() => {}); }, Math.max(1000, ticket.expiresAtUnixMilliseconds - Date.now() - 30000));
        } catch (error) {
            if (String(error).includes('account_changed')) this.invalidate();
            else if (/access_unavailable|generation_changed|upgrade_required/.test(String(error))) await this.block('access_unavailable');
            else { this.disconnectWire(); this.schedule(); }
            throw error;
        }
    }
    private async deliver() {
        if (!this.current() || !this.synced || !this.provider || this.jobs.length || this.storageError || this.page.recoveryReason || this.sending) return;
        try {
            await this.requireCurrent(); const generation = this.wireGeneration; const page = await this.options.store.load(); await this.requireCurrent();
            if (generation !== this.wireGeneration || !this.synced || !page || this.sending || this.jobs.length) return;
            this.page = page; const envelope = page.pending[0]; if (!envelope) { this.emit(); return; }
            this.sending = envelope.updateId; this.provider?.sendStateless(JSON.stringify(envelope)); this.emit();
        } catch (error) { if (String(error).includes('account_changed')) this.invalidate(); else this.disconnectWire(); }
    }
    private async receiveReceipt(payload: string, generation: number) {
        try {
            await this.requireCurrent(); if (generation !== this.wireGeneration) return;
            const message = JSON.parse(payload);
            if (message.type === 'presenceSnapshot' && this.options.interactionsEnabled) {
                if (!Array.isArray(message.members) || message.members.length > 64 || message.members.some((m: PresenceMember) => !m || typeof m.actorId !== 'string' || m.actorId.length > 128
                    || m.displayName !== undefined && (typeof m.displayName !== 'string' || m.displayName.length > 256)
                    || typeof m.connectionId !== 'string' || m.connectionId.length > 128 || m.cursor !== null && (!m.cursor || typeof m.cursor.start !== 'string' || typeof m.cursor.end !== 'string' || m.cursor.start.length > 1024 || m.cursor.end.length > 1024))) throw new Error('collaboration_presence_invalid');
                this.presenceMembers = message.members; for (const listener of this.presenceListeners) listener(this.presenceMembers); return;
            }
            if (message.type === 'commitRejected') {
                if (/unsupported|upgrade|generation/.test(message.code)) await this.block('schema_changed');
                else { this.disconnectWire(); this.schedule(); } return;
            }
            if (message.type !== 'durableReceipt') return;
            const receipt = message.receipt as DurableReceipt; requireReceipt(this.page, receipt);
            const prior = this.page.receipts.find(r => r.updateId === receipt.updateId), envelope = this.page.pending.find(e => e.updateId === receipt.updateId);
            if (prior ? !same(prior, receipt) : !envelope || envelope.sha256 !== receipt.sha256) throw new Error('collaboration_receipt_invalid');
            await this.enqueue(async () => {
                await this.requireCurrent(); if (generation !== this.wireGeneration) return;
                this.page = await this.options.store.acknowledge(receipt);
                if (this.sending === receipt.updateId) this.sending = null;
            });
            this.emit(); void this.deliver();
        } catch (error) { if (String(error).includes('account_changed')) this.invalidate(); else await this.block('access_unavailable'); }
    }
    private async block(reason: string) {
        this.disconnectWire();
        if (!this.current()) return;
        await this.enqueue(async () => { await this.requireCurrent(); this.page = await this.options.store.recovery(reason); }).catch(() => {}); this.emit();
    }
    async dispose() {
        if (this.disposed) return; this.manuallyOffline = true; this.disconnectWire();
        // Refuse to discard an unsaved in-memory recovery copy when a route/host asks to dispose.
        await this.flush(); this.doc.off('update', this.localUpdate); this.stopWatch?.(); this.stopAccount(); this.disposed = true; this.doc.destroy();
    }
}

/** The web host still supplies a fence driven by its authenticated account generation. No tickets enter storage. */
export function webTicketSource(scope: JournalScope) {
    scope = normalizeScope(scope);
    return async (installationId: string, previous?: SessionTicket): Promise<SessionTicket> => {
        if (new URL(scope.backend).origin !== location.origin) throw new Error('collaboration_access_unavailable');
        const meResponse = await fetch(new URL('api/auth/me', scope.backend), { credentials: 'same-origin', cache: 'no-store', signal: AbortSignal.timeout(15000) });
        if (!meResponse.ok) throw new Error('collaboration_access_unavailable'); const me = await meResponse.json();
        // Scope.ownerId is the local authenticated actor. The backend checks the manuscript owner's funding.
        if (!me.isAuthenticated || me.userId !== scope.ownerId) throw new Error('collaboration_account_changed');
        const path = `api/documents/${scope.documentId}/pages/${scope.pageId}/collaboration/sessions`;
        const response = await fetch(new URL(previous ? `${path}/${previous.sessionId}/refresh` : path, scope.backend), {
            method: 'POST', credentials: 'same-origin', cache: 'no-store', signal: AbortSignal.timeout(15000), headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(previous ? { version: previous.version } : { generation: scope.generation, installationId, ...scope.support, requestCommit: true, membershipRevision: scope.membershipRevision ?? 0, environment: scope.environment }),
        });
        if (!response.ok) throw new Error(response.status === 403 || response.status === 404 ? 'collaboration_access_unavailable' : 'collaboration_transport_unavailable');
        return response.json();
    };
}

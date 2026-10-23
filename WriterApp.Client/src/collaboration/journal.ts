import type { EditorSupport, PageIdentity } from './session';
import { indexedDbConnection } from '../../wwwroot/js/indexeddb-connection.js';

export interface JournalScope extends PageIdentity { backend: string; ownerId: string; support: EditorSupport; membershipRevision?: number }
export interface PendingEnvelope {
    type: 'commitUpdate'; protocolVersion: number; documentId: string; pageId: string; generation: string;
    installationId: string; updateId: string; update: string; sha256: string;
}
export interface DurableReceipt {
    pageId: string; generation: string; installationId: string; updateId: string; sha256: string;
    commitSequence: number; committedAtUnixMilliseconds: number;
}
export interface JournalPage {
    version: number; scope: JournalScope; installationId: string; revision: number; state: string; stateSha256: string;
    pending: PendingEnvelope[]; receipts: DurableReceipt[]; recoveryReason: string | null;
}
export interface JournalStore {
    readonly scope: JournalScope;
    load(): Promise<JournalPage | null>;
    initialize(state: Uint8Array): Promise<JournalPage>;
    save(revision: number, state: Uint8Array, envelope?: PendingEnvelope): Promise<JournalPage>;
    acknowledge(receipt: DurableReceipt): Promise<JournalPage>;
    recovery(reason: string | null): Promise<JournalPage>;
    assertCurrent(): Promise<void>;
    watch?(changed: () => void): () => void;
}
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const hashPattern = /^[0-9a-f]{64}$/;
const nonzero = (id: string) => typeof id === 'string' && uuid.test(id) && id !== '00000000-0000-0000-0000-000000000000';
const only = (value: object, fields: string[]) => Object.keys(value).every(key => fields.includes(key));
const fail = (code = 'collaboration_journal_invalid'): never => { throw new Error(code); };
export function same(a: unknown, b: unknown): boolean {
    if (a === b) return true;
    if (!a || !b || typeof a !== 'object' || typeof b !== 'object' || Array.isArray(a) !== Array.isArray(b)) return false;
    const keys = Object.keys(a).sort(), other = Object.keys(b).sort();
    return keys.length === other.length && keys.every((key, index) => key === other[index] && same((a as any)[key], (b as any)[key]));
}
export async function digest(bytes: Uint8Array | string) {
    const result = await crypto.subtle.digest('SHA-256', typeof bytes === 'string' ? new TextEncoder().encode(bytes) : new Uint8Array(bytes));
    return [...new Uint8Array(result)].map(x => x.toString(16).padStart(2, '0')).join('');
}
export function encode(bytes: Uint8Array) {
    let value = ''; for (let offset = 0; offset < bytes.length; offset += 8192) value += String.fromCharCode(...bytes.subarray(offset, offset + 8192));
    return btoa(value);
}
export function decode(value: string, maximum = 16 * 1024 * 1024) {
    if (typeof value !== 'string' || value.length > Math.ceil(maximum / 3) * 4 || !/^[A-Za-z0-9+/]+={0,2}$/.test(value)) return fail();
    let raw: string; try { raw = atob(value); } catch { return fail(); }
    // Avoid the string iterator and one callback per byte for every checked
    // checkpoint load. A journal read can decode the full manuscript repeatedly.
    const bytes = new Uint8Array(raw.length);
    for (let index = 0; index < raw.length; index++) bytes[index] = raw.charCodeAt(index);
    if (!bytes.length || bytes.length > maximum || encode(bytes) !== value) return fail(); return bytes;
}
export function normalizeScope(value: JournalScope): JournalScope {
    const backend = new URL(value.backend);
    if (!(backend.protocol === 'https:' || backend.protocol === 'http:' && ['localhost', '127.0.0.1', '[::1]'].includes(backend.hostname))
        || backend.username || backend.password || backend.search || backend.hash || typeof value.ownerId !== 'string' || !value.ownerId.trim()
        || value.ownerId.length > 256 || /[\u0000-\u001f\u007f-\u009f]/.test(value.ownerId) || !/^[A-Za-z0-9_-]{1,64}$/.test(value.environment)
        || ![value.documentId, value.pageId, value.generation].every(id => nonzero(id?.toLowerCase()))
        || !Number.isSafeInteger(value.membershipRevision ?? 0) || (value.membershipRevision ?? 0) < 0
        || value.support?.contentContractVersion !== 2 || value.support?.protocolVersion !== 1 || !hashPattern.test(value.support?.schemaFingerprint)
        || !only(value.support, ['contentContractVersion', 'protocolVersion', 'schemaFingerprint'])) return fail();
    return Object.freeze({ backend: backend.href.replace(/\/+$/, '') + '/', ownerId: value.ownerId, environment: value.environment,
        documentId: value.documentId.toLowerCase(), pageId: value.pageId.toLowerCase(), generation: value.generation.toLowerCase(), support: Object.freeze({ ...value.support }),
        ...(value.membershipRevision ? { membershipRevision: value.membershipRevision } : {}) });
}
export function scopeIdentity(scope: JournalScope) {
    scope = normalizeScope(scope); return [scope.backend, scope.ownerId, scope.environment, scope.documentId, scope.pageId, scope.generation,
        ...(scope.membershipRevision ? [String(scope.membershipRevision)] : [])].join('\n');
}
export function requireReceipt(page: JournalPage, receipt: DurableReceipt) {
    if (!receipt || receipt.pageId !== page.scope.pageId || receipt.generation !== page.scope.generation || receipt.installationId !== page.installationId
        || !nonzero(receipt.updateId) || !hashPattern.test(receipt.sha256) || !Number.isSafeInteger(receipt.commitSequence) || receipt.commitSequence < 1
        || !Number.isSafeInteger(receipt.committedAtUnixMilliseconds) || receipt.committedAtUnixMilliseconds < 1
        || !only(receipt, ['pageId', 'generation', 'installationId', 'updateId', 'sha256', 'commitSequence', 'committedAtUnixMilliseconds'])) fail('collaboration_receipt_invalid');
}
export async function requireEnvelope(page: JournalPage, envelope: PendingEnvelope) {
    if (!envelope || envelope.type !== 'commitUpdate' || envelope.protocolVersion !== 1 || envelope.documentId !== page.scope.documentId
        || envelope.pageId !== page.scope.pageId || envelope.generation !== page.scope.generation || envelope.installationId !== page.installationId
        || !nonzero(envelope.updateId) || !hashPattern.test(envelope.sha256)
        || !only(envelope, ['type', 'protocolVersion', 'documentId', 'pageId', 'generation', 'installationId', 'updateId', 'update', 'sha256'])
        || await digest(decode(envelope.update, 512 * 1024)) !== envelope.sha256) fail();
}
export async function validatePage(page: JournalPage, scope: JournalScope) {
    if (!page || page.version !== 1 || !same(normalizeScope(page.scope), normalizeScope(scope)) || !nonzero(page.installationId)
        || !Number.isSafeInteger(page.revision) || page.revision < 0 || !hashPattern.test(page.stateSha256)
        || !only(page, ['version', 'scope', 'installationId', 'revision', 'state', 'stateSha256', 'pending', 'receipts', 'recoveryReason'])
        || !Array.isArray(page.pending) || !Array.isArray(page.receipts) || page.pending.length > 4096 || page.receipts.length > 50000
        || ![null, 'access_unavailable', 'generation_changed', 'schema_changed'].includes(page.recoveryReason)
        || await digest(decode(page.state)) !== page.stateSha256) fail();
    const ids = new Set<string>(), sequences = new Set<number>(); let size = 0;
    for (const envelope of page.pending) { await requireEnvelope(page, envelope); if (ids.has(envelope.updateId)) fail(); ids.add(envelope.updateId); size += envelope.update.length; }
    for (const receipt of page.receipts) { requireReceipt(page, receipt); if (ids.has(receipt.updateId) || sequences.has(receipt.commitSequence)) fail(); ids.add(receipt.updateId); sequences.add(receipt.commitSequence); }
    if (size > 44 * 1024 * 1024 || new TextEncoder().encode(JSON.stringify(page)).length > 64 * 1024 * 1024) fail('collaboration_journal_full');
    return page;
}
interface StoredPage { key: string; payload: string; sha256: string }

/** One aggregate transaction couples its Yjs checkpoint, immutable envelopes and receipt bookkeeping. */
export class IndexedDbJournal implements JournalStore {
    readonly scope: JournalScope;
    private readonly key: Promise<string>;
    private readonly connection: ReturnType<typeof indexedDbConnection>;
    private readonly channel: BroadcastChannel;
    constructor(scope: JournalScope, databaseName = 'prosa-collaboration-v1') {
        this.scope = Object.freeze(normalizeScope(scope)); this.key = digest(scopeIdentity(this.scope));
        this.channel = new BroadcastChannel(databaseName);
        this.connection = indexedDbConnection(databaseName, 1, db => {
            db.createObjectStore('pages', { keyPath: 'key' }); db.createObjectStore('settings', { keyPath: 'key' });
        });
    }
    async assertCurrent() { /* Browser account lifetime is checked by the session's account fence. */ }
    private async readRaw() {
        const key = await this.key, tx = await this.connection.transaction('pages');
        return new Promise<StoredPage | undefined>((resolve, reject) => {
            const get = tx.objectStore('pages').get(key);
            tx.oncomplete = () => resolve(get.result); tx.onabort = () => reject(tx.error); tx.onerror = () => {};
        });
    }
    private async unpack(value: StoredPage | undefined) {
        if (!value) return null;
        if (typeof value.payload !== 'string' || value.payload.length > 64 * 1024 * 1024 || await digest(value.payload) !== value.sha256) return fail();
        return validatePage(JSON.parse(value.payload), this.scope);
    }
    async load() { return this.unpack(await this.readRaw()); }
    private async replace(previous: StoredPage | undefined, page: JournalPage) {
        await validatePage(page, this.scope);
        const key = await this.key, payload = JSON.stringify(page), sha256 = await digest(payload);
        const tx = await this.connection.transaction('pages', 'readwrite', { durability: 'strict' });
        await new Promise<void>((resolve, reject) => {
            // Never await hashing inside the transaction: IndexedDB would auto-commit before the outbox write.
            let conflict = false; const store = tx.objectStore('pages'), get = store.get(key);
            get.onsuccess = () => {
                if (get.result?.sha256 !== previous?.sha256 || get.result?.payload !== previous?.payload) { conflict = true; tx.abort(); return; }
                store.put({ key, payload, sha256 });
            };
            tx.oncomplete = () => resolve(); tx.onerror = () => {};
            tx.onabort = () => reject(conflict ? new Error('collaboration_journal_revision_changed') : tx.error ?? new Error('collaboration_storage_failed'));
        });
        this.channel.postMessage({ key }); return structuredClone(page);
    }
    async initialize(state: Uint8Array) {
        const prior = await this.readRaw(); if (prior) return (await this.unpack(prior))!;
        const tx = await this.connection.transaction('settings', 'readwrite', { durability: 'strict' });
        const installationId = await new Promise<string>((resolve, reject) => {
            const store = tx.objectStore('settings'), get = store.get('installation'); let value: string;
            get.onsuccess = () => { value = get.result?.value ?? crypto.randomUUID(); if (!nonzero(value)) { tx.abort(); return; } store.put({ key: 'installation', value }); };
            tx.oncomplete = () => resolve(value); tx.onabort = () => reject(tx.error ?? new Error('collaboration_installation_invalid')); tx.onerror = () => {};
        });
        const page: JournalPage = { version: 1, scope: this.scope, installationId, revision: 0, state: encode(state), stateSha256: await digest(state), pending: [], receipts: [], recoveryReason: null };
        try { return await this.replace(undefined, page); }
        catch (error) { if (String(error).includes('revision_changed')) return (await this.load())!; throw error; }
    }
    async save(revision: number, state: Uint8Array, envelope?: PendingEnvelope) {
        const raw = await this.readRaw(), page = await this.unpack(raw); if (!page) return fail();
        if (envelope) {
            await requireEnvelope(page, envelope);
            const prior = page.pending.find(e => e.updateId === envelope.updateId), receipt = page.receipts.find(r => r.updateId === envelope.updateId);
            if (prior || receipt) { if (prior && !same(prior, envelope) || receipt && receipt.sha256 !== envelope.sha256) return fail(); return page; }
        }
        if (page.revision !== revision) return fail('collaboration_journal_revision_changed');
        return this.replace(raw, { ...page, revision: page.revision + 1, state: encode(state), stateSha256: await digest(state), pending: envelope ? [...page.pending, envelope] : page.pending });
    }
    private async mutate(change: (page: JournalPage) => JournalPage) {
        for (let attempt = 0; attempt < 8; attempt++) {
            const raw = await this.readRaw(), page = await this.unpack(raw); if (!page) return fail(); const next = change(page);
            if (next === page) return page;
            try { return await this.replace(raw, next); } catch (error) { if (!String(error).includes('revision_changed')) throw error; }
        }
        return fail('collaboration_journal_busy');
    }
    async acknowledge(receipt: DurableReceipt) {
        return this.mutate(page => {
            requireReceipt(page, receipt); const prior = page.receipts.find(r => r.updateId === receipt.updateId);
            if (prior) { if (!same(prior, receipt)) return fail('collaboration_receipt_invalid'); return page; }
            const envelope = page.pending.find(e => e.updateId === receipt.updateId);
            if (!envelope || envelope.sha256 !== receipt.sha256) return fail('collaboration_receipt_invalid');
            return { ...page, revision: page.revision + 1, pending: page.pending.filter(e => e.updateId !== receipt.updateId), receipts: [...page.receipts, receipt] };
        });
    }
    async recovery(reason: string | null) { return this.mutate(page => ({ ...page, revision: page.revision + 1, recoveryReason: reason })); }
    watch(changed: () => void) {
        const listener = async (event: MessageEvent) => { if (event.data?.key === await this.key) changed(); };
        this.channel.addEventListener('message', listener); return () => this.channel.removeEventListener('message', listener);
    }
    async listRecovery() {
        const tx = await this.connection.transaction('pages');
        const rows = await new Promise<StoredPage[]>((resolve, reject) => { const get = tx.objectStore('pages').getAll(); tx.oncomplete = () => resolve(get.result); tx.onabort = () => reject(tx.error); });
        const scopes: JournalScope[] = [];
        for (const row of rows) {
            if (await digest(row.payload) !== row.sha256) continue;
            const page = JSON.parse(row.payload) as JournalPage;
            if (page.scope.backend === this.scope.backend && page.scope.ownerId === this.scope.ownerId && page.scope.environment === this.scope.environment) {
                await validatePage(page, page.scope); if (page.pending.length || page.recoveryReason) scopes.push(page.scope);
            }
        }
        return scopes;
    }
    async close() { this.channel.close(); await this.connection.close(); }
}

export class NativeJournalAdapter implements JournalStore {
    readonly scope: JournalScope;
    constructor(private readonly bridge: { invokeMethodAsync<T>(method: string, ...args: unknown[]): Promise<T> }, scope: JournalScope) { this.scope = normalizeScope(scope); }
    async assertCurrent() { if (!await this.bridge.invokeMethodAsync<boolean>('IsCurrent')) fail('collaboration_account_changed'); }
    private async call(method: string, ...args: unknown[]) { const page = await this.bridge.invokeMethodAsync<JournalPage | null>(method, ...args); return page ? validatePage(page, this.scope) : null; }
    load() { return this.call('Load'); }
    async initialize(state: Uint8Array) { return (await this.call('Initialize', encode(state), await digest(state)))!; }
    async save(revision: number, state: Uint8Array, envelope?: PendingEnvelope) { return (await this.call('Save', { revision, state: encode(state), stateSha256: await digest(state), envelope: envelope ?? null }))!; }
    async acknowledge(receipt: DurableReceipt) { return (await this.call('Acknowledge', receipt))!; }
    async recovery(reason: string | null) { return (await this.call('Recovery', reason))!; }
}

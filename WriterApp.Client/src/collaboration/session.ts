import { Editor, Extension, getSchema } from "@tiptap/core";
import { Plugin } from '@tiptap/pm/state';
import { DOMSerializer } from '@tiptap/pm/model';
import Collaboration from "@tiptap/extension-collaboration";
import { prosemirrorJSONToYDoc, yDocToProsemirrorJSON, yUndoPluginKey, ySyncPluginKey } from "@tiptap/y-tiptap";
import * as Y from "yjs";
import { richExtensions } from "../editor-schema";
import { prepare, validateJson } from "../editor-content";
import contract from "../../../WriterApp.Shared/Editor/content-contract.json";
import { validateDocumentRoots } from './document-roots';
import { JournaledSession, type DurableSessionOptions } from './durable-session';
import { NativeJournalAdapter, type JournalScope } from './journal';
import { EditorInteractions } from './interactions';
export { IndexedDbJournal, NativeJournalAdapter } from './journal';
export { JournaledSession, CollaborationAccountFence, webTicketSource } from './durable-session';

export interface PageIdentity { environment: string; documentId: string; pageId: string; generation: string }
export interface EditorSupport { contentContractVersion: number; protocolVersion: number; schemaFingerprint: string }
export interface PageCapability extends EditorSupport { pageId: string; generation: string | null }
export interface Capabilities {
    documentId: string; mode: number; featureAvailable: boolean; canRead: boolean;
    canWriteCollaboratively: boolean; pages: PageCapability[]; membershipRevision?: number; interactionsAvailable?: boolean; environment?: string | null;
}
/** Volatile editor updates only. Durability, receipts and authenticated transport belong to P5/P6. */
export interface EditorUpdate { identity: PageIdentity; support: EditorSupport; bytes: Uint8Array }
export interface SessionCallbacks {
    onLocalUpdate?: (update: EditorUpdate) => void;
    onProjection?: (html: string) => void;
}

const schema = getSchema(richExtensions(true));
const remoteOrigin = Symbol("committed-remote-update");
const active = new WeakMap<HTMLElement, IsolatedSession>();
const journalHosts = new WeakMap<HTMLElement, JournaledEditor>();
const openingHosts = new WeakSet<HTMLElement>();
const viewerProjection = 'prosa-viewer-projection';
// Checked tools calculate a candidate in an isolated editor. No preview transaction
// enters the shared Y.Doc or its durable outbox before the server accepts the source.
export function createCheckedPreview(editor: Editor) {
    const preview = new Editor({element:document.createElement('div'), extensions:richExtensions(true), content:editor.getJSON()});
    (preview as any).__prosaCheckedPreview=true;
    preview.commands.setTextSelection({from:editor.state.selection.from,to:editor.state.selection.to});
    return preview;
}
export function checkedContent(editor: Editor): string {
    const json=editor.getJSON();validateJson(json);
    const node=schema.nodeFromJSON(json);node.check();
    const wrapper=document.createElement('div');
    wrapper.appendChild(DOMSerializer.fromSchema(schema).serializeFragment(node.content,{document}));
    return wrapper.innerHTML;
}
export function retainCheckedDraft(accountKey: string, documentId: string, pageId: string, operation: string, html: string) {
    sessionStorage.setItem(`prosa-checked-draft:${accountKey}:${documentId}:${pageId}`,JSON.stringify({operation,html,createdAt:Date.now()}));
}
export function createReadOnlyEditor(host: HTMLElement, html: string, factory: (options: any) => Editor) {
    const readOnly = Extension.create({name:'prosaReadOnly',addProseMirrorPlugins:()=>[new Plugin({filterTransaction:tr=>!tr.docChanged || tr.getMeta(viewerProjection)===true})]});
    const editor=factory({element:host,extensions:[...richExtensions(true),readOnly],content:html,editable:false});
    (editor as any).__prosaCanEdit=()=>false;
    return editor;
}
export function updateReadOnlyEditor(editor: Editor, json: any) {
    const value=typeof json==='string'?JSON.parse(json):json;
    validateJson(value);
    // Node types belong to a particular schema instance, including presentation
    // extensions installed by the full editor factory.
    const doc=editor.schema.nodeFromJSON(value);doc.check();
    if(editor.state.doc.eq(doc))return;
    editor.view.dispatch(editor.state.tr.replaceWith(0,editor.state.doc.content.size,doc.content).setMeta(viewerProjection,true).setMeta('addToHistory',false));
}

export interface JournaledEditor {
    editor: Editor;
    session: JournaledSession;
    interactions: EditorInteractions;
    invalidate(): void;
    destroy(): Promise<void>;
}
/** Only a migrated page with a current host-authenticated capability may be mounted. Server flags remain closed. */
export async function createJournaledEditor(host: HTMLElement, options: DurableSessionOptions, capabilities: Capabilities, factory?: (options: any) => Editor): Promise<JournaledEditor> {
    const scope = options.store.scope, support = await getSupport();
    requireCapability(capabilities, scope, support);
    if (capabilities.environment && capabilities.environment !== scope.environment) throw new Error('collaboration_environment_changed');
    if ((capabilities.membershipRevision ?? 0) !== (scope.membershipRevision ?? 0)) throw new Error('collaboration_access_changed');
    if (journalHosts.has(host) || active.has(host) || openingHosts.has(host)) throw new Error('collaboration_editor_already_mounted');
    openingHosts.add(host);
    let editor: Editor | undefined;
    let opened: JournaledSession | undefined;
    try {
        const session = opened = await JournaledSession.open(options);
        const interactions = new EditorInteractions(session);
        // Programmatic toolbar commands also need a fence after access changes.
        const canEdit = () => { const s=session.status; return !s.accountChanged && !s.error && !s.recoveryReason; };
        // Render retained/committed Yjs content even while user edits are fenced.
        // Blocking the binding's initial projection leaves recovery views blank.
        const permissionFence = Extension.create({name:'prosaPermissionFence',addProseMirrorPlugins:()=>[new Plugin({filterTransaction:tr=>!tr.docChanged || !!tr.getMeta(ySyncPluginKey) || canEdit()})]});
        const editorOptions = { element: host, extensions: [...richExtensions(true), Collaboration.configure({ document: session.doc, field: 'default' }), blockReplacement, interactions.extension, permissionFence],
            enableContentCheck: true, parseOptions: { preserveWhitespace: 'full' as const },
            editorProps: { attributes: { role: 'textbox', 'aria-label': 'Manuscript text', 'aria-multiline': 'true' } },
            onContentError: () => session.invalidate(false) };
        editor = factory ? factory(editorOptions) : new Editor(editorOptions);
        (editor as any).__prosaCanEdit = canEdit;
        const stop = session.watchStatus(status => {
            const editable = !status.accountChanged && !status.error && !status.recoveryReason;
            // Durability notifications are frequent; only a permission transition needs
            // to update the view. This is not a content edit or an undo transaction.
            if (editor!.isEditable !== editable) editor!.setEditable(editable, false);
            if (!editable) interactions.clear();
        });
        let destroyed = false;
        interactions.attach(editor, options.interactionsEnabled === true);
        const binding: JournaledEditor = { editor, session, interactions,
            // A manuscript grant change must not broadcast a global account sign-out.
            invalidate() { interactions.clear(); session.invalidate(false); editor!.setEditable(false); },
            async destroy() {
                if (destroyed) return;
                // Failed disk writes keep the editor, Y.Doc and immutable recovery queue alive.
                await session.flush();
                if (destroyed) return;
                destroyed = true; stop(); interactions.destroy(); editor!.destroy(); await session.dispose();
                if (journalHosts.get(host) === binding) { journalHosts.delete(host); host.replaceChildren(); }
            } };
        journalHosts.set(host, binding);
        if (options.getTicket) void session.connect().catch(() => {});
        return binding;
    } catch (error) { editor?.destroy(); await opened?.dispose(); throw error; }
    finally { openingHosts.delete(host); }
}

/** Native host subscribes Invalidated immediately and calls binding.invalidate before another account can mount. */
export async function createNativeJournaledEditor(host: HTMLElement, bridge: any, scope: JournalScope, capabilities: Capabilities,
    onStatus?: DurableSessionOptions['onStatus']) {
    const store = new NativeJournalAdapter(bridge, scope);
    let current = true;
    return createJournaledEditor(host, { store, account: { valid: () => current, subscribe: () => () => {}, invalidate: () => { current = false; } },
        getTicket: installationId => bridge.invokeMethodAsync('GetTicket', installationId), onStatus, interactionsEnabled: capabilities.interactionsAvailable === true }, capabilities);
}

export async function mountNativeJournaledEditor(host: HTMLElement, bridge: any, scope: JournalScope, capabilities: Capabilities, callbacks: any) {
    const binding = await createNativeJournaledEditor(host, bridge, scope, capabilities,
        status => { void callbacks.invokeMethodAsync('OnStatus', status); });
    return (globalThis as any).DotNet.createJSObjectReference(binding);
}
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const blockReplacement = Extension.create({
    name: "collaborationReplacementFence",
    addCommands() { return { setContent: () => () => {
        throw new Error("collaboration_legacy_write_blocked: Use a reviewed editor transaction, not HTML replacement.");
    } }; }
});
let supportPromise: Promise<EditorSupport> | undefined;

export function getSupport(): Promise<EditorSupport> {
    return supportPromise ??= (async () => {
        const description = JSON.stringify({ revision: 1, tiptap: "3.31.3", contract,
            nodes: Object.entries(schema.nodes).map(([name, node]) => [name, node.spec.content, node.spec.inline, node.spec.attrs]),
            marks: Object.entries(schema.marks).map(([name, mark]) => [name, mark.spec.attrs]) });
        const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(description));
        return Object.freeze({ contentContractVersion: contract.version, protocolVersion: 1,
            schemaFingerprint: [...new Uint8Array(digest)].map(byte => byte.toString(16).padStart(2, "0")).join("") });
    })();
}

function identityKey(identity: PageIdentity) {
    if (!identity || typeof identity.environment !== "string" || !/^[a-zA-Z0-9_-]{1,64}$/.test(identity.environment)
        || ![identity.documentId, identity.pageId, identity.generation].every(id => typeof id === "string" && uuid.test(id)))
        throw new Error("collaboration_identity_invalid: Document, page and generation identities are required.");
    return `${identity.environment}/${identity.documentId.toLowerCase()}/${identity.pageId.toLowerCase()}/${identity.generation.toLowerCase()}`;
}

function requireSupport(actual: EditorSupport, expected: EditorSupport) {
    if (!actual || actual.protocolVersion !== expected.protocolVersion || actual.contentContractVersion !== expected.contentContractVersion
        || actual.schemaFingerprint !== expected.schemaFingerprint)
        throw new Error("collaboration_upgrade_required: This editor cannot preserve the page schema. Keep its original state.");
}

/** Called before allocating Y.Doc/Editor; never grants access from a room name or a client flag. */
export function requireCapability(capabilities: Capabilities, identity: PageIdentity, support: EditorSupport) {
    identityKey(identity);
    if (!capabilities || capabilities.documentId?.toLowerCase() !== identity.documentId.toLowerCase()
        || capabilities.mode !== 2 || !capabilities.canRead || !Array.isArray(capabilities.pages))
        throw new Error("collaboration_access_unavailable: Preserve your local draft.");
    const matches = capabilities.pages.filter(page => page.pageId?.toLowerCase() === identity.pageId.toLowerCase());
    if (matches.length !== 1 || matches[0].generation?.toLowerCase() !== identity.generation.toLowerCase())
        throw new Error("collaboration_generation_changed: Preserve your local draft for recovery.");
    requireSupport(matches[0], support);
    if (!capabilities.featureAvailable || !capabilities.canWriteCollaboratively)
        throw new Error("collaboration_unavailable: Collaborative editing is disabled in this build. Keep your local draft.");
}

export async function readCapabilities(identity: PageIdentity, signal?: AbortSignal): Promise<Capabilities> {
    identityKey(identity);
    const support = await getSupport();
    const query = new URLSearchParams({ pageId: identity.pageId, generation: identity.generation,
        protocolVersion: String(support.protocolVersion), schemaFingerprint: support.schemaFingerprint });
    const response = await fetch(`/api/documents/${identity.documentId}/collaboration/capabilities?${query}`,
        { credentials: "same-origin", cache: "no-store", signal });
    if (!response.ok) throw new Error("collaboration_capabilities_unavailable: Preserve your draft; access could not be verified.");
    return response.json();
}

/** Web uses the owner-authenticated GET; a native host may pass its authenticated .NET response. */
export async function createLiveEditor(_host: HTMLElement, identity: PageIdentity, capabilities?: Capabilities): Promise<never> {
    requireCapability(capabilities ?? await readCapabilities(identity), identity, await getSupport());
    // Deliberate P3 gate, independent of client-supplied or stale capability flags.
    throw new Error("collaboration_unavailable: Collaborative editing is not available in this build. Preserve your local draft.");
}

function validateState(doc: Y.Doc) {
    validateDocumentRoots(doc);
    const json = yDocToProsemirrorJSON(doc, "default");
    // Validate raw converted JSON before schema parsing can discard unknown nodes/attributes/marks.
    validateJson(json);
    schema.nodeFromJSON(json).check();
}

export async function createIsolatedSeed(content: string, format = "Html") {
    if (!["Html", "LegacyJson", "LegacyText"].includes(format) || typeof content !== "string"
        || content.length > 16 * 1024 * 1024 || format === "Html" && content.includes("<!--"))
        throw new Error("collaboration_source_unsupported: Preserve the original source for recovery.");
    const json = prepare(content, format);
    validateJson(json);
    const doc = prosemirrorJSONToYDoc(schema, json, "default");
    try { validateState(doc); return { support: await getSupport(), state: Y.encodeStateAsUpdate(doc) }; }
    finally { doc.destroy(); }
}

export interface IsolatedSession {
    readonly editor: Editor;
    readonly identity: PageIdentity;
    readonly support: EditorSupport;
    receive(update: EditorUpdate): void;
    snapshot(): Uint8Array;
    stopUndoCapture(): void;
    replaceContent(content: string): never;
    destroy(): void;
}

/** In-memory integration surface, shared by both shipped bundles. It cannot connect to a server. */
export async function createIsolatedEditor(host: HTMLElement, identity: PageIdentity,
    seed: { support: EditorSupport; state: Uint8Array }, callbacks: SessionCallbacks = {}): Promise<IsolatedSession> {
    identity = Object.freeze({ ...identity });
    const key = identityKey(identity);
    if (identity.environment !== "isolated") throw new Error("collaboration_isolation_required: This surface cannot open application manuscripts.");
    if (journalHosts.has(host) || openingHosts.has(host)) throw new Error('collaboration_editor_already_mounted');
    const support = await getSupport();
    requireSupport(seed?.support, support);
    if (!(seed?.state instanceof Uint8Array) || seed.state.byteLength > 16 * 1024 * 1024)
        throw new Error("collaboration_snapshot_invalid: Preserve the original state.");
    const doc = new Y.Doc();
    let editor: Editor | undefined;
    let destroyed = false;
    let deferred: Uint8Array | undefined;
    try {
        Y.applyUpdate(doc, new Uint8Array(seed.state), remoteOrigin);
        validateState(doc);
        active.get(host)?.destroy();
        editor = new Editor({ element: host, extensions: [...richExtensions(true), Collaboration.configure({ document: doc, field: "default" }), blockReplacement],
            enableContentCheck: true, parseOptions: { preserveWhitespace: "full" },
            onContentError: ({ error }) => { throw error; },
            editorProps: { attributes: { role: "textbox", "aria-label": "Isolated collaborative text", "aria-multiline": "true" } },
            onUpdate: ({ editor: current }) => { if (!destroyed) callbacks.onProjection?.(current.getHTML()); }
        });
        const localUpdate = (bytes: Uint8Array, _origin: unknown, _doc: Y.Doc, transaction: Y.Transaction) => {
            if (!destroyed && transaction.local)
                callbacks.onLocalUpdate?.({ identity: { ...identity }, support, bytes: new Uint8Array(bytes) });
        };
        doc.on("update", localUpdate);
        const requireActive = () => { if (destroyed) throw new Error("collaboration_session_disposed: Open the current page."); };
        const session: IsolatedSession = {
            editor, identity: Object.freeze({ ...identity }), support,
            receive(update) {
                requireActive();
                if (identityKey(update.identity) !== key) throw new Error("collaboration_generation_changed: Update targets another room.");
                requireSupport(update.support, support);
                if (!(update.bytes instanceof Uint8Array) || update.bytes.byteLength > 1024 * 1024)
                    throw new Error("collaboration_update_invalid: Invalid or oversized update.");
                const candidate = new Y.Doc();
                try {
                    const merged = deferred ? Y.mergeUpdates([deferred, update.bytes]) : new Uint8Array(update.bytes);
                    if (merged.byteLength > 16 * 1024 * 1024)
                        throw new Error('collaboration_update_invalid: Deferred updates exceed the snapshot limit.');
                    Y.applyUpdate(candidate, Y.encodeStateAsUpdate(doc), remoteOrigin);
                    Y.applyUpdate(candidate, merged, remoteOrigin);
                    // A delta can precede the structs it references. Keep it out of
                    // the editor and its snapshots until the dependency set is complete.
                    if (candidate.store.pendingStructs || candidate.store.pendingDs) {
                        deferred = merged;
                        return;
                    }
                    try { validateState(candidate); }
                    catch (error) { deferred = undefined; throw error; }
                    Y.applyUpdate(doc, merged, remoteOrigin);
                    deferred = undefined;
                } finally { candidate.destroy(); }
            },
            snapshot() { requireActive(); return Y.encodeStateAsUpdate(doc); },
            stopUndoCapture() { requireActive(); yUndoPluginKey.getState(editor!.state)?.undoManager.stopCapturing(); },
            replaceContent(_content) { throw new Error("collaboration_legacy_write_blocked: Use a reviewed editor transaction, not HTML replacement."); },
            destroy() {
                if (destroyed) return;
                destroyed = true;
                deferred = undefined;
                doc.off("update", localUpdate);
                editor!.destroy();
                doc.destroy();
                if (active.get(host) === session) active.delete(host);
                host.replaceChildren();
            }
        };
        active.set(host, session);
        return session;
    } catch (error) { editor?.destroy(); doc.destroy(); throw error; }
}

export function requireLegacyReplacement(editor: Editor) {
    if (editor.extensionManager.extensions.some(extension => extension.name === "collaboration"))
        throw new Error("collaboration_legacy_write_blocked: Remote edits must be applied as binary updates.");
}

import { Editor, Extension } from '@tiptap/core';
import { Plugin, PluginKey, TextSelection } from '@tiptap/pm/state';
import { Decoration, DecorationSet } from '@tiptap/pm/view';
import { absolutePositionToRelativePosition, relativePositionToAbsolutePosition, ySyncPluginKey } from '@tiptap/y-tiptap';
import * as Y from 'yjs';
import { decode, encode } from './journal';
import type { JournaledSession, PresenceMember, RelativeAnchor } from './durable-session';
import { PresenceView, participantColor, participantName } from './presence-view';

export interface AnchoredThread { id: string; pageId: string; generation: string; detached: boolean; resolved: boolean; anchor: RelativeAnchor | null }
const key = new PluginKey('prosa-collaboration-interactions');
export class EditorInteractions {
    private editor?: Editor;
    private members: PresenceMember[] = [];
    private threads: AnchoredThread[] = [];
    private stop?: () => void;
    private timer?: ReturnType<typeof setInterval>;
    private last = 0;
    private connected = false;
    private roster?: PresenceView;
    private stopStatus?: () => void;
    private layoutFrame?: number;
    private viewport?: HTMLElement;
    constructor(private session: JournaledSession) { }
    readonly extension = Extension.create({ name: 'prosaInteractions', addProseMirrorPlugins: () => [new Plugin({ key,
        view: () => ({ update: () => this.queueLabelLayout() }),
        props: { decorations: state => {
            const binding = ySyncPluginKey.getState(state)?.binding; if (!binding) return DecorationSet.empty;
            const decorations: Decoration[] = [];
            const cursors = new Map<string, PresenceMember>();
            for (const member of this.members) if (member.cursor || !cursors.has(member.actorId)) cursors.set(member.actorId, member);
            for (const member of cursors.values()) {
                if (!member.cursor || member.actorId === this.session.scope.ownerId) continue;
                const range = this.range(member.cursor); if (!range) continue;
                const color = participantColor(member.actorId);
                if (range.from < range.to) decorations.push(Decoration.inline(range.from, range.to, { class: 'collaboration-remote-selection', style: '--participant-color:' + color }));
                decorations.push(Decoration.widget(range.to, () => {
                    const name = participantName(member);
                    const caret = document.createElement('span'); caret.className = 'collaboration-remote-caret'; caret.style.setProperty('--participant-color', color); caret.setAttribute('aria-label', name + ' cursor');
                    const label = document.createElement('span'); label.className = 'collaboration-remote-caret-label'; label.textContent = name; caret.append(label); return caret;
                }, { key: JSON.stringify([member.connectionId, participantName(member)]), side: 1 }));
            }
            for (const thread of this.threads) {
                if (thread.detached || thread.resolved || !thread.anchor) continue;
                const range = this.range(thread.anchor); if (range && range.from < range.to) decorations.push(Decoration.inline(range.from, range.to, { class: 'collaboration-comment-anchor', 'data-thread-id': thread.id, style: 'background:rgba(240,185,30,.25);border-bottom:1px solid #b58400' }));
            }
            return DecorationSet.create(state.doc, decorations);
        } }
    })] });
    private queueLabelLayout = () => {
        if (this.layoutFrame !== undefined) return;
        this.layoutFrame = requestAnimationFrame(() => {
            this.layoutFrame = undefined;
            if (!this.editor || this.editor.isDestroyed) return;
            const root = this.editor.view.dom;
            const bounds = (this.viewport ?? root).getBoundingClientRect();
            for (const caret of root.querySelectorAll<HTMLElement>('.collaboration-remote-caret')) {
                const label = caret.querySelector<HTMLElement>('.collaboration-remote-caret-label'); if (!label) continue;
                const rect = caret.getBoundingClientRect(), size = label.getBoundingClientRect();
                caret.dataset.placement = rect.top - size.height - 3 < bounds.top ? 'below' : 'above';
                caret.dataset.align = rect.left + size.width > bounds.right - 4 ? 'end' : 'start';
            }
        });
    };
    private refresh() { if (this.editor && !this.editor.isDestroyed) this.editor.view.dispatch(this.editor.state.tr.setMeta(key, true).setMeta('addToHistory', false)); }
    private range(anchor: RelativeAnchor) {
        if (!this.editor) return null;
        const binding = ySyncPluginKey.getState(this.editor.state)?.binding; if (!binding) return null;
        try {
            const root = this.session.doc.getXmlFragment('default');
            const from = relativePositionToAbsolutePosition(this.session.doc, root, Y.decodeRelativePosition(decode(anchor.start, 768)), binding.mapping);
            const to = relativePositionToAbsolutePosition(this.session.doc, root, Y.decodeRelativePosition(decode(anchor.end, 768)), binding.mapping);
            return from === null || to === null || from > to || from < 0 || to > this.editor.state.doc.content.size ? null : { from, to };
        } catch { return null; }
    }
    selectionAnchor(): RelativeAnchor | null {
        if (!this.editor) return null;
        const binding = ySyncPluginKey.getState(this.editor.state)?.binding; if (!binding) return null;
        const { from, to } = this.editor.state.selection, root = this.session.doc.getXmlFragment('default');
        const position = (offset: number, affinity: number) => {
            const relative = absolutePositionToRelativePosition(offset, root, binding.mapping);
            const absolute = Y.createAbsolutePositionFromRelativePosition(relative, this.session.doc);
            if (!absolute) throw new Error('collaboration_anchor_detached');
            return encode(Y.encodeRelativePosition(Y.createRelativePositionFromTypeIndex(absolute.type, absolute.index, affinity)));
        };
        // Keep boundary insertions outside a selected passage. A caret has one right-associated position.
        return { start: position(from, 0), end: position(to, from === to ? 0 : -1) };
    }
    setThreads(threads: AnchoredThread[]) {
        if (threads.length > 200 || threads.some(t => t.pageId !== this.session.scope.pageId || t.generation !== this.session.scope.generation)) throw new Error('collaboration_generation_changed');
        this.threads = threads; this.refresh();
    }
    focusThread(id: string) {
        const thread = this.threads.find(t => t.id === id); if (!thread || thread.detached || !thread.anchor || !this.editor) return false;
        const range = this.range(thread.anchor); if (!range || range.from >= range.to) return false;
        this.editor.view.dispatch(this.editor.state.tr.setSelection(TextSelection.create(this.editor.state.doc, range.from, range.to)).scrollIntoView()); this.editor.view.focus(); return true;
    }
    private publish = () => {
        if (Date.now() - this.last < 200) return; this.last = Date.now(); this.session.publishPresence(this.selectionAnchor());
    };
    attach(editor: Editor, enabled: boolean) {
        this.editor = editor;
        this.viewport = editor.view.dom.closest<HTMLElement>('.editor-viewport') ?? undefined;
        this.viewport?.addEventListener('scroll', this.queueLabelLayout, { passive: true });
        window.addEventListener('resize', this.queueLabelLayout);
        if (enabled) {
            const host = editor.options.element as HTMLElement;
            const slot = host.closest('.editor-main')?.querySelector<HTMLElement>('.collaboration-presence-slot');
            // A dedicated shell slot keeps collaborator controls outside the manuscript paper.
            // Standalone/device editors still place them before, never inside, editable content.
            const mount = slot ?? document.createElement('div');
            if (!slot) { mount.className = 'collaboration-presence-slot'; host.prepend(mount); }
            this.roster = new PresenceView(mount, this.session.scope.ownerId);
            this.stopStatus = this.session.watchStatus(status => {
                this.roster?.setStatus(status);
                const joined = status.connected && !this.connected; this.connected = status.connected;
                if (joined) this.publish();
            });
        }
        this.stop = this.session.watchPresence(members => { this.members = members; this.roster?.setMembers(members); this.refresh(); });
        if (enabled) { editor.on('selectionUpdate', this.publish); this.timer = setInterval(this.publish, 5000); }
    }
    clear() { this.members = []; this.threads = []; this.roster?.setMembers([]); this.refresh(); }
    destroy() {
        clearInterval(this.timer); this.stop?.(); this.stopStatus?.(); this.editor?.off('selectionUpdate', this.publish); this.roster?.destroy();
        if (this.layoutFrame !== undefined) cancelAnimationFrame(this.layoutFrame);
        this.viewport?.removeEventListener('scroll', this.queueLabelLayout); window.removeEventListener('resize', this.queueLabelLayout);
        this.members = []; this.threads = []; this.editor = undefined;
    }
}

/// <reference types="vite/client" />
import styles from './presence.css?inline';
import type { DurableStatus, PresenceMember } from './durable-session';

export const participantName = (member: PresenceMember) => member.displayName?.trim() && member.displayName.trim() !== member.actorId ? member.displayName.trim() : 'Collaborator';
const colors = ['#315b99', '#7b4593', '#27745d', '#a24937', '#286d82', '#875c1c', '#9b3f6e', '#505b88'];
export function participantColor(actorId: string) {
    let hash = 2166136261;
    for (const c of actorId) hash = Math.imul(hash ^ c.codePointAt(0)!, 16777619);
    return colors[(hash >>> 0) % colors.length];
}
function element<K extends keyof HTMLElementTagNameMap>(tag: K, className: string, text?: string) {
    const node = document.createElement(tag); node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
}
function avatar(member: PresenceMember) {
    const name = participantName(member), words = name.split(/\s+/u);
    const initials = Array.from(words[0])[0] + (words.length > 1 ? Array.from(words[words.length - 1])[0] : '');
    const node = element('span', 'collaboration-avatar', initials.toLocaleUpperCase());
    node.style.setProperty('--participant-color', participantColor(member.actorId));
    node.setAttribute('aria-hidden', 'true'); return node;
}

/** Presentation only: never stores presence in the manuscript or changes document selection. */
export class PresenceView {
    readonly root = element('section', 'collaboration-presence');
    private status = element('span', 'collaboration-presence-status');
    private button = element('button', 'collaboration-people-button');
    private avatars = element('span', 'collaboration-avatar-stack');
    private count = element('span', 'collaboration-people-count');
    private panel = element('div', 'collaboration-people-panel');
    private list = element('ul', 'collaboration-people-list');
    private announcement = element('span', 'collaboration-sr-only');
    private members: PresenceMember[] = [];
    private connected = false;
    private connectedBefore = false;
    private unavailable = false;
    private rendered = '';
    constructor(host: HTMLElement, private localActorId: string) {
        if (!document.getElementById('prosa-presence-styles')) {
            const style = document.createElement('style'); style.id = 'prosa-presence-styles'; style.textContent = styles; document.head.append(style);
        }
        this.root.setAttribute('aria-label', 'Collaboration');
        const heading = element('div', 'collaboration-presence-heading');
        heading.append(element('span', 'collaboration-presence-title', 'Collaboration'), this.status);
        this.status.setAttribute('role', 'status');
        this.button.type = 'button'; this.button.setAttribute('aria-expanded', 'false');
        this.panel.id = 'collaboration-people-' + crypto.randomUUID();
        this.button.setAttribute('aria-controls', this.panel.id);
        this.avatars.setAttribute('aria-hidden', 'true');
        const chevron = element('span', 'collaboration-people-chevron', '⌄'); chevron.setAttribute('aria-hidden', 'true');
        this.button.append(this.avatars, this.count, chevron);
        this.panel.hidden = true; this.panel.setAttribute('role', 'region'); this.panel.setAttribute('aria-label', 'People on this page');
        this.panel.append(element('h3', 'collaboration-people-title', 'People on this page'), this.list,
            element('p', 'collaboration-people-hint', 'Match each color to a cursor in the manuscript.'));
        this.announcement.setAttribute('role', 'status'); this.announcement.setAttribute('aria-live', 'polite'); this.announcement.setAttribute('aria-atomic', 'true');
        this.root.append(heading, this.button, this.panel, this.announcement);
        host.append(this.root);
        this.button.addEventListener('click', this.toggle);
        this.root.addEventListener('keydown', this.keydown);
        document.addEventListener('pointerdown', this.outside);
        document.addEventListener('focusin', this.focusOutside);
        this.render();
    }
    private toggle = () => this.open(this.panel.hidden);
    private open(value: boolean) { this.panel.hidden = !value; this.button.setAttribute('aria-expanded', String(value)); }
    private keydown = (event: KeyboardEvent) => {
        if (event.key === 'Escape' && !this.panel.hidden) { event.preventDefault(); event.stopPropagation(); this.open(false); this.button.focus(); }
    };
    private outside = (event: Event) => { if (!this.root.contains(event.target as Node)) this.open(false); };
    private focusOutside = (event: Event) => {
        // Window activation can focus the document body without a user leaving the popover.
        const target = event.target as Element;
        if (target.closest?.('button, a[href], input, select, textarea, [tabindex]') && !this.root.contains(target)) this.open(false);
    };
    setMembers(members: PresenceMember[]) {
        this.members = [...new Map(members.map(m => [m.actorId, m])).values()].sort((a, b) =>
            Number(b.actorId === this.localActorId) - Number(a.actorId === this.localActorId) || participantName(a).localeCompare(participantName(b)) || a.actorId.localeCompare(b.actorId));
        this.render();
    }
    setStatus(status: DurableStatus) {
        this.connected = status.connected; this.connectedBefore ||= status.connected;
        this.unavailable = !!(status.accountChanged || status.error || status.recoveryReason);
        this.render();
    }
    private render() {
        const state = this.unavailable ? 'unavailable' : this.connected ? 'live' : this.connectedBefore ? 'offline' : 'connecting';
        const stateText = { unavailable: 'Unavailable', live: 'Live', offline: 'Not connected', connecting: 'Connecting…' }[state];
        const members = state === 'live' ? this.members : [];
        // Heartbeats and durability notifications must not replace focused controls or announce unchanged names.
        const signature = JSON.stringify([state, members.map(m => [m.actorId, participantName(m)])]);
        if (signature === this.rendered) return; this.rendered = signature;
        this.root.dataset.state = state; this.status.textContent = stateText;
        this.status.title = state === 'live' ? 'Live presence on this page. Save status is shown separately.' : 'Live participant information is unavailable until connected.';
        this.count.textContent = members.length === 1 ? members[0].actorId === this.localActorId ? 'Only you' : '1 person' : members.length ? `${members.length} people` : 'People';
        this.button.disabled = members.length === 0;
        const buttonLabel = members.length ? `Show ${members.length === 1 ? '1 person' : members.length + ' people'} on this page` : state === 'live' ? 'Waiting for live participants' : 'Live participants unavailable';
        this.button.setAttribute('aria-label', buttonLabel); this.button.title = buttonLabel;
        this.avatars.replaceChildren(...members.slice(0, 3).map(avatar));
        if (members.length > 3) this.avatars.append(element('span', 'collaboration-avatar collaboration-avatar-overflow', '+' + (members.length - 3)));
        this.list.replaceChildren(...members.map(member => {
            const row = element('li', 'collaboration-person');
            const name = element('span', 'collaboration-person-name', participantName(member)); name.title = participantName(member);
            row.append(avatar(member), name, element('span', 'collaboration-person-meta', member.actorId === this.localActorId ? 'You' : 'On this page'));
            return row;
        }));
        this.announcement.textContent = state === 'live' ? `${members.length} ${members.length === 1 ? 'person' : 'people'} on this page. ` + members.map(m => participantName(m) + (m.actorId === this.localActorId ? ', you' : '')).join('; ') : stateText;
        if (!members.length) this.open(false);
    }
    destroy() {
        this.button.removeEventListener('click', this.toggle); this.root.removeEventListener('keydown', this.keydown);
        document.removeEventListener('pointerdown', this.outside); document.removeEventListener('focusin', this.focusOutside); this.root.remove();
    }
}

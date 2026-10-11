// Highlights decorate DOM ranges only; they never become manuscript formatting.
const changedRanges = new Map();
function paintChanges() {
    if (globalThis.CSS?.highlights && globalThis.Highlight) {
        CSS.highlights.set('prosa-working-draft', new Highlight(...[...changedRanges.values()].flat()));
    }
}
// The same shipped common editor and mapper in both hosts; no manuscript save callback.
export async function create(host, html, kind, receiver, candidateId, fingerprint) {
    const factory = kind === 'Device'
        ? (await import('/_content/WriterApp.Device.Shared/editor/device-editor.js')).create
        : window.tiptapEditor?.createBrainstormProposal;
    if (!factory) throw new Error('Proposal editor unavailable. Rebuild this host; saved versions remain intact.');
    let editor, formatting, editable = true, previousText = '', showChanges = false;
    const highlightOwner = {};
    function highlightChanges() {
        const ranges = [];
        if (showChanges && previousText) for (const element of mount.querySelectorAll('.ProseMirror > *')) {
            const text = element.textContent.trim();
            if (text && !previousText.includes(text)) { const range = document.createRange(); range.selectNodeContents(element); ranges.push(range); }
        }
        changedRanges.set(highlightOwner, ranges); paintChanges();
    }
    const controls = [];
    let block;
    function refresh() {
        if (!formatting || !block) return;
        block.value = formatting.state.blockType;
        block.disabled = !editable || !formatting.state.canApplyHeading;
        for (const control of controls) {
            control.button.disabled = !editable || (control.command === 'undo' ? !formatting.undo : control.command === 'redo' ? !formatting.redo : false);
            if (control.active) control.button.setAttribute('aria-pressed', String(!!formatting.state[control.active]));
        }
    }
    const adapter = { invokeMethodAsync(method, state, undo, redo) {
        if (method === 'OnFormattingChanged') { formatting = { state, undo, redo }; refresh(); return Promise.resolve(); }
        if (method !== 'OnContentChanged' || !editor) return Promise.resolve();
        const value = editor.captureAi(true, false, true);
        highlightChanges();
        return receiver.invokeMethodAsync('OnProposalChanged', value.html, value.plainText, candidateId, fingerprint);
    }};
    // Each instance owns its DOM. Late cleanup of an old proposal must never
    // clear the container used by the newly completed follow-up.
    const mount = document.createElement('div');
    host.append(mount);
    try { editor = factory(mount, html, 'Html', adapter, true); }
    catch (error) { mount.remove(); throw error; }
    // Reuse the shipped editor commands and formatting state in both hosts.
    const toolbar = document.createElement('div');
    toolbar.className = 'proposal-formatting';
    toolbar.setAttribute('role', 'group'); toolbar.setAttribute('aria-label', 'Draft formatting');
    block = document.createElement('select'); block.setAttribute('aria-label', 'Draft paragraph style');
    for (const [value, label] of [['paragraph', 'Paragraph'], ...[1,2,3,4,5,6].map(n => [`heading:${n}`, `Heading ${n}`])]) {
        const option = document.createElement('option'); option.value = value; option.textContent = label; block.append(option);
    }
    block.addEventListener('change', () => {
        if (!editable) return;
        const [command, level] = block.value.split(':'); editor.command(command, level ? Number(level) : undefined);
    });
    toolbar.append(block);
    for (const [command, label, glyph, active] of [
        ['bold','Bold','B','isBold'], ['italic','Italic','I','isItalic'],
        ['bulletList','Bulleted list','≡'], ['orderedList','Numbered list','1.'],
        ['blockquote','Quote','❞'], ['link','Link','↗','isLink'],
        ['undo','Undo','↶'], ['redo','Redo','↷']
    ]) {
        const button = document.createElement('button'); button.type = 'button'; button.textContent = glyph;
        button.title = label; button.setAttribute('aria-label', label);
        button.addEventListener('mousedown', event => event.preventDefault());
        button.addEventListener('click', () => { if (editable) editor.command(command); });
        toolbar.append(button); controls.push({ button, command, active });
    }
    mount.prepend(toolbar); refresh();
    mount.querySelector('[role=textbox]')?.setAttribute('aria-label', 'Proposed prose editor');
    return {
        snapshot: () => editor.captureAi(true, false, true),
        selection: () => editor.captureAi(false, true, true),
        setEditable: value => { editable = value; editor.setEditable(value); refresh(); },
        setChanges: (previous, visible) => { previousText = previous || ''; showChanges = visible; highlightChanges(); },
        destroy: () => { try { editor.destroy(); } finally { changedRanges.delete(highlightOwner); paintChanges(); mount.remove(); } }
    };
}

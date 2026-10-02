const bindings = new WeakMap();
const projectBindings = new WeakMap();
const documentType = 'application/x-prosa-standalone-document';

export function attachProjects(collection, receiver) {
    detachProjects(collection);
    let source = null;
    let target = null;
    let pending = false;
    const enabled = () => !pending && collection.dataset.libraryDisabled !== 'true';
    const clearTarget = () => { target?.classList.remove('is-project-drop-target'); target = null; };
    const clear = () => { clearTarget(); source?.classList.remove('is-document-dragging'); source = null; };
    const projectAt = event => {
        const row = event.target.closest?.('[data-library-project-id]');
        return row && collection.contains(row) ? row : null;
    };
    const start = event => {
        clear();
        const row = event.target.closest?.('[data-standalone-document-id]');
        if (!enabled() || !row || !collection.contains(row) || row.draggable !== true) {
            event.preventDefault();
            return;
        }
        source = row;
        event.dataTransfer.setData(documentType, row.dataset.standaloneDocumentId);
        event.dataTransfer.effectAllowed = 'move';
        row.classList.add('is-document-dragging');
    };
    const over = event => {
        const row = enabled() && source ? projectAt(event) : null;
        clearTarget();
        if (!row) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
        target = row;
        row.classList.add('is-project-drop-target');
    };
    const leave = event => {
        if (target && !target.contains(event.relatedTarget)) clearTarget();
    };
    const drop = async event => {
        const row = enabled() && source ? projectAt(event) : null;
        if (!row) { clear(); return; }
        event.preventDefault();
        const documentId = source.dataset.standaloneDocumentId;
        const projectId = row.dataset.libraryProjectId;
        clear();
        pending = true;
        try { await receiver.invokeMethodAsync('MoveDocumentToProject', documentId, projectId); }
        catch { await receiver.invokeMethodAsync('ReportDropError', 'The document could not be moved. Reload the library and try again.'); }
        finally { pending = false; }
    };
    const handlers = { dragstart: start, dragenter: over, dragover: over, dragleave: leave, drop, dragend: clear };
    for (const [name, handler] of Object.entries(handlers)) collection.addEventListener(name, handler);
    projectBindings.set(collection, { handlers, clear });
}

export function detachProjects(collection) {
    const binding = projectBindings.get(collection);
    if (!binding) return;
    for (const [name, handler] of Object.entries(binding.handlers)) collection.removeEventListener(name, handler);
    binding.clear();
    projectBindings.delete(collection);
}

export function attach(zone, receiver) {
    detach(zone);
    let depth = 0;
    const hasFiles = event => Array.from(event.dataTransfer?.types ?? []).includes('Files');
    const enter = event => {
        if (!hasFiles(event)) return;
        event.preventDefault();
        depth++;
        zone.classList.add('is-dragging');
    };
    const leave = event => {
        event.preventDefault();
        if (--depth <= 0) { depth = 0; zone.classList.remove('is-dragging'); }
    };
    const over = event => {
        if (!hasFiles(event)) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = 'copy';
    };
    const drop = event => {
        event.preventDefault();
        depth = 0;
        zone.classList.remove('is-dragging');
        const files = event.dataTransfer?.files;
        if (!files?.length) return;
        if (files.length !== 1) { void receiver.invokeMethodAsync('ReportDropError', 'Drop one document at a time.'); return; }
        if (files[0].size > 5 * 1024 * 1024) { void receiver.invokeMethodAsync('ReportDropError', 'The import file must be at most 5 MB.'); return; }
        const input = zone.querySelector('input[type=file]');
        input.files = files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    };
    const handlers = { dragenter: enter, dragleave: leave, dragover: over, drop };
    for (const [name, handler] of Object.entries(handlers)) zone.addEventListener(name, handler);
    bindings.set(zone, handlers);
}

export function detach(zone) {
    const handlers = bindings.get(zone);
    if (!handlers) return;
    for (const [name, handler] of Object.entries(handlers)) zone.removeEventListener(name, handler);
    zone.classList.remove('is-dragging');
    bindings.delete(zone);
}

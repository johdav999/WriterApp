const bindings = new WeakMap();

const allowed = (type, parentType) => type === "part" ? !parentType
    : type === "chapter" ? !parentType || parentType === "part"
    : type === "scene" && parentType === "chapter";

// Upper/lower edges insert beside a row; the middle moves into a valid parent.
export function dropPlacement(type, targetType, parentType, fraction) {
    if (fraction >= 0.25 && fraction <= 0.75 && allowed(type, targetType)) return "inside";
    return allowed(type, parentType) ? (fraction < 0.5 ? "before" : "after") : null;
}

export function attach(root, reference) {
    if (!root || bindings.has(root)) return;
    let source = null, indicator = null, pending = false, gesture = null, suppressClick = false, scrollFrame = null;
    const blocked = () => pending || root.dataset.outlineDisabled === "true";
    const rowAt = event => {
        const row = event.target.closest("[data-outline-id]");
        return row && root.contains(row) ? row : null;
    };
    const clearIndicator = () => {
        indicator?.classList.remove("outline-drop-before", "outline-drop-after", "outline-drop-inside");
        indicator = null;
    };
    const clear = () => {
        const previous = gesture;
        gesture = null;
        if (scrollFrame !== null) cancelAnimationFrame(scrollFrame);
        scrollFrame = null;
        if (previous && root.hasPointerCapture(previous.id)) root.releasePointerCapture(previous.id);
        clearIndicator();
        source?.classList.remove("outline-is-dragging");
        source = null;
        root.classList.remove("outline-drag-active");
    };
    const destination = event => {
        if (!source || blocked()) return null;
        const target = rowAt(event);
        if (target) {
            if (source === target) return null;
            const parent = [...root.querySelectorAll("[data-outline-id]")].find(row => row.dataset.outlineId === target.dataset.outlineParent);
            const rect = target.getBoundingClientRect();
            // A hidden/collapsed parent still has a visible row in the outline.
            const placement = dropPlacement(source.dataset.outlineType, target.dataset.outlineType, parent?.dataset.outlineType,
                (event.clientY - rect.top) / Math.max(rect.height, 1));
            return placement ? { target, id: target.dataset.outlineId, placement } : null;
        }
        const atRoot = event.target.closest("[data-outline-root]");
        return atRoot && root.contains(atRoot) && allowed(source.dataset.outlineType, null)
            ? { target: atRoot, id: null, placement: "inside" } : null;
    };
    const dragstart = event => {
        if (gesture || event.target.closest('[data-outline-handle]')) { event.preventDefault(); return; }
        const row = rowAt(event);
        if (!row || blocked() || event.target.closest("button, input, select, textarea, a")) { event.preventDefault(); return; }
        source = row;
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("application/x-prosa-outline", row.dataset.outlineId);
        row.classList.add("outline-is-dragging");
        root.classList.add("outline-drag-active");
    };
    const dragover = event => {
        const drop = destination(event);
        clearIndicator();
        if (!drop) { if (source && event.dataTransfer) event.dataTransfer.dropEffect = "none"; return; }
        event.preventDefault();
        event.dataTransfer.dropEffect = "move";
        indicator = drop.target;
        indicator.classList.add(`outline-drop-${drop.placement}`);
    };
    const drop = async event => {
        const destinationRow = destination(event);
        if (!destinationRow) { clear(); return; }
        event.preventDefault();
        event.stopPropagation();
        const id = source.dataset.outlineId;
        pending = true;
        clear();
        try { await reference.invokeMethodAsync("DropOutlineAsync", id, destinationRow.id, destinationRow.placement); }
        finally { pending = false; }
    };
    const dragleave = event => { if (!root.contains(event.relatedTarget)) clearIndicator(); };
    const keydown = event => { if (event.key === "Escape") clear(); };
    // Pointer capture keeps touch gestures in the WebView. Hit testing must use
    // coordinates because captured events keep targeting the source outline.
    const pointerDestination = event => {
        const target = root.ownerDocument.elementFromPoint(event.clientX, event.clientY);
        return target ? destination({ target, clientY: event.clientY }) : null;
    };
    const showPointerDestination = event => {
        const place = pointerDestination(event);
        clearIndicator();
        if (place) { indicator = place.target; indicator.classList.add(`outline-drop-${place.placement}`); }
    };
    const autoScroll = () => {
        scrollFrame = null;
        if (!gesture?.active) return;
        if (blocked()) { clear(); return; }
        const view = root.ownerDocument.defaultView;
        for (let track = root; track; track = track.parentElement) {
            if (track.scrollHeight <= track.clientHeight || !/(auto|scroll)/.test(view.getComputedStyle(track).overflowY)) continue;
            const rect = track.getBoundingClientRect();
            const top = Math.max(0, rect.top), bottom = Math.min(view.innerHeight, rect.bottom);
            const y = gesture.last.clientY;
            const delta = y < top + 40 ? -10 : y > bottom - 40 ? 10 : 0;
            if (delta) { track.scrollTop += delta; showPointerDestination(gesture.last); }
            break;
        }
        scrollFrame = requestAnimationFrame(autoScroll);
    };
    const pointerdown = event => {
        suppressClick = false;
        if (gesture || blocked() || event.button !== 0 || event.isPrimary === false || !event.target.closest('[data-outline-handle]')) return;
        const row = rowAt(event);
        if (!row) return;
        gesture = { id: event.pointerId, row, x: event.clientX, y: event.clientY, active: false, last: event };
        root.setPointerCapture(event.pointerId);
    };
    const pointermove = event => {
        if (!gesture || event.pointerId !== gesture.id) return;
        if (blocked()) { clear(); return; }
        gesture.last = event;
        if (!gesture.active && Math.hypot(event.clientX - gesture.x, event.clientY - gesture.y) < 6) return;
        event.preventDefault();
        if (!gesture.active) {
            gesture.active = true;
            source = gesture.row;
            source.classList.add('outline-is-dragging');
            root.classList.add('outline-drag-active');
            scrollFrame = requestAnimationFrame(autoScroll);
        }
        showPointerDestination(event);
    };
    const pointerup = async event => {
        if (!gesture || event.pointerId !== gesture.id) return;
        const place = gesture.active ? pointerDestination(event) : null;
        const id = source?.dataset.outlineId;
        if (gesture.active) { event.preventDefault(); suppressClick = true; }
        clear();
        if (!place || !id) return;
        pending = true;
        try { await reference.invokeMethodAsync('DropOutlineAsync', id, place.id, place.placement); }
        finally { pending = false; }
    };
    const cancelPointer = event => { if (event.pointerId === gesture?.id) clear(); };
    const click = event => {
        if (!suppressClick) return;
        suppressClick = false;
        event.preventDefault();
        event.stopImmediatePropagation();
    };
    const listeners = { dragstart, dragover, drop, dragend: clear, dragleave, keydown };
    for (const [name, handler] of Object.entries(listeners)) root.addEventListener(name, handler);
    const pointerListeners = { pointerdown, pointermove, pointerup, pointercancel: cancelPointer, lostpointercapture: cancelPointer };
    for (const [name, handler] of Object.entries(pointerListeners)) root.addEventListener(name, handler);
    root.addEventListener('click', click, true);
    const blur = () => clear();
    root.ownerDocument?.defaultView.addEventListener('blur', blur);
    bindings.set(root, () => {
        clear();
        for (const [name, handler] of Object.entries(listeners)) root.removeEventListener(name, handler);
        for (const [name, handler] of Object.entries(pointerListeners)) root.removeEventListener(name, handler);
        root.removeEventListener('click', click, true);
        root.ownerDocument?.defaultView.removeEventListener('blur', blur);
    });
}

export function detach(root) {
    bindings.get(root)?.();
    bindings.delete(root);
}

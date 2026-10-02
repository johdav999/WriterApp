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
    let source = null, indicator = null, pending = false;
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
    const listeners = { dragstart, dragover, drop, dragend: clear, dragleave, keydown };
    for (const [name, handler] of Object.entries(listeners)) root.addEventListener(name, handler);
    bindings.set(root, () => {
        clear();
        for (const [name, handler] of Object.entries(listeners)) root.removeEventListener(name, handler);
    });
}

export function detach(root) {
    bindings.get(root)?.();
    bindings.delete(root);
}

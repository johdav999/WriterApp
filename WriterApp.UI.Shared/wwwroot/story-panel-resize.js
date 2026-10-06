const bindings = new WeakMap();
const storageKey = "prosa.story-panel-height";
const defaultHeight = 320;

export function attach(handle) {
    const panel = handle.closest(".rp-panel");
    const content = panel?.querySelector(".rp-content");
    const existing = bindings.get(handle);
    if (existing?.panel === panel) { existing.refresh(); return; }
    detach(handle);
    if (!panel || !content) return;

    let preferred = defaultHeight;
    try {
        const saved = Number(localStorage.getItem(storageKey));
        if (Number.isFinite(saved) && saved >= 120) preferred = saved;
    } catch { /* Resizing also works without preference storage. */ }
    let drag = null;
    const bounds = () => {
        const available = Math.max(0, panel.clientHeight - handle.getBoundingClientRect().height);
        const max = Math.max(0, available - Math.min(160, available * .4));
        return { min: Math.min(120, max), max };
    };
    const apply = value => {
        const { min, max } = bounds();
        const height = Math.round(Math.max(min, Math.min(max, value)));
        panel.style.setProperty("--story-panel-height", `${height}px`);
        handle.setAttribute("aria-valuemin", String(Math.round(min)));
        handle.setAttribute("aria-valuemax", String(Math.round(max)));
        handle.setAttribute("aria-valuenow", String(height));
        handle.setAttribute("aria-valuetext", `${height} pixels for Story`);
        return height;
    };
    const persist = () => { try { localStorage.setItem(storageKey, String(preferred)); } catch { } };
    const finish = (cancel = false) => {
        if (!drag) return;
        preferred = cancel ? drag.preferred : content.getBoundingClientRect().height;
        const pointerId = drag.id;
        drag = null;
        if (handle.hasPointerCapture(pointerId)) handle.releasePointerCapture(pointerId);
        handle.classList.remove("is-dragging");
        apply(preferred);
        if (!cancel) persist();
    };
    const down = event => {
        if (event.button !== 0) return;
        event.preventDefault();
        handle.focus({ preventScroll: true });
        drag = { id: event.pointerId, y: event.clientY, height: content.getBoundingClientRect().height, preferred };
        handle.setPointerCapture(event.pointerId);
        handle.classList.add("is-dragging");
    };
    const move = event => {
        if (drag?.id !== event.pointerId) return;
        apply(drag.height + event.clientY - drag.y);
    };
    const keydown = event => {
        if (event.key === "Escape" && drag) { event.preventDefault(); finish(true); return; }
        if (!["ArrowUp", "ArrowDown", "Home", "End"].includes(event.key)) return;
        event.preventDefault();
        const { min, max } = bounds();
        preferred = apply(event.key === "Home" ? min : event.key === "End" ? max
            : content.getBoundingClientRect().height + (event.key === "ArrowDown" ? 1 : -1) * (event.shiftKey ? 40 : 16));
        persist();
    };
    const reset = () => { preferred = defaultHeight; apply(preferred); persist(); };
    const refresh = () => apply(preferred);
    const observer = new ResizeObserver(refresh);
    observer.observe(panel);
    const events = { pointerdown: down, pointermove: move, pointerup: () => finish(), pointercancel: () => finish(true), lostpointercapture: () => finish(), keydown, dblclick: reset };
    for (const [name, listener] of Object.entries(events)) handle.addEventListener(name, listener);
    apply(preferred);
    bindings.set(handle, { panel, refresh, dispose: () => {
        finish(true);
        observer.disconnect();
        for (const [name, listener] of Object.entries(events)) handle.removeEventListener(name, listener);
    } });
}

export function detach(handle) { bindings.get(handle)?.dispose(); bindings.delete(handle); }

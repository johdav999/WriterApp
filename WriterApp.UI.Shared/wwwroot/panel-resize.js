const bindings = new WeakMap();

// The shared shell and editor use these same limits in web and native WebView hosts.
export function widthBounds(side, available, otherPanel = 0) {
    const min = side === "left" ? 140 : 280;
    const preferredMax = side === "left" ? 360 : 680;
    const reserved = side === "left" ? 480 + otherPanel : 420;
    return { min, max: Math.max(min, Math.min(preferredMax, available - reserved)) };
}

export function attach(handle, side) {
    const left = side === "left";
    const owner = handle.closest(left ? ".app-shell" : ".editor-shell");
    const panel = owner?.querySelector(left ? ".app-nav" : ".context-drawer");
    const existing = bindings.get(handle);
    if (existing?.panel === panel) { existing?.refresh(); return; }
    detach(handle);
    if (!owner || !panel) return;
    const variable = left ? "--app-nav-width" : "--context-panel-width";
    const key = `prosa.panel-width.${side}`;
    const defaultWidth = () => left ? 180 : window.innerWidth <= 1200 ? 300 : 360;
    let preferred = defaultWidth();
    try {
        const saved = Number(localStorage.getItem(key));
        if (Number.isFinite(saved) && saved >= (left ? 140 : 280)) preferred = saved;
    } catch { /* Restricted storage must not prevent resizing. */ }
    let drag = null;
    const bounds = () => {
        const area = left ? owner : owner.querySelector(".editor-workspace");
        const other = left ? owner.querySelector(".context-drawer")?.getBoundingClientRect().width ?? 0 : 0;
        return widthBounds(side, area?.clientWidth ?? owner.clientWidth, other);
    };
    const clamp = value => { const { min, max } = bounds(); return Math.round(Math.min(max, Math.max(min, value))); };
    const apply = value => {
        const width = clamp(value);
        owner.style.setProperty(variable, `${width}px`);
        if (!left) owner.closest(".app-shell")?.style.setProperty("--app-editor-context-panel-width", `${width}px`);
        const { min, max } = bounds();
        handle.setAttribute("aria-valuemin", String(min));
        handle.setAttribute("aria-valuemax", String(max));
        handle.setAttribute("aria-valuenow", String(width));
        handle.setAttribute("aria-valuetext", `${width} pixels`);
        return width;
    };
    const persist = () => { try { localStorage.setItem(key, String(preferred)); } catch { } };
    const finish = (cancel = false) => {
        if (!drag) return;
        if (cancel) preferred = drag.preferred;
        else preferred = panel.getBoundingClientRect().width;
        const pointerId = drag.id; drag = null;
        if (handle.hasPointerCapture(pointerId)) handle.releasePointerCapture(pointerId);
        handle.classList.remove("is-dragging");
        owner.classList.remove("is-resizing-panel");
        apply(preferred); if (!cancel) persist();
    };
    const down = event => {
        if (event.button !== 0) return;
        event.preventDefault(); handle.focus({ preventScroll: true });
        drag = { id: event.pointerId, x: event.clientX, width: panel.getBoundingClientRect().width, preferred };
        handle.setPointerCapture(event.pointerId);
        handle.classList.add("is-dragging"); owner.classList.add("is-resizing-panel");
    };
    const move = event => {
        if (drag?.id !== event.pointerId) return;
        apply(drag.width + (event.clientX - drag.x) * (left ? 1 : -1));
    };
    const up = () => finish();
    const cancel = () => finish(true);
    const reset = () => { preferred = defaultWidth(); apply(preferred); persist(); };
    const keydown = event => {
        if (event.key === "Escape" && drag) { event.preventDefault(); finish(true); return; }
        if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
        event.preventDefault();
        const { min, max } = bounds();
        const step = event.shiftKey ? 40 : 16;
        preferred = event.key === "Home" ? min : event.key === "End" ? max
            : clamp(panel.getBoundingClientRect().width + (event.key === "ArrowRight" ? 1 : -1) * (left ? 1 : -1) * step);
        apply(preferred); persist();
    };
    const resize = () => { if (window.innerWidth > 980) apply(preferred); };
    const observer = new ResizeObserver(resize);
    observer.observe(left ? owner : owner.querySelector(".editor-workspace"));
    const events = { pointerdown: down, pointermove: move, pointerup: up, pointercancel: cancel, lostpointercapture: up, keydown, dblclick: reset };
    for (const [name, listener] of Object.entries(events)) handle.addEventListener(name, listener);
    window.addEventListener("resize", resize);
    apply(preferred);
    bindings.set(handle, { panel, refresh: () => apply(preferred), dispose: () => {
        finish(true); observer.disconnect(); window.removeEventListener("resize", resize);
        for (const [name, listener] of Object.entries(events)) handle.removeEventListener(name, listener);
    } });
}

export function detach(handle) { bindings.get(handle)?.dispose(); bindings.delete(handle); }

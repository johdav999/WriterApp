const bindings = new WeakMap();

export function attach(handle, name) {
    const owner = handle.closest('.storyboard-page__layout');
    const panel = owner?.querySelector(`.storyboard-page__panel--${name}`);
    const existing = bindings.get(handle);
    if (existing?.panel === panel) { existing.refresh(); return; }
    detach(handle);
    if (!owner || !panel) return;

    const variable = `--storyboard-${name}-width`;
    const storageKey = `prosa.storyboard-panel-width.${name}`;
    const defaultWidth = name === 'insights' ? 320 : 360;
    const minimumWidth = name === 'insights' ? 240 : 280;
    let preferred = defaultWidth;
    try {
        const saved = Number(localStorage.getItem(storageKey));
        if (Number.isFinite(saved) && saved >= minimumWidth) preferred = saved;
    } catch { /* Resizing works with restricted storage too. */ }
    let drag = null;
    const bounds = () => {
        const other = owner.querySelector(`.storyboard-page__panel--${name === 'insights' ? 'detail' : 'insights'}`);
        const handleWidth = handle.getBoundingClientRect().width;
        const space = owner.clientWidth - handleWidth * (other ? 2 : 1) - 200;
        let otherWidth = other?.getBoundingClientRect().width ?? 0;
        if (other && space - otherWidth < minimumWidth) {
            otherWidth = Math.max(0, space - minimumWidth);
            owner.style.setProperty(`--storyboard-${name === 'insights' ? 'detail' : 'insights'}-width`, `${otherWidth}px`);
        }
        const available = space - otherWidth;
        const max = Math.max(0, Math.min(640, available));
        return { min: Math.min(minimumWidth, max), max };
    };
    const apply = value => {
        if (window.innerWidth <= 1200) return panel.getBoundingClientRect().width;
        const { min, max } = bounds();
        const width = Math.round(Math.max(min, Math.min(max, value)));
        owner.style.setProperty(variable, `${width}px`);
        handle.setAttribute('aria-valuemin', String(Math.round(min)));
        handle.setAttribute('aria-valuemax', String(Math.round(max)));
        handle.setAttribute('aria-valuenow', String(width));
        handle.setAttribute('aria-valuetext', `${width} pixels`);
        return width;
    };
    const persist = () => { try { localStorage.setItem(storageKey, String(preferred)); } catch { } };
    const finish = (cancel = false) => {
        if (!drag) return;
        preferred = cancel ? drag.preferred : panel.getBoundingClientRect().width;
        const id = drag.id;
        drag = null;
        if (handle.hasPointerCapture(id)) handle.releasePointerCapture(id);
        handle.classList.remove('is-dragging');
        owner.classList.remove('is-resizing-panel');
        apply(preferred);
        if (!cancel) persist();
    };
    const down = event => {
        if (event.button !== 0 || window.innerWidth <= 1200 || drag) return;
        event.preventDefault();
        handle.focus({ preventScroll: true });
        drag = { id: event.pointerId, x: event.clientX, width: panel.getBoundingClientRect().width, preferred };
        handle.setPointerCapture(event.pointerId);
        handle.classList.add('is-dragging');
        owner.classList.add('is-resizing-panel');
    };
    const move = event => {
        if (drag?.id === event.pointerId) apply(drag.width + drag.x - event.clientX);
    };
    const keydown = event => {
        if (event.key === 'Escape' && drag) { event.preventDefault(); finish(true); return; }
        if (window.innerWidth <= 1200 || !['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
        event.preventDefault();
        const { min, max } = bounds();
        preferred = apply(event.key === 'Home' ? min : event.key === 'End' ? max
            : panel.getBoundingClientRect().width + (event.key === 'ArrowLeft' ? 1 : -1) * (event.shiftKey ? 40 : 16));
        persist();
    };
    const reset = () => { preferred = defaultWidth; apply(preferred); persist(); };
    const refresh = () => { if (!drag) apply(preferred); };
    const observer = new ResizeObserver(refresh);
    observer.observe(owner);
    const events = { pointerdown: down, pointermove: move, pointerup: () => finish(), pointercancel: () => finish(true), lostpointercapture: () => finish(), keydown, dblclick: reset };
    for (const [event, listener] of Object.entries(events)) handle.addEventListener(event, listener);
    window.addEventListener('resize', refresh);
    apply(preferred);
    bindings.set(handle, { panel, refresh, dispose: () => {
        finish(true);
        observer.disconnect();
        window.removeEventListener('resize', refresh);
        for (const [event, listener] of Object.entries(events)) handle.removeEventListener(event, listener);
    } });
}

export function detach(handle) { bindings.get(handle)?.dispose(); bindings.delete(handle); }

export function scrollToElement(id) {
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "nearest" });
}
export function focusAndSelectInput(input) {
    input?.focus();
    input?.select();
}

const pointerBindings = new WeakMap();

// WinUI WebView2 cannot complete in-page HTML5 drags. Pointer capture keeps the
// gesture in the WebView; only the final placement crosses into the existing move API.
export function pointerPlacement(list, target, clientY) {
    const gap = target.closest('[data-storyboard-before]');
    if (gap && list.contains(gap)) return { indicator: gap, before: gap.dataset.storyboardBefore || null };
    const card = target.closest('[data-storyboard-scene]');
    if (card && list.contains(card)) {
        const rect = card.getBoundingClientRect();
        const next = card.nextElementSibling;
        return clientY < rect.top + rect.height / 2
            ? { indicator: card, before: card.dataset.storyboardScene }
            : { indicator: next || list, before: next?.dataset.storyboardBefore || null };
    }
    return { indicator: list, before: null };
}

export function attachPointerDrag(root, reference) {
    if (!root || pointerBindings.has(root)) return;
    let gesture = null, indicator = null, pending = false, suppressClick = false, scrollFrame = null;
    const blocked = () => pending || root.dataset.storyboardBusy === 'true';
    const clearIndicator = () => { indicator?.classList.remove('is-drop-target'); indicator = null; };
    const clear = () => {
        clearIndicator();
        if (scrollFrame !== null) cancelAnimationFrame(scrollFrame);
        scrollFrame = null;
        const previous = gesture;
        gesture = null;
        previous?.source.classList.remove('is-dragging');
        root.classList.remove('is-pointer-dragging');
        if (previous && root.hasPointerCapture(previous.id)) root.releasePointerCapture(previous.id);
    };
    const destination = event => {
        const hit = root.ownerDocument.elementFromPoint(event.clientX, event.clientY);
        const list = hit?.closest('[data-storyboard-chapter]');
        if (!list || !root.contains(list)) return null;
        const placement = pointerPlacement(list, hit, event.clientY);
        return { chapter: list.dataset.storyboardChapter, ...placement };
    };
    const showDestination = event => {
        const drop = destination(event);
        clearIndicator();
        if (drop) { indicator = drop.indicator; indicator.classList.add('is-drop-target'); }
    };
    const autoScroll = () => {
        scrollFrame = null;
        if (!gesture?.active) return;
        const track = gesture.source.closest('.project-storyboard-board');
        if (track) {
            const rect = track.getBoundingClientRect();
            const x = gesture.last.clientX;
            const delta = x < rect.left + 40 ? -12 : x > rect.right - 40 ? 12 : 0;
            if (delta) { track.scrollLeft += delta; showDestination(gesture.last); }
        }
        scrollFrame = requestAnimationFrame(autoScroll);
    };
    const pointerdown = event => {
        // Some WebViews omit the click after a captured drag. A new press must
        // never inherit suppression intended for the previous gesture.
        suppressClick = false;
        if (event.button !== 0 || event.isPrimary === false || blocked() || gesture
            || event.target.closest('button,input,select,textarea,a,[contenteditable="true"]')) return;
        const source = event.target.closest('[data-storyboard-scene]');
        if (!source || !root.contains(source)) return;
        gesture = { source, id: event.pointerId, x: event.clientX, y: event.clientY, active: false, last: event };
    };
    const pointermove = event => {
        if (!gesture || event.pointerId !== gesture.id) return;
        if (blocked()) { clear(); return; }
        gesture.last = event;
        if (!gesture.active && Math.hypot(event.clientX - gesture.x, event.clientY - gesture.y) < 6) return;
        event.preventDefault();
        if (!gesture.active) {
            gesture.active = true;
            root.setPointerCapture(event.pointerId);
            gesture.source.classList.add('is-dragging');
            root.classList.add('is-pointer-dragging');
            scrollFrame = requestAnimationFrame(autoScroll);
        }
        showDestination(event);
    };
    const pointerup = async event => {
        if (!gesture || event.pointerId !== gesture.id) return;
        const wasDragging = gesture.active;
        const scene = gesture.source.dataset.storyboardScene;
        const drop = wasDragging && !blocked() ? destination(event) : null;
        if (wasDragging) { event.preventDefault(); suppressClick = true; }
        clear();
        if (!drop || drop.before === scene) return;
        pending = true;
        try { await reference.invokeMethodAsync('DropStoryboardSceneAsync', scene, drop.chapter, drop.before); }
        finally { pending = false; }
    };
    const click = event => {
        if (suppressClick) { suppressClick = false; event.preventDefault(); event.stopImmediatePropagation(); }
    };
    const cancel = event => {
        if (!event || event.pointerId === gesture?.id) clear();
    };
    const keydown = event => { if (event.key === 'Escape') clear(); };
    // Track releases outside the board even before the movement threshold has
    // been crossed, so a short press cannot leave a stale active gesture.
    const listeners = [
        [root, 'pointerdown', pointerdown],
        [root.ownerDocument, 'pointermove', pointermove],
        [root.ownerDocument, 'pointerup', pointerup],
        [root.ownerDocument, 'pointercancel', cancel],
        [root, 'lostpointercapture', cancel]
    ];
    for (const [target, name, listener] of listeners) target.addEventListener(name, listener);
    root.addEventListener('click', click, true);
    root.ownerDocument.addEventListener('keydown', keydown);
    const blur = () => clear();
    root.ownerDocument.defaultView.addEventListener('blur', blur);
    pointerBindings.set(root, () => {
        clear();
        for (const [target, name, listener] of listeners) target.removeEventListener(name, listener);
        root.removeEventListener('click', click, true);
        root.ownerDocument.removeEventListener('keydown', keydown);
        root.ownerDocument.defaultView.removeEventListener('blur', blur);
    });
}

export function detachPointerDrag(root) {
    pointerBindings.get(root)?.();
    pointerBindings.delete(root);
}

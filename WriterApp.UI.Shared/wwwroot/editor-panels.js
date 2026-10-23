// Shared presentation only: hosts continue to own focus mode, editor state, and navigation.
// Compact drawers are transient; only docked preferences are stored.
const bindings = new WeakMap();
const active = new Set();
const storageKey = "prosa.editor-panels.v1";
let nextPanelId = 0;

export function attach(root) {
    if (!root?.isConnected) return;
    if (bindings.has(root)) { bindings.get(root).refresh(); return; }
    const media = window.matchMedia("(max-width: 1199px)");
    const workspace = () => root.querySelector(".editor-workspace");
    const hostCollapsed = () => !!workspace()?.matches(".is-context-collapsed, .is-panels-collapsed");
    const focused = () => root.classList.contains("is-focus-mode");
    const brainstorming = () => root.classList.contains("is-brainstorming");
    const previewing = () => !!workspace()?.classList.contains("is-preview-mode");
    let desktop = { left: true, right: !hostCollapsed() };
    try {
        const saved = JSON.parse(localStorage.getItem(storageKey));
        if (typeof saved?.left === "boolean" && typeof saved?.right === "boolean") desktop = saved;
    } catch { /* Missing or restricted preferences do not prevent opening panels. */ }
    let compact = media.matches, drawer = null, returnTo = null, selection = null;
    let lastFocus = focused(), lastCollapsed = hostCollapsed(), location = root.dataset.editorLocation;
    let lastBrainstorm = brainstorming(), lastPreview = previewing(), pendingOpen = null, forwarding = false, disposed = false;
    const annotationMode = () => root.dataset.annotationPanel === "true";
    const nonModal = () => compact && drawer === "right" && annotationMode();
    const shellHost = root.closest(".app-shell");
    function viewportChanged() {
        if (nonModal() && window.visualViewport) shellHost?.style.setProperty("--annotation-viewport-height", `${window.visualViewport.height}px`);
        else shellHost?.style.removeProperty("--annotation-viewport-height");
    }
    let lastDrawer = null;
    const account = root.closest(".app-shell")?.querySelector(".editor-account-menu");
    if (account) account.open = !compact;
    const inerted = new Map();
    const panel = side => root.querySelector(side === "left" ? ".editor-navigation-host" : ".editor-context-host");
    const button = side => root.querySelector(`[data-panel-action="${side}"]`);
    const store = () => { try { localStorage.setItem(storageKey, JSON.stringify(desktop)); } catch { } };
    function hostClick(action) {
        const control = button(action);
        if (!control) return;
        forwarding = true;
        try { control.click(); } finally { forwarding = false; }
    }
    function syncHost() {
        if (!compact && !focused() && !brainstorming() && !previewing() && desktop.right === hostCollapsed()) hostClick("right");
    }
    function setInert(element, value) {
        if (!element) return;
        if (value && !inerted.has(element)) inerted.set(element, element.inert);
        if (value) element.inert = true;
        else if (inerted.has(element)) { element.inert = inerted.get(element); inerted.delete(element); }
    }
    function restoreFocus() {
        if (selection?.startContainer.isConnected && selection?.endContainer.isConnected) {
            const current = window.getSelection();
            current.removeAllRanges(); current.addRange(selection);
        }
        if (returnTo?.isConnected && returnTo.getClientRects().length) returnTo.focus({ preventScroll: true });
        returnTo = null; selection = null;
    }
    function render() {
        if (disposed || !root.isConnected) return;
        const focus = focused(), brainstorm = brainstorming(), preview = previewing();
        if (focus || brainstorm || preview) drawer = null;
        viewportChanged();
        const leftOpen = !focus && !brainstorm && !preview && (compact ? drawer === "left" : desktop.left);
        const rightOpen = !focus && !brainstorm && !preview && (compact ? drawer === "right" : desktop.right);
        root.dataset.panelsReady = "true";
        root.dataset.panelsCompact = String(compact);
        root.dataset.leftOpen = String(leftOpen);
        root.dataset.rightOpen = String(rightOpen);
        root.dataset.openDrawer = drawer ?? "";
        for (const side of ["left", "right"]) {
            const control = button(side), target = panel(side), open = side === "left" ? leftOpen : rightOpen;
            if (control) control.hidden = brainstorm || preview;
            if (control && target) {
                target.id ||= `prosa-${side}-${++nextPanelId}`;
                control.setAttribute("aria-controls", target.id);
                control.setAttribute("aria-expanded", String(open));
                target.setAttribute("role", compact && open && !nonModal() ? "dialog" : "complementary");
                if (compact && open && !nonModal()) target.setAttribute("aria-modal", "true");
                else target.removeAttribute("aria-modal");
                setInert(target, !open);
            }
        }
        setInert(root.querySelector(".editor-main"), compact && !!drawer && !nonModal());
        const shell = root.closest(".app-shell");
        // Include breadcrumbs and other content outside the workspace, not just the editor.
        for (let branch = root; branch.parentElement && branch !== shell; branch = branch.parentElement) {
            for (const sibling of branch.parentElement.children) if (sibling !== branch) setInert(sibling, compact && !!drawer && !nonModal());
        }
        if (compact && drawer && account) account.open = false;
        // Other shell navigation is hidden while this workspace owns the projects drawer.
        setInert(shell?.querySelector(".app-nav"), !brainstorm);
        if (drawer !== lastDrawer) {
            if (drawer) {
                const target = panel(drawer);
                queueMicrotask(() => { if (!disposed && drawer && target?.isConnected) (target.querySelector("button") ?? target).focus({ preventScroll: true }); });
            } else restoreFocus();
            lastDrawer = drawer;
        }
    }
    function open(side, trigger) {
        if (!panel(side) || brainstorming() || previewing()) return;
        if (focused()) { pendingOpen = { side, trigger }; hostClick("focus"); return; }
        if (compact) {
            if (drawer === side) { close(); return; }
            if (!drawer) {
                returnTo = trigger ?? button(side);
                const current = window.getSelection();
                selection = current?.rangeCount && root.querySelector(".editor-main")?.contains(current.anchorNode) ? current.getRangeAt(0).cloneRange() : null;
            }
            drawer = side;
        } else { desktop[side] = !desktop[side]; store(); }
        render();
        syncHost();
    }
    function close(side) {
        if (compact) drawer = null;
        else if (side) { desktop[side] = false; store(); }
        render(); syncHost();
        if (!compact && side) button(side)?.focus({ preventScroll: true });
    }
    function refresh() {
        const focus = focused(), collapsed = hostCollapsed(), brainstorm = brainstorming(), preview = previewing();
        if (focus && !lastFocus) drawer = null;
        // Host commands (for example, revealing an annotation) still open the tools.
        // Focus mode's derived collapsed class must never overwrite docked preferences.
        if (!focus && !lastFocus && collapsed !== lastCollapsed && !brainstorm && !preview && !lastPreview) {
            if (compact) {
                if (!collapsed) { returnTo = button("right"); drawer = "right"; }
                else if (drawer === "right") drawer = null;
            } else { desktop.right = !collapsed; store(); }
        }
        const nextLocation = root.dataset.editorLocation;
        if (nextLocation !== location || brainstorm !== lastBrainstorm || preview !== lastPreview) { drawer = null; pendingOpen = null; }
        location = nextLocation; lastBrainstorm = brainstorm; lastPreview = preview;
        lastFocus = focus; lastCollapsed = collapsed;
        if (!focus && pendingOpen) {
            const request = pendingOpen; pendingOpen = null;
            // An explicit open from focus mode opens, even if the dock was previously visible.
            if (!compact) desktop[request.side] = false;
            open(request.side, request.trigger);
        } else render();
    }
    function click(event) {
        const control = event.target.closest("[data-panel-action]");
        if (!control || !root.contains(control) || forwarding) return;
        const action = control.dataset.panelAction;
        if (action === "annotation-size") {
            event.preventDefault(); event.stopImmediatePropagation();
            root.style.removeProperty("--annotation-height");
            const expanded = root.dataset.annotationExpanded !== "true";
            root.dataset.annotationExpanded = String(expanded);
            control.textContent = expanded ? "Less space" : "More space";
            return;
        }
        if (action === "focus") { drawer = null; render(); return; }
        event.preventDefault(); event.stopImmediatePropagation();
        if (action === "left" || action === "right") open(action, control);
        else close(action === "close-left" ? "left" : action === "close-right" ? "right" : null);
    }
    function keydown(event) {
        if (!drawer || !compact) return;
        if (event.key === "Escape") { event.preventDefault(); event.stopPropagation(); close(); return; }
        if (event.key !== "Tab" || nonModal()) return;
        const target = panel(drawer);
        const controls = [...target.querySelectorAll('button, a[href], input, textarea, select, [tabindex]:not([tabindex="-1"])')]
            .filter(element => !element.disabled && !element.closest("[inert]") && element.getClientRects().length);
        const first = controls[0] ?? target, last = controls.at(-1) ?? target;
        if (event.shiftKey && (document.activeElement === first || !target.contains(document.activeElement))) { event.preventDefault(); last.focus(); }
        else if (!event.shiftKey && (document.activeElement === last || !target.contains(document.activeElement))) { event.preventDefault(); first.focus(); }
    }
    let resizing = false;
    function pointerDown(event) {
        if (!nonModal() || !event.target.closest(".annotation-resize-grip")) return;
        resizing = true; event.target.setPointerCapture(event.pointerId); event.preventDefault();
    }
    function pointerMove(event) {
        if (!resizing) return;
        const rect = workspace().getBoundingClientRect();
        const percent = Math.min(65, Math.max(25, (rect.bottom - event.clientY) / rect.height * 100));
        root.style.setProperty("--annotation-height", `${percent}%`);
    }
    function pointerUp() { resizing = false; }
    function resize() {
        if (compact === media.matches) return;
        compact = media.matches; drawer = null; render(); syncHost();
        if (account) account.open = !compact;
    }
    const observer = new MutationObserver(refresh);
    observer.observe(root, { attributes: true, attributeFilter: ["class", "data-editor-location", "data-annotation-panel"], childList: true, subtree: true });
    root.addEventListener("pointerdown", pointerDown);
    root.addEventListener("pointermove", pointerMove);
    root.addEventListener("pointerup", pointerUp);
    root.addEventListener("pointercancel", pointerUp);
    root.addEventListener("click", click, true);
    document.addEventListener("keydown", keydown, true);
    media.addEventListener("change", resize);
    window.visualViewport?.addEventListener("resize", viewportChanged);
    const binding = { refresh, openContext: () => { if (compact) drawer = "right"; else desktop.right = true; render(); syncHost(); }, closeActive: () => { if (!drawer) return false; close(); return true; }, dispose: () => {
        disposed = true; observer.disconnect();
        window.visualViewport?.removeEventListener("resize", viewportChanged); shellHost?.style.removeProperty("--annotation-viewport-height");
        root.removeEventListener("pointerdown", pointerDown); root.removeEventListener("pointermove", pointerMove);
        root.removeEventListener("pointerup", pointerUp); root.removeEventListener("pointercancel", pointerUp); root.removeEventListener("click", click, true);
        document.removeEventListener("keydown", keydown, true); media.removeEventListener("change", resize);
        for (const [element, value] of inerted) element.inert = value;
        inerted.clear(); active.delete(binding);
        if (account) account.open = true;
    } };
    bindings.set(root, binding); active.add(binding);
    render(); syncHost();
}

// Native Android Back invokes this before navigating away or saving a different document.
export function closeActive() { for (const binding of active) if (binding.closeActive()) return true; return false; }
export function detach(root) { bindings.get(root)?.dispose(); bindings.delete(root); }

export function openContext(root) { bindings.get(root)?.openContext(); }

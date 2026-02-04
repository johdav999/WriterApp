export function downloadFile(base64Data, mimeType, fileName) {
    if (!base64Data) {
        return;
    }

    const binary = atob(base64Data);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }

    const blob = new Blob([bytes], { type: mimeType || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName || "document";
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}

export function printHtmlAsPdf(html) {
    const frame = document.createElement("iframe");
    frame.style.position = "fixed";
    frame.style.right = "0";
    frame.style.bottom = "0";
    frame.style.width = "0";
    frame.style.height = "0";
    frame.style.border = "0";

    document.body.appendChild(frame);

    const doc = frame.contentDocument || frame.contentWindow.document;
    doc.open();
    doc.write(html);
    doc.close();

    frame.contentWindow.focus();
    frame.contentWindow.print();

    setTimeout(() => {
        document.body.removeChild(frame);
    }, 1000);
}

export function printIframe(frameId) {
    const frame = document.getElementById(frameId);
    if (!frame) {
        return;
    }

    const targetWindow = frame.contentWindow;
    if (!targetWindow) {
        return;
    }

    targetWindow.focus();
    targetWindow.print();
}

function mmToPx(mm) {
    return (Number(mm) || 0) * (96 / 25.4);
}

function getFrameDocument(frameId) {
    const frame = document.getElementById(frameId);
    if (!frame) {
        return null;
    }
    return frame.contentDocument || frame.contentWindow?.document || null;
}

function ensurePreviewOverlay(doc) {
    if (!doc?.body) {
        return null;
    }
    let overlay = doc.body.querySelector(".preview-pagebreak-overlay");
    if (!overlay) {
        overlay = doc.createElement("div");
        overlay.className = "preview-pagebreak-overlay";
        doc.body.appendChild(overlay);
    }
    return overlay;
}

function clearPreviewSearchInternal(doc) {
    if (!doc) {
        return;
    }
    const highlights = doc.querySelectorAll(".preview-search-hit");
    highlights.forEach(node => {
        const parent = node.parentNode;
        if (!parent) {
            return;
        }
        parent.replaceChild(doc.createTextNode(node.textContent || ""), node);
        parent.normalize();
    });
}

function highlightSearch(doc, term) {
    if (!doc || !term) {
        return;
    }
    clearPreviewSearchInternal(doc);
    const normalized = term.toLowerCase();
    const walker = doc.createTreeWalker(doc.body, NodeFilter.SHOW_TEXT);
    const hits = [];
    while (walker.nextNode()) {
        const node = walker.currentNode;
        const text = node.nodeValue || "";
        const lower = text.toLowerCase();
        let index = 0;
        while ((index = lower.indexOf(normalized, index)) !== -1) {
            hits.push({ node, index, length: term.length });
            index += term.length;
        }
    }
    hits.reverse().forEach(hit => {
        const range = doc.createRange();
        range.setStart(hit.node, hit.index);
        range.setEnd(hit.node, hit.index + hit.length);
        const mark = doc.createElement("mark");
        mark.className = "preview-search-hit";
        range.surroundContents(mark);
    });
}

const PAGE_EPSILON_PX = 48;
const PROGRAMMATIC_SCROLL_MS = 250;

function getPreviewRoot(doc) {
    return doc?.scrollingElement || doc?.documentElement || doc?.body || null;
}

function getPreviewBody(doc) {
    return doc?.getElementById("preview-body") || null;
}

function computeBodyMetrics(doc, pageHeightPx) {
    const root = getPreviewRoot(doc);
    const body = getPreviewBody(doc);
    const frontMatter = doc?.getElementById("preview-frontmatter") || null;
    if (!root) {
        return { bodyOffsetTop: 0, bodyHeight: 0, totalPages: 1, currentPage: 1, hasFrontMatter: false };
    }

    const bodyOffsetTop = body
        ? body.getBoundingClientRect().top + root.scrollTop
        : 0;
    const bodyHeight = body ? body.scrollHeight : root.scrollHeight;
    const rawPages = pageHeightPx > 0 ? bodyHeight / pageHeightPx : 1;
    const remainder = pageHeightPx > 0 ? bodyHeight % pageHeightPx : 0;
    const totalPages = Math.max(
        1,
        remainder > 0 && remainder < PAGE_EPSILON_PX ? Math.floor(rawPages) : Math.ceil(rawPages)
    );
    const bodyScrollTop = Math.max(0, root.scrollTop - bodyOffsetTop);
    const inFrontMatter = root.scrollTop + 4 < bodyOffsetTop;
    const currentPage = inFrontMatter ? 0 : Math.min(totalPages, Math.max(1, Math.floor(bodyScrollTop / pageHeightPx) + 1));
    const hasFrontMatter = !!(frontMatter && frontMatter.textContent && frontMatter.textContent.trim().length > 0);

    return { bodyOffsetTop, bodyHeight, totalPages, currentPage, hasFrontMatter };
}

function renderPageBreaks(doc, pageHeightPx, show, bodyOffsetTop, totalPages) {
    const overlay = ensurePreviewOverlay(doc);
    if (!overlay) {
        return 1;
    }
    const debugEnabled = typeof window !== "undefined" && window.__DEBUG_PAGINATION__ === true;
    overlay.innerHTML = "";
    const shouldShow = show || debugEnabled;
    overlay.style.display = shouldShow ? "block" : "none";
    if (!shouldShow) {
        return 1;
    }
    const count = Math.max(1, Number(totalPages) || 1);
    const offset = Number(bodyOffsetTop) || 0;
    for (let i = 1; i < count; i += 1) {
        const line = doc.createElement("div");
        line.className = "preview-pagebreak-line";
        line.style.top = `${offset + i * pageHeightPx}px`;
        overlay.appendChild(line);
    }
    return count;
}

export function initPreviewFrame(frameId, pageWidthMm, pageHeightMm, showBreaks) {
    const doc = getFrameDocument(frameId);
    if (!doc) {
        return null;
    }
    if (doc.body) {
        doc.body.dataset.pageHeightMm = String(pageHeightMm || 297);
        doc.body.dataset.pageWidthMm = String(pageWidthMm || 210);
    }
    const pageHeightPx = mmToPx(pageHeightMm || 297);
    const metrics = computeBodyMetrics(doc, pageHeightPx);
    renderPageBreaks(doc, pageHeightPx, !!showBreaks, metrics.bodyOffsetTop, metrics.totalPages);
    return { pageCount: metrics.totalPages, currentPage: metrics.currentPage, hasFrontMatter: metrics.hasFrontMatter };
}

export function registerPreviewScroll(frameId, dotNetRef) {
    const doc = getFrameDocument(frameId);
    if (!doc) {
        return;
    }
    const root = getPreviewRoot(doc);
    if (!root) {
        return;
    }
    if (doc.__writerPreviewScrollHandler) {
        root.removeEventListener("scroll", doc.__writerPreviewScrollHandler);
    }
    if (doc.body) {
        doc.body.dataset.previewProgrammatic = "false";
    }
    const handler = () => {
        if (!doc.body) {
            return;
        }
        if (doc.body.dataset.previewProgrammatic === "true") {
            return;
        }
        const pageHeightPx = mmToPx(doc.body.dataset.pageHeightMm || 297);
        const metrics = computeBodyMetrics(doc, pageHeightPx);
        dotNetRef.invokeMethodAsync("OnPreviewScroll", metrics.totalPages, metrics.currentPage, metrics.hasFrontMatter);
    };
    root.addEventListener("scroll", handler, { passive: true });
    doc.__writerPreviewScrollHandler = handler;
    if (doc.body) {
        doc.body.dataset.previewScrollHandler = "true";
    }
}

export function unregisterPreviewScroll(frameId) {
    const doc = getFrameDocument(frameId);
    if (!doc) {
        return;
    }
    const root = getPreviewRoot(doc);
    if (!root) {
        return;
    }
    if (doc.__writerPreviewScrollHandler) {
        root.removeEventListener("scroll", doc.__writerPreviewScrollHandler);
        doc.__writerPreviewScrollHandler = null;
    }
}

export function setPreviewPageBreaks(frameId, showBreaks) {
    const doc = getFrameDocument(frameId);
    if (!doc) {
        return;
    }
    const pageHeightPx = mmToPx(doc.body?.dataset?.pageHeightMm || 297);
    const metrics = computeBodyMetrics(doc, pageHeightPx);
    renderPageBreaks(doc, pageHeightPx, !!showBreaks, metrics.bodyOffsetTop, metrics.totalPages);
}

export function getPreviewFit(frameId, pageWidthMm, pageHeightMm) {
    const frame = document.getElementById(frameId);
    if (!frame) {
        return null;
    }
    const wrap = frame.parentElement;
    if (!wrap) {
        return null;
    }
    const widthPx = mmToPx(pageWidthMm || 210);
    const heightPx = mmToPx(pageHeightMm || 297);
    const fitWidth = wrap.clientWidth / widthPx;
    const fitPage = wrap.clientHeight / heightPx;
    return { fitWidth: Math.min(fitWidth, 2.5), fitPage: Math.min(fitPage, 2.5) };
}

export function scrollPreviewToPage(frameId, pageNumber) {
    const frame = document.getElementById(frameId);
    if (!frame) {
        return;
    }
    const doc = frame.contentDocument || frame.contentWindow?.document;
    if (!doc) {
        return;
    }
    const pageHeightPx = mmToPx(doc.body?.dataset?.pageHeightMm || 297);
    const page = Math.max(1, Number(pageNumber) || 1);
    const root = getPreviewRoot(doc);
    if (!root) {
        return;
    }
    const metrics = computeBodyMetrics(doc, pageHeightPx);
    const targetTop = Math.max(0, metrics.bodyOffsetTop + (page - 1) * pageHeightPx);
    if (doc.body) {
        doc.body.dataset.previewProgrammatic = "true";
    }
    root.scrollTo({ top: targetTop, behavior: "smooth" });
    setTimeout(() => {
        if (doc.body) {
            doc.body.dataset.previewProgrammatic = "false";
        }
    }, PROGRAMMATIC_SCROLL_MS);
}

export function scrollPreviewToFrontMatter(frameId) {
    const doc = getFrameDocument(frameId);
    if (!doc) {
        return;
    }
    const root = getPreviewRoot(doc);
    if (!root) {
        return;
    }
    if (doc.body) {
        doc.body.dataset.previewProgrammatic = "true";
    }
    root.scrollTo({ top: 0, behavior: "smooth" });
    setTimeout(() => {
        if (doc.body) {
            doc.body.dataset.previewProgrammatic = "false";
        }
    }, PROGRAMMATIC_SCROLL_MS);
}

export function searchPreview(frameId, term) {
    const doc = getFrameDocument(frameId);
    if (!doc) {
        return;
    }
    if (!term) {
        clearPreviewSearchInternal(doc);
        return;
    }
    highlightSearch(doc, term);
}

export function clearPreviewSearch(frameId) {
    const doc = getFrameDocument(frameId);
    if (!doc) {
        return;
    }
    clearPreviewSearchInternal(doc);
}


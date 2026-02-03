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

function renderPageBreaks(doc, pageHeightPx, show) {
    const overlay = ensurePreviewOverlay(doc);
    if (!overlay) {
        return 1;
    }
    overlay.innerHTML = "";
    overlay.style.display = show ? "block" : "none";
    if (!show) {
        return 1;
    }
    const height = Math.max(doc.body.scrollHeight, doc.documentElement.scrollHeight);
    const count = Math.max(1, Math.ceil(height / pageHeightPx));
    for (let i = 1; i < count; i += 1) {
        const line = doc.createElement("div");
        line.className = "preview-pagebreak-line";
        line.style.top = `${i * pageHeightPx}px`;
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
    const pageCount = renderPageBreaks(doc, pageHeightPx, !!showBreaks);
    return { pageCount, currentPage: 1 };
}

export function setPreviewPageBreaks(frameId, showBreaks) {
    const doc = getFrameDocument(frameId);
    if (!doc) {
        return;
    }
    const pageHeightPx = mmToPx(doc.body?.dataset?.pageHeightMm || 297);
    renderPageBreaks(doc, pageHeightPx, !!showBreaks);
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
    frame.contentWindow.scrollTo({ top: (page - 1) * pageHeightPx, behavior: "smooth" });
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


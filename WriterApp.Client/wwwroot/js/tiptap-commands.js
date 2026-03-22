export function toggleBold(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().toggleBold().run();
}

export function toggleItalic(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().toggleItalic().run();
}

export function toggleStrike(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().toggleStrike().run();
}

export function toggleCode(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().toggleCode().run();
}

export function setParagraph(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().setParagraph().run();
}

export function toggleHeading(editor, level) {
    if (!editor) {
        return;
    }

    editor.chain().focus().toggleHeading({ level }).run();
}

export function setHeading(editor, level) {
    if (!editor) {
        return;
    }

    editor.chain().focus().setHeading({ level }).run();
}

export function toggleBlockquote(editor) {
    if (!editor) {
        return;
    }

    const currentSelection = editor.state?.selection;
    const storedSelection = editor.__writerLastSelectionDocRange;
    const selectionToRestore = currentSelection && !currentSelection.empty
        ? { from: currentSelection.from, to: currentSelection.to }
        : storedSelection && Number.isFinite(storedSelection.from) && Number.isFinite(storedSelection.to) && storedSelection.to > storedSelection.from
            ? { from: storedSelection.from, to: storedSelection.to }
            : null;

    let chain = editor.chain().focus();
    if (selectionToRestore) {
        chain = chain.setTextSelection(selectionToRestore);
    }

    chain.toggleBlockquote().run();
}

export function insertHorizontalRule(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().setHorizontalRule().run();
}

export function toggleBulletList(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().toggleBulletList().run();
}

export function toggleOrderedList(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().toggleOrderedList().run();
}

export function insertTable(editor, rows = 3, cols = 3, withHeaderRow = true) {
    if (!editor) {
        return;
    }

    const rowCount = Number(rows);
    const colCount = Number(cols);
    editor.chain().focus().insertTable({
        rows: Number.isFinite(rowCount) && rowCount > 0 ? rowCount : 3,
        cols: Number.isFinite(colCount) && colCount > 0 ? colCount : 3,
        withHeaderRow: withHeaderRow !== false
    }).run();
}

export function addTableRowBefore(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().addRowBefore().run();
}

export function addTableRowAfter(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().addRowAfter().run();
}

export function deleteTableRow(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().deleteRow().run();
}

export function addTableColumnBefore(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().addColumnBefore().run();
}

export function addTableColumnAfter(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().addColumnAfter().run();
}

export function deleteTableColumn(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().deleteColumn().run();
}

export function deleteTable(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().deleteTable().run();
}

export function toggleTableHeaderRow(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().toggleHeaderRow().run();
}

export function toggleTableHeaderColumn(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().toggleHeaderColumn().run();
}

export function mergeTableCells(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().mergeCells().run();
}

function isTableSelectionDebugEnabled() {
    try {
        return window?.localStorage?.getItem("writerapp.tableSelectionDebug") === "true"
            || window?.localStorage?.getItem("writerapp.debug") === "true";
    } catch {
        return false;
    }
}

function getSelectionTypeName(selection) {
    return selection?.constructor?.name
        || selection?.jsonID
        || selection?.type
        || typeof selection;
}

function getCurrentTableCellInfo(editor, selection = editor?.state?.selection) {
    const $from = selection?.$from;
    if (!$from) {
        return null;
    }

    for (let depth = $from.depth; depth >= 0; depth -= 1) {
        const node = $from.node(depth);
        const role = node?.type?.spec?.tableRole;
        if (role === "cell" || role === "header_cell") {
            return {
                depth,
                nodeType: node.type?.name ?? null,
                tableRole: role,
                attrs: {
                    colspan: Number(node.attrs?.colspan ?? 1),
                    rowspan: Number(node.attrs?.rowspan ?? 1),
                    colwidth: Array.isArray(node.attrs?.colwidth) ? [...node.attrs.colwidth] : node.attrs?.colwidth ?? null
                }
            };
        }
    }

    return null;
}

function debugSplitCellCommand(editor, reason, extra = null) {
    if (!isTableSelectionDebugEnabled() || !editor) {
        return;
    }

    const selection = editor.state?.selection;
    let canSplitTableCell = null;
    let canSplitChainExists = false;
    try {
        const canChain = editor.can?.().chain?.();
        canSplitChainExists = typeof canChain?.splitCell === "function";
        canSplitTableCell = canSplitChainExists ? editor.can().chain().splitCell().run() : null;
    } catch {
    }

    try {
        console.debug("[split-cell-command]", {
            reason,
            selectionType: getSelectionTypeName(selection),
            splitCommandExists: typeof editor.commands?.splitCell === "function",
            splitCanChainExists: canSplitChainExists,
            canSplitTableCell,
            activeTable: editor.isActive?.("table") ?? false,
            activeTableCell: editor.isActive?.("tableCell") ?? false,
            activeTableHeader: editor.isActive?.("tableHeader") ?? false,
            currentCell: getCurrentTableCellInfo(editor, selection),
            ...(extra || {})
        });
    } catch {
    }
}

export function splitTableCell(editor) {
    if (!editor) {
        return;
    }

    debugSplitCellCommand(editor, "before-command");
    const result = editor.chain().focus().splitCell().run();
    debugSplitCellCommand(editor, "after-command", { commandResult: result });
}

export function insertImageFromUrl(editor, url, alt = "", title = "", width = null, assetUrl = null, assetId = null) {
    if (!editor) {
        return;
    }

    const src = typeof url === "string" ? url.trim() : "";
    if (!src) {
        return;
    }

    const attrs = { src };
    if (typeof alt === "string" && alt.trim().length > 0) {
        attrs.alt = alt.trim();
    }
    if (typeof title === "string" && title.trim().length > 0) {
        attrs.title = title.trim();
    }
    if (width !== null && width !== undefined && `${width}`.trim().length > 0) {
        attrs.width = `${width}`.trim();
    }
    if (typeof assetUrl === "string" && assetUrl.trim().length > 0) {
        attrs.assetUrl = assetUrl.trim();
    }
    if (typeof assetId === "string" && assetId.trim().length > 0) {
        attrs.assetId = assetId.trim();
    }

    editor.chain().focus().setImage(attrs).run();
}

export function replaceSelectedImage(editor, url, alt = "", title = "", width = null, assetUrl = null, assetId = null) {
    if (!editor) {
        return;
    }

    const src = typeof url === "string" ? url.trim() : "";
    if (!src) {
        return;
    }

    const attrs = { src };
    if (typeof alt === "string" && alt.trim().length > 0) {
        attrs.alt = alt.trim();
    }
    if (typeof title === "string" && title.trim().length > 0) {
        attrs.title = title.trim();
    }
    if (width !== null && width !== undefined && `${width}`.trim().length > 0) {
        attrs.width = `${width}`.trim();
    }
    if (typeof assetUrl === "string" && assetUrl.trim().length > 0) {
        attrs.assetUrl = assetUrl.trim();
    }
    if (typeof assetId === "string" && assetId.trim().length > 0) {
        attrs.assetId = assetId.trim();
    }

    if (editor.isActive("image")) {
        editor.chain().focus().updateAttributes("image", attrs).run();
        return;
    }

    editor.chain().focus().setImage(attrs).run();
}

export function removeSelectedImage(editor) {
    if (!editor) {
        return;
    }

    if (!editor.isActive("image")) {
        return;
    }

    editor.chain().focus().deleteSelection().run();
}

export function setTextAlign(editor, alignment) {
    if (!editor) {
        return;
    }

    const value = typeof alignment === "string" ? alignment.toLowerCase() : "";
    if (!value) {
        return;
    }

    editor.chain().focus().setTextAlign(value).run();
}

export function setLink(editor, href) {
    if (!editor) {
        return;
    }

    const url = typeof href === "string" ? href.trim() : "";
    if (!url) {
        return;
    }

    editor.chain().focus().extendMarkRange("link").setLink({ href: url }).run();
}

export function unsetLink(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().unsetLink().run();
}

export function setFontSize(editor, size) {
    if (!editor) {
        return;
    }

    const sizeValue = Number(size);
    if (!Number.isFinite(sizeValue)) {
        return;
    }

    editor.chain().focus().setMark("textStyle", { fontSize: `${sizeValue}px` }).run();
}

export function increaseIndent(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().increaseIndent().run();
}

export function decreaseIndent(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().decreaseIndent().run();
}

export function setFontFamily(editor, fontFamily) {
    if (!editor) {
        return;
    }

    const family = typeof fontFamily === "string" ? fontFamily.trim() : "";
    if (!family) {
        return;
    }

    editor.chain().focus().setMark("textStyle", { fontFamily: family }).run();
}

export function clearFontFamily(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().setMark("textStyle", { fontFamily: null }).run();
}

export function focusEditor(editor) {
    if (!editor) {
        return;
    }

    editor.commands.focus();
}

export function undo(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().undo().run();
}

export function redo(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().redo().run();
}

export function replaceSelection(editor, content) {
    if (!editor) {
        return;
    }

    const text = typeof content === "string" ? content : "";
    editor.chain().focus().insertContent(text).run();
}

export function replaceTextRange(editor, from, to, content) {
    if (!editor) {
        return;
    }

    const start = Number(from);
    const end = Number(to);
    if (!Number.isFinite(start) || !Number.isFinite(end)) {
        return;
    }

    const text = typeof content === "string" ? content : "";
    const normalizedFrom = Math.max(0, Math.min(start, end));
    const normalizedTo = Math.max(normalizedFrom, Math.max(start, end));
    editor.chain().focus().setTextSelection({ from: normalizedFrom, to: normalizedTo }).insertContent(text).run();
}

export function appendParagraph(editor, content) {
    if (!editor) {
        return;
    }

    const text = typeof content === "string" ? content.trim() : "";
    if (!text) {
        return;
    }

    const paragraph = {
        type: "paragraph",
        content: [{ type: "text", text }]
    };

    editor.chain().focus().insertContentAt(editor.state.doc.content.size, paragraph).run();
}

export function appendImportedHtml(editor, html) {
    if (!editor) {
        return;
    }

    const incoming = typeof html === "string" ? html.trim() : "";
    if (!incoming) {
        return;
    }

    const hasContent = (editor.state?.doc?.textContent || "").trim().length > 0;
    const endPos = editor.state?.doc?.content?.size ?? 0;
    const chain = editor.chain().focus(endPos);
    if (hasContent) {
        chain.insertContent("<p><br /></p>");
    }

    chain.insertContent(incoming).run();
}

export function scrollToPosition(editor, position) {
    if (!editor) {
        return;
    }

    const pos = Number(position);
    if (!Number.isFinite(pos)) {
        return;
    }

    editor.chain().focus().setTextSelection(pos).run();
    if (editor.view?.state?.tr && editor.view?.dispatch) {
        editor.view.dispatch(editor.view.state.tr.scrollIntoView());
    }
}

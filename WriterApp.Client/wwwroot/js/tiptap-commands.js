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

    editor.chain().focus().toggleBlockquote().run();
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

export function splitTableCell(editor) {
    if (!editor) {
        return;
    }

    editor.chain().focus().splitCell().run();
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

export function scrollToPosition(editor, position) {
    if (!editor) {
        return;
    }

    const pos = Number(position);
    if (!Number.isFinite(pos)) {
        return;
    }

    editor.chain().focus().setTextSelection(pos).run();
    if (editor.view) {
        editor.view.scrollIntoView();
    }
}

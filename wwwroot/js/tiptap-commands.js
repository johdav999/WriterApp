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

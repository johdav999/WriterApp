export function toggleBold(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().toggleBold().run();
}

export function toggleItalic(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().toggleItalic().run();
}

export function toggleStrike(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().toggleStrike().run();
}

export function toggleCode(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().toggleCode().run();
}

export function setParagraph(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().setParagraph().run();
}

export function toggleHeading(editor: any, level: number) {
  if (!editor) {
    return;
  }

  editor.chain().focus().toggleHeading({ level }).run();
}

export function setHeading(editor: any, level: number) {
  if (!editor) {
    return;
  }

  editor.chain().focus().setHeading({ level }).run();
}

export function toggleBlockquote(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().toggleBlockquote().run();
}

export function insertHorizontalRule(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().setHorizontalRule().run();
}

export function toggleBulletList(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().toggleBulletList().run();
}

export function toggleOrderedList(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().toggleOrderedList().run();
}

export function setTextAlign(editor: any, alignment: string) {
  if (!editor) {
    return;
  }

  const value = typeof alignment === "string" ? alignment.toLowerCase() : "";
  if (!value) {
    return;
  }

  editor.chain().focus().setTextAlign(value).run();
}

export function setLink(editor: any, href: string) {
  if (!editor) {
    return;
  }

  const url = typeof href === "string" ? href.trim() : "";
  if (!url) {
    return;
  }

  editor.chain().focus().extendMarkRange("link").setLink({ href: url }).run();
}

export function unsetLink(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().unsetLink().run();
}

export function setFontSize(editor: any, size: number) {
  if (!editor) {
    return;
  }

  const sizeValue = Number(size);
  if (!Number.isFinite(sizeValue)) {
    return;
  }

  editor.chain().focus().setMark("textStyle", { fontSize: `${sizeValue}px` }).run();
}

export function increaseIndent(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().increaseIndent().run();
}

export function decreaseIndent(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().decreaseIndent().run();
}

export function setFontFamily(editor: any, fontFamily: string) {
  if (!editor) {
    return;
  }

  const family = typeof fontFamily === "string" ? fontFamily.trim() : "";
  if (!family) {
    return;
  }

  editor.chain().focus().setMark("textStyle", { fontFamily: family }).run();
}

export function clearFontFamily(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().setMark("textStyle", { fontFamily: null }).run();
}

export function focusEditor(editor: any) {
  if (!editor) {
    return;
  }

  editor.commands.focus();
}

export function undo(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().undo().run();
}

export function redo(editor: any) {
  if (!editor) {
    return;
  }

  editor.chain().focus().redo().run();
}

export function replaceSelection(editor: any, content: string) {
  if (!editor) {
    return;
  }

  const text = typeof content === "string" ? content : "";
  editor.chain().focus().insertContent(text).run();
}

export function appendParagraph(editor: any, content: string) {
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

export function scrollToPosition(editor: any, position: number) {
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

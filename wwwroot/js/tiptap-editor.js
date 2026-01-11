import { Editor, Extension } from "https://esm.sh/@tiptap/core@2.1.13?bundle";
import StarterKit from "https://esm.sh/@tiptap/starter-kit@2.1.13?bundle";
import TextStyle from "https://esm.sh/@tiptap/extension-text-style@2.1.13?bundle";
import {
    toggleBold,
    toggleItalic,
    toggleStrike,
    toggleCode,
    setParagraph,
    toggleHeading,
    toggleBlockquote,
    insertHorizontalRule,
    toggleBulletList,
    toggleOrderedList,
    setFontSize
} from "/js/tiptap-commands.js";

const TextStyleWithFontSize = TextStyle.extend({
    addAttributes() {
        return {
            fontSize: {
                default: null,
                parseHTML: element => element.style.fontSize || null,
                renderHTML: attributes => {
                    if (!attributes.fontSize) {
                        return {};
                    }

                    return { style: `font-size: ${attributes.fontSize}` };
                }
            },
            fontFamily: {
                default: null,
                parseHTML: element => element.style.fontFamily || null,
                renderHTML: attributes => {
                    if (!attributes.fontFamily) {
                        return {};
                    }

                    return { style: `font-family: ${attributes.fontFamily}` };
                }
            }
        };
    }
});

const fontSizePresets = [12, 14, 16, 18, 24, 32];

function parseFontSize(value) {
    if (value === null || value === undefined) {
        return null;
    }

    const match = String(value).match(/(\d+(\.\d+)?)/);
    if (!match) {
        return null;
    }

    const parsed = Number(match[1]);
    return Number.isFinite(parsed) ? parsed : null;
}

function adjustFontSize(editor, direction) {
    const attributes = editor.getAttributes("textStyle") ?? {};
    const currentSize = parseFontSize(attributes.fontSize) ?? 16;
    let index = fontSizePresets.indexOf(currentSize);
    if (index === -1) {
        index = fontSizePresets.indexOf(16);
    }

    if (direction > 0 && index < fontSizePresets.length - 1) {
        index += 1;
    } else if (direction < 0 && index > 0) {
        index -= 1;
    }

    setFontSize(editor, fontSizePresets[index]);
}

function focusFontFamilySelect() {
    const select = document.getElementById("fontFamilySelect");
    if (select) {
        select.focus();
    }
}

window.tiptapEditor = {
    create: function (elementId, initialContent, dotNetRef) {
        const ShortcutExtension = Extension.create({
            name: "appShortcuts",
            addKeyboardShortcuts() {
                return {
                    "Mod-b": () => {
                        toggleBold(this.editor);
                        return true;
                    },
                    "Mod-i": () => {
                        toggleItalic(this.editor);
                        return true;
                    },
                    "Mod-Shift-s": () => {
                        toggleStrike(this.editor);
                        return true;
                    },
                    "Mod-e": () => {
                        toggleCode(this.editor);
                        return true;
                    },
                    "Mod-Alt-0": () => {
                        setParagraph(this.editor);
                        return true;
                    },
                    "Mod-Alt-1": () => {
                        toggleHeading(this.editor, 1);
                        return true;
                    },
                    "Mod-Alt-2": () => {
                        toggleHeading(this.editor, 2);
                        return true;
                    },
                    "Mod-Alt-3": () => {
                        toggleHeading(this.editor, 3);
                        return true;
                    },
                    "Mod-Shift-q": () => {
                        toggleBlockquote(this.editor);
                        return true;
                    },
                    "Mod-Shift-h": () => {
                        insertHorizontalRule(this.editor);
                        return true;
                    },
                    "Mod-Shift-8": () => {
                        toggleBulletList(this.editor);
                        return true;
                    },
                    "Mod-Shift-7": () => {
                        toggleOrderedList(this.editor);
                        return true;
                    },
                    "Mod-Shift-.": () => {
                        adjustFontSize(this.editor, 1);
                        return true;
                    },
                    "Mod-Shift-,": () => {
                        adjustFontSize(this.editor, -1);
                        return true;
                    },
                    "Mod-Shift-f": () => {
                        focusFontFamilySelect();
                        return true;
                    },
                    "Mod-z": () => {
                        if (dotNetRef) {
                            dotNetRef.invokeMethodAsync("OnUndoShortcut");
                        }
                        return true;
                    },
                    "Mod-Shift-z": () => {
                        if (dotNetRef) {
                            dotNetRef.invokeMethodAsync("OnRedoShortcut");
                        }
                        return true;
                    },
                    "Mod-y": () => {
                        if (dotNetRef) {
                            dotNetRef.invokeMethodAsync("OnRedoShortcut");
                        }
                        return true;
                    }
                };
            }
        });

        const editor = new Editor({
            element: document.getElementById(elementId),
            extensions: [
                StarterKit,
                TextStyleWithFontSize,
                ShortcutExtension
            ],
            content: initialContent,
            editorProps: {
                attributes: {
                    class: "ProseMirror tiptap-content",
                    spellcheck: "true",
                    style: "white-space: pre-wrap;"
                }
            },
            onUpdate({ editor }) {
                dotNetRef.invokeMethodAsync(
                    "OnEditorContentChanged",
                    editor.getHTML()
                );
            }
        });

        return editor;
    },

    setContent: function (editor, content) {
        editor.commands.setContent(content, false);
    },

    destroy: function (editor) {
        editor.destroy();
    }
};
